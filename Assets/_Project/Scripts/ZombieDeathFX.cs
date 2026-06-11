using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>BakeMesh로 만든 런타임 Mesh를 프롭 파괴 시 확실히 정리 — 어느 경로(트윈/안전망/씬 언로드)로 죽어도 누수 없음.</summary>
public class BakedMeshCleanup : MonoBehaviour
{
    public Mesh mesh;
    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}

/// <summary>
/// 죽음의 스펙터클 전담 싱글톤 — "신체 일부가 터져서 쓰러지며" (디자인 나침반 §3.1).
/// 역할 3분리(2026-06-10 확정):
///   터짐 = 머리/모자 SMR 베이크 → 분리 프롭 포물선 비산 (질량)
///   쓰러짐 = 시체 잔류는 ZombieController가, 잔류 관리(상한·와해 타이밍)는 여기서 (무게)
///   와해 = 시안 나노봇 버스트 + 축소 → 제거. 디스폰 = 사후처리부의 정화 (세계관)
/// + 킬 링 펄스(시안 도넛 1.8m) — 명중과 구분되는 "죽였다"의 원거리 확인 신호 (아트 검토 1순위).
/// 색은 라이팅 바이블 색상 예산의 시안(0.35, 0.75, 0.85) = 보상 — 킬이 보상임을 색이 말한다.
/// 첫 사용 시 자동 생성. 수치는 CombatFeelConfig(PlayerCombat이 Configure로 주입), 미주입 시 기본값.
/// </summary>
public class ZombieDeathFX : MonoBehaviour
{
    static ZombieDeathFX s_instance;

    // CombatFeelConfig 주입값 (기본값 = SO 기본과 동일)
    static float s_headPopDistance = 3f;
    static float s_corpseLinger = 6f;
    static int s_corpseMax = 18;
    static float s_dissolveTime = 0.3f;
    static float s_killRingSize = 1.8f;

    static readonly Color CyanReward = new Color(0.35f, 0.75f, 0.85f);   // 색상 예산: 시안 = 보상

    /// <summary>PlayerCombat.Awake가 CombatFeelConfig 값으로 1회 주입.</summary>
    public static void Configure(CombatFeelConfig feel)
    {
        if (feel == null) return;
        s_headPopDistance = feel.headPopDistance;
        s_corpseLinger = feel.corpseLinger;
        s_corpseMax = Mathf.Max(1, feel.corpseMax);
        s_dissolveTime = feel.dissolveTime;
        s_killRingSize = feel.killRingSize;
    }

    static ZombieDeathFX Get()
    {
        if (s_instance == null)
        {
            var go = new GameObject("ZombieDeathFX");
            s_instance = go.AddComponent<ZombieDeathFX>();
        }
        return s_instance;
    }

    // ── 런타임 자원 ──
    ParticleSystem _burstPS;        // 시안 와해 버스트(공유 — 위치 이동 후 Emit)
    Material _burstMat;
    Mesh _quadMesh;                 // 킬 링 빌보드 쿼드
    Texture2D _ringTex;             // 도넛(중앙 투명) 텍스처
    Material _ringMat;
    Camera _cam;

    const int RingPoolSize = 8;
    Transform[] _ringTr;
    MeshRenderer[] _ringMR;
    float[] _ringTimer;
    float[] _ringLife;    // 링별 수명(수렴 킬=연장)
    float[] _ringScale;   // 링별 최대 지름(수렴 킬=대형)
    const float RingLife = 0.22f;
    MaterialPropertyBlock _mpb;
    int _ringEvict;

    // 시체/프롭 잔류 관리 — 상한 초과 시 오래된 것부터 와해
    readonly List<ZombieController> _corpses = new List<ZombieController>();
    readonly List<GameObject> _props = new List<GameObject>();
    readonly HashSet<int> _dissolving = new HashSet<int>();   // 상한 퇴출 ↔ 잔류 코루틴의 이중 와해 방지

