using UnityEngine;

/// <summary>
/// 톱다운 플레이어 이동 모터 — transform.position만 옮기고 루트 회전은 안 한다(자식 카메라/조준 보호.
/// 몸 회전은 비주얼 드라이버가 조준 방향으로 적용).
///
/// 질량감: 가속/감속을 분리해 "출발은 또렷이 밀고, 멈추거나 꺾을 땐 미끄러지며 정착"하게 한다.
/// 대시(스택 충전 + 무적 옵션), 벽 가드(SphereCast 슬라이드), 지면 레이캐스트 추종을 갖는다.
/// 소음·HP·grapple 같은 폐기방향 책임은 갖지 않는다 — 순수 이동(단일 책임).
///
/// <see cref="PlayerBrain"/>이 Aim 다음에 Tick(in input, aimDir)로 호출 — 정지 대시 방향 폴백 = aimDir.
/// </summary>
public class PlayerMotor : MonoBehaviour
{
    [Header("Move (무게감)")]
    [SerializeField] float moveSpeed = 5f;
    [Tooltip("정지→최대속도 가속도(m/s²). 클수록 즉각적, 작을수록 묵직. 도달시간 ≈ moveSpeed/이값.")]
    [SerializeField] float acceleration = 50f;
    [Tooltip("입력을 떼거나 꺾을 때 감속도(m/s²). 작을수록 더 미끄러지듯 정착.")]
    [SerializeField] float deceleration = 40f;
    [Header("달리기 (계단식 버스트 가속)")]
    [Tooltip("★스프린트 단계별 기본 속도(m/s). 단계 사이는 '버스트'로 순간 점프(계단). 예: 9,15,24 — 극적으로 10,40,80도 가능. 첫 칸이 달리기 시작 속도.")]
    [SerializeField] float[] sprintTierSpeeds = { 9f, 15f, 24f };
    [Tooltip("각 단계 유지 시간(초) — 이만큼 지나면 다음 단계로 버스트(순간 가속).")]
    [SerializeField, Min(0.1f)] float sprintTierDuration = 1.0f;
    [Tooltip("단계 내 완만한 크립(m/s per sec) — 버스트 전 살짝 차오름(10→11→12). 0이면 순수 계단.")]
    [SerializeField] float sprintTierCreep = 1.5f;

    [Header("Dash (코드 구동 자유방향 회피)")]
    [Tooltip("★대시 거리(m) — 직접 조절. 키보드 방향(정지 시 조준)으로 자유 회피, 카디널 스냅 안 함.")]
    [SerializeField] float dashDistance = 3f;
    [Tooltip("대시 이동 시간(초) — 짧을수록 빠르고 딱딱하게(순간이동에 가깝게). 속도 = 거리/이값. (무적창은 iframeDuration 별도.)")]
    [SerializeField] float dashDuration = 0.10f;
    [Tooltip("스택 1개 충전 시간(초).")]
    [SerializeField] float dashCooldown = 1f;
    [Tooltip("저장 가능한 대시 횟수(스택). 시작 시 풀충전.")]
    [SerializeField, Min(1)] int maxDashCharges = 2;
    [Tooltip("켜면 대시 동안 무적(접촉 피해 무시) — 회피기로. 끄면 순수 이동기.")]
    [SerializeField] bool dashInvulnerable = true;
    [Tooltip("★무적(i-frame) 지속(초) — 대시 누른 순간부터. 이동창(dashDuration)과 별개로 조절. " +
             "이 중 앞부분(PlayerHealth.parryWindow)이 패링 창이다.")]
    [SerializeField] float iframeDuration = 0.3f;
    [Tooltip("★대시캔슬 무적 배율(하데스식 대가) — 공격(회수 포함) 도중 대시로 끊으면 그 대시의 i-frame을 이 배율로 줄인다. " +
             "1=대가 없음, 0=무적 완전 희생. 공격적으로 빨리 가면 무방비 = 리스크/리워드. 평시(비공격) 대시는 항상 풀 무적.")]
    [SerializeField, Range(0f, 1f)] float dashCancelIframeScale = 0.5f;

    [Header("Collision / Ground")]
    [Tooltip("지면 레이어 — 높이 추종 레이캐스트 대상.")]
    [SerializeField] LayerMask groundLayer = 1 << 6;
    [Tooltip("이동/대시를 막는 장애물 레이어 — 벽 통과 방지.")]
    [SerializeField] LayerMask obstacleMask = 1 << 8;

