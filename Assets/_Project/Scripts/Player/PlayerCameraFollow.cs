using UnityEngine;

/// <summary>
/// 톱다운 추적 카메라 — 하이브리드 투-스테이트(2026-06-23 액션감 기획, 나+Codex+리서치 수렴).
///
/// ★설계 결론(외부 실측 + 크로스프로바이더): 카메라를 가까이 끌어 액션감을 만들지 않는다.
///   "평소=읽히는 베이스(호드 압박 가독성)"를 깔고, 피니셔/저밀도 순간에만 짧게 시네마틱으로 줌인한다.
///   (Hades/Ruiner/V Rising은 전부 가독성 베이스 + 국소 주스. 저각 근접 카메라는 호드게임에서 후방 사각·과부하.)
///
/// 레이어 (아래→위):
///   1) 베이스 추종: 고정 pitch/yaw/거리에서 SmoothDamp(스케일 시간 — 기존 추종감 보존).
///   2) 시네마틱 상태(투-스테이트): cinEased(0~1)로 pitch↓·거리↓(push-in)·FOV↓(줌인)을 베이스 위에
///      ★실시간(unscaled) 블렌드. 피니셔 타격(EngageCinematic)·저밀도 지속(lowDensitySustain)이 구동.
///      히트스탑(timeScale 0.05) 중에도 줌인이 보이도록 unscaled.
///   3) FOV 줌펀치: 매 타격 위치 불변 미세 줌(평타<피니셔). 총 수축 캡으로 폭주 방지.
///   4) 충격: 방향 킥(AddKick, 칼 닿는 순간) + LabCameraShake 오프셋. XZ·unscaled.
///   5) 호드 밀도 적응(Soulstone 교훈): 고밀도=주스 억제(과부하 방지), 저밀도=은은한 시네마틱 풀인.
///
/// ★회전(pitch)은 이제 시네마틱 비트에만 짧게 움직인다(전 버전 "고정"에서 변경 — 유저 하이브리드 선택).
///   delta(피치·거리·FOV) 0 + 줌펀치(light/finisherFovPunch) 0으로 두면 옛 고정 동작으로 완전 롤백(자기복원형).
/// </summary>
public class PlayerCameraFollow : MonoBehaviour
{
    /// <summary>현재 활성 카메라(전투 코드가 손맛 신호를 쏠 진입점). HitStop과 동일한 정적 접근 패턴.</summary>
    public static PlayerCameraFollow Active { get; private set; }

    [Header("베이스 프레이밍 (가독성 상태 — 평소)")]
    [SerializeField] Transform target;
    [Tooltip("하강 각도(도). 45 = 비스듬한 톱다운. 호드 가독성 베이스(연구 권장 50~55, 시네마틱서 낮춤).")]
    [SerializeField] float pitch = 45f;
    [Tooltip("타깃으로부터 거리(m).")]
    [SerializeField] float distance = 15f;
    [Tooltip("수평 방위각(도). 0 = 정북에서 내려봄.")]
    [SerializeField] float yaw = 0f;
    [Tooltip("추종 부드러움(작을수록 즉각, 클수록 느슨).")]
    [SerializeField] float followSmooth = 0.12f;

    [Header("시네마틱 상태 (투-스테이트 — 피니셔/저밀도 순간만 짧게 극적으로)")]
    [Tooltip("시네마틱 진입 시 피치 가감(도). 음수=더 낮게(극적). 0이면 피치 고정(옛 동작).")]
    [SerializeField] float cinematicPitchDelta = -6f;
    [Tooltip("시네마틱 진입 시 거리 가감(m). 음수=당겨 들어옴(push-in). 0이면 거리 고정.")]
    [SerializeField] float cinematicDistanceDelta = -2.5f;
    [Tooltip("시네마틱 진입 시 FOV 가감(도). 음수=줌인(망원 압축감). 0이면 FOV 고정.")]
    [SerializeField] float cinematicFovDelta = -4f;
    [Tooltip("시네마틱 진입 속도(1/초). 클수록 빨리 당겨짐(타격 순간 스냅).")]
    [SerializeField, Min(0.1f)] float cinematicInSpeed = 11f;
    [Tooltip("시네마틱 복귀 속도(1/초). 진입보다 느리게 — 천천히 풀려야 여운이 산다.")]
    [SerializeField, Min(0.1f)] float cinematicOutSpeed = 3.5f;
    [Tooltip("피니셔/특수 타격 시 시네마틱 유지 시간(초) — 이 시간 동안 줌인 유지 후 복귀.")]
    [SerializeField, Min(0f)] float cinematicHold = 0.14f;

