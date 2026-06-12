using UnityEngine;

/// <summary>
/// 도보 플레이어용 탑다운 카메라 리그 — 카메라 시스템의 단일 권위.
/// 명세: docs/00_authority/2026-06-10-camera-system.md (프레이밍 45°/15m은 씬 트랜스폼이 보존)
///
/// 레이어 구조 (아래에서 위로 합성):
///   1) 추종: 초점(플레이어+커서 리드+속도 리드)을 SmoothDamp XZ로 따라감. Y는 즉시.
///   2) 상태: 조준/울트 등 상태가 리드 배율·FOV를 부드럽게 가감 (외부에서 SetAimState 등으로 구동).
///   3) 충격: 무방향 쉐이크(Perlin) + 방향성 킥(발사 반동) + 펀치인(킬 — 목표 쪽 스냅).
///      전부 XZ 평면 오프셋, 총량 클램프. ★회전·높이(Y)·피치는 절대 건드리지 않는다 —
///      탑다운 멀미 방지 + 스크린스페이스 틸트시프트 밴드 보존.
///
/// ★ 카메라가 Player의 자식이면 부모 변위가 매 프레임 먼저 적용돼 지연이 상쇄된다(랙이 안 생김).
///   → Awake에서 월드 트랜스폼을 유지한 채 분리(SetParent(null)). 씬 파일은 안 건드리고 런타임만 분리.
/// ★ 시간은 unscaledDeltaTime — 슬로모(산데비스탄 등) 중에도 카메라 반응은 실시간이어야 답답하지 않다.
/// </summary>
[DefaultExecutionOrder(-50)]   // MMWiggle(Feel 쉐이크)보다 먼저 실행 → 쉐이크가 추종 위치 위에 얹힘(차량 CameraController와 동일)
[RequireComponent(typeof(Camera))]
public class PlayerCameraRig : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("따라갈 플레이어. 비우면 PlayerController를 자동 검색.")]
    [SerializeField] Transform target;
    [Tooltip("커서 월드 투영용 PlayerCombat. 비우면 target에서 검색. 없으면 커서 리드 비활성(이동 추종만).")]
    [SerializeField] PlayerCombat aimSource;

    [Header("Cursor Lead (멀미 주의 — 0이면 비활성)")]
    [Tooltip("평시 커서 리드 비율. 이원 카메라(2026-06-11): 평시=연결감만 남기고 차분(0.18), 주시(우클릭)=의도 기반으로 크게.")]
    [SerializeField, Range(0f, 0.6f)] float leadFraction = 0.18f;
    [Tooltip("평시 커서 리드 최대 거리(m).")]
    [SerializeField, Min(0f)] float leadMaxDistance = 2f;
    [Tooltip("주시/조준(우클릭 홀드) 시 리드 배율. 0.18×2.2≈커서 쪽 40% — '내가 보겠다고 한 순간'에만 카메라가 넘어간다.")]
    [SerializeField, Min(1f)] float aimLeadBoost = 2.2f;
    [Tooltip("주시/조준 시 리드 최대 거리(m) — 평시 캡과 별도. 코너 너머 정찰의 실질 사거리.")]
    [SerializeField, Min(0f)] float aimLeadMaxDistance = 5.5f;
    // 커서 리드 월드거리 상한 — 0 이하 = 무제한. 평시+조준 합산 최종 커서 리드의 총량 캡(2026-06-12 유저 판정: 이동량 일관).
    [Tooltip("커서 리드 총량 상한(m). 평시·조준 캡과 별도로 최종 커서 리드 크기를 한 번 더 클램프. 0 이하 = 무제한.")]
    [SerializeField] float maxCursorLead = 3f;

    [Header("Velocity Lead (이동 방향 예측 — 0이면 비활성)")]
    [Tooltip("이동 방향으로 미리 보는 시간(초). 속도감 보강용. 과하면 화면이 흐물거린다(swimmy).")]
    [SerializeField, Min(0f)] float velocityLeadTime = 0f;
    [Tooltip("속도 리드 최대 거리(m).")]
    [SerializeField, Min(0f)] float velocityLeadMax = 2f;

    [Header("Follow Damping")]
    [Tooltip("SmoothDamp 추종 시간(초). 액션 기준 0.1~0.25. 1.0은 흐물거림(swimmy) — 구버전 실수값.")]
    [SerializeField, Min(0f)] float followSmoothTime = 0.18f;

    [Header("Aim State (조준 의식 — 외부에서 SetAimState로 구동)")]
    [Tooltip("조준 시 FOV 감소량(도) = 진짜 줌. 구 ±3° 제약은 평시 틸트시프트 밴드 정합용이었고, " +
             "평시 밴드 폐지(2026-06-11)로 해제 — 콘 스택은 플레이어 추적형이라 FOV 변화에 안전.")]
    // 2026-06-12 유저 판정: 줌인 기각("확대되면 그쪽이 제대로 안 보임") — 조준 정보는 커서 리드가 담당.
    // 필드·적용 코드는 노브로 보존(0 = 비활성).
    [SerializeField, Range(0f, 12f)] float aimFovDrop = 0f;
    [Tooltip("조준 진입 속도(1/초). 4≈0.25초 — 들숨처럼 '천천히 당겨져야' 줌이 모션으로 읽힌다(빠르면 컷처럼 보임).")]
    [SerializeField, Min(0.1f)] float aimBlendSpeed = 4f;
    [Tooltip("조준 해제 속도(1/초). 진입보다 빠르게 — 전투 복귀가 굼뜨면 안 된다.")]
    [SerializeField, Min(0.1f)] float aimBlendOutSpeed = 8f;

    /// <summary>조준/주시 블렌드(0~1, 이징 적용) — FOV·리드·콘 수축·어두움·포커스 풀의 단일 구동값.
    /// 선형 진행에 smoothstep을 씌워 "기계적으로 확 붙는" 느낌을 제거(자연스러운 가감속).</summary>
    public float AimBlend => _aimBlend * _aimBlend * (3f - 2f * _aimBlend);

    [Header("Aim Convergence (정조준 수렴 — B-009: 콘이 무너져 거의 직선이 되는 2단계 집중)")]
    [Tooltip("주시 블렌드 완료 후 완전 수렴까지(초) — 의식의 길이. 콘 22°→수렴각으로 무너지는 시간.")]
    [SerializeField, Min(0.1f)] float convergeTime = 0.6f;

    float _convergence;        // 0~1 — 블렌드 완료 후부터 누적, 발사/해제로 리셋
    ConvergeGate _convergeGate = ConvergeGate.Reset;   // 수렴 게이트 — PlayerCombat이 매 프레임 구동

    /// <summary>수렴 진행도(0~1) — 콘 붕괴·수렴샷 판정의 구동값. 1≈"완전히 모인" 상태.</summary>
    public float AimConvergence => _convergence;

    /// <summary>수렴 파괴 — 발사 반동이 집중의 직선을 깨뜨린다(다음 발은 다시 모아야). 한 발 한 발의 리듬.</summary>
    public void BreakConvergence() => _convergence = 0f;

    /// <summary>수렴 게이트 3상태(리뷰 HIGH — grace 중 충전 어뷰즈 차단).
    /// Charge=레이저가 살아있는 타깃을 실제로 비추는 프레임만 누적,
    /// Hold=grace(프레임 지터) 동안 현재값 유지(증감 없음), Reset=즉시 0.</summary>
    public enum ConvergeGate { Reset, Hold, Charge }

    /// <summary>수렴 게이트(2026-06-12 유저 판정: "어디에든 조준" 즉사 금지 — 대상을 쉬지 않고 지속 조준할 때만 충전).
    /// PlayerCombat이 매 프레임 구동: 실명중=Charge / grace 내 타깃 유지=Hold / 그 외=Reset.</summary>
    public void SetConvergeGate(ConvergeGate g) => _convergeGate = g;

    [Header("Impulse — Shake (무방향 Perlin · 피격/폭발용)")]
    [Tooltip("쉐이크 감쇠율(클수록 빨리 멎음).")]
    [SerializeField] float shakeDamping = 12f;
    [Tooltip("쉐이크 진동 주파수(Perlin).")]
    [SerializeField] float shakeFrequency = 22f;

    [Header("Impulse — Kick (방향성 · 발사 반동)")]
    [Tooltip("킥 감쇠율(지수). 18~22 = 60~90ms 복귀.")]
    [SerializeField] float kickDecay = 20f;

    [Header("Impulse — Kick 연사 서스테인 (어택-서스테인 모델 · 멀미 방지)")]
    [Tooltip("이 간격(초) 미만으로 킥이 연속 오면 연사로 판정 — 매발 임펄스 대신 고정 밀림으로 전환. 0.25 ≈ 240RPM.")]
    [SerializeField, Min(0.05f)] float rapidKickInterval = 0.25f;
    [Tooltip("연사 유지 중 반동 방향 고정 밀림(m). 진동(고주파)을 밀림(0Hz)으로 치환해 멀미 원인 제거.")]
    [SerializeField, Min(0f)] float sustainedKickOffset = 0.05f;
    [Tooltip("서스테인 진입/추종 스무딩(초).")]
    [SerializeField, Min(0.01f)] float sustainedRampTime = 0.08f;

    [Header("Impulse — Punch-In (방향성 · 킬 확인)")]
    [Tooltip("펀치인 감쇠율(지수). 킥보다 살짝 느긋하게 복귀.")]
    [SerializeField] float punchDecay = 14f;

    [Header("Impulse — 안전 클램프")]
    [Tooltip("쉐이크+킥+펀치인 합산 오프셋 상한(m). 호드 대량킬 발작 방지.")]
    [SerializeField, Min(0.05f)] float impulseTotalCap = 0.5f;

    /// <summary>도보 카메라 싱글턴 — PlayerCombat·PlayerController가 충격 트리거에 사용.</summary>
    public static PlayerCameraRig Instance { get; private set; }

    Camera _cam;
    Vector3 _baseOffset;     // 시작 시 카메라-플레이어 월드 오프셋(높이/각도 프레이밍 보존)
    float _baseFov;
    Vector3 _lastTargetPos;
    float _velX, _velZ;      // SmoothDamp 내부 속도 상태(축별 분리 — Y 변위가 XZ 수렴에 안 섞이게)
    bool _ready;

    float _shakeIntensity, _shakeTime;
    Vector3 _kickOffset;     // 방향성 킥(반동 반대) — 지수 감쇠
    Vector3 _punchOffset;    // 방향성 펀치인(킬 방향) — 지수 감쇠
    float _lastKickTime = float.NegativeInfinity;   // 연사 판정용(unscaled)
    bool _sustainedFire;     // 연사 서스테인 상태 — 임펄스 누적 대신 고정 밀림 유지
    Vector3 _sustainDir;     // 서스테인 밀림 방향(반동 반대, 최신 킥 방향 추종)
    float _sustainVel;       // 서스테인 SmoothDamp 속도 상태
    bool _aiming;
    float _aimBlend;         // 0~1 — 조준 상태 부드러운 전환

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ── 충격 API ──────────────────────────────────────────────

    /// <summary>무방향 화면 펀치(피격·폭발). intensity ≈ 오프셋 최대치(m).</summary>
    public void TriggerShake(float intensity)
    {
        _shakeIntensity = Mathf.Max(_shakeIntensity, Mathf.Clamp(intensity, 0f, 1f));
    }

    /// <summary>연사 서스테인 중인가 — PlayerCombat이 연사 보조 쉐이크를 끄는 데 사용(22Hz 노이즈가 멀미 주범).</summary>
    public bool IsSustainedFire => _sustainedFire;

    /// <summary>발사 반동 킥 — 사격 반대 방향으로 순간 밀렸다 복귀. dir = 카메라가 밀릴 방향(수평), magnitude(m).
    /// 어택-서스테인: 첫 발(단발)은 풀 임펄스("무게"), rapidKickInterval 미만 간격의 연속 킥은
    /// 임펄스 누적 대신 고정 밀림 유지로 전환 — 5~10Hz 반복 충격(전정계 민감 대역)을 0Hz 밀림으로 치환.</summary>
    public void TriggerKick(Vector3 dir, float magnitude)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f || magnitude <= 0f) return;

        float now = Time.unscaledTime;
        // 히스테리시스 — 발사 간격이 정확히 임계 근처인 총기가 단발/서스테인을 매발 오가며
        // 채터링(멀미 방지 무력화)하지 않게, 이미 서스테인 중이면 임계를 20% 늘려 잡는다(리뷰 M-1).
        float threshold = _sustainedFire ? rapidKickInterval * 1.2f : rapidKickInterval;
        bool rapid = (now - _lastKickTime) < threshold;
        _lastKickTime = now;
        _sustainDir = dir.normalized;   // 서스테인 방향은 항상 최신 반동 방향 추종

        if (rapid)
        {
            _sustainedFire = true;      // 연사 — 임펄스 추가 없이 LateUpdate에서 밀림 유지
            return;
        }
        _sustainedFire = false;
        // 첫 발/단발 — 풀 임펄스. 입력단에서도 채널 캡 — 출력 클램프만 있으면 내부 누적이 캡 해제 순간 터진다(리뷰 M-1).
        _kickOffset = Vector3.ClampMagnitude(_kickOffset + dir.normalized * magnitude, impulseTotalCap);
    }

    /// <summary>킬 펀치인 — 처치 방향으로 카메라가 미세하게 당겨졌다 복귀("탄이 꽂혔다"의 공감각).</summary>
    public void TriggerPunchIn(Vector3 towardDir, float magnitude)
    {
        towardDir.y = 0f;
        if (towardDir.sqrMagnitude < 0.0001f || magnitude <= 0f) return;
        _punchOffset = Vector3.ClampMagnitude(_punchOffset + towardDir.normalized * magnitude, impulseTotalCap);
    }

    /// <summary>조준 상태(조준 의식) — 리드 강화 + FOV 미세 수축. 추후 조준 입력에서 구동.</summary>
    public void SetAimState(bool aiming) => _aiming = aiming;

    // ── Lifecycle ────────────────────────────────────────────

    void Awake()
    {
        Instance = this;
        _cam = GetComponent<Camera>();

        // 부모(Player) 리지드 결합이 지연을 상쇄하므로, 월드 트랜스폼을 유지한 채 분리한다.
        if (transform.parent != null) transform.SetParent(null, true);

        if (target == null)
        {
            var pc = FindObjectOfType<PlayerController>();
            if (pc != null) target = pc.transform;
        }
        if (aimSource == null && target != null) aimSource = target.GetComponent<PlayerCombat>();
    }

    void Start()
    {
        // FOV 베이스는 Start에서 캡처 — 다른 컴포넌트가 Start에서 FOV를 만질 수 있어 Awake 캡처는 이르다(리뷰 M-3).
        _baseFov = _cam.fieldOfView;

        if (target == null) return;
        // 분리 직후의 월드 오프셋 = 기존 프레이밍(높이/뒤로 기운 각도)을 그대로 보존.
        _baseOffset = transform.position - target.position;
        _lastTargetPos = target.position;
        _ready = true;
    }

    void LateUpdate()
    {
        if (!_ready || target == null) return;

        // 슬로모/일시정지와 무관하게 카메라는 실시간 반응(충격 감쇠가 timeScale에 묶이면 답답해진다).
        // 일시정지(timeScale 0) 중에도 커서 리드가 살아있는 것은 의도 — 사무실/정산은 풀스크린 UI라 무해.
        // ★unscaled는 maximumDeltaTime 보호를 안 받는다 — ALT+TAB/씬 전환 스파이크에서 SmoothDamp
        //   속도 상태가 폭주하지 않게 클램프(리뷰 M-2).
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (dt <= 0f) return;

        Vector3 p = target.position;

        // --- 조준 상태 블렌딩(리드 배율·FOV) --- 진입은 들숨(느리게), 해제는 빠르게. 소비는 이징값으로.
        _aimBlend = Mathf.MoveTowards(_aimBlend, _aiming ? 1f : 0f, (_aiming ? aimBlendSpeed : aimBlendOutSpeed) * dt);
        float aimEased = AimBlend;
        // aimFovDrop 자기복원형(리뷰 L-1) — 라이브 튜닝 중 0으로 내려도 항상 베이스 기준으로 다시 쓰므로 FOV가 복원된다.
        if (_cam != null)
            _cam.fieldOfView = _baseFov - aimFovDrop * aimEased;

        // --- 수렴(2단계 집중): 블렌드가 다 들어온 뒤부터, 레이저가 같은 대상을 실제로 비추는 프레임(Charge)만
        //     콘이 직선을 향해 무너진다. Hold(grace 지터 관용)는 유지만 — 안 비추는 동안 충전되지 않는다(리뷰 HIGH).
        //     Reset(타깃 없음·교체·사망·비조준)은 즉시 0 — 허공 조준은 모이지 않는다. ---
        if (_convergeGate == ConvergeGate.Charge && _aiming && _aimBlend >= 0.999f)
            _convergence = Mathf.MoveTowards(_convergence, 1f, dt / Mathf.Max(0.01f, convergeTime));
        else if (_convergeGate == ConvergeGate.Reset)
            _convergence = 0f;

        // --- 커서 리드: 플레이어↔커서 지면 투영점 사이를 leadFraction만큼, 최대 거리로 캡 ---
        Vector3 lead = Vector3.zero;
        if (leadFraction > 0f && _cam != null)
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            Plane ground = new Plane(Vector3.up, new Vector3(0f, p.y, 0f));
            if (ground.Raycast(ray, out float enter))
            {
                Vector3 cursor = ray.GetPoint(enter);
                lead = cursor - p; lead.y = 0f;
                lead *= leadFraction * Mathf.Lerp(1f, aimLeadBoost, aimEased);
                // 캡도 주시 상태에 따라 확장 — 평시엔 짧게(차분), 주시엔 코너 너머까지(이원 카메라).
                float cap = Mathf.Lerp(leadMaxDistance, aimLeadMaxDistance, aimEased);
                float m = lead.magnitude;
                if (m > cap) lead *= cap / m;
                // 총량 캡 — 최종 커서 리드 벡터 크기를 클램프(방향 보존). SmoothDamp 입력 이전(목표값 단계)이라 댐핑과 간섭 없음.
                if (maxCursorLead > 0f) lead = Vector3.ClampMagnitude(lead, maxCursorLead);
            }
        }

        // --- 속도 리드: 직전 프레임 변위로 이동 방향 예측(PlayerController 비침습 — 위치 차분으로 산출) ---
        Vector3 velLead = Vector3.zero;
        if (velocityLeadTime > 0f)
        {
            Vector3 v = (p - _lastTargetPos) / dt; v.y = 0f;
            velLead = v * velocityLeadTime;
            float m = velLead.magnitude;
            if (m > velocityLeadMax) velLead *= velocityLeadMax / m;
        }
        _lastTargetPos = p;

        // 초점 = 플레이어 + 리드. 카메라 목표 = 초점 + 기존 오프셋(높이/각도 보존).
        Vector3 focus = p + lead + velLead;
        Vector3 desired = focus + _baseOffset;
        desired.y = p.y + _baseOffset.y;   // 높이는 지연 없이 지면을 따라가 상하 흔들림 방지

        // XZ만 지연 추종(각 축 독립 SmoothDamp — Y 변위가 속도 상태에 섞여 XZ 수렴을 흔들지 않게), Y는 즉시 고정.
        float smooth = Mathf.Max(0.0001f, followSmoothTime);   // 0이면 Unity가 내부 클램프하지만 의도 명시
        Vector3 cur = transform.position;
        float nx = Mathf.SmoothDamp(cur.x, desired.x, ref _velX, smooth, Mathf.Infinity, dt);
        float nz = Mathf.SmoothDamp(cur.z, desired.z, ref _velZ, smooth, Mathf.Infinity, dt);
        Vector3 pos = new Vector3(nx, desired.y, nz);

        // --- 충격 합성: 쉐이크(무방향) + 킥(반동) + 펀치인(킬) → 총량 클램프 후 가산 ---
        Vector3 impulse = Vector3.zero;

        if (_shakeIntensity > 0.001f)
        {
            _shakeTime = (_shakeTime + dt * shakeFrequency) % 1000f;
            float ox = (Mathf.PerlinNoise(_shakeTime, 0f) - 0.5f) * 2f * _shakeIntensity;
            float oz = (Mathf.PerlinNoise(0f, _shakeTime + 1f) - 0.5f) * 2f * _shakeIntensity;
            impulse += new Vector3(ox, 0f, oz);
            _shakeIntensity = Mathf.Lerp(_shakeIntensity, 0f, 1f - Mathf.Exp(-shakeDamping * dt));
        }

        // 사격이 멈추면(킥 공백 > 연사 간격) 서스테인 해제 → 아래 기존 지수 감쇠로 부드럽게 복귀(릴리즈).
        // 해제도 TriggerKick과 같은 확장 임계(×1.2) — 진입보다 해제가 먼저 일어나면 히스테리시스가 무의미.
        if (_sustainedFire && Time.unscaledTime - _lastKickTime > rapidKickInterval * 1.2f)
            _sustainedFire = false;

        if (_sustainedFire)
        {
            // 연사 서스테인 — 반동 방향으로 "밀려 있는 상태"를 유지(임펄스 반복 없음 → 진동 0Hz).
            float next = Mathf.SmoothDamp(_kickOffset.magnitude, sustainedKickOffset, ref _sustainVel,
                                          sustainedRampTime, Mathf.Infinity, dt);
            _kickOffset = _sustainDir * Mathf.Min(next, impulseTotalCap);
            impulse += _kickOffset;
        }
        else if (_kickOffset.sqrMagnitude > 0.000001f)
        {
            impulse += _kickOffset;
            _kickOffset = Vector3.Lerp(_kickOffset, Vector3.zero, 1f - Mathf.Exp(-kickDecay * dt));
            _sustainVel = 0f;
        }

        if (_punchOffset.sqrMagnitude > 0.000001f)
        {
            impulse += _punchOffset;
            _punchOffset = Vector3.Lerp(_punchOffset, Vector3.zero, 1f - Mathf.Exp(-punchDecay * dt));
        }

        impulse.y = 0f;   // ★Y 금지 — 틸트시프트 밴드·멀미
        if (impulse.sqrMagnitude > impulseTotalCap * impulseTotalCap)
            impulse = impulse.normalized * impulseTotalCap;

        transform.position = pos + impulse;
    }
}
