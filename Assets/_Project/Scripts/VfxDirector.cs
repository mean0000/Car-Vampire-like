// VFX 중앙 디렉터 — 기존 4개 풀(TelegraphPool/SmashImpactPool/SlashVfxPool/ProjectilePool) 위의 *얇은 조율층*.
//   적이 텔레그래프/임팩트를 직접 켜는 대신 이 디렉터를 경유 → 호드 culling + 레이어 관리를 한 곳에서.
//
// ════════ 설계 원칙 (CLAUDE.md simplicity 준수) ════════
//   - 라우팅 + culling + wind-up 글로우만. God 클래스 금지.
//   - 기존 풀은 소유권 그대로 유지 — 디렉터는 참조만 보관하는 라우터.
//   - 새 종이 추가되면 Register* 1회 호출로 끝. 기존 종은 건드리지 않음.
//   - 수치(maxConcurrentTelegraphs 등)는 SerializeField 노브로 — 실측 후 튜닝.
//
// ════════ 생명주기 ════════
//   씬에 1개 배치(MonoBehaviour). 자가 부트스트랩 싱글톤으로도 작동.
//   스포너가 자신의 풀을 생성한 뒤 Register*Pool을 호출해 디렉터에 등록.
//   적 드라이버는 RequestTelegraph / RequestImpact를 호출 — 내부적으로 기존 풀 경유.
//
// ════════ culling 규칙 (노브로 뽑아둠 — 실측 후 수치 조정) ════════
//   - maxConcurrentTelegraphs: 동시 활성 텔레그래프 상한. 초과 시 가장 먼 것 생략.
//   - telegraphCullRadius: 이 반경 밖 텔레그래프는 항상 생략(플레이어 시야 밖 노이즈 제거).
//
// ════════ wind-up 글로우 ════════
//   RequestTelegraph 시 해당 적 메시에 레드오렌지 림라이트 펄스(MaterialPropertyBlock + DOTween, 0→HDR→0, ~0.25s).
//   신규 셰이더 0 — URP Lit의 Emission 채널을 MPB로 직접 조작.
//
// ★Venosaur 전환 = 이 파일의 RequestImpact를 VenosaurBrawler가 쓰는 것 1가지만.
//   나머지 종(Crassorrid/Caniathrox/Dimaxillosaurus)은 앞으로 이 위에 얹힘.
using System.Collections;
using UnityEngine;
using DG.Tweening;

public class VfxDirector : MonoBehaviour
{
    // ── 싱글톤 ──
    static VfxDirector _instance;

