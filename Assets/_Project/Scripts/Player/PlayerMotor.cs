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

    [Header("Dash (회피 버스트)")]
    [Tooltip("대시 중 순간 속도(m/s). 거리 ≈ 이값 × dashDuration.")]
    [SerializeField] float dashSpeed = 28f;
    [Tooltip("대시 지속(초). 짧을수록 순간이동에 가깝게.")]
    [SerializeField] float dashDuration = 0.18f;
    [Tooltip("스택 1개 충전 시간(초).")]
    [SerializeField] float dashCooldown = 1f;
    [Tooltip("저장 가능한 대시 횟수(스택). 시작 시 풀충전.")]
    [SerializeField, Min(1)] int maxDashCharges = 2;
    [Tooltip("켜면 대시 동안 무적(접촉 피해 무시) — 회피기로. 끄면 순수 이동기.")]
    [SerializeField] bool dashInvulnerable = true;

    [Header("Collision / Ground")]
    [Tooltip("지면 레이어 — 높이 추종 레이캐스트 대상.")]
    [SerializeField] LayerMask groundLayer = 1 << 6;
    [Tooltip("이동/대시를 막는 장애물 레이어 — 벽 통과 방지.")]
    [SerializeField] LayerMask obstacleMask = 1 << 8;

    const float BodyRadius = 0.5f;   // 벽 가드 스피어캐스트 몸 반경
    const float SkinWidth = 0.05f;   // 벽면 밀착 여유(0이면 다음 프레임 캐스트가 벽 안에서 시작)

    Vector3 _velocity;        // XZ 현재 속도 — 가속/감속으로 목표를 추종(질량감)
    float _dashTimer;         // >0 = 대시 중(남은 시간)
    float _rechargeTimer;     // 다음 1스택 충전까지(스택<max일 때만 흐름)
    int _dashCharges;
    Vector3 _dashDir;
    float _groundOffset;      // 콜라이더 중심→바닥 거리(지면 정렬 시 더해 발이 지면에 닿게)

    public bool IsDashing => _dashTimer > 0f;
    /// <summary>대시 무적 구간인가 — 접촉 피해 무시(HP 컴포넌트가 읽는다).</summary>
    public bool IsInvulnerable => _dashTimer > 0f && dashInvulnerable;
    public int DashCharges => _dashCharges;
    public int MaxDashCharges => maxDashCharges;
    /// <summary>대시 지속(초) — 대시 모션 배속 동기용(애니 드라이버가 읽는다).</summary>
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
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        RechargeDash(dt);

        // 대시 진행 중이면 완료까지 우선(회피 관성 보장 — 공격 잠금보다 먼저).
        if (_dashTimer > 0f) { UpdateDash(dt); return; }

        // 공격 커밋(콤보 등) 중엔 이동/대시 입력을 무시하고 즉시 정지 — 제자리 공격이라 발 미끄러짐이 사라진다.
        if (locked) { _velocity = Vector3.zero; return; }

        Vector3 move = new Vector3(input.move.x, 0f, input.move.y);
        if (move.sqrMagnitude > 1f) move.Normalize();

        if (_dashCharges > 0 && input.dashDown)
            StartDash(move, aimDir);

        if (_dashTimer > 0f) { UpdateDash(dt); return; }   // 이번 프레임 대시 시작 = 고정 방향·고정 속도 버스트

        // 가속/감속 분리: "같은 방향으로 더 빨라질 때"만 가속. 그 외(정지·감속·역방향)는 감속.
        // dot 검사가 없으면 역방향 입력이 가속으로 잡혀 제동 없이 오버슈트한다.
        Vector3 targetVel = move * moveSpeed;
        bool speedingUp = Vector3.Dot(targetVel, _velocity) >= 0f
                          && targetVel.sqrMagnitude >= _velocity.sqrMagnitude;
        float rate = speedingUp ? acceleration : deceleration;
        _velocity = Vector3.MoveTowards(_velocity, targetVel, rate * dt);

        // 입력을 떼도 감속 꼬리가 남으므로 속도가 살아있는 동안 위치·지면 갱신.
        if (_velocity.sqrMagnitude > 0.0001f)
        {
            Vector3 next = transform.position + WallGuardedStep(_velocity * dt);
            next.y = SampleGround(next) + _groundOffset;
            transform.position = next;
        }
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

    void StartDash(Vector3 move, Vector3 aimDir)
    {
        // 방향: 이동 입력이 있으면 그쪽, 없으면(정지) 조준 방향으로 회피 대시.
        Vector3 dir = move;
        if (dir.sqrMagnitude < 0.0001f) dir = aimDir;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;   // 방향을 못 구하면 취소(스택 소모 X)

        // 풀충전에서 처음 소모하는 순간부터 충전 타이머를 건다(이미 충전 중이면 진행도 유지).
        if (_dashCharges >= maxDashCharges) _rechargeTimer = dashCooldown;
        _dashCharges--;
        _dashDir = dir.normalized;
        _dashTimer = dashDuration;
        _velocity = _dashDir * dashSpeed;   // 종료 후 속도 꼬리가 자연 감속하도록 미리 채움
    }

    void UpdateDash(float dt)
    {
        _dashTimer -= dt;
        float step = dashSpeed * dt;

        // 벽 통과 방지: 경로에 장애물이면 그 앞까지만 + 대시 즉시 종료.
        bool wallHit = false;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        if (Physics.SphereCast(origin, BodyRadius, _dashDir, out RaycastHit hit,
                step + SkinWidth, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            step = Mathf.Max(0f, hit.distance - SkinWidth);
            _dashTimer = 0f;
            wallHit = true;
        }

        Vector3 next = transform.position + _dashDir * step;
        next.y = SampleGround(next) + _groundOffset;
        transform.position = next;

        _velocity = wallHit ? Vector3.zero : _dashDir * dashSpeed;
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
