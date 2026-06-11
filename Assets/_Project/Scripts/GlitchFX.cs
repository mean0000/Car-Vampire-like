using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 싱크 글리치(Sync Glitch) — 시그니처 풀스크린 신호 붕괴 이펙트 드라이버.
///
/// 세계관: 주인공은 나노봇 싱크로 연결된 존재. 동기화율(SyncRate)이 1.0에 가까워질수록
/// (=군체에 잠식될수록) "나"의 신호가 깨진다. 글리치 = 연결 붕괴의 시각 언어.
/// 색상 예산: 시안(0.35,0.75,0.85)=본사/보상(기존), 마젠타=신호 붕괴/규정 외(신규) —
/// 단 마젠타는 직접 칠하지 않고 RGB 채널 분리에서 자연 발생하는 프린지로 등장한다. 빨강 금지.
///
/// 구동: 자가 부트스트랩 싱글톤(DontDestroyOnLoad — 어떤 씬에서도 알아서 존재).
///   Camera.main 니어플레인 직후 자식 쿼드(프러스텀 풀커버)에 ZombieCrush/SyncGlitch 머티리얼.
///   씬 컬러 접근은 requiresColorOption 런타임 On — 렌더러/에셋 파일 무수정 조건의 유일한 우회.
/// 강도 = 베이스라인(sync 0.7→1.0을 0→0.6 리맵 — HUD 비네트와 같은 70% 임계)
///       + Burst 스파이크(동기화 완료/사망 = 1.0, 0.7s) 합산 clamp01.
///
/// ★타이밍 전부 unscaled — 사망 시 timeScale=0(GameOverUI), 히트스탑 0.05.
///   scaled를 쓰면 사망 Burst가 그 프레임에 멎는다(가장 잘 밟는 지뢰).
/// 발작 가드: 베이스라인(상시)은 변위·색분리만(휘도 플래시 없음).
///   화이트 플래시는 Burst 중에만, 최소 간격 0.5s(초당 2회 이하).
/// </summary>
public class GlitchFX : MonoBehaviour
{
    // ── 튜닝 수치(한곳에) ──
    const float SyncThreshold = 0.7f;        // 베이스라인 시작 임계 — HUD 비네트가 70%+에서 램프하는 기존 문법과 동일
    const float BaselineMax = 0.6f;          // sync 1.0에서의 베이스라인 상한(완전 붕괴 1.0은 Burst 몫)
    const float ActivationFloor = 0.005f;    // 총 강도 미만이면 쿼드 비활성 — 평시 비용 0
    const float StutterIntervalMax = 0.14f;  // 시드 재롤 간격 상한(저강도) — "유지하다 툭" 리듬
    const float StutterIntervalMin = 0.06f;  // 시드 재롤 간격 하한(고강도일수록 짧게)
    const float ResubRetryInterval = 0.5f;   // [M-1] 늦은 싱글톤(매니저/플레이어) 재구독 재시도 간격(unscaled)
    const float FlashWindow = 0.06f;         // Burst 화이트 플래시 지속(첫 60ms)
    const float FlashMinInterval = 0.5f;     // 플래시 최소 간격 — 초당 2회 이하(발작 가드)
    const float NearPlaneFactor = 1.05f;     // 쿼드 거리 = near × 1.05(니어플레인 클리핑 마진)
    const float QuadOverscan = 1.02f;        // 프러스텀 가장자리 틈 방지
    const float DeathBurstStrength = 1f;     // 동기화 완료/사망 = 신호 완전 붕괴
    const float DeathBurstDuration = 0.7f;
    const float DebugBaseline = 0.5f;        // ContextMenu 디버그 베이스라인 강도

    [SerializeField] bool _debugBaselineOn;  // "Debug/Baseline 0.5 토글" 상태(인스펙터에서 확인 가능)

    static readonly int IntensityId  = Shader.PropertyToID("_Intensity");
    static readonly int GlitchTimeId = Shader.PropertyToID("_GlitchTime");
    static readonly int SeedId       = Shader.PropertyToID("_Seed");
    static readonly int BurstFlashId = Shader.PropertyToID("_BurstFlash");