    const float BodyRadius = 0.5f;   // 벽 가드 스피어캐스트 몸 반경
    const float SkinWidth = 0.05f;   // 벽면 밀착 여유(0이면 다음 프레임 캐스트가 벽 안에서 시작)

    Vector3 _velocity;        // XZ 현재 속도 — 가속/감속으로 목표를 추종(질량감)
    bool _dashAppliedThisFrame;  // 이번 프레임 ApplyRootStep가 위치를 썼나 — 같은 프레임 이중 적용/로코모션 충돌 차단(Stab H-2/Codex M)
    float _dashTimer;         // >0 = 대시 중(남은 이동 창)
    float _iframeTimer;       // >0 = 무적 중(누른 순간부터 iframeDuration). 이동창과 별개로 흐른다.
    float _dashStartTime = -999f;  // 마지막 대시 시작 시각(unscaledTime) — 퍼펙트 회피 창(대시 시작 프레임) 판정 기준
    Vector3 _dashDir;         // 대시 진행 방향(키보드/조준, 자유방향) — 이동은 코드가 이 방향으로 버스트
    float _rechargeTimer;     // 다음 1스택 충전까지(스택<max일 때만 흐름)
    int _dashCharges;
    bool _dashStartedThisFrame;  // 이번 프레임 대시가 시작됐나 — 애니 드라이버가 엣지에서 DashX/DashY를 잠근다
    float _dashLocalX, _dashLocalY;  // 대시 방향을 facing 프레임에 투영·카디널 스냅(우=+X, 전진=+Y) — 어느 Step 클립을 고를지
    float _groundOffset;      // 콜라이더 중심→바닥 거리(지면 정렬 시 더해 발이 지면에 닿게)
    bool _sprinting;          // 이번 프레임 스프린트 중인가 = Shift 홀드 + 실제 이동 + 非대시·非공격(의도 기반 — 측정속도 아님)
    float _sprintHoldTime;    // 스프린트 누적 시간 — 단계/크립 계산(비스프린트 시 0)
    int _sprintTier = -1;     // 현재 스프린트 단계(-1=비스프린트, 0..N-1)
    bool _sprintBurstedThisFrame;  // 이번 프레임 단계 버스트(순간 점프) 발생 — 카메라/VFX 주스가 읽는다

    public bool IsDashing => _dashTimer > 0f;
    /// <summary>스프린트(달리기) 의도 중인가 — Shift 홀드 + 실제 이동 + 대시/공격 아님.</summary>
    public bool IsSprinting => _sprinting;
    /// <summary>현재 스프린트 단계(0=1단, -1=비스프린트). 애니 속도 매칭·주스 강도에 쓴다.</summary>
    public int SprintTier => _sprintTier;
    /// <summary>이번 프레임 단계가 버스트(순간 점프)했나 — 카메라 킥/스피드라인 트리거.</summary>
    public bool SprintBurstedThisFrame => _sprintBurstedThisFrame;
    /// <summary>이번 프레임에 대시가 막 시작됐나 — 애니 드라이버가 DashX/DashY(클립 선택)를 엣지에서 1회 잠근다.</summary>
    public bool DashStartedThisFrame => _dashStartedThisFrame;
    /// <summary>대시 방향(facing 프레임·카디널 스냅). x = 우측(+)/좌측(−), y = 전진(+)/후진(−). 어느 Step 클립을 재생할지 결정.</summary>
    public float DashLocalX => _dashLocalX;
    public float DashLocalY => _dashLocalY;
    /// <summary>대시 무적 구간인가 — 누른 순간부터 iframeDuration 동안(이동창과 별개). HP 컴포넌트가 읽는다.</summary>
    public bool IsInvulnerable => _iframeTimer > 0f && dashInvulnerable;
    /// <summary>대시 무적 옵션 켜짐 여부 — PlayerHealth가 미발화 경고에 읽는다.</summary>
    public bool DashInvulnerable => dashInvulnerable;
    /// <summary>마지막 대시 시작 시각(Time.unscaledTime) — PlayerHealth가 패링 창(닿기 직전 회피) 판정에 쓴다.</summary>
    public float DashStartTime => _dashStartTime;