    /// <summary>씬에 이미 존재하는 디렉터를 반환. 없으면 자동생성(씬 배치 우선, 폴백 생성).
    /// ★자동생성 부작용 주의: 폴백 경로에서 이 getter를 부르면 디렉터가 생성된다.
    ///   등록 여부만 확인하려면 <see cref="HasInstance"/>를 사용할 것.</summary>
    public static VfxDirector Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindObjectOfType<VfxDirector>();
            if (_instance == null)
            {
                var go = new GameObject("VfxDirector (auto)");
                _instance = go.AddComponent<VfxDirector>();
            }
            return _instance;
        }
    }

    /// <summary>자동생성 없이 현재 디렉터 존재 여부만 확인.
    /// 폴백 경로에서 Instance를 건드려 의도치 않은 자동생성을 유발하지 않도록 이걸 쓴다(H-2).</summary>
    public static bool HasInstance => _instance != null;

    /// <summary>자동생성 없이 현재 디렉터 참조를 반환. null이면 디렉터 미존재(H-2 폴백 경로용).</summary>
    public static VfxDirector Existing => _instance;

    // ★씬 재시작 시 stale 참조 정리(BruteSlamCoordinator/CaniathroxChaser 패턴과 동일, H-2).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearStatic() => _instance = null;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        _mpb = new MaterialPropertyBlock();   // ★MPB는 여기서 생성(필드 이니셜라이저 금지 — 위 주석)
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ── culling 노브 (SerializeField — Inspector에서 실측 튜닝) ──
    [Header("호드 Culling 노브")]
    [Tooltip("동시 활성 텔레그래프 상한. 초과 시 플레이어에서 먼 요청부터 생략. "
           + "잠정 기본=4. 실측 후 조정.")]
    [SerializeField, Range(1, 16)] int maxConcurrentTelegraphs = 4;

    [Tooltip("이 반경(m) 밖 적의 텔레그래프 요청은 생략. 0 = 거리 제한 없음.")]
    [SerializeField, Min(0f)] float telegraphCullRadius = 0f;

    // ── wind-up 글로우 노브 ──
    [Header("Wind-up 글로우 노브")]
    [Tooltip("글로우 지속시간(s). 텔레그래프 fillDuration보다 짧게 유지(예고 끝 전에 꺼져야 자연스러움).")]
    [SerializeField, Min(0f)] float glowDuration = 0.25f;

    [Tooltip("레드오렌지 글로우 HDR 강도. URP Lit Emission 채널 직접 조작 — 1.5~2.0 권장.")]
    [SerializeField, Min(0f)] float glowHdrIntensity = 1.8f;

    [Tooltip("글로우 색(레드오렌지 기본 — 적 위협색 캐넌). HDR 강도는 glowHdrIntensity로 곱해짐.")]
    [SerializeField] Color glowBaseColor = new Color(1f, 0.42f, 0.1f, 1f); // 레드오렌지

    // ── 등록된 풀 참조 (라우팅 전용 — 소유권은 각 스포너/풀) ──
    TelegraphPool _telegraphPool;
    SmashImpactPool _impactPool;

    // ── 활성 텔레그래프 추적 (culling용) ──
    // 카운트만 필요하므로 TelegraphPad 참조 대신 숫자만.
    // TelegraphPad.Return이 불리면 디렉터의 _activeTelegraphCount도 줄어야 함 — RequestTelegraph가 콜백 래핑.
    int _activeTelegraphCount;

    // ── 플레이어 참조(culling 거리 계산용 — SetPlayer로 주입, null이면 거리 체크 생략) ──
    Transform _player;

    // ── MaterialPropertyBlock 재사용(alloc 0) ──
    // ★Awake에서 생성한다 — 필드 이니셜라이저(= MonoBehaviour 생성자 시점)에서 new MaterialPropertyBlock()을 호출하면
    //   Unity가 "CreateImpl is not allowed from a MonoBehaviour constructor" UnityException을 던진다(런타임에만 발현).
    MaterialPropertyBlock _mpb;
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // ════════ 등록 API (스포너가 자신의 풀 생성 직후 호출) ════════

    /// <summary>텔레그래프 풀 등록. CrassorridLabSpawner 등이 telGo 생성 직후 호출.</summary>
    public void RegisterTelegraphPool(TelegraphPool pool) => _telegraphPool = pool;

    /// <summary>임팩트 풀 등록. 여러 스포너가 각자 다른 풀을 등록할 수 있으나,
    /// 현 설계에서는 공유 1개가 기본. 나중에 종별로 분리 필요시 Dictionary로 확장.</summary>
    public void RegisterImpactPool(SmashImpactPool pool) => _impactPool = pool;

    /// <summary>플레이어 Transform 주입 — culling 거리 계산에 사용. 씬 시작 시 스포너가 호출.</summary>
    public void SetPlayer(Transform player) => _player = player;

    // ════════ 요청 API (드라이버가 호출하는 공개 인터페이스) ════════

    /// <summary>
    /// 텔레그래프 부채꼴 요청. culling 통과 시 TelegraphPool.Acquire().SpawnFan() 호출.
    /// 반환값 = 발동된 TelegraphPad(취소/ForceFull용), culling 생략 시 null.
    /// ★호드 culling: 동시 활성 maxConcurrentTelegraphs 초과 시 원거리 요청 생략.
    /// </summary>
    public TelegraphPad RequestTelegraph(
        Vector3 origin, Vector3 forward, float radius, float angleDeg,
        float fillDuration, float holdAfterFull, Color color,
        float masterAlpha, float fillAlpha, float edgeAlpha, float edgeWorld,
        Renderer[] glowRenderers = null)
    {
        if (_telegraphPool == null) return null;
        if (!PassesCull(origin)) return null;

        var pad = _telegraphPool.Acquire();
        if (pad == null) return null;

        pad.SpawnFan(origin, forward, radius, angleDeg,
                     fillDuration, holdAfterFull, color,
                     masterAlpha, fillAlpha, edgeAlpha, edgeWorld);

        _activeTelegraphCount++;
        // 패드 수명 종료 시 카운트 감소 — TelegraphPad.Return이 TelegraphPool.Return을 호출하므로,
        // 그 이후를 훅하려면 패드 수명을 추적해야 한다. 가장 단순한 방법 = 패드 coroutine 대신 Update 폴링.
        // ★단순 유지: WatchPad를 MonoBehaviour Update 대신 TelegraphCountWatcher로 분리(아래).
        StartCoroutine(WatchPadReturn(pad));

        // wind-up 글로우
        if (glowRenderers != null && glowRenderers.Length > 0)
            PulseGlow(glowRenderers, fillDuration);

        return pad;
    }

    /// <summary>원형 텔레그래프 요청. SpawnCircle 래핑.</summary>
    public TelegraphPad RequestTelegraphCircle(
        Vector3 origin, float radius, float fillDuration, float holdAfterFull,
        Color color, float masterAlpha, float fillAlpha, float edgeAlpha, float edgeWorld,
        Renderer[] glowRenderers = null)
    {
        if (_telegraphPool == null) return null;
        if (!PassesCull(origin)) return null;

        var pad = _telegraphPool.Acquire();
        if (pad == null) return null;

        pad.SpawnCircle(origin, radius, fillDuration, holdAfterFull,
                        color, masterAlpha, fillAlpha, edgeAlpha, edgeWorld);

        _activeTelegraphCount++;
        StartCoroutine(WatchPadReturn(pad));

        if (glowRenderers != null && glowRenderers.Length > 0)
            PulseGlow(glowRenderers, fillDuration);

        return pad;
    }

    /// <summary>링 텔레그래프 요청. SpawnRing 래핑.</summary>
    public TelegraphPad RequestTelegraphRing(
        Vector3 origin, float radius, float inner01,
        float fillDuration, float holdAfterFull,
        Color color, float masterAlpha, float fillAlpha, float edgeAlpha, float edgeWorld,
        Renderer[] glowRenderers = null)
    {
        if (_telegraphPool == null) return null;
        if (!PassesCull(origin)) return null;

        var pad = _telegraphPool.Acquire();
        if (pad == null) return null;

        pad.SpawnRing(origin, radius, inner01, fillDuration, holdAfterFull,
                      color, masterAlpha, fillAlpha, edgeAlpha, edgeWorld);

        _activeTelegraphCount++;
        StartCoroutine(WatchPadReturn(pad));

        if (glowRenderers != null && glowRenderers.Length > 0)
            PulseGlow(glowRenderers, fillDuration);

        return pad;
    }

    /// <summary>
    /// 임팩트 VFX 요청. SmashImpactPool.Acquire().Play() 래핑.
    /// ★Venosaur 컨택 임팩트 전환 진입점 — clawImpactPool.Acquire()를 이 호출 1개로 대체.
    /// </summary>
    public SmashImpactFX RequestImpact(
        Vector3 origin, float radius,
        Color shockColor, float intensity, float coreFlash, float ringWidth,
        float shockDuration, float scorchDuration,
        float dustSizeScale, Color scorchColor)
    {
        if (_impactPool == null) return null;
        var fx = _impactPool.Acquire();
        if (fx == null) return null;
        fx.Play(origin, radius, shockColor, intensity, coreFlash, ringWidth,
                shockDuration, scorchDuration, dustSizeScale, scorchColor);
        return fx;
    }

    // ════════ culling 내부 ════════

    bool PassesCull(Vector3 origin)
    {
        // 1) 동시 활성 상한 체크
        if (_activeTelegraphCount >= maxConcurrentTelegraphs)
        {
            // 더 가까운 요청이면 허용 검토 — 지금은 단순 상한만(수치 튜닝 여유 남김).
            // TODO(실측 후): 원거리 활성 패드를 찾아 교체하는 우선순위 로직 추가.
            return false;
        }

        // 2) 반경 체크 (0 = 제한 없음)
        if (telegraphCullRadius > 0f && _player != null)
        {
            float sqDist = (origin - _player.position).sqrMagnitude;
            if (sqDist > telegraphCullRadius * telegraphCullRadius) return false;
        }

        return true;
    }

    // TelegraphPad가 Return을 호출할 때(= _pool.Return) 카운트를 줄임.
    // TelegraphPad는 Deactivate() 시 _mr.enabled = false로 렌더러를 끈다 — 이걸 폴링.
    // ★오버헤드 최소: coroutine 하나가 1프레임 간격으로 enabled 체크 — Update보다 가벼움.
    // ★GetComponent 비용 회피: Acquire 시점에 MeshRenderer를 1회 캐시.
    //
    // H-1 방어:
    //   (a) null/destroyed 체크: 첫 yield 이후 pad GO가 파괴되면 NRE로 카운트 누락 → Mathf.Max(0) 감소 보장.
    //   (b) watchdog 타임아웃: fillDuration+holdAfterFull+2s 이내로 상한 설정.
    //       TelegraphPool.RecycleOldest가 ForceDeactivate를 호출하면 mr.enabled=false로 즉시 탈출.
    //       혹시 여기서도 루프 탈출 못할 경우를 대비한 하드 상한(무한 고착 방지 — _activeTelegraphCount 언락).
    System.Collections.IEnumerator WatchPadReturn(TelegraphPad pad)
    {
        // 1프레임 대기 — pad가 Begin()으로 MeshRenderer.enabled=true가 된 뒤에 루프 진입.
        yield return null;

        // ★H-1(a): 첫 yield 후 pad 또는 GO가 파괴됐을 경우 즉시 카운트 감소.
        if (pad == null || pad.gameObject == null)
        {
            _activeTelegraphCount = Mathf.Max(0, _activeTelegraphCount - 1);
            yield break;
        }

        var mr = pad.GetComponentInChildren<MeshRenderer>(true);
        if (mr == null)
        {
            // MeshRenderer 없으면(=Build 실패한 풀 패드) 즉시 카운트 감소
            _activeTelegraphCount = Mathf.Max(0, _activeTelegraphCount - 1);
            yield break;
        }

        // ★H-1(b): watchdog 타임아웃 — TelegraphPad의 최장 수명(fillDuration + holdAfterFull)을 초과하면
        //   어떤 경로로든 카운트 감소를 보장. maxConcurrentTelegraphs 고착 방지.
        //   상한 = 넉넉하게 30s(실제 fillDuration+holdAfterFull은 수 초 이내).
        const float k_WatchdogMaxSeconds = 30f;
        float elapsed = 0f;

        // MeshRenderer가 꺼질 때까지(= Deactivate/ForceDeactivate 호출) 대기.
        while (true)
        {
            // ★H-1(a): 루프 진행 중 pad 또는 GO 파괴 시 탈출(NRE 방지).
            if (pad == null || (object)pad.gameObject == null)
                break;
            // 정상 탈출: 비활성화 감지.
            if (!mr.enabled)
                break;
            // ★H-1(b): watchdog 타임아웃.
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= k_WatchdogMaxSeconds)
            {
                Debug.LogWarning("[VfxDirector] WatchPadReturn watchdog 초과 — 강제 카운트 감소. pad 수명이 비정상적으로 길거나 ForceDeactivate 미호출.");
                break;
            }
            yield return null;
        }

        // 어느 경로로든 도달 — 카운트 감소 보장.
        _activeTelegraphCount = Mathf.Max(0, _activeTelegraphCount - 1);
    }

    // ════════ wind-up 글로우 ════════

    void PulseGlow(Renderer[] renderers, float fillDuration)
    {
        // DOTween으로 레드오렌지 Emission 펄스(0 → HDR → 0). 신규 셰이더 0.
        // ★MaterialPropertyBlock 사용: 공유 머티리얼 인스턴싱 없이 per-renderer 조작.

        // ★M-3: fillDuration <= 0 가드 — 0이면 트윈 duration=0으로 0-division/에러 유발.
        if (fillDuration <= 0f) return;

        Color peak = glowBaseColor * glowHdrIntensity;
        // glowDuration 이내로 클램프(fillDuration보다 길면 텔레그래프 끝난 뒤에도 글로우가 남음 — 어색).
        float actualDuration = Mathf.Min(glowDuration, fillDuration);
        // glowDuration 자체가 0인 경우도 방어.
        if (actualDuration <= 0f) return;

        // ★M-2: 같은 렌더러에 연속 텔레그래프 시 트윈이 누적돼 글로우가 떨린다.
        //   진입 시 각 렌더러의 기존 트윈을 Kill해 소유권 확보.
        foreach (var r in renderers)
        {
            if (r == null) continue;
            DOTween.Kill(r);   // SetTarget(r)로 연결된 트윈만 종료.
        }

        float t = 0f;
        DOTween.To(() => t, v =>
        {
            t = v;
            // 0→0.5: 0→peak, 0.5→1: peak→0 (Triangle envelope)
            float normalized = t / actualDuration;
            float envelope = normalized < 0.5f
                ? normalized * 2f
                : 1f - (normalized - 0.5f) * 2f;
            Color c = Color.Lerp(Color.black, peak, envelope);

            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorId, c);
                r.SetPropertyBlock(_mpb);
            }
        }, actualDuration, actualDuration)   // endValue=actualDuration, duration=actualDuration
        .SetEase(Ease.Linear)
        .SetTarget(renderers.Length > 0 ? renderers[0] : null)   // ★M-2: 대표 렌더러를 타겟으로 등록해 Kill 가능하게.
        .OnComplete(() =>
        {
            // 글로우 종료 시 Emission 초기화
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorId, Color.black);
                r.SetPropertyBlock(_mpb);
            }
        });
    }
}
