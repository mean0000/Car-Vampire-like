using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 후방 위협 청각 힌트 — 콘∩LOS 밖(실물 숨김) 좀비의 접근을 ①발치 방향성 아크 파문(60° 양자화)
/// + ②시간차 발자국(0.5s 지연 얼룩) 하이브리드로 알린다. "청각의 시각화" (2026-06-12 동결).
///
/// ★철학: 정답 UI 금지 — 방향까지만, 위치 금지. "힌트만으로 조준이 가능해지면 실패."
///  - 아크 = 수신자 측 경보(DbD 문법), 발자국 = 위협 측 교사 기호("방금 거기" — 보편 기호로 온보딩 담당).
///  - 단계 기준 = TTC(도달 시간), 거리 아님 — 스프린터는 자동으로 더 멀리서 발화.
///  - 펄스 케이던스 = 발소리 시뮬레이션(보폭 0.7m ÷ 속도) — 빠른 좀비 = 다급한 펄스가 공짜.
///  - 이산 펄스만(연속 추적 금지), 섹터당 최근접 1마리 구동, 동시 아크 ≤ 3, 강도 = max(합산 금지).
///  - 어그로 절규(비어그로→어그로 전환) = 거리·TTC 무관 해당 섹터 중펄스 1회 즉발.
///  - TTC ≤ 1.2s 첫 진입 시 엘 토스트 1회(쿨다운 10s) + 게임 최초 1회 티칭 토스트(PlayerPrefs).
///  - 재감사 트리거: 비조준 무기 도입 시 "힌트만으로 블라인드 처치=0" 재검 / B-005a 오디오 합류 시 발자국 과잉화 관찰.
///
/// ★렌더 = Screen Space Overlay 캔버스 (2026-06-12 디버깅 확정):
///  월드 투명 쿼드는 시야 콘 합성 패스(상시)가 지워버린다 — 플레이에서 24펄스 발화에도 픽셀 0 실측.
///  오버레이 캔버스는 포스트·글리치·콘 합성 이후라 순서 문제 원천 면제(PurgeSnapshotFX 동일 구조).
///  발치 월드 좌표를 매 프레임 스크린에 투영, 카메라 피치만큼 세로 압축(×0.7)으로 "바닥 파문" 느낌 보존.
///  캔버스 order 관행: HUD 0 / RunLoop 50 / Purge 200 — 힌트 = 10.
///
/// 구동: 자가 부트스트랩(GlitchFX 패턴) — 씬 배치·씬 저장 불필요. 튜닝값 전부 코드 상수
/// (SerializeField 씬 오버라이드 함정 회피). 시간축 = scaled(게임 세계 현상 — 정산 정지 수용).
/// 게이트 비활성(콘 드라이버 부재)이면 IsHiddenFromPlayer가 전부 false라 자동 무동작.
/// 빌드 전: ZombieCrush/ThreatArc를 Graphics > Always Included Shaders에 등록할 것.
/// </summary>
[DefaultExecutionOrder(100)]   // ZombieLKPSilhouette(기본 0)의 LateUpdate가 hidden을 갱신한 뒤 조회 — 1프레임 stale 방지
public class RearThreatHint : MonoBehaviour
{
    // ── 튜닝 레버 (전부 코드 상수) ──
    const float ArcRadius = 1.8f;       // 발치 아크 반경(m) — 15m 부감 가독 하한
    const float MaxDistance = 12f;      // 절대 가청 상한(m) — TTC 무관(스프린터 과보호 방지)
    const float TtcFaint = 4.5f;        // 단계 임계(초) — 거리 아닌 도달 시간. 걷는 좀비 ~6m부터
    const float TtcMid = 2.5f;
    const float TtcStrong = 1.2f;