    /// <summary>지금 대시를 시작할 수 있나 — 충전 있고 대시 중 아님. PlayerBrain이 회피 최우선 캔슬 판정에 쓴다.</summary>
    public bool CanDash => _dashCharges > 0 && _dashTimer <= 0f;
    public int DashCharges => _dashCharges;
    public int MaxDashCharges => maxDashCharges;
    /// <summary>대시 i-frame/busy 창(초) — Step 클립 길이에 맞춘 값(거리 아님).</summary>
    public float DashDuration => dashDuration;
    /// <summary>현재 평면 속도(m/s) — 로코모션 블렌드 매핑·잔상 등에서 공유.</summary>
    public Vector3 Velocity => _velocity;
    /// <summary>충전 중인 다음 스택 진행도(0~1). 풀충전이면 1.</summary>
    public float DashRechargeProgress01 =>
        (_dashCharges >= maxDashCharges || dashCooldown <= 0f) ? 1f
        : 1f - Mathf.Clamp01(_rechargeTimer / dashCooldown);

    void Awake() => _dashCharges = maxDashCharges;

    void Start()
    {
        // 월드 바운드 확정 후(Start) 계산 — Awake 시 스케일/바운드 미확정 회피.
        var col = GetComponent<Collider>();
        _groundOffset = col != null ? col.bounds.center.y - col.bounds.min.y : 0f;
    }