    static GlitchFX s_instance;

    /// <summary>
    /// 외부 이벤트용 글리치 스파이크(향후 엘 파편 획득 등). 스파이크 후 ease-out 감쇠,
    /// 베이스라인과 합산 clamp01. 인스턴스 부재/셰이더 미발견 시 no-op.
    /// </summary>
    public static void Burst(float strength, float duration)
    {
        if (s_instance == null || !s_instance.enabled) return;
        s_instance.BurstInternal(strength, duration);
    }

    // ── 런타임 자원 ──
    Material _mat;
    Mesh _mesh;
    GameObject _quadGO;   // 카메라 자식 — 씬 언로드 시 카메라와 함께 파괴될 수 있음 → EnsureQuad로 재생성
    Transform _quadT;
    MeshRenderer _quadMR; // [M-2] 카메라별 렌더 격리용 캐시
    Camera _cam;

    // 구독 원본 추적 — 씬 전환으로 싱글톤이 교체돼도 정확히 해제하기 위해 인스턴스를 기억
    SyncRateManager _subbedSync;
    PlayerController _subbedPlayer;

    // ── 상태(전부 unscaled 기준) ──
    float _sync;                       // 마지막 OnSyncChanged 값
    float _burstStrength;
    float _burstDuration = 0.01f;
    float _burstStart = -999f;
    float _flashStrength;
    float _flashUntil = -999f;
    float _lastFlashTime = -999f;
    float _nextSeedTime;
    float _nextResubTime;   // [M-1] 다음 재구독 재시도 시각(unscaled)

