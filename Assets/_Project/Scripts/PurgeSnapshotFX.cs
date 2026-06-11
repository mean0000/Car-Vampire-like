using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 처리 스냅샷(Purge Snapshot) — 수렴샷 킬 확정 순간의 임팩트 프레임 1컷.
/// 세계관: 화이트아웃 = 파트너 AI "엘"이 처리 기록을 캡처하는 순간 (v1 미니멀 — 텍스트/아이콘 없음).
///
/// 시퀀스(전부 unscaled — 수렴샷 킬 순간은 HitStop으로 timeScale 0.05다. ZombieDeathFX.EchoRing 선례):
///   t=0      종이 화이트(#F7F5F0) 스냅 온 — 페이드 없음, 컷이 스타일
///   t=0      잉크 실루엣(InkBlob 셰이더) — 피격 좀비의 스크린 좌표, 탄 방향 비대칭 스트레치, 4프레임 성장
///   t=0.067s 둘 다 컷 오프 — 월드 복귀. 기존 헤드팝/히트스탑/킬 링은 그대로 진행(여기선 안 건드림)
///   병행     회백색(#C8C8C8) 스미어 트레일 — victimPos→shotDir 스트레치드 빌보드, 0.25~0.35s 빠른 페이드
/// 발작 가드: 화이트아웃 최소 간격 0.5s(unscaled) — 간격 내 재호출은 스미어만.
///
/// ZombieDeathFX 패턴: static API + 첫 사용 시 자동 생성 싱글톤, 자원 전부 코드 생성, OnDestroy 정리.
/// 마무리 보상색은 기존 시안 킬 링이 받는다 — 이 이펙트는 흑/백/회백만 쓴다(색상 예산).
/// </summary>
public class PurgeSnapshotFX : MonoBehaviour
{
    // ── 튜닝 수치(다음 세션 핑퐁 대비 한곳에) ──
    const float WhiteoutDuration = 0.067f;   // 60fps 4프레임 — 컷 온/오프(페이드 없음)
    const float MinInterval = 0.5f;          // 발작 가드 — 간격 내 재호출은 화이트아웃·잉크 스킵
    const float InkHeightFrac = 0.35f;       // 잉크 사각 한 변 = 화면 높이의 35%
    const float InkScaleStart = 1.0f;        // 4프레임 성장: 시작 스케일
    const float InkScaleEnd = 1.15f;         // 4프레임 성장: 끝 스케일
    const float InkStretch = 0.6f;           // 탄 방향 비대칭 늘림 강도(셰이더 _Stretch)
    const float AnchorHeight = 1.0f;         // 좀비 발밑→몸통 보정(월드 m) — 잉크 위치·스미어 시점 공용
    const float SmearLifeMin = 0.25f;        // 스미어 수명 하한
    const float SmearLifeMax = 0.35f;        // 스미어 수명 상한
    const int SmearCount = 7;                // 스미어 줄기 수
    const float SmearSpeedMin = 5f;          // 스미어 속도 하한(m/s)
    const float SmearSpeedMax = 9f;          // 스미어 속도 상한
    const int CanvasOrder = 200;             // HUD Canvas(0)·RunLoopSetup 오버레이(50)보다 확실히 위

    static readonly Color PaperWhite = new Color(0.969f, 0.961f, 0.941f);        // #F7F5F0 — 순백 아님, 살짝 웜(종이)
    static readonly Color SmearGray = new Color(0.784f, 0.784f, 0.784f, 0.30f);  // #C8C8C8, 알파 낮게

    static PurgeSnapshotFX s_instance;

    static PurgeSnapshotFX Get()
    {
        if (s_instance == null)
        {
            var go = new GameObject("PurgeSnapshotFX");
            s_instance = go.AddComponent<PurgeSnapshotFX>();
        }
        return s_instance;
    }

    /// <summary>수렴샷 킬 확정 순간 1회 호출 — victimWorldPos=피격 좀비 위치, shotDir=탄 방향.</summary>
    public static void Play(Vector3 victimWorldPos, Vector3 shotDir) => Get().PlayInternal(victimWorldPos, shotDir);

    // ── 런타임 자원 ──
    Canvas _canvas;
    Image _whiteout;        // 풀스크린 단색(스프라이트 불필요)
    Image _ink;             // InkBlob 머티리얼
    Material _inkMat;
    ParticleSystem _smearPS;
    Material _smearMat;
    float _lastSnapshot = -999f;   // 인스턴스 필드 — 도메인 리로드 꺼진 에디터에서도 플레이마다 리셋
    Coroutine _snapshotCo;