    /// <summary>PlayerBrain이 매 프레임 Aim 다음에 호출. aimDir = 정지 중 대시 방향 폴백(조준 쪽으로 회피).</summary>
    public void Tick(in PlayerInputState input, Vector3 aimDir, bool locked)
    {
        // ★스프린트 플래그는 *어떤 early-return보다 먼저* 리셋(Stab M-1/Codex 수렴) — timeScale=0(정산/일시정지)로
        //   dt<=0 빠져나가도 IsSprinting이 직전 값으로 고착돼, unscaled 카메라가 멈춘 화면서 스프린트 프레이밍을 끄는 것 방지.
        _sprinting = false;              // 기본 false — 아래 일반 이동 경로에서만 true(대시/공격 early-return 경로는 false 유지)
        _sprintBurstedThisFrame = false; // 엣지 1프레임만 true(주스가 이 프레임에 카메라킥/스피드라인)
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        _dashAppliedThisFrame = false;   // 매 프레임 초기화 — 이후 OnAnimatorMove의 ApplyRootStep가 읽는다(Update→OnAnimatorMove 순서 보장)
        _dashStartedThisFrame = false;   // 엣지 1프레임만 true(드라이버가 DashX/DashY를 이 프레임에 잠금)
        // 무적 창 감쇠 — ★unscaled: i-frame은 사람 반응(회피 타이밍)에 귀속(§7). _dashStartTime(unscaled)과 정합.
        //   scaled로 감쇠하면 패링 슬로모 중 대시 시 무적이 timeScale 배율만큼 과장된다(Stab High). 이동 종료 후에도 남은 i-frame 유지.
        if (_iframeTimer > 0f) _iframeTimer -= Time.unscaledDeltaTime;
        RechargeDash(dt);

        // 대시 진행 중이면 완료까지 우선(회피 관성 보장 — 공격 잠금보다 먼저). 코드 버스트가 위치를 만든다.
        if (_dashTimer > 0f) { UpdateDash(dt); return; }

        Vector3 move = new Vector3(input.move.x, 0f, input.move.y);
        if (move.sqrMagnitude > 1f) move.Normalize();

        // ★회피 최우선(self-cancel 캐넌): 공격 커밋(locked) 중에도 대시는 즉시 발동한다 — locked 게이트보다 먼저 평가.
        //   busy=Animator 진실이라 캔슬 프레임엔 아직 IsActionPlaying=true → locked가 들어오지만, 대시가 그 잠금을
        //   무시하고 시작해야 "공격 클립이 끝날 때까지 회피 불가" desync가 풀린다(PlayerBrain이 같은 프레임 Weapon.Cancel).
        // ★전제: 여기 도달 시 _dashTimer<=0f 보장(위 L105 UpdateDash early-return이 진행 중 대시를 거른다).
        //   따라서 CanDash(_dashCharges>0 && _dashTimer<=0f)의 _dashTimer 조건은 이미 성립 — 충전만 확인하면 된다.
        if (_dashCharges > 0 && input.dashDown)
        {
            // locked(공격 커밋 중) 상태에서의 대시 = 공격을 끊는 캔슬 대시 → i-frame 대가(하데스식). 평시 대시는 풀 무적.
            StartDash(move, aimDir, locked);
            if (_dashTimer > 0f) { UpdateDash(dt); return; }   // 이번 프레임 대시 시작 — 고정 방향·고정 속도 버스트
        }

        // 공격 커밋(콤보 등) 중엔 이동 입력을 무시하고 즉시 정지 — 제자리 공격이라 발 미끄러짐이 사라진다.
        // (대시는 위에서 이미 처리 — 회피는 이 잠금을 무시하고 빠져나간다.) 공격은 스프린트 기어를 리셋(멈춰서 벰).
        if (locked) { _velocity = Vector3.zero; _sprintHoldTime = 0f; _sprintTier = -1; return; }

        // 달리기 재개(2026-06-28) — 모멘텀 슬래셔 코어(질주 손맛 화이트박스). Shift 홀드 + 실제 이동이면 스프린트.
        //   여기 도달 시 비대시·비공격(locked) 보장(위에서 early-return) — 추가 가드 불필요.
        _sprinting = input.sprintHeld && move.sqrMagnitude > 0.01f;
        if (_sprinting)
        {
            // ★계단식 버스트 가속: 단계별 기본속도 + 단계 내 완만한 크립, 단계 시간 지나면 다음 단계 속도로 목표 상승.
            //   목표 spd는 단계 상승 시 즉시 점프하나, 실제 _velocity는 아래 MoveTowards(acceleration)로 부드럽게 따라붙는다
            //   (2026-06-28 관성화 — 각진 달리기 해소). 방향도 같은 MoveTowards로 곡선 전환. 크립은 완만히 차오름.
            _sprintHoldTime += dt;
            int n = sprintTierSpeeds != null ? sprintTierSpeeds.Length : 0;
            float spd;
            if (n > 0)
            {
                int tier = Mathf.Min((int)(_sprintHoldTime / Mathf.Max(0.01f, sprintTierDuration)), n - 1);
                // 크립 시간은 단계 시간으로 클램프 — 마지막 단계서 무한 증가(속도 발산) 방지(끝 단계도 한 번 크립 후 평탄).
                float timeInTier = Mathf.Min(_sprintHoldTime - tier * sprintTierDuration, sprintTierDuration);
                spd = sprintTierSpeeds[tier] + sprintTierCreep * timeInTier;
                if (tier != _sprintTier) { _sprintBurstedThisFrame = _sprintTier >= 0; _sprintTier = tier; }   // 단계 상승=버스트(시작 진입 제외)
            }
            else { spd = moveSpeed; _sprintTier = 0; }   // 배열 비면 폴백
            // ★스프린트도 걷기와 같은 velocity inertia(MoveTowards) — 속도 벡터가 곡선지며 티어 속도로 이행(달리기 각짐 해소, Codex+나 진단).
            //   트레이드오프: 티어 '버스트'(순간 점프)가 부드러운 가속 빌드로 바뀜(펀치↓ 부드러움↑). 펀치 원하면 별도 sprintAccel 노브로 분리 가능.
            //   가속/감속 분리 = 걷기와 동일 정책(역방향=감속) — Stab 게이트: 스프린트가 이 분기를 빠뜨려 역전이 더 빨리 수렴하던 것 수정.
            Vector3 targetVel = move.normalized * spd;
            bool sprintSpeedingUp = Vector3.Dot(targetVel, _velocity) >= 0f && targetVel.sqrMagnitude >= _velocity.sqrMagnitude;
            _velocity = Vector3.MoveTowards(_velocity, targetVel, (sprintSpeedingUp ? acceleration : deceleration) * dt);
        }
        else
        {
            _sprintHoldTime = 0f; _sprintTier = -1;
            // 가속/감속 분리: "같은 방향으로 더 빨라질 때"만 가속. 그 외(정지·감속·역방향)는 감속.
            // dot 검사가 없으면 역방향 입력이 가속으로 잡혀 제동 없이 오버슈트한다.
            Vector3 targetVel = move * moveSpeed;
            bool speedingUp = Vector3.Dot(targetVel, _velocity) >= 0f
                              && targetVel.sqrMagnitude >= _velocity.sqrMagnitude;
            float rate = speedingUp ? acceleration : deceleration;
            _velocity = Vector3.MoveTowards(_velocity, targetVel, rate * dt);
        }

        // 입력을 떼도 감속 꼬리가 남으므로 속도가 살아있는 동안 위치·지면 갱신.
        if (_velocity.sqrMagnitude > 0.0001f)
        {
            Vector3 next = transform.position + WallGuardedStep(_velocity * dt);
            next.y = SampleGround(next) + _groundOffset;
            transform.position = next;
        }
    }