    void Awake()
    {
        if (s_instance != null && s_instance != this) { Destroy(gameObject); return; }
        s_instance = this;

        _burstMat = CreateAdditive(CyanReward * 3f);
        _burstPS = CreateBurstPS();

        _quadMesh = BuildQuad();
        _ringTex = BuildRingTexture(64);
        _ringMat = CreateAdditive(Color.white);
        if (_ringMat.HasProperty("_BaseMap")) _ringMat.SetTexture("_BaseMap", _ringTex);
        if (_ringMat.HasProperty("_MainTex")) _ringMat.SetTexture("_MainTex", _ringTex);
        if (_ringMat.HasProperty("_Cull")) _ringMat.SetInt("_Cull", 0);

        _mpb = new MaterialPropertyBlock();
        _ringTr = new Transform[RingPoolSize];
        _ringMR = new MeshRenderer[RingPoolSize];
        _ringTimer = new float[RingPoolSize];
        _ringLife = new float[RingPoolSize];
        _ringScale = new float[RingPoolSize];
        for (int i = 0; i < RingPoolSize; i++)
        {
            var go = new GameObject("KillRing" + i);
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = _quadMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _ringMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
            _ringTr[i] = go.transform;
            _ringMR[i] = mr;
        }
    }

    void OnDestroy()
    {
        if (s_instance == this) s_instance = null;
        // 진행 중 잔류/와해 정리 — 파괴된 타겟에 남은 DOTween 트윈이 다음 씬에서 터지지 않게(리뷰 H-4).
        StopAllCoroutines();
        foreach (var p in _props) if (p != null) { p.transform.DOKill(); Destroy(p); }
        _props.Clear();
        foreach (var c in _corpses) if (c != null) c.transform.DOKill();
        _corpses.Clear();
        if (_burstMat != null) Destroy(_burstMat);
        if (_ringMat != null) Destroy(_ringMat);
        if (_ringTex != null) Destroy(_ringTex);
        if (_quadMesh != null) Destroy(_quadMesh);
    }

    // ──────────── 공개 API (전부 static — 호출부 단순) ────────────

    /// <summary>킬 링 펄스 — 모든 킬의 확인 신호(클램프 없음: 위치별이라 겹쳐도 정보).
    /// converged = 수렴샷 킬: 대형(×1.95)+긴 수명+0.08s 뒤 메아리 링 — "이 에너지가 주변을 쓸었다".</summary>
    public static void PlayKillRing(Vector3 pos, bool converged = false) => Get().RingAt(pos, converged);

    /// <summary>
    /// 머리/모자 팝 — 머리 계열 SMR을 현재 포즈로 베이크해 분리 프롭으로 날리고 원본은 끈다.
    /// dir = 비산 기준 방향(탄/타격 방향). converged = 수렴샷 킬: 비산 거리·스핀·버스트 과장(탄도 방향이 공간에 써진다).
    /// </summary>
    public static void PopHead(ZombieController zombie, Vector3 dir, bool converged = false) => Get().PopHeadInternal(zombie, dir, converged);

    /// <summary>시체 등록 — 잔류 후 와해. 상한 초과 시 오래된 시체부터 즉시 와해.</summary>
    public static void RegisterCorpse(ZombieController corpse) => Get().RegisterCorpseInternal(corpse);

    /// <summary>와해 버스트만(프롭/특수 케이스용).</summary>
    public static void PlayDissolveBurst(Vector3 pos)
    {
        var fx = Get();
        fx._burstPS.transform.position = pos;
        fx._burstPS.Emit(26);
    }

    // ──────────── 내부 구현 ────────────

    void RingAt(Vector3 pos, bool converged = false)
    {
        if (converged)
        {
            // 수렴 킬: 대형 본 링 + 0.08s 뒤 메아리 링(빠르고 작게) — 충격파의 2박자.
            SpawnRing(pos, s_killRingSize * 1.95f, RingLife * 1.6f);
            StartCoroutine(EchoRing(pos));
        }
        else SpawnRing(pos, s_killRingSize, RingLife);
    }

    System.Collections.IEnumerator EchoRing(Vector3 pos)
    {
        // 실시간 대기 — 히트스탑(timeScale 0.05) 중에도 "2박자 충격파" 간격이 설계값(리뷰 F-2).
        yield return new WaitForSecondsRealtime(0.08f);
        if (this == null) yield break;   // 씬 전환 중 파괴 방어
        SpawnRing(pos, s_killRingSize * 1.55f, RingLife * 1.2f);
    }