    void Awake()
    {
        if (s_instance != null && s_instance != this) { Destroy(gameObject); return; }
        s_instance = this;

        // ── 오버레이 캔버스 — CanvasScaler 없음: 캔버스 단위 = 스크린 픽셀(WorldToScreenPoint 결과 그대로) ──
        var canvasGO = new GameObject("PurgeSnapshotCanvas");
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = CanvasOrder;   // HUD 위 — 화이트아웃이 HUD까지 덮어야 "1컷"이 된다

        // 화이트아웃 — 풀스트레치 단색 Image
        var wGO = new GameObject("Whiteout");
        wGO.transform.SetParent(canvasGO.transform, false);
        _whiteout = wGO.AddComponent<Image>();
        _whiteout.color = PaperWhite;
        _whiteout.raycastTarget = false;
        var wrt = _whiteout.rectTransform;
        wrt.anchorMin = Vector2.zero; wrt.anchorMax = Vector2.one;
        wrt.offsetMin = Vector2.zero; wrt.offsetMax = Vector2.zero;
        wGO.SetActive(false);

        // 잉크 실루엣 — 화이트아웃 다음 형제(=위에 그려짐). 스프라이트 없는 Image도 UV 0~1 풀쿼드를 만든다.
        var inkGO = new GameObject("InkBlob");
        inkGO.transform.SetParent(canvasGO.transform, false);
        _ink = inkGO.AddComponent<Image>();
        _ink.color = Color.white;   // 셰이더가 버텍스 컬러 곱 — 흰색=무틴트
        _ink.raycastTarget = false;
        var inkShader = Shader.Find("ZombieCrush/InkBlob");
        if (inkShader != null)
        {
            _inkMat = new Material(inkShader);
            _ink.material = _inkMat;
        }
        inkGO.SetActive(false);   // 셰이더 미발견 시 영구 비활성 — 화이트아웃·스미어는 그대로 동작

        // 스미어 트레일 — 회백 연기, 알파 블렌드(가산 아님 — 잿빛이어야 한다)
        _smearMat = CreateAlphaBlend(SmearGray);
        _smearPS = CreateSmearPS();
    }

    void OnDestroy()
    {
        if (s_instance == this) s_instance = null;
        StopAllCoroutines();
        if (_inkMat != null) Destroy(_inkMat);
        if (_smearMat != null) Destroy(_smearMat);
    }

    void OnDisable()
    {
        // M-1 페일세이프 — GO 비활성화로 코루틴이 끊기면 화이트 화면이 영구 잔존한다. null 가드: Awake 중복 가드로 파괴되는 인스턴스에서도 불린다.
        if (_whiteout != null) _whiteout.gameObject.SetActive(false);
        if (_ink != null) _ink.gameObject.SetActive(false);
        _snapshotCo = null;
    }

    // ──────────── 내부 구현 ────────────

    void PlayInternal(Vector3 victimPos, Vector3 shotDir)
    {
        // 스미어는 항상 — 발작 가드에 걸려도 "이 킬도 처리됐다"는 흔적은 남긴다.
        EmitSmear(victimPos, shotDir);

        if (Time.unscaledTime - _lastSnapshot < MinInterval) return;   // 발작 가드
        _lastSnapshot = Time.unscaledTime;

        if (_snapshotCo != null) StopCoroutine(_snapshotCo);
        _snapshotCo = StartCoroutine(SnapshotCut(victimPos, shotDir));
    }