    // ── 06-12 동결 (유저 판정 + PM/uiux 감사): 침식 단겹 아크(B 표준) + 시간차 발자국 상시 하이브리드 ──
    const float AlphaFaint = 0.22f;     // 강도 = B 표준 (판정 랩 3안 중 채택)
    const float AlphaMid = 0.38f;
    const float AlphaStrong = 0.55f;
    const float PulseLife = 0.5f;       // 아크 펄스 수명(초)
    const float FootprintDelay = 0.5f;  // "지금 거기"가 아니라 "방금 거기" — 정답화 방지 가드. ★0.5 = 안전 하한(uiux), 더 줄이지 말 것
    const float FootprintLife = 0.8f;
    const float FootprintSize = 0.55f;  // 블롯 지름(m)
    const int   FootprintQueueCap = 24;
    const float StrideLength = 0.7f;    // 발소리 케이던스 = 보폭 ÷ 속도
    const float MinHintSpeed = 0.5f;    // 이 미만 + 비어그로 = 무발화(정지 좀비는 소리가 없다)
    const int   MaxActiveSectors = 3;   // 호드 스팸 상한 — 초과분은 최근접 거리가 먼 섹터부터 탈락
    const float ToastCooldown = 10f;    // 엘 토스트 전역 쿨다운(초)
    const float RescanInterval = 1f;
    const float AimAlphaMul = 0.7f;     // 조준 중 알파 배율 — 유지하되 콘 발치 비주얼에 살짝 양보
    const int   PoolSize = 24;          // 아크+발자국 하이브리드: 3섹터 × (아크 + 발자국 수명 0.8s 잔류) 최악 ~20
    const float GroundSquash = 0.7f;    // 카메라 피치 45° 보정 — 스크린 원이 아니라 바닥 타원으로 읽히게
    const int   CanvasOrder = 10;       // HUD 0 / RunLoop 50 / Purge 200 사이
    static readonly Color ColorGray = new Color(0.75f, 0.75f, 0.75f);
    static readonly Color ColorAmber = new Color(1f, 0.72f, 0.3f);
    const string BarkLine = "후방 진동. 판독 불가.";   // 어휘 사전 레지스터 — 변경 금지(Story 감사 통과 06-12)
    const string TeachLine = "안내. 바닥 무늬는 진동 기록임. 위치 보증 없음.";   // 티칭 1회 — Story 감사 확정 카피, 변경 금지
    const string TaughtKey = "RearThreatHint.Taught";   // PlayerPrefs — 게임 최초 1회

    static RearThreatHint s_instance;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    static readonly int SeedId = Shader.PropertyToID("_Seed");
    static readonly int StyleId = Shader.PropertyToID("_Style");

    class TrackedZombie
    {
        public ZombieController z;
        public Vector3 prevPos;
        public bool prevAggro;
    }

    class Pulse
    {
        public GameObject go;       // 캔버스 자식(위치+세로압축 담당)
        public RectTransform rect;  // = go의 RectTransform
        public RectTransform arc;   // 손자(회전+크기 담당 — 압축축을 스크린 세로에 고정하기 위한 분리)
        public Material mat;
        public float yawDeg;        // 섹터 중심 월드 방위 — 매 프레임 스크린 투영용
        public float age;
        public float life;          // 발화 시점 프리셋의 수명
        public float baseAlpha;
        public bool active;
        public bool worldAnchored;  // 발자국 = 고정 월드 좌표 (아크 = 발치 추종)
        public Vector3 worldPos;
        public float sizeMeters;
    }

    readonly List<TrackedZombie> _tracked = new List<TrackedZombie>();
    Pulse[] _pool;
    Canvas _canvas;
    readonly float[] _sectorNextPulse = new float[6];
    // 프레임 집계 버퍼(6섹터 × 60°)
    readonly float[] _secDist = new float[6];
    readonly float[] _secSpeed = new float[6];
    readonly int[] _secStage = new int[6];
    readonly bool[] _secScream = new bool[6];
    readonly Vector3[] _secPos = new Vector3[6];        // 섹터 최근접 좀비 위치 — 발자국 스타일용
    readonly Vector3[] _secScreamPos = new Vector3[6];  // 절규 좀비 위치
    readonly bool[] _secStepLeft = new bool[6];         // 좌/우 발 교차