    void SpawnRing(Vector3 pos, float scale, float life)
    {
        int idx = -1;
        for (int i = 0; i < RingPoolSize; i++)
            if (_ringTimer[i] <= 0f) { idx = i; break; }
        if (idx < 0) { idx = _ringEvict; _ringEvict = (_ringEvict + 1) % RingPoolSize; }

        _ringTimer[idx] = life;
        _ringLife[idx] = life;
        _ringScale[idx] = scale;
        _ringTr[idx].position = pos + Vector3.up * 0.9f;
        _ringTr[idx].localScale = Vector3.zero;
        _ringMR[idx].gameObject.SetActive(true);
    }

    void Update()
    {
        if (_cam == null) _cam = Camera.main;
        Vector3 camPos = _cam != null ? _cam.transform.position : Vector3.up * 100f;
        float dt = Time.deltaTime;

        for (int i = 0; i < RingPoolSize; i++)
        {
            if (_ringTimer[i] <= 0f) continue;
            _ringTimer[i] -= dt;
            if (_ringTimer[i] <= 0f) { _ringMR[i].gameObject.SetActive(false); continue; }

            float t = 1f - _ringTimer[i] / Mathf.Max(0.01f, _ringLife[i]);   // 0→1 (링별 수명)
            float pop = 1f - (1f - t) * (1f - t);                   // ease-out 팽창
            _ringTr[i].localScale = Vector3.one * (_ringScale[i] * pop);
            Vector3 to = _ringTr[i].position - camPos;
            if (to.sqrMagnitude > 1e-6f) _ringTr[i].rotation = Quaternion.LookRotation(to);

            Color c = CyanReward * 4f; c.a = 1f - t;                // HDR ×4 — 블룸 통과, 선형 페이드
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            _ringMR[i].SetPropertyBlock(_mpb);
        }
    }