    /// <summary>화이트아웃+잉크 1컷 — 스냅 온 → 4프레임 성장 → 컷 오프. 전 구간 unscaled.</summary>
    IEnumerator SnapshotCut(Vector3 victimPos, Vector3 shotDir)
    {
        var cam = Camera.main;

        // M-1 방어적 리셋 — 이전 재생 잔존(StopCoroutine으로 끊긴 컷) 대비, 끈 뒤 깨끗하게 다시 켠다
        _whiteout.gameObject.SetActive(false);
        _ink.gameObject.SetActive(false);

        _whiteout.gameObject.SetActive(true);   // 컷 온 — 페이드 인 없음

        bool inkOn = false;
        if (_inkMat != null && cam != null)
        {
            Vector3 anchor = victimPos + Vector3.up * AnchorHeight;   // 발밑이 아니라 몸통에 도장
            Vector3 sp = cam.WorldToScreenPoint(anchor);
            if (sp.z > 0f)   // 카메라 뒤면 잉크만 생략
            {
                inkOn = true;
                var rt = _ink.rectTransform;
                rt.position = new Vector3(sp.x, sp.y, 0f);
                float size = Screen.height * InkHeightFrac;
                rt.sizeDelta = new Vector2(size, size);
                rt.localScale = Vector3.one * InkScaleStart;

                // shotDir의 스크린 투영 — 잉크가 탄 방향으로 튄다(45° 부감에서 월드 방향≠스크린 방향)
                Vector3 sp2 = cam.WorldToScreenPoint(anchor + shotDir);
                Vector2 sdir = sp2.z > 0f                              // 카메라 평면 뒤(sp2.z<=0)면 미러 좌표 → 방향 반전, 폴백
                    ? new Vector2(sp2.x - sp.x, sp2.y - sp.y)
                    : Vector2.right;
                if (sdir.sqrMagnitude < 1e-4f) sdir = Vector2.right;
                sdir.Normalize();

                _inkMat.SetFloat("_Seed", Random.Range(0f, 100f));   // 매번 다른 얼룩
                _inkMat.SetVector("_StretchDir", new Vector4(sdir.x, sdir.y, 0f, 0f));
                _inkMat.SetFloat("_Stretch", InkStretch);
                _inkMat.SetFloat("_Growth", 0f);
                _ink.gameObject.SetActive(true);
            }
        }

        // 4프레임 성장 — unscaled 수동 적분(WaitForSeconds는 scaled라 히트스탑에 1.3초로 늘어진다)
        float t = 0f;
        while (t < WhiteoutDuration)
        {
            yield return null;
            t += Time.unscaledDeltaTime;
            if (inkOn)
            {
                float g = Mathf.Clamp01(t / WhiteoutDuration);
                _inkMat.SetFloat("_Growth", g);                                          // 윤곽 성장 + 위성 등장
                _ink.rectTransform.localScale = Vector3.one * Mathf.Lerp(InkScaleStart, InkScaleEnd, g);
            }
        }

        // 컷 오프 — 페이드 아웃 없음. 월드 복귀(헤드팝/히트스탑/킬 링은 그동안에도 자기 진행).
        _whiteout.gameObject.SetActive(false);
        if (inkOn) _ink.gameObject.SetActive(false);
        _snapshotCo = null;
    }

    /// <summary>회백 스미어 — victimPos 몸통 높이에서 shotDir로 좁게 산포해 분출(속도 비례 스트레치).</summary>
    void EmitSmear(Vector3 victimPos, Vector3 shotDir)
    {
        if (_smearPS == null) return;
        shotDir.y = 0f;
        if (shotDir.sqrMagnitude < 1e-4f) shotDir = Vector3.forward;
        shotDir.Normalize();
        Vector3 origin = victimPos + Vector3.up * AnchorHeight;

        var ep = new ParticleSystem.EmitParams();
        for (int k = 0; k < SmearCount; k++)
        {
            // 좁은 산포(±12°) + 살짝 떠오름 — 연기는 정확히 직선이면 레이저처럼 보인다
            Vector3 d = Quaternion.AngleAxis(Random.Range(-12f, 12f), Vector3.up) * shotDir;
            d = (d + Vector3.up * Random.Range(0.02f, 0.15f)).normalized;
            ep.position = origin + d * Random.Range(0f, 0.3f);
            ep.velocity = d * Random.Range(SmearSpeedMin, SmearSpeedMax);
            _smearPS.Emit(ep, 1);
        }
    }

    // ──────────── 자원 생성(ZombieDeathFX.CreateBurstPS 패턴) ────────────

    ParticleSystem CreateSmearPS()
    {
        var go = new GameObject("SmearPS");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.useUnscaledTime = true;   // ★히트스탑(timeScale 0.05) 중에도 스미어는 실시간으로 흐른다
        main.startLifetime = new ParticleSystem.MinMaxCurve(SmearLifeMin, SmearLifeMax);
        main.startSpeed = 0f;          // 속도는 EmitParams로 직접 지정
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startColor = SmearGray;
        main.maxParticles = 64;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;

        // 빠른 페이드 — 초반 유지 후 급락(연기가 "찍" 긋고 사라지는 인상)
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.35f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Stretch;   // 스트레치드 빌보드 — 스미어의 본체
        psr.lengthScale = 1.5f;
        psr.velocityScale = 0.08f;     // 속도 비례 늘림 — 빠른 줄기일수록 길게 긋는다
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psr.receiveShadows = false;
        psr.sharedMaterial = _smearMat;
        return ps;
    }

    /// <summary>알파 블렌드 언릿(가산 아님) — 잿빛 연기는 배경을 밝히면 안 된다.</summary>
    Material CreateAlphaBlend(Color tint)
    {
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var mat = new Material(sh);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);   // 0 = Alpha(가산=2와 다름)
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return mat;
    }
}