    [Header("FOV 줌펀치 (매 타격 — 위치 안 움직이는 미세 줌)")]
    [Tooltip("평타(1·2타) 줌펀치(도). 연구 권장 1~1.5.")]
    [SerializeField, Min(0f)] float lightFovPunch = 1.2f;
    [Tooltip("피니셔/반격/대시베기/스킬 줌펀치(도). 권장 4~6. 시네마틱 FOV와 합산은 캡으로 제한.")]
    [SerializeField, Min(0f)] float finisherFovPunch = 4f;
    [Tooltip("줌펀치 복귀 속도(지수). 클수록 빨리 원복. 9 ≈ 시정수 0.11s.")]
    [SerializeField, Min(0.1f)] float fovPunchReturn = 9f;
    [Tooltip("FOV 총 수축 상한(도) — 줌펀치+시네마틱 합산 narrowing 클램프(어안·폭주 방지).")]
    [SerializeField, Min(0.5f)] float fovNarrowCap = 7f;

    [Header("전투 임팩트 킥 (스냅 손맛 — 방향성)")]
    [Tooltip("임팩트 킥 복귀 속도(지수 감쇠율) — 클수록 빨리 제자리로(스냅). 14 ≈ 시정수 0.07s.")]
    [SerializeField] float impactKickReturn = 14f;
    [Tooltip("임팩트 킥 누적 상한(m) — 연타 시 킥이 무한 누적돼 카메라가 날아가는 것 방지.")]
    [SerializeField] float impactKickMax = 0.6f;

    [Header("호드 밀도 적응 (Soulstone 교훈 — 대량 전투=주스 억제, 저밀도=시네마틱 풀인)")]
    [Tooltip("밀도 측정 반경(m). 0이면 밀도 적응 전체 비활성(주스 항상 100%, 풀인 없음).")]
    [SerializeField, Min(0f)] float densityRadius = 9f;
    [Tooltip("밀도 측정 레이어(적). KatanaWeapon enemyMask와 동일(기본 layer 7).")]
    [SerializeField] LayerMask densityMask = 1 << 7;
    [Tooltip("이 수 이하의 적 = 저밀도(주스 100% + 시네마틱 풀인 허용).")]
    [SerializeField, Min(0)] int lowDensityCount = 3;
    [Tooltip("이 수 이상의 적 = 고밀도(주스 최대 억제). 사이는 선형 보간.")]
    [SerializeField, Min(1)] int highDensityCount = 14;
    [Tooltip("고밀도 시 줌펀치·시네마틱에 곱할 최소 배율(0.35 = 65% 억제). 1이면 억제 안 함.")]
    [SerializeField, Range(0f, 1f)] float highDensityJuiceScale = 0.35f;
    [Tooltip("저밀도 지속 시 은은한 시네마틱 풀인 가중(0~1). 0이면 평소 완전 가독 고정. 작게 권장(0.2).")]
    [SerializeField, Range(0f, 1f)] float lowDensitySustain = 0.2f;

    [Header("스프린트 프레이밍 (질주 — 룩어헤드 + 풀백)")]
    [Tooltip("질주 중 이동 방향으로 카메라를 미리 내보내는 거리(m) — 갈 곳을 보여줘 속도감↑(플레이어가 화면 뒤로 드리프트). 0=없음.")]
    [SerializeField] float sprintLead = 3f;
    [Tooltip("질주 중 카메라를 뒤로 빼는 거리(m) — 시야 넓혀 속도감↑. 0=없음.")]
    [SerializeField] float sprintPullback = 2.5f;
    [Tooltip("질주 프레이밍 진입/복귀 속도(1/초) — 클수록 빨리 붙고 빨리 풀림.")]
    [SerializeField, Min(0.1f)] float sprintBlendSpeed = 6f;

    Camera _cam;
    float _baseFov;
    bool _hasFov;            // 퍼스펙티브 카메라일 때만 FOV 채널 사용

    Vector3 _vel;
    Vector3 _followPos;      // 킥/시네마틱을 뺀 순수 베이스 추종 위치(SmoothDamp 대상)
    bool _followInit;
    Vector3 _impactKick;     // 전투 임팩트 킥 오프셋(unscaled 감쇠)
    PlayerMotor _motor;      // 스프린트 프레이밍 소스 — target에서 해석(없으면 스프린트 프레이밍 무동작)
    float _sprint;           // 0~1 — 질주 프레이밍 블렌드(_motor.IsSprinting 추종, 스무딩)

    float _cinematic;        // 0~1 — 현재 시네마틱 블렌드(unscaled로 cinTarget 추종)
    float _transient;        // 0~1 — 피니셔 트리거 일시 가중(hold 후 감쇠)
    float _holdTimer;        // 시네마틱 유지 잔여(초)
    float _fovPunch;         // FOV 수축 누적(도, 지수 감쇠)