    /// <summary>자가 부트스트랩 — 씬 배치 불필요, 어떤 씬에서 시작해도 알아서 존재.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (s_instance != null) return;
        var go = new GameObject("GlitchFX");
        go.AddComponent<GlitchFX>();
    }

    void Awake()
    {
        if (s_instance != null && s_instance != this) { Destroy(gameObject); return; }
        s_instance = this;
        DontDestroyOnLoad(gameObject);

        var shader = Shader.Find("ZombieCrush/SyncGlitch");
        if (shader == null)
        {
            // 알려진 결함 패턴: 씬/머티리얼이 참조하지 않는 셰이더는 빌드에서 스트립된다.
            // 빌드 전 Project Settings > Graphics > Always Included Shaders 등록으로 해결 예정.
            Debug.LogWarning("[GlitchFX] ZombieCrush/SyncGlitch 셰이더 미발견 — 이펙트 영구 비활성. " +
                             "빌드 스트립 의심: Graphics > Always Included Shaders에 등록 필요.");
            enabled = false;
            return;
        }
        _mat = new Material(shader);

        // 프러스텀 풀커버 쿼드 메시 — 단위 사각(스케일은 매 프레임 UpdateQuadFit이 결정)
        _mesh = new Mesh { name = "SyncGlitchQuad" };
        _mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f), new Vector3(0.5f,  0.5f, 0f),
        };
        _mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };   // 셰이더 Cull Off — winding 무관
        _mesh.RecalculateBounds();

        SceneManager.sceneLoaded += OnSceneLoaded;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;   // [M-2] 타 카메라 격리
        Resubscribe();
        AcquireCamera();
    }

    void OnDestroy()
    {
        if (s_instance == this) s_instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;   // [M-2]
        Unsubscribe();
        if (_mat != null) Destroy(_mat);
        if (_mesh != null) Destroy(_mesh);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 싱글톤(SyncRateManager/PlayerController)·카메라가 교체됐을 수 있다 — 재구독·재획득
        Resubscribe();
        AcquireCamera();
    }

    void Update()
    {
        float now = Time.unscaledTime;   // ★unscaled — timeScale 0/0.05에서도 흐른다

        // [M-1] 늦은 싱글톤 재시도 — 구독 시점에 매니저/플레이어가 아직 없었으면 0.5s 간격으로 재구독
        if (now >= _nextResubTime)
        {
            _nextResubTime = now + ResubRetryInterval;
            if ((_subbedSync == null && SyncRateManager.Instance != null) ||
                (_subbedPlayer == null && PlayerController.Instance != null))
                Resubscribe();
        }

        if (_cam == null || _quadGO == null || !_cam.isActiveAndEnabled) AcquireCamera();   // [M-3] 카메라/쿼드 파괴 + 카메라 비활성·교체 대비
        if (_cam == null || _quadGO == null) return;

        // ── 총 강도 = 베이스라인 + Burst 감쇠, 합산 clamp01 ──
        float baseline = _debugBaselineOn ? DebugBaseline : BaselineFromSync(_sync);
        float burst = 0f;
        float elapsed = now - _burstStart;
        if (elapsed < _burstDuration)
        {
            float u = 1f - elapsed / _burstDuration;
            burst = _burstStrength * u * u;   // 스파이크 → ease-out 감쇠(초반 급락, 부드러운 잔향)
        }
        float total = Mathf.Clamp01(baseline + burst);

        if (total < ActivationFloor)
        {
            if (_quadGO.activeSelf) _quadGO.SetActive(false);   // 평시 비용 0
            return;
        }
        if (!_quadGO.activeSelf) _quadGO.SetActive(true);

        UpdateQuadFit();

        // ── 스터터 리듬 — 매 프레임 연속 변화가 아니라 "유지하다 툭 바뀌는" 인상 ──
        // 불규칙 간격(60~140ms, unscaled)으로 시드 재롤. 강도가 높을수록 간격 짧게.
        if (now >= _nextSeedTime)
        {
            _mat.SetFloat(SeedId, Random.Range(0f, 1000f));
            float interval = Mathf.Lerp(StutterIntervalMax, StutterIntervalMin, total) * Random.Range(0.75f, 1.25f);
            // [L-1] 클램프를 지터 범위까지 확장 — 양끝 강도에서도 ±25% 지터 보존
            _nextSeedTime = now + Mathf.Clamp(interval, StutterIntervalMin * 0.75f, StutterIntervalMax * 1.25f);
        }

        _mat.SetFloat(IntensityId, total);
        _mat.SetFloat(GlitchTimeId, now);
        _mat.SetFloat(BurstFlashId, now < _flashUntil ? _flashStrength : 0f);   // 베이스라인에선 항상 0
    }

    // ──────────── 내부 구현 ────────────

    /// <summary>sync 0.7 미만=0, 0.7→1.0을 smoothstep으로 0→0.6 리맵.</summary>
    static float BaselineFromSync(float sync)
    {
        float t = Mathf.Clamp01((sync - SyncThreshold) / (1f - SyncThreshold));
        t = t * t * (3f - 2f * t);   // smoothstep — 임계 직후 완만, 1.0 근처 가속
        return t * BaselineMax;
    }

    void BurstInternal(float strength, float duration)
    {
        float now = Time.unscaledTime;
        _burstStrength = Mathf.Clamp01(strength);
        _burstDuration = Mathf.Max(0.01f, duration);
        _burstStart = now;
        _nextSeedTime = 0f;   // 스파이크 첫 프레임에 즉시 시드 재롤 — 신호가 "툭" 끊기는 인상

        // 화이트 플래시 — Burst 첫 60ms에만, 최소 간격 0.5s(발작 가드: 초당 2회 이하)
        if (now - _lastFlashTime >= FlashMinInterval)
        {
            _lastFlashTime = now;
            _flashUntil = now + FlashWindow;
            _flashStrength = _burstStrength;
        }
    }

    /// <summary>씬 이벤트 구독 — 전부 null-safe. 기존 구독은 정확히 해제 후 새 인스턴스에 재구독.</summary>
    void Resubscribe()
    {
        Unsubscribe();

        var sync = SyncRateManager.Instance;
        if (sync != null)
        {
            sync.OnSyncChanged += HandleSyncChanged;
            sync.OnSyncMaxed += HandleSyncMaxed;
            _subbedSync = sync;
            _sync = sync.SyncRate;   // 씬 진입 시점의 현재값으로 동기화(이벤트 대기 없이)
        }
        else
        {
            _sync = 0f;   // 매니저 없는 씬(허브 등) — 베이스라인 0
        }

        var player = PlayerController.Instance;
        if (player != null)
        {
            player.OnPlayerDied += HandlePlayerDied;
            _subbedPlayer = player;
        }
    }

    void Unsubscribe()
    {
        if (_subbedSync != null)
        {
            _subbedSync.OnSyncChanged -= HandleSyncChanged;
            _subbedSync.OnSyncMaxed -= HandleSyncMaxed;
            _subbedSync = null;
        }
        if (_subbedPlayer != null)
        {
            _subbedPlayer.OnPlayerDied -= HandlePlayerDied;
            _subbedPlayer = null;
        }
    }

    void HandleSyncChanged(float sync) => _sync = sync;
    void HandleSyncMaxed() => BurstInternal(DeathBurstStrength, DeathBurstDuration);   // 동기화 완료 = 신호 완전 붕괴
    void HandlePlayerDied() => BurstInternal(DeathBurstStrength, DeathBurstDuration);

    /// <summary>Camera.main 획득 + 쿼드 부착 + 씬 컬러(Opaque Texture) 카메라 오버라이드 On.</summary>
    void AcquireCamera()
    {
        EnsureQuad();
        var cam = Camera.main;
        if (cam == null)
        {
            _cam = null;
            if (_quadGO != null) _quadGO.SetActive(false);
            return;
        }
        if (cam == _cam && _quadT.parent == cam.transform) return;   // 이미 부착됨

        _cam = cam;
        _quadT.SetParent(_cam.transform, false);
        _quadT.localRotation = Quaternion.identity;

        // 씬 컬러 접근 — 렌더러/에셋 파일 수정 금지 조건의 우회: 카메라별 오버라이드를 런타임 1회 On
        _cam.GetUniversalAdditionalCameraData().requiresColorOption = CameraOverrideOption.On;
    }

    /// <summary>쿼드 재생성 — 카메라 자식이라 씬 언로드에 휩쓸려 파괴될 수 있다.</summary>
    void EnsureQuad()
    {
        if (_quadGO != null) return;
        _quadGO = new GameObject("SyncGlitchQuad");
        _quadT = _quadGO.transform;
        var mf = _quadGO.AddComponent<MeshFilter>();
        mf.sharedMesh = _mesh;
        var mr = _quadGO.AddComponent<MeshRenderer>();
        _quadMR = mr;   // [M-2]
        mr.sharedMaterial = _mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        mr.allowOcclusionWhenDynamic = false;   // 니어플레인 쿼드가 오클루전 컬링에 잘리는 것 방지
        _quadGO.SetActive(false);
    }

    /// <summary>
    /// [M-2] 글리치 쿼드를 메인 카메라 렌더에서만 켠다 — JudgeCam/룩데브 캡처 카메라에
    /// 쿼드·stale opaque texture가 찍히는 검증 오염 차단. begin마다 set이라 복원 불필요.
    /// </summary>
    void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (_quadMR != null) _quadMR.enabled = (cam == _cam);
    }

    /// <summary>니어플레인 직후 프러스텀 풀커버 — FOV/aspect 변동 대비 매 프레임 갱신.</summary>
    void UpdateQuadFit()
    {
        float dist = _cam.nearClipPlane * NearPlaneFactor;
        float h = _cam.orthographic
            ? _cam.orthographicSize * 2f
            : 2f * dist * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float w = h * _cam.aspect;
        _quadT.localPosition = new Vector3(0f, 0f, dist);
        _quadT.localScale = new Vector3(w * QuadOverscan, h * QuadOverscan, 1f);
    }

    // ──────────── 디버그(MCP 없이 인스펙터 우클릭으로 검증) ────────────

    [ContextMenu("Debug/Baseline 0.5 토글")]
    void DebugToggleBaseline() => _debugBaselineOn = !_debugBaselineOn;

    [ContextMenu("Debug/Burst Small(0.5, 0.3s)")]
    void DebugBurstSmall() => BurstInternal(0.5f, 0.3f);

    [ContextMenu("Debug/Burst Death(1, 0.7s)")]
    void DebugBurstDeath() => BurstInternal(1f, 0.7f);
}
