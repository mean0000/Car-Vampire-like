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
    [Tooltip("플레이어↔커서 사이 어디에 카메라 초점을 둘지. 0=플레이어 중앙, 0.4≈커서 쪽 40%. 멀미 유발 핵심 노브.")]
    [SerializeField, Range(0f, 0.6f)] float leadFraction = 0.4f;
    [Tooltip("커서 리드가 밀어낼 수 있는 최대 거리(m). 멀리 조준해도 캐릭터가 화면 밖으로 안 나가게 캡. 카메라 15m 기준 4m.")]
    [SerializeField, Min(0f)] float leadMaxDistance = 4f;
    [Tooltip("조준 상태에서 리드 배율(>1 = 타겟 쪽이 더 보임). SetAimState(true)일 때 부드럽게 적용.")]
    [SerializeField, Min(1f)] float aimLeadBoost = 1.35f;

    [Header("Velocity Lead (이동 방향 예측 — 0이면 비활성)")]
    [Tooltip("이동 방향으로 미리 보는 시간(초). 속도감 보강용. 과하면 화면이 흐물거린다(swimmy).")]
    [SerializeField, Min(0f)] float velocityLeadTime = 0f;
    [Tooltip("속도 리드 최대 거리(m).")]
    [SerializeField, Min(0f)] float velocityLeadMax = 2f;

    [Header("Follow Damping")]
    [Tooltip("SmoothDamp 추종 시간(초). 액션 기준 0.1~0.25. 1.0은 흐물거림(swimmy) — 구버전 실수값.")]
    [SerializeField, Min(0f)] float followSmoothTime = 0.18f;

    [Header("Aim State (조준 의식 — 외부에서 SetAimState로 구동)")]
    [Tooltip("조준 시 FOV 감소량(도). ±3도 이내 — 틸트시프트 월드 투영 오차 한계.")]
    [SerializeField, Range(0f, 3f)] float aimFovDrop = 2.5f;
    [Tooltip("조준 상태 전환 속도(1/초).")]
    [SerializeField, Min(0.1f)] float aimBlendSpeed = 6f;

    [Header("Impulse — Shake (무방향 Perlin · 피격/폭발용)")]
    [Tooltip("쉐이크 감쇠율(클수록 빨리 멎음).")]
    [SerializeField] float shakeDamping = 12f;
    [Tooltip("쉐이크 진동 주파수(Perlin).")]
    [SerializeField] float shakeFrequency = 22f;

    [Header("Impulse — Kick (방향성 · 발사 반동)")]
    [Tooltip("킥 감쇠율(지수). 18~22 = 60~90ms 복귀.")]
    [SerializeField] float kickDecay = 20f;

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
    bool _aiming;
    float _aimBlend;         // 0~1 — 조준 상태 부드러운 전환

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ── 충격 API ──────────────────────────────────────────────

    /// <summary>무방향 화면 펀치(피격·폭발). intensity ≈ 오프셋 최대치(m).</summary>
    public void TriggerShake(float intensity)
    {
        _shakeIntensity = Mathf.Max(_shakeIntensity, Mathf.Clamp(intensity, 0f, 1f));
    }

    /// <summary>발사 반동 킥 — 사격 반대 방향으로 순간 밀렸다 복귀. dir = 카메라가 밀릴 방향(수평), magnitude(m).</summary>
    public void TriggerKick(Vector3 dir, float magnitude)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f || magnitude <= 0f) return;
        // 입력단에서도 채널 캡 — 출력 클램프만 있으면 내부 누적이 캡 해제 순간 터진다(리뷰 M-1).
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

        // --- 조준 상태 블렌딩(리드 배율·FOV) ---
        _aimBlend = Mathf.MoveTowards(_aimBlend, _aiming ? 1f : 0f, aimBlendSpeed * dt);
        if (_cam != null && aimFovDrop > 0f)
            _cam.fieldOfView = _baseFov - aimFovDrop * _aimBlend;

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
                lead *= leadFraction * Mathf.Lerp(1f, aimLeadBoost, _aimBlend);
                float m = lead.magnitude;
                if (m > leadMaxDistance) lead *= leadMaxDistance / m;
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

        if (_kickOffset.sqrMagnitude > 0.000001f)
        {
            impulse += _kickOffset;
            _kickOffset = Vector3.Lerp(_kickOffset, Vector3.zero, 1f - Mathf.Exp(-kickDecay * dt));
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