    float _densityTimer;
    float _densityScale = 1f;   // 1=억제 없음 → highDensityJuiceScale
    float _lowWeight;           // 1=저밀도 → 0=고밀도(저밀도 풀인 구동)
    readonly Collider[] _densityBuf = new Collider[64];

    void OnEnable()  { Active = this; }
    void OnDisable() { if (Active == this) Active = null; }

    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    void Start()
    {
        // FOV 베이스는 Start에서 — 다른 컴포넌트가 Start에서 FOV를 만질 수 있어 Awake 캡처는 이르다(옛 리그 리뷰 M-3 교훈).
        if (_cam != null && !_cam.orthographic) { _baseFov = _cam.fieldOfView; _hasFov = true; }
        // 인스펙터로 target이 미리 꽂힌 경우 모터 해석(런타임 SetTarget 경로는 거기서 해석).
        if (target != null && _motor == null) _motor = target.GetComponent<PlayerMotor>();
    }

    /// <summary>런타임에 타깃을 바꿔 끼울 때(플레이어 스폰 후 와이어링).</summary>
    public void SetTarget(Transform t) { target = t; _motor = t != null ? t.GetComponent<PlayerMotor>() : null; }

    // ── 손맛 API ──────────────────────────────────────────────

    /// <summary>전투 임팩트 킥 — worldDir 방향으로 카메라를 amount(m)만큼 즉시 민다(decay로 복귀).
    /// 칼이 적에 닿는 순간 호출(KatanaWeapon). 누적 상한으로 클램프해 연타 폭주 방지.</summary>
    public void AddKick(Vector3 worldDir, float amount)
    {
        if (amount <= 0f) return;
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.0001f) return;
        _impactKick = Vector3.ClampMagnitude(_impactKick + worldDir.normalized * amount, impactKickMax);
    }

    /// <summary>타격 신호 — 무기는 "평타/피니셔" 의미만 넘기고, 줌펀치·시네마틱 크기는 카메라가 소유(단일 다이얼).
    /// 평타=FOV 줌펀치만, 피니셔/특수=큰 줌펀치 + 시네마틱 상태 진입.</summary>
    public void NotifyHit(bool finisher)
    {
        AddFovPunch(finisher ? finisherFovPunch : lightFovPunch);
        if (finisher) EngageCinematic(1f, cinematicHold);
    }

    /// <summary>FOV 미세 줌펀치(도) — 위치 불변, 빠른 원복. 호드 밀도로 억제.</summary>
    public void AddFovPunch(float degrees)
    {
        if (degrees <= 0f || !_hasFov) return;
        _fovPunch = Mathf.Min(_fovPunch + degrees * _densityScale, fovNarrowCap);
    }

    /// <summary>시네마틱 상태 진입 — weight01(0~1) 세기로 hold초 유지 후 복귀. 피니셔/특수 비트 전용.</summary>
    public void EngageCinematic(float weight01, float hold)
    {
        _transient = Mathf.Max(_transient, Mathf.Clamp01(weight01));
        _holdTimer = Mathf.Max(_holdTimer, hold);
    }

    /// <summary>현재 호드 밀도 주스 배율(1=억제 없음). SmashFeel/쉐이크 측 억제에 재사용 가능.</summary>
    public float DensityJuiceScale => _densityScale;

    // ── 합성 ──────────────────────────────────────────────────

    void LateUpdate()
    {
        if (target == null) return;

        // 시네마틱·FOV·밀도는 실시간(unscaled) — 히트스탑 중에도 줌인이 살아 "박혔다"의 펀치가 보인다.
        float udt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

        UpdateDensity(udt);

        // --- 시네마틱 가중 목표 = max(피니셔 일시, 저밀도 지속) × 밀도 억제 ---
        if (_holdTimer > 0f) _holdTimer -= udt;
        else _transient = Mathf.MoveTowards(_transient, 0f, cinematicOutSpeed * udt);

        float cinTarget = Mathf.Max(_transient, lowDensitySustain * _lowWeight) * _densityScale;
        float spd = cinTarget > _cinematic ? cinematicInSpeed : cinematicOutSpeed;
        _cinematic = Mathf.MoveTowards(_cinematic, cinTarget, spd * udt);
        float cinEased = _cinematic * _cinematic * (3f - 2f * _cinematic);   // smoothstep

        // --- 베이스 추종(가독 상태) — 기존 동작 보존(스케일 시간 SmoothDamp) ---
        Quaternion baseRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 baseDesired = target.position + baseRot * (Vector3.back * distance);
        if (!_followInit) { _followPos = baseDesired; _followInit = true; }
        _followPos = Vector3.SmoothDamp(_followPos, baseDesired, ref _vel, followSmooth);

        // --- 시네마틱 프레이밍 — ★베이스의 *지연된 초점*을 기준으로 delta만 얹는다(target 직접 아님).
        //     이래야 ①delta=0이면 cinPos==_followPos(완전 롤백) ②시네마틱 진입이 추종 지연을 안 지운다(스냅 없음). (Stab/Codex 수렴 MED)
        float pitchC = pitch + cinematicPitchDelta;
        float distC = Mathf.Max(1f, distance + cinematicDistanceDelta);
        Quaternion cinRot = Quaternion.Euler(pitchC, yaw, 0f);
        Vector3 laggedFocus = _followPos - baseRot * (Vector3.back * distance);   // 지연 추종이 현재 바라보는 초점
        Vector3 cinPos = laggedFocus + cinRot * (Vector3.back * distC);

        Vector3 framedPos = Vector3.Lerp(_followPos, cinPos, cinEased);
        Quaternion rot = Quaternion.Slerp(baseRot, cinRot, cinEased);

        // --- FOV: 줌펀치 + 시네마틱 narrowing, [0,cap] 클램프 + 바닥(좁은 base FOV서 0 접근 방지, Stab/Codex 수렴) ---
        float narrow = Mathf.Clamp((-cinematicFovDelta) * cinEased + _fovPunch, 0f, fovNarrowCap);
        if (_hasFov) _cam.fieldOfView = Mathf.Max(10f, _baseFov - narrow);
        _fovPunch = Mathf.Lerp(_fovPunch, 0f, 1f - Mathf.Exp(-fovPunchReturn * udt));   // ★_hasFov 무관 감쇠(ortho 전환 후 누적 폭주 방지)

        // --- 충격: 킥(unscaled 감쇠) + LabCameraShake 오프셋(킥과 동형, XZ·unscaled, 프레임당 1회 소비) ---
        // --- 스프린트 프레이밍: 이동방향 룩어헤드 + 뒤로 풀백(가산 오프셋 — base/시네마틱 재구성에 불간섭) ---
        //     knob 0이면 오프셋 0(자기복원). _sprint 스무딩으로 진입/복귀가 부드럽게 이징된다.
        float sprintT = (_motor != null && _motor.IsSprinting) ? 1f : 0f;
        // ★스프린트 프레이밍은 *이동(scaled)에 결합* — 전투 주스(unscaled)와 달리 월드가 멈추면(히트스탑/일시정지) 같이 멈춘다.
        //   (Codex 게이트=시간축 의도적 결정 / Stab N-2=히트스탑 중 시네마틱과 누적 완화). 정지 프레임 dt=0이면 블렌드 동결.
        float sdt = Mathf.Min(Time.deltaTime, 0.1f);
        _sprint = Mathf.MoveTowards(_sprint, sprintT, sprintBlendSpeed * sdt);
        Vector3 sprintOffset = Vector3.zero;
        if (_sprint > 0.0001f && _motor != null)
        {
            Vector3 v = _motor.Velocity; v.y = 0f;
            Vector3 leadDir = v.sqrMagnitude > 0.01f ? v.normalized : Vector3.zero;
            sprintOffset = (leadDir * sprintLead + rot * Vector3.back * sprintPullback) * _sprint;
        }

        Vector3 shake = LabCameraShake.Evaluate();
        transform.position = framedPos + _impactKick + shake + sprintOffset;
        transform.rotation = rot;

        _impactKick = Vector3.Lerp(_impactKick, Vector3.zero, 1f - Mathf.Exp(-impactKickReturn * udt));
    }

    // 호드 밀도 — 주기적 OverlapSphere(매 프레임 X). _densityScale(주스 억제)·_lowWeight(저밀도 풀인) 구동.
    void UpdateDensity(float udt)
    {
        if (densityRadius <= 0f) { _densityScale = 1f; _lowWeight = 0f; return; }
        _densityTimer -= udt;
        if (_densityTimer > 0f) return;
        _densityTimer = 0.15f;

        int n = Physics.OverlapSphereNonAlloc(target.position, densityRadius, _densityBuf, densityMask, QueryTriggerInteraction.Collide);
        float hi = Mathf.Max(lowDensityCount + 1, highDensityCount);   // 분모 0 방지(고≤저 입력 방어)
        float t = Mathf.Clamp01(Mathf.InverseLerp(lowDensityCount, hi, n));
        _densityScale = Mathf.Lerp(1f, highDensityJuiceScale, t);
        _lowWeight = 1f - t;
    }

    void OnValidate()
    {
        // 밀도 램프 분모가 뒤집히면(고≤저) 즉발 풀억제로 무너진다 — 에디터에서 경고(코드는 hi=Max로 방어하나 의도와 다른 동작).
        if (highDensityCount <= lowDensityCount)
            Debug.LogWarning("[PlayerCameraFollow] highDensityCount는 lowDensityCount보다 커야 밀도 램프가 정상 작동한다.", this);
    }
}