    void PopHeadInternal(ZombieController zombie, Vector3 dir, bool converged = false)
    {
        if (zombie == null) return;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = -zombie.transform.forward;
        dir.Normalize();

        // 수렴 킬 과장 계수 — 탑다운 15m에서 "방향으로 날았다"가 읽히려면 4~5m 비산 필요(art 판정).
        float distMult = converged ? 1.7f : 1f;
        float spinMult = converged ? 1.6f : 1f;

        var smrs = zombie.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in smrs)
        {
            if (smr == null || smr.sharedMesh == null) continue;
            string n = smr.gameObject.name;
            bool isHeadPart = n.StartsWith("Head") || n.StartsWith("Hat");
            bool isFacePart = n.StartsWith("Eye");   // 눈/눈꺼풀 — 프롭 만들기엔 작아서 그냥 끔
            if (!isHeadPart && !isFacePart) continue;

            if (isHeadPart)
            {
                // 현재 포즈 스냅샷 베이크 → 분리 프롭. 스킨 본 의존이 끊긴 정적 메시.
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var prop = new GameObject(n + "_Pop");
                prop.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                var mf = prop.AddComponent<MeshFilter>(); mf.sharedMesh = baked;
                prop.AddComponent<BakedMeshCleanup>().mesh = baked;   // 어느 파괴 경로든 메시 누수 차단
                var mr = prop.AddComponent<MeshRenderer>();
                mr.sharedMaterials = smr.sharedMaterials;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // 포물선 비산(과장 — 20m 카메라 가독) + 랜덤 스핀. 물리 없이 트윈(시체와 동일한 수명 관리).
                // 수렴 킬: 산포각을 좁혀(±35→±15°) 탄도 방향성이 또렷하게 — "어디서 맞았는지"가 공간에 써진다.
                float scatterArc = converged ? 15f : 35f;
                Vector3 scatter = Quaternion.Euler(0f, Random.Range(-scatterArc, scatterArc), 0f) * dir;
                Vector3 target = prop.transform.position + scatter * (s_headPopDistance * distMult * Random.Range(0.8f, 1.2f));
                target.y = zombie.transform.position.y + 0.15f;   // 바닥 근처로 착지
                prop.transform.DOJump(target, Random.Range(1.2f, 1.8f), 1, 0.55f).SetEase(Ease.Linear);
                prop.transform.DORotate(new Vector3(Random.Range(180f, 540f) * spinMult, Random.Range(-180f, 180f), 0f), 0.55f, RotateMode.FastBeyond360);

                _props.Add(prop);
                StartCoroutine(DissolveProp(prop, s_corpseLinger * Random.Range(0.7f, 1f)));
            }

            smr.gameObject.SetActive(false);   // 원본 머리는 끈다 — 목 스텀프(Body 메시)가 남는다
        }
        // 터지는 순간의 시안 버스트 — 절단면의 나노봇 와해(세계관: 고어가 아니라 정화). 수렴 킬은 2배 가까이.
        var headPos = zombie.transform.position + Vector3.up * 1.6f;
        _burstPS.transform.position = headPos;
        _burstPS.Emit(converged ? 34 : 18);
    }

    void RegisterCorpseInternal(ZombieController corpse)
    {
        if (corpse == null) return;
        _corpses.Add(corpse);

        // 상한 초과 — 오래된 시체부터 즉시 와해(호드 성능·화면 청소).
        while (_corpses.Count > s_corpseMax)
        {
            var oldest = _corpses[0];
            _corpses.RemoveAt(0);
            if (oldest != null) DissolveCorpse(oldest);
        }
        StartCoroutine(CorpseLinger(corpse));
    }

    System.Collections.IEnumerator CorpseLinger(ZombieController corpse)
    {
        yield return new WaitForSeconds(s_corpseLinger);
        if (corpse == null) yield break;
        if (!_corpses.Remove(corpse)) yield break;   // 이미 상한 퇴출로 와해됨(리뷰 C-1 — _dissolving 셋과 이중 방어)
        DissolveCorpse(corpse);
    }

    void DissolveCorpse(ZombieController corpse)
    {
        if (corpse == null) return;
        int id = corpse.GetInstanceID();
        if (!_dissolving.Add(id)) return;   // 이미 와해 중(상한 퇴출과 잔류 만료가 겹친 레이스)
        var go = corpse.gameObject;
        // MMSpringScale이 localScale을 계속 쓰면 축소 트윈과 싸운다 — 와해 전에 끈다.
        var spring = go.GetComponent<MoreMountains.Feedbacks.MMSpringScale>();
        if (spring != null) spring.enabled = false;
        PlayDissolveBurst(go.transform.position + Vector3.up * 0.6f);
        go.transform.DOKill();
        go.transform.DOScale(Vector3.zero, s_dissolveTime).SetEase(Ease.InQuad)
            .OnComplete(() => { _dissolving.Remove(id); if (go != null) Destroy(go); });
        Destroy(go, s_dissolveTime + 1f);   // 트윈이 끊겨도 확실히 제거(이때 id는 잔존하나 int뿐 — 무해)
    }

    System.Collections.IEnumerator DissolveProp(GameObject prop, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (prop == null) yield break;
        _props.Remove(prop);
        PlayDissolveBurst(prop.transform.position);
        prop.transform.DOKill();
        var mf = prop.GetComponent<MeshFilter>();
        Mesh baked = mf != null ? mf.sharedMesh : null;
        prop.transform.DOScale(Vector3.zero, s_dissolveTime).SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                if (baked != null) Destroy(baked);   // BakeMesh 인스턴스 누수 방지
                if (prop != null) Destroy(prop);
            });
        Destroy(prop, s_dissolveTime + 1f);
    }

    // ──────────── 자원 생성(PlayerCombat 패턴 재사용) ────────────

    ParticleSystem CreateBurstPS()
    {
        var go = new GameObject("NanoDissolvePS");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
        main.startColor = CyanReward * 2.5f;   // HDR — 블룸 통과(보상의 색)
        main.gravityModifier = -0.15f;          // 나노봇은 살짝 떠오른다 — 낙하(잔해)가 아니라 증발(정화)
        main.maxParticles = 512;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.45f; sh.radiusThickness = 0.4f;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psr.receiveShadows = false;
        psr.sharedMaterial = _burstMat;
        return ps;
    }

    Material CreateAdditive(Color hdr)
    {
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var mat = new Material(sh);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hdr);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", hdr);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return mat;
    }

    Mesh BuildQuad()
    {
        var m = new Mesh { name = "KillRingQuad" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),   new Vector3(-0.5f, 0.5f, 0f),
        };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateBounds();
        return m;
    }

    /// <summary>도넛 링 텍스처 — 중앙·외곽 투명, 반경 0.38~0.5 밴드만 발광(충격파 링).</summary>
    Texture2D BuildRingTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float half = (size - 1) * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half, dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);            // 0(중심)~1(모서리)
                float band = 1f - Mathf.Clamp01(Mathf.Abs(d - 0.44f) / 0.09f);   // 링 밴드
                px[y * size + x] = new Color(1f, 1f, 1f, band * band);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