    struct StepMark { public Vector3 pos; public float showAt; public int stage; }
    readonly List<StepMark> _stepQueue = new List<StepMark>();   // 시간차 발자국 지연 큐
    float _rescanTimer;
    float _nextToastOk;
    bool _strongLastFrame;
    bool _taught;
    PartnerAIUI _partner;
    Camera _cam;
    Vector3 _feet;

    /// <summary>자가 부트스트랩 — 씬 배치 불필요, 어떤 씬에서 시작해도 알아서 존재.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (s_instance != null) return;
        var go = new GameObject("RearThreatHint");
        go.AddComponent<RearThreatHint>();
    }

    void Awake()
    {
        if (s_instance != null && s_instance != this) { Destroy(gameObject); return; }
        s_instance = this;
        DontDestroyOnLoad(gameObject);
        _taught = PlayerPrefs.GetInt(TaughtKey, 0) == 1;

        var shader = Shader.Find("ZombieCrush/ThreatArc");
        if (shader == null)
        {
            // 알려진 결함 패턴: 씬/머티리얼이 참조하지 않는 셰이더는 빌드에서 스트립된다.
            Debug.LogWarning("[RearThreatHint] ZombieCrush/ThreatArc 셰이더 미발견 — 힌트 영구 비활성. " +
                             "빌드 스트립 의심: Graphics > Always Included Shaders에 등록 필요.");
            enabled = false;
            return;
        }

        // 오버레이 캔버스 — 콘 합성·글리치·포스트 이후 합성이라 월드 패스에 지워지지 않는다.
        // ⚠️CanvasScaler 부착 금지 — PositionPulse가 WorldToScreenPoint 픽셀 좌표를 1:1로 쓴다(스케일러가 붙으면 위치·크기 어긋남).
        var canvasGo = new GameObject("RearThreatHintCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = CanvasOrder;

        _pool = new Pulse[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var pgo = new GameObject("ThreatArcPulse", typeof(RectTransform));
            var rect = pgo.GetComponent<RectTransform>();
            rect.SetParent(canvasGo.transform, false);
            rect.localScale = new Vector3(1f, GroundSquash, 1f);   // 바닥 타원 보정(스크린 세로축 고정)

            var arcGo = new GameObject("Arc", typeof(RectTransform));
            var arc = arcGo.GetComponent<RectTransform>();
            arc.SetParent(rect, false);
            var img = arcGo.AddComponent<RawImage>();
            var mat = new Material(shader);
            img.material = mat;          // 펄스별 인스턴스 — _Progress/_Alpha 개별 구동
            img.raycastTarget = false;

            pgo.SetActive(false);
            _pool[i] = new Pulse { go = pgo, rect = rect, arc = arc, mat = mat };
        }
    }

    void OnEnable() { UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded; }

    /// <summary>씬 전환 — 이전 씬 좌표 기반 상태(발자국 큐·활성 펄스·추적)는 전부 무효(스테일 표출 방지, 리뷰 C-2).</summary>
    void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        _tracked.Clear();
        _stepQueue.Clear();
        System.Array.Clear(_sectorNextPulse, 0, _sectorNextPulse.Length);
        _strongLastFrame = false;
        _partner = null;    // 파괴된 참조 정리 — 다음 리스캔에서 재탐색
        _rescanTimer = 0f;  // 즉시 리스캔
        if (_pool != null)
            for (int i = 0; i < _pool.Length; i++)
                if (_pool[i].active) { _pool[i].active = false; _pool[i].go.SetActive(false); }
    }

    void LateUpdate()
    {
        // 플레이 전용 — 에디터 외부 틱(SendMessage 등) 방어(LKP 사고 교훈).
        if (!Application.isPlaying) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // 정산 timeScale=0 — 펄스도 세계와 함께 멎는다(의도)

        var pc = PlayerController.Instance;
        Transform player = pc != null ? pc.transform : null;
        if (player != null) _feet = player.position;
        if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;

        UpdatePulses(dt);

        var lkp = ZombieLKPSilhouette.Instance;
        if (player == null || lkp == null)
        {
            _tracked.Clear();   // 씬 전환 등 — 어그로 엣지 캐시 리셋
            System.Array.Clear(_sectorNextPulse, 0, _sectorNextPulse.Length);
            _stepQueue.Clear();
            _strongLastFrame = false;
            return;
        }

        _rescanTimer -= dt;
        if (_rescanTimer <= 0f)
        {
            Rescan();
            if (_partner == null) _partner = FindObjectOfType<PartnerAIUI>();   // DDOL이라 씬 리로드마다 재탐색
            _rescanTimer = RescanInterval;
        }

        for (int s = 0; s < 6; s++)
        {
            _secDist[s] = float.MaxValue;
            _secSpeed[s] = 0f;
            _secStage[s] = 0;
            _secScream[s] = false;
        }

        Vector3 pPos = player.position;
        for (int i = _tracked.Count - 1; i >= 0; i--)
        {
            var t = _tracked[i];
            if (t.z == null || t.z.IsDead) { _tracked.RemoveAt(i); continue; }

            Vector3 pos = t.z.transform.position;
            Vector3 vel = (pos - t.prevPos) / dt;
            vel.y = 0f;
            t.prevPos = pos;

            // 어그로 엣지는 가시성과 무관하게 매 프레임 갱신(숨겨진 순간의 전환을 놓치지 않게).
            bool aggro = t.z.IsAggro;
            bool scream = aggro && !t.prevAggro;
            t.prevAggro = aggro;

            if (!lkp.IsHiddenFromPlayer(t.z)) continue;   // 보이는 좀비는 힌트 불필요

            Vector3 toPlayer = pPos - pos;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist < 0.001f) continue;
            int sector = SectorOf(pos - pPos);   // 플레이어→좀비 방위

            // 어그로 절규 — 그르렁 시점, 거리·TTC 무관 즉발 예약.
            if (scream) { _secScream[sector] = true; _secScreamPos[sector] = pos; }

            if (dist > MaxDistance) continue;
            float speed = vel.magnitude;
            if (speed < MinHintSpeed && !aggro) continue;

            float closing = Vector3.Dot(vel, toPlayer / dist);
            if (closing <= 0f) continue;   // 멀어지는 중 = TTC ∞ = 무발화

            float ttc = dist / Mathf.Max(closing, 0.1f);
            int stage = ttc <= TtcStrong ? 3 : ttc <= TtcMid ? 2 : ttc <= TtcFaint ? 1 : 0;
            if (stage == 0) continue;

            // 섹터 집계 — 케이던스는 최근접 1마리가 구동, 강도는 max(합산 금지).
            if (dist < _secDist[sector]) { _secDist[sector] = dist; _secSpeed[sector] = Mathf.Max(speed, MinHintSpeed); _secPos[sector] = pos; }
            if (stage > _secStage[sector]) _secStage[sector] = stage;
        }

        // 동시 활성 섹터 상한 — 초과분은 최근접 거리가 먼 섹터부터 탈락.
        int activeCount = 0;
        for (int s = 0; s < 6; s++) if (_secStage[s] > 0) activeCount++;
        while (activeCount > MaxActiveSectors)
        {
            int worst = -1;
            float worstDist = -1f;
            for (int s = 0; s < 6; s++)
                if (_secStage[s] > 0 && _secDist[s] > worstDist) { worstDist = _secDist[s]; worst = s; }
            _secStage[worst] = 0;
            activeCount--;
        }

        float now = Time.time;
        for (int s = 0; s < 6; s++)
        {
            if (_secScream[s])
            {
                // 절규 = 최소 중펄스 즉발 + 발자국(짧은 지연 0.3s — 위치 즉답 방지).
                FirePulse(s, Mathf.Max(_secStage[s], 2));
                EnqueueStep(_secScreamPos[s], s, Mathf.Max(_secStage[s], 2), now, 0.3f);
                _sectorNextPulse[s] = now + 0.15f;          // 직후 케이던스 중복 방지
                continue;
            }
            if (_secStage[s] <= 0) continue;
            if (now < _sectorNextPulse[s]) continue;
            // 하이브리드(06-12 채택): 발치 아크(방향) + 시간차 발자국(흔적) 동시.
            FirePulse(s, _secStage[s]);
            EnqueueStep(_secPos[s], s, _secStage[s], now, FootprintDelay);
            // 발소리 케이던스 — 보폭 ÷ 최근접 좀비 속도. 빠른 좀비 = 다급한 펄스.
            _sectorNextPulse[s] = now + Mathf.Clamp(StrideLength / _secSpeed[s], 0.1f, 1.4f);
        }

        // 시간차 발자국 — 만기된 큐 표출("방금 거기").
        for (int i = _stepQueue.Count - 1; i >= 0; i--)
        {
            if (now < _stepQueue[i].showAt) continue;
            FireFootprint(_stepQueue[i].pos, _stepQueue[i].stage);
            _stepQueue.RemoveAt(i);
        }

        // 티칭(게임 최초 1회) — 첫 힌트 발화 시 기호를 엘이 가르친다(uiux 감사 요구, Story 확정 카피).
        bool anyStage = false;
        for (int s = 0; s < 6; s++) if (_secStage[s] > 0 || _secScream[s]) { anyStage = true; break; }
        if (!_taught && anyStage && _partner != null)
        {
            _partner.ShowBark(TeachLine);
            PlayerPrefs.SetInt(TaughtKey, 1);
            _taught = true;
            _nextToastOk = now + 4f;   // 티칭 직후 위급 토스트가 덮어쓰지 않게 짧은 유예
        }

        // 엘 토스트 — 강펄스 단계 첫 진입(상승 엣지) + 전역 쿨다운.
        bool strongNow = false;
        for (int s = 0; s < 6; s++) if (_secStage[s] >= 3) { strongNow = true; break; }
        if (strongNow && !_strongLastFrame && now >= _nextToastOk && _partner != null)
        {
            _partner.ShowBark(BarkLine);
            _nextToastOk = now + ToastCooldown;
        }
        _strongLastFrame = strongNow;
    }

    void UpdatePulses(float dt)
    {
        if (_pool == null) return;
        float aimMul = 1f;
        var rig = PlayerCameraRig.Instance;
        if (rig != null) aimMul = Mathf.Lerp(1f, AimAlphaMul, rig.AimBlend);

        for (int i = 0; i < _pool.Length; i++)
        {
            var p = _pool[i];
            if (!p.active) continue;
            p.age += dt;
            if (p.age >= p.life) { p.active = false; p.go.SetActive(false); continue; }
            float u = p.age / p.life;
            PositionPulse(p);   // 발치 추종 — 파문은 위협이 아니라 "내 귀"의 사건
            float fade = 1f - u;
            fade *= fade;            // 제곱 페이드
            p.mat.SetFloat(ProgressId, u);
            p.mat.SetFloat(AlphaId, p.baseAlpha * fade * aimMul);
        }
    }

    /// <summary>앵커 월드 좌표(아크=발치 추종, 발자국=고정) → 스크린 투영. 위치/크기/회전 매 프레임 갱신.</summary>
    void PositionPulse(Pulse p)
    {
        if (_cam == null) return;
        Vector3 anchor = p.worldAnchored ? p.worldPos : _feet;
        Vector3 sFeet = _cam.WorldToScreenPoint(anchor);
        if (sFeet.z <= 0f) return;   // 카메라 뒤 — 갱신만 생략(수명은 계속 깎임)

        // 픽셀/미터 — 카메라 우측 1m 프로브(부감 고정이라 안정).
        Vector3 sRight = _cam.WorldToScreenPoint(anchor + _cam.transform.right);
        float ppm = ((Vector2)(sRight - sFeet)).magnitude;
        if (ppm < 1f) ppm = 1f;

        // 섹터 월드 방위 → 스크린 방위.
        Vector3 dir = new Vector3(Mathf.Sin(p.yawDeg * Mathf.Deg2Rad), 0f, Mathf.Cos(p.yawDeg * Mathf.Deg2Rad));
        Vector3 sTip = _cam.WorldToScreenPoint(anchor + dir);
        Vector2 sDir = (Vector2)(sTip - sFeet);
        float screenAng = sDir.sqrMagnitude > 0.01f ? Mathf.Atan2(sDir.y, sDir.x) * Mathf.Rad2Deg : 90f;

        p.rect.position = new Vector3(sFeet.x, sFeet.y, 0f);
        float sizePx = p.sizeMeters * ppm;
        p.arc.sizeDelta = new Vector2(sizePx, sizePx);
        p.arc.localRotation = Quaternion.Euler(0f, 0f, screenAng - 90f);   // 셰이더 전방 = UV +y(↑)
    }

    /// <summary>진단용 — 조건 무시하고 6섹터 전부 강펄스 발화. 렌더 경로/조건 경로 분리 테스트.
    /// 플레이 중 RearThreatHint GO의 컴포넌트 우클릭 또는 리플렉션 호출.</summary>
    [ContextMenu("진단: 6섹터 테스트 링")]
    public void DebugTestRing()
    {
        if (!Application.isPlaying) return;
        // 하이브리드 미리보기 — 아크 6방위 + 발자국 주변 2m 6방위
        for (int s = 0; s < 6; s++)
        {
            FirePulse(s, 3);
            float a = (s * 60f + 30f) * Mathf.Deg2Rad;
            FireFootprint(_feet + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 2f, 3);
        }
        Debug.Log($"[RearThreatHint] 테스트 링 발화 — feet:{_feet} pool:{(_pool != null ? _pool.Length : 0)} enabled:{enabled}");
    }

    public static void DebugTestRingStatic()
    {
        if (s_instance != null) s_instance.DebugTestRing();
        else Debug.LogWarning("[RearThreatHint] 인스턴스 없음 — Bootstrap 미실행?");
    }

    void FirePulse(int sector, int stage)
    {
        var p = GetPooled();
        if (p == null) return;   // 풀 고갈 = 이번 펄스 스킵(스팸 상황 — 의도된 상한)
        p.active = true;
        p.age = 0f;
        p.life = PulseLife;
        p.yawDeg = sector * 60f + 30f;
        p.worldAnchored = false;
        p.sizeMeters = ArcRadius * 2f;
        p.baseAlpha = stage >= 3 ? AlphaStrong : stage == 2 ? AlphaMid : AlphaFaint;
        p.mat.SetColor(ColorId, stage >= 3 ? ColorAmber : ColorGray);
        p.mat.SetFloat(SeedId, Random.value * 100f);
        p.mat.SetFloat(StyleId, 0f);   // 침식 단겹(동결) — 풀 공유라 발자국(_Style 2) 잔존값 명시 리셋 필수
        p.mat.SetFloat(ProgressId, 0f);
        p.mat.SetFloat(AlphaId, 0f);   // 첫 틱에서 산출 — 1프레임 풀알파 깜빡임 방지
        PositionPulse(p);
        p.go.SetActive(true);
    }

    /// <summary>시간차 발자국 예약 — 좌/우 발 교차 오프셋(진행 수직 ±0.18m), 큐 상한 초과 시 오래된 것 폐기.</summary>
    void EnqueueStep(Vector3 zombiePos, int sector, int stage, float now, float delay)
    {
        Vector3 toP = _feet - zombiePos;
        toP.y = 0f;
        Vector3 perp = toP.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, toP.normalized) : Vector3.right;
        _secStepLeft[sector] = !_secStepLeft[sector];
        Vector3 pos = zombiePos + perp * (_secStepLeft[sector] ? 0.18f : -0.18f);
        if (_stepQueue.Count >= FootprintQueueCap) _stepQueue.RemoveAt(0);
        _stepQueue.Add(new StepMark { pos = pos, showAt = now + delay, stage = stage });
    }

    /// <summary>발자국 블롯 표출 — 고정 월드 좌표, 빠른 소멸.</summary>
    void FireFootprint(Vector3 worldPos, int stage)
    {
        var p = GetPooled();
        if (p == null) return;
        p.active = true;
        p.age = 0f;
        p.life = FootprintLife;
        p.yawDeg = 0f;
        p.worldAnchored = true;
        p.worldPos = worldPos;
        p.sizeMeters = FootprintSize;
        // 블롯이 작아서 동일 알파면 묻힘 — 1.3배 보정(상한 0.85)
        float a = stage >= 3 ? AlphaStrong : stage == 2 ? AlphaMid : AlphaFaint;
        p.baseAlpha = Mathf.Min(a * 1.3f, 0.85f);
        // 발자국은 항상 무채 — 앰버(단계 언어)는 수신자 측 아크 전용. 위협 측 앰버 블롯은 아이템 오독(uiux 감사).
        p.mat.SetColor(ColorId, ColorGray);
        p.mat.SetFloat(SeedId, Random.value * 100f);
        p.mat.SetFloat(StyleId, 2f);
        p.mat.SetFloat(ProgressId, 0f);
        p.mat.SetFloat(AlphaId, 0f);
        PositionPulse(p);
        p.go.SetActive(true);
    }

    Pulse GetPooled()
    {
        for (int i = 0; i < _pool.Length; i++)
            if (!_pool[i].active) return _pool[i];
        return null;
    }

    /// <summary>외부 스포너용 즉시 편입 — 리스캔 주기(1s)를 기다리지 않는다.
    /// 귀로 웨이브처럼 어그로 상태로 스폰되는 좀비의 힌트 공백(최대 1s) 차단(리뷰 M-1).</summary>
    public static void RegisterImmediate(ZombieController z)
    {
        if (s_instance == null || !s_instance.enabled || z == null || z.IsDead) return;
        for (int i = 0; i < s_instance._tracked.Count; i++)
            if (s_instance._tracked[i].z == z) return;
        s_instance._tracked.Add(new TrackedZombie { z = z, prevPos = z.transform.position, prevAggro = z.IsAggro });
    }

    void Rescan()
    {
        var all = FindObjectsOfType<ZombieController>();
        foreach (var z in all)
        {
            if (z.IsDead) continue;
            bool known = false;
            for (int i = 0; i < _tracked.Count; i++)
                if (_tracked[i].z == z) { known = true; break; }
            if (known) continue;
            _tracked.Add(new TrackedZombie { z = z, prevPos = z.transform.position, prevAggro = z.IsAggro });
        }
    }

    /// <summary>플레이어→위협 방위를 60° 섹터(6개)로 양자화 — 정밀 방향 누설 금지.</summary>
    static int SectorOf(Vector3 dirFromPlayer)
    {
        float ang = Mathf.Atan2(dirFromPlayer.x, dirFromPlayer.z) * Mathf.Rad2Deg;
        ang = (ang % 360f + 360f) % 360f;
        return Mathf.Clamp((int)(ang / 60f), 0, 5);
    }

    void OnDestroy()
    {
        if (s_instance == this) s_instance = null;
        if (_pool != null)
            for (int i = 0; i < _pool.Length; i++)
                if (_pool[i] != null && _pool[i].mat != null) Destroy(_pool[i].mat);
    }
}