    /// <summary>외부(공격 루트모션) 월드 변위를 이동과 동일한 벽가드+지면 파이프라인으로 적용한다.
    /// 애니가 진실 — 공격 런지 거리는 클립이 소유하고, 코드는 이 변위를 받아 옮길 뿐이다.
    /// 공격 커밋 중 <see cref="Tick"/>은 locked 조기반환으로 위치를 안 쓰고 양보하므로, 위치 소유는 Motor 단일.
    /// ★대시 중엔 코드 버스트(<see cref="UpdateDash"/>)가 위치를 소유하므로 양보(루트모션 미사용 — Step 클립은 비주얼만).</summary>
    public void ApplyRootStep(Vector3 worldDelta)
    {
        // 대시가 이 프레임 위치를 소유하면(진행 중이거나 막 적용) 양보 — 대시=코드 버스트 이동, 루트모션 무시.
        if (_dashTimer > 0f || _dashAppliedThisFrame) return;
        worldDelta.y = 0f;
        if (worldDelta.sqrMagnitude < 1e-8f) return;

        Vector3 next = transform.position + WallGuardedStep(worldDelta);
        next.y = SampleGround(next) + _groundOffset;
        transform.position = next;
        _velocity = Vector3.zero;   // 루트모션 구동 중 속도 꼬리 제거 — 공격 종료 후 미끄러짐 방지
    }

    void RechargeDash(float dt)
    {
        if (_dashCharges >= maxDashCharges || dashCooldown <= 0f) return;
        _rechargeTimer -= dt;
        // while: 큰 프레임(렉/일시정지 복귀)에 여러 스택이 한 번에 차도록.
        while (_rechargeTimer <= 0f && _dashCharges < maxDashCharges)
        {
            _dashCharges++;
            _rechargeTimer += dashCooldown;
        }
        if (_dashCharges >= maxDashCharges) _rechargeTimer = 0f;
    }

    void StartDash(Vector3 move, Vector3 aimDir, bool cancelingAction)
    {
        // 방향: 이동 입력이 있으면 그쪽, 없으면(정지) 조준 방향으로 회피 대시.
        Vector3 dir = move;
        if (dir.sqrMagnitude < 0.0001f) dir = aimDir;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;   // 방향을 못 구하면 취소(스택 소모 X)
        dir.Normalize();

        // ★대시는 스프린트 기어를 리셋한다 — 대시 자체가 속도 순간이고, 재스프린트는 0단부터 다시 버스트하며 spool(무음 점프 방지, Stab H-2).
        _sprintHoldTime = 0f; _sprintTier = -1;

        // 대시 방향을 몸 facing(=조준) 프레임에 투영해 '어느 Step 클립'을 고를지 정한다.
        // 비주얼이 aimDir을 향한 채 Step_F(+Z local)를 재생하면 그 루트모션이 aimDir로 회전돼 전진 회피가 된다.
        // 따라서 facing은 조준에 고정하고, 좌/우/뒤 회피는 Step_L/R/B 클립 선택으로 만든다(8방향 모두 가능).
        Vector3 fwd = aimDir; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = transform.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);   // 좌수계: up×fwd = right
        float fwdDot = Vector3.Dot(dir, fwd);
        float rightDot = Vector3.Dot(dir, right);

        // 카디널 스냅: 지배 축 한 개만 1로(F/B/L/R 단일 클립 — 한 동작 = 한 클립, 블렌드 뭉갬 없음).
        if (Mathf.Abs(fwdDot) >= Mathf.Abs(rightDot)) { _dashLocalX = 0f; _dashLocalY = fwdDot >= 0f ? 1f : -1f; }
        else                                          { _dashLocalY = 0f; _dashLocalX = rightDot >= 0f ? 1f : -1f; }

        // 풀충전에서 처음 소모하는 순간부터 충전 타이머를 건다(이미 충전 중이면 진행도 유지).
        if (_dashCharges >= maxDashCharges) _rechargeTimer = dashCooldown;
        _dashCharges--;
        _dashDir = dir;                     // ★자유방향(카디널 스냅 안 함) — 이동은 키보드 방향 그대로
        _dashTimer = dashDuration;          // 이동 창
        // ★누른 순간부터 무적 시작(이동창과 별개). 공격을 끊는 캔슬 대시면 무적을 배율만큼 줄인다(하데스식 대가 — 공격적으로 빨리 가면 무방비).
        _iframeTimer = iframeDuration * (cancelingAction ? dashCancelIframeScale : 1f);
        _dashStartTime = Time.unscaledTime; // 퍼펙트 회피 창 판정 기준 — 슬로모 timeScale에 안 끌리게 unscaled
        _dashStartedThisFrame = true;       // 드라이버가 이 엣지에서 DashX/DashY(비주얼 클립)를 잠그고 Dash 트리거를 건다
        _velocity = Vector3.zero;           // ★속도 꼬리 없음 — 대시 끝나면 딱 멈춤(미끄러짐 제거)
    }

    /// <summary>대시 이동 — 고정 방향(_dashDir)·고정 속도(dashSpeed) 버스트. 벽이면 그 앞까지만 + 즉시 종료.
    /// 위치는 이 메서드가 소유하고 <see cref="ApplyRootStep"/>는 양보한다(루트모션 미사용).</summary>
    void UpdateDash(float dt)
    {
        _dashTimer -= dt;
        float speed = dashDistance / Mathf.Max(0.01f, dashDuration);   // 거리 기반 — 짧은 시간 = 빠르고 딱딱
        float step = speed * dt;

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        if (Physics.SphereCast(origin, BodyRadius, _dashDir, out RaycastHit hit,
                step + SkinWidth, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            step = Mathf.Max(0f, hit.distance - SkinWidth);
            _dashTimer = 0f;   // 벽이면 즉시 종료
        }

        Vector3 next = transform.position + _dashDir * step;
        next.y = SampleGround(next) + _groundOffset;
        transform.position = next;
        _dashAppliedThisFrame = true;   // 이 프레임 위치는 대시가 소유 — ApplyRootStep 양보
        _velocity = Vector3.zero;       // ★속도 꼬리 0 — 대시 끝에 미끄러지지 않고 딱 멈춤
    }

    /// <summary>진행 경로가 막히면 벽 앞까지만 가고 남은 분량은 벽면을 따라 슬라이드(코너 끈적임 방지).
    /// 스피어캐스트라 몸 반경(BodyRadius)이 자연 반영된다.</summary>
    Vector3 WallGuardedStep(Vector3 step)
    {
        float dist = step.magnitude;
        if (dist < 1e-5f) return step;
        Vector3 dir = step / dist;
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        if (!Physics.SphereCast(origin, BodyRadius, dir, out RaycastHit hit,
                dist + SkinWidth, obstacleMask, QueryTriggerInteraction.Ignore))
            return step;

        float allowed = Mathf.Max(0f, hit.distance - SkinWidth);
        Vector3 moved = dir * allowed;

        // 남은 이동을 벽 접면으로 투영 — 한 번 더 차단 검사(안쪽 코너 끼임 방지).
        Vector3 slide = Vector3.ProjectOnPlane(step - moved, hit.normal);
        slide.y = 0f;
        if (slide.sqrMagnitude > 1e-6f &&
            !Physics.SphereCast(origin + moved, BodyRadius, slide.normalized, out _,
                slide.magnitude + SkinWidth, obstacleMask, QueryTriggerInteraction.Ignore))
            moved += slide;

        return moved;
    }

    float SampleGround(Vector3 pos)
    {
        Vector3 origin = new Vector3(pos.x, 200f, pos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f, groundLayer, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return transform.position.y - _groundOffset;   // 레이 미스 시 현재 높이 유지(드리프트 누적 방지)
    }
}
