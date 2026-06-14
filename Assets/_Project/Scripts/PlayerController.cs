using UnityEngine;
using MoreMountains.Feedbacks;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("HP")]
    [SerializeField] float maxHP = 100f;
    [Tooltip("피격 시 카메라 쉐이크 세기(m). 발사보다 크게 — 맞았다는 충격.")]
    [SerializeField] float damageShake = 0.4f;
    float _currentHP;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float runMultiplier = 1.7f;    // Shift 달리기 속도 배율
    [SerializeField] float crouchMultiplier = 0.5f; // Ctrl 앉기 속도 배율 (느림)
    [SerializeField] LayerMask groundLayer = 1 << 6;

    [Header("Movement Weight (가속/감속 — 무게감)")]
    [Tooltip("정지→최대속도 가속도(m/s²). 도달시간 ≈ moveSpeed/이값. 클수록 즉각적, 작을수록 묵직.")]
    [SerializeField] float acceleration = 50f;
    [Tooltip("입력을 떼거나 느려질 때 감속도(m/s²). 작을수록 더 미끄러지듯 정착.")]
    [SerializeField] float deceleration = 40f;

    Vector3 _velocity;   // XZ 평면 현재 속도(m/s). 가속/감속으로 목표속도를 추종 → 질량감.

    [Header("Dash (빠른 회피 버스트 + 잔상)")]
    [SerializeField] KeyCode dashKey = KeyCode.Space;
    [Tooltip("조준/주시(우클릭 홀드) 중 이동 속도 배율 — 의식의 비용. 보장 원샷과 반드시 짝(pm 판정).")]
    [SerializeField, Range(0.2f, 1f)] float aimMoveMultiplier = 0.5f;
    [Tooltip("대시 중 순간 속도(m/s). 거리 ≈ 이값 × dashDuration.")]
    [SerializeField] float dashSpeed = 28f;
    [Tooltip("대시 지속 시간(초). 짧을수록 순간이동에 가깝게 톡.")]
    [SerializeField] float dashDuration = 0.18f;
    [Tooltip("대시 스택 1개를 충전하는 데 걸리는 시간(초).")]
    [SerializeField] float dashCooldown = 1f;
    [Tooltip("저장해 둘 수 있는 대시 횟수(스택). 시작 시 풀충전. 연속 대시 = 이 수만큼.")]
    [SerializeField, Min(1)] int maxDashCharges = 2;
    [Tooltip("켜면 대시 동안 무적(좀비 접촉 피해 무시) — 회피기로 동작. 끄면 순수 이동기.")]
    [SerializeField] bool dashInvulnerable = true;
    [Tooltip("대시 순간 소음(호드 유발 가능). 0이면 무음.")]
    [SerializeField] float dashNoise = 30f;
    [Tooltip("대시 경로를 막는 장애물 레이어 — 벽 통과 방지.")]
    [SerializeField] LayerMask dashObstacleMask = 1 << 8;

    const float DashBodyRadius = 0.5f;   // 벽 클램프용 몸 반경 패딩
    float _dashTimer;          // >0이면 대시 중(남은 시간)
    int _dashCharges;          // 현재 보유 대시 스택
    float _rechargeTimer;      // 다음 1스택 충전까지 남은 시간(스택<max일 때만 흐름)
    Vector3 _dashDir;
    PlayerCombat _combat;      // 정지 중 대시 방향 폴백(조준)

    /// <summary>대시 중이면 true — 잔상 컴포넌트가 폴링한다.</summary>
    public bool IsDashing => _dashTimer > 0f;

    /// <summary>대시 지속 시간(초) — 대시 모션 배속 동기용(PlayerLocomotionAnimator가 읽는다).</summary>
    public float DashDuration => dashDuration;

    /// <summary>현재 보유한 대시 스택 수 — HUD가 폴링.</summary>
    public int DashCharges => _dashCharges;
    /// <summary>최대 대시 스택 수 — HUD 아이콘 개수.</summary>
    public int MaxDashCharges => maxDashCharges;
    /// <summary>충전 중인 다음 스택의 진행도(0~1). 풀충전이면 1.</summary>
    public float DashRechargeProgress01 =>
        (_dashCharges >= maxDashCharges || dashCooldown <= 0f)
            ? 1f
            : 1f - Mathf.Clamp01(_rechargeTimer / dashCooldown);

    [Header("Noise (이동 중 지속 소음 레벨)")]
    [SerializeField] float crouchNoiseLevel = 2f;  // 앉기: 거의 무음(반경 ~0.5) — 암살 접근용. 닿을 듯해야 들림
    [SerializeField] float walkNoiseLevel = 22f;   // 걷기: 근처 2~3마리는 반응(반경 ~5.5) — 기본 이동에 긴장
    [SerializeField] float runNoiseLevel = 70f;    // 달리기: 추격 임계(50) 초과 — 확 커지는 위험한 스파이크

    [Header("Grapple (게이트0 ③ — 좀비에게 잡힘. 무한 카이팅 치즈 차단)")]
    [Tooltip("탈출에 필요한 연타 횟수(대시 키 연타 — 패닉 연타가 곧 탈출 입력).")]
    [SerializeField, Min(1)] int grappleEscapePresses = 5;

    ZombieController _grappler;   // 잡고 있는 좀비(null = 자유). 동시 grapple은 1마리만.
    float _grappleTimer;
    int _escapeLeft;

    /// <summary>좀비에게 잡혀 있는가 — 이동·대시·사격이 잠긴다.</summary>
    public bool IsGrappled => _grappler != null;
    /// <summary>대시 무적 중인가 — 런지 접촉이 헛손질로 빠진다(회피 보상).
    /// 발도 돌진 구간도 같은 무적 채널을 공유한다(KatanaController가 BeginLungeMotion으로 켬 — 충전 중은 무방비).</summary>
    public bool IsUntouchable => (_dashTimer > 0f && dashInvulnerable) || _lungeMoveActive;

    // ── 발도 돌진 이동 채널 (KatanaController 전용) ──────────────────
    // ★위치 단일 소유: 발도 이동도 *PlayerController가* 위치를 쓴다(WallGuardedStep+지면 샘플링 경유).
    //   KatanaController(실행순서 -10)는 같은 프레임 UpdateMovement(순서 0)보다 먼저 돌아 MoveLungeStep을
    //   직접 호출 → PlayerController가 즉시 가드 이동을 수행하고, _lungeMoveActive 플래그로 이번 프레임
    //   일반 이동을 건너뛴다(대시와 동형). 이로써 C-1/C-2(위치 이중 소유) + M-2(지면 미정렬)가 한 번에 해소.
    bool _lungeMoveActive;     // 발도 돌진 중 — UpdateMovement 일반 이동 차단 + i-frame

    /// <summary>좀비 시야 인식의 노출 배수 — 뛰면 잘 보이고, 앉으면 덜 보인다. ZombieController 인식 틱이 읽는다.</summary>
    public float SightExposureMult { get; private set; } = 1f;

    float _groundOffset;

    public float CurrentHP => _currentHP;
    public float MaxHP => maxHP * PlayerStats.MaxHPMult;   // 강화 골격 카드 반영

    public event System.Action OnPlayerDied;

    /// <summary>피격 성공 시(무적 프레임 통과 후 실제 피해 적용) 발화. 인자 = 적용된 피해량. HUD 히트 플래시가 구독.</summary>
    public static event System.Action<float> OnPlayerDamaged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        PlayerStats.Reset();   // 매 런(씬 1회) 시작 시 카드 보정 초기화
        _currentHP = maxHP;
        _dashCharges = maxDashCharges;   // 시작 시 대시 스택 풀충전
    }

    void Start()
    {
        // ★ 월드 바운드가 확정된 뒤(Start) 계산 — Awake 시 스케일/바운드 미확정 가능성 회피
        var col = GetComponent<Collider>();
        _groundOffset = col != null ? col.bounds.center.y - col.bounds.min.y : 0f;

        _combat = GetComponent<PlayerCombat>();   // 정지 중 대시 방향 폴백(조준)
    }

    void OnDestroy()
    {
        // 잡힌 채 파괴(씬 전환 등)되면 좀비를 풀어준다 — 좀비 Grapple 고착 방지(리뷰 반영).
        if (_grappler != null)
        {
            var z = _grappler;
            _grappler = null;
            z.OnGrappleEnded(false);
        }
        if (Instance == this)
        {
            Instance = null;
            OnPlayerDamaged = null;   // static 이벤트 — 씬 리로드 시 잔존 구독자 제거(파괴된 HUD로의 발화 방지)
        }
    }

    void Update()
    {
        // 잡혀 있으면 이동 대신 발버둥(연타 탈출)만 — 무한 카이팅을 끊는 안티치즈(§6.5).
        if (IsGrappled)
        {
            UpdateGrapple();
            return;
        }

        UpdateMovement();
    }

    // ── Grapple (게이트0 ③) ──────────────────────────────────

    /// <summary>좀비(런지 접촉)가 호출. 성공 시 플레이어는 잠기고 연타 탈출 루프로 들어간다.</summary>
    public bool TryBeginGrapple(ZombieController zombie, float holdDuration)
    {
        if (_currentHP <= 0f || _grappler != null) return false;
        if (IsUntouchable) return false;   // 대시 무적 — 회피 성공

        _grappler = zombie;
        _grappleTimer = holdDuration;
        _escapeLeft = grappleEscapePresses;
        _velocity = Vector3.zero;
        _dashTimer = 0f;
        return true;
    }

    /// <summary>좀비 사망/경직 등 외부 요인 해제 — 해당 좀비가 잡고 있을 때만 푼다.</summary>
    public void ReleaseGrapple(ZombieController zombie)
    {
        if (_grappler == zombie) _grappler = null;
    }

    void UpdateGrapple()
    {
        if (_grappler == null) return;

        // 발버둥 = 최대 노출 — 잡힌 상태가 의도치 않은 은신이 되지 않게(리뷰 반영).
        SightExposureMult = 1.5f;
        // 발버둥 소음 — 걷기급(조용히 잡혀 있진 않다).
        NoiseManager.Instance?.SetMovementNoise(walkNoiseLevel);

        // 연타 탈출: 대시 키(패닉 연타가 자연스럽게 탈출 입력이 된다).
        if (Input.GetKeyDown(dashKey) && --_escapeLeft <= 0)
        {
            var z = _grappler;
            _grappler = null;
            z.OnGrappleEnded(true);   // 좀비를 밀쳐내고 탈출
            return;
        }

        _grappleTimer -= Time.deltaTime;
        if (_grappleTimer <= 0f)
        {
            var z = _grappler;
            _grappler = null;
            z.OnGrappleTimeout();     // 탈출 실패의 청구서 — 한 입 더
        }
    }

    void UpdateMovement()
    {
        // 발도 돌진 중엔 일반 이동을 통째로 건너뛴다 — KatanaController가 이번 프레임 위치를 *단일 소유*
        // (MoveLungeStep으로 이미 가드 이동 수행). 대시 차단과 동형. 노출 배수만 갱신.
        if (_lungeMoveActive)
        {
            SightExposureMult = 1.5f;
            NoiseManager.Instance?.SetMovementNoise(runNoiseLevel);
            return;
        }

        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 1f) input.Normalize();

        bool moving = input.sqrMagnitude > 0.001f;
        bool crouching = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool running = moving && !crouching && Input.GetKey(KeyCode.LeftShift); // 앉기 중엔 못 달림

        // 좀비 시야 인식의 노출 배수(combat-texture-foundation §2.2 — 플레이어 행동이 노출 확률 스케일).
        if (!moving) SightExposureMult = 0.7f;
        else if (crouching) SightExposureMult = 0.6f;
        else if (running || IsDashing) SightExposureMult = 1.5f;
        else SightExposureMult = 1f;

        // 대시 스택 충전: 보유<max인 동안 항상 타이머가 흘러 dashCooldown마다 1스택씩 회복.
        // (대시 중에도 흐른다 — 회피 직후 다음 회피가 자연스럽게 차오르게.)
        if (_dashCharges < maxDashCharges && dashCooldown > 0f)
        {
            _rechargeTimer -= Time.deltaTime;
            // while: 큰 프레임(렉 스파이크/일시정지 복귀)에 여러 스택이 한 번에 차도록 — if면 프레임당 1개만 회복.
            while (_rechargeTimer <= 0f && _dashCharges < maxDashCharges)
            {
                _dashCharges++;
                _rechargeTimer += dashCooldown;
            }
            if (_dashCharges >= maxDashCharges) _rechargeTimer = 0f;   // 풀충전이면 타이머 정지
        }

        // 대시 발동: 제작 중 아님 + 대시 중 아님 + 스택 보유 시.
        bool crafting = CraftingSystem.Instance != null && CraftingSystem.Instance.IsCrafting;
        if (!crafting && _dashTimer <= 0f && _dashCharges > 0 && Input.GetKeyDown(dashKey))
            TryStartDash(input);

        // 대시 중이면 일반 이동 로직을 통째로 건너뛴다 — 고정 방향·고정 속도 버스트.
        if (_dashTimer > 0f)
        {
            UpdateDash(Time.deltaTime);
            return;
        }

        float mult = crouching ? crouchMultiplier : (running ? runMultiplier : 1f);
        mult *= PlayerStats.MoveSpeedMult;   // 경량화 카드 반영

        // 조준/주시 중 이동 둔화(B-009) — 의식의 비용이자 보장 원샷의 균형 짝(카이팅 치즈 방지).
        if (PlayerCameraRig.Instance != null)
            mult *= Mathf.Lerp(1f, aimMoveMultiplier, PlayerCameraRig.Instance.AimBlend);

        // 목표 속도 = 입력 방향 × 현재 속도. 입력 없으면 0(감속해 정착).
        Vector3 targetVel = input * (moveSpeed * mult);

        // 가속/감속 분리: "같은 방향으로 더 빨라질 때"만 가속. 그 외(정지·감속·역방향)는 감속.
        // dot 검사가 없으면 역방향 입력(크기 동일)이 가속으로 잡혀 제동 없이 오버슈트한다.
        // → 출발은 또렷이 밀고 나가고, 멈추거나 꺾을 땐 미끄러지며 정착 = 질량감.
        bool speedingUp = Vector3.Dot(targetVel, _velocity) >= 0f
                          && targetVel.sqrMagnitude >= _velocity.sqrMagnitude;
        float rate = speedingUp ? acceleration : deceleration;
        _velocity = Vector3.MoveTowards(_velocity, targetVel, rate * Time.deltaTime);

        // 입력을 떼도 감속 꼬리가 남으므로 속도가 살아있는 동안 위치·지면 갱신.
        if (_velocity.sqrMagnitude > 0.0001f)
        {
            // 벽 통과 방지(2026-06-11): 대시와 같은 Obstacle 마스크로 경로를 막고, 벽면을 따라 미끄러진다.
            Vector3 next = transform.position + WallGuardedStep(_velocity * Time.deltaTime);
            next.y = SampleGroundHeight(next) + _groundOffset;
            transform.position = next;
        }

        // 지속 소음을 매 프레임 갱신 — 정지=0, 앉기<걷기<달리기.
        // NoiseManager가 attack/release 엔벨로프로 "확 커졌다 확 줄어드는" 질감을 만든다.
        float noiseLevel;
        if (!moving) noiseLevel = 0f;
        else if (crouching) noiseLevel = crouchNoiseLevel;
        else if (running) noiseLevel = runNoiseLevel;
        else noiseLevel = walkNoiseLevel;
        NoiseManager.Instance?.SetMovementNoise(noiseLevel);
    }

    /// <summary>일반 이동 벽 가드 — 진행 경로가 막히면 벽 앞까지만 가고, 남은 분량은 벽면을 따라
    /// 슬라이드(코너에서 끈적하게 멈추지 않게). 스피어캐스트라 몸 반경(DashBodyRadius)이 자연 반영.</summary>
    Vector3 WallGuardedStep(Vector3 step)
    {
        float dist = step.magnitude;
        if (dist < 1e-5f) return step;
        Vector3 dir = step / dist;
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        // 초기 겹침 탈출(리뷰 H): SphereCast는 시작 시점에 겹친 콜라이더를 무시한다 —
        // 이미 벽 안이면(레거시 통과 잔재·텔레포트) 밀어내기 벡터를 먼저 더해 빠져나온다.
        Collider[] overlaps = Physics.OverlapSphere(origin, DashBodyRadius, dashObstacleMask, QueryTriggerInteraction.Ignore);
        if (overlaps.Length > 0)
        {
            var self = GetComponent<Collider>();
            foreach (var col in overlaps)
            {
                if (Physics.ComputePenetration(self, transform.position, transform.rotation,
                        col, col.transform.position, col.transform.rotation,
                        out Vector3 pushDir, out float pushDist))
                {
                    Vector3 push = pushDir * (pushDist + SkinWidth);
                    push.y = 0f;
                    step += push;
                }
            }
            return step;   // 이번 프레임은 탈출 우선 — 다음 프레임부터 정상 가드
        }

        if (!Physics.SphereCast(origin, DashBodyRadius, dir, out RaycastHit hit,
                dist + SkinWidth, dashObstacleMask, QueryTriggerInteraction.Ignore))
            return step;

        float allowed = Mathf.Max(0f, hit.distance - SkinWidth);
        Vector3 moved = dir * allowed;

        // 남은 이동을 벽 접면으로 투영 — 한 번 더 차단 검사(안쪽 코너 끼임 방지).
        Vector3 slide = Vector3.ProjectOnPlane(step - moved, hit.normal);
        slide.y = 0f;
        if (slide.sqrMagnitude > 1e-6f &&
            !Physics.SphereCast(origin + moved, DashBodyRadius, slide.normalized, out _,
                slide.magnitude + SkinWidth, dashObstacleMask, QueryTriggerInteraction.Ignore))
            moved += slide;

        return moved;
    }

    const float SkinWidth = 0.05f;   // 벽면 밀착 여유 — 0이면 다음 프레임 캐스트가 내부에서 시작

    void TryStartDash(Vector3 input)
    {
        // 방향 결정: WASD 입력이 있으면 그 방향, 없으면(정지) 조준 방향, 그것도 없으면 현재 바라보는 방향.
        // → 멈춰서도 마우스 쪽으로 회피 대시가 나간다(톱다운 트윈스틱의 직관).
        Vector3 dir = input;
        if (dir.sqrMagnitude < 0.0001f)
            dir = _combat != null ? _combat.AimDirection : transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;   // 방향을 못 구하면 대시 취소(스택 소모 X)

        // 풀충전 상태에서 처음 소모하는 순간부터 충전 타이머를 건다.
        // (이미 충전 중이면 진행도를 유지 — 소모해도 타이머를 리셋하지 않는다.)
        if (_dashCharges >= maxDashCharges) _rechargeTimer = dashCooldown;
        _dashCharges--;

        _dashDir = dir.normalized;
        _dashTimer = dashDuration;
        _velocity = _dashDir * dashSpeed;   // 대시 종료 후에도 속도 꼬리가 자연스럽게 감속하도록 미리 채움

        if (dashNoise > 0f) NoiseManager.Instance?.EmitImpulse(dashNoise);
    }

    void UpdateDash(float dt)
    {
        _dashTimer -= dt;

        float step = dashSpeed * dt;

        // 벽 통과 방지: 진행 경로에 장애물이 있으면 그 앞까지만 이동하고 대시 즉시 종료.
        // (리뷰 반영: 중심 레이→스피어캐스트 — 몸 반경이 얇은 벽 모서리를 비껴 뚫는 것 방지)
        bool wallHit = false;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        if (Physics.SphereCast(origin, DashBodyRadius, _dashDir, out RaycastHit hit, step + SkinWidth, dashObstacleMask, QueryTriggerInteraction.Ignore))
        {
            step = Mathf.Max(0f, hit.distance - SkinWidth);
            _dashTimer = 0f;   // 벽에 막히면 더 밀지 않는다
            wallHit = true;
        }

        Vector3 next = transform.position + _dashDir * step;
        next.y = SampleGroundHeight(next) + _groundOffset;
        transform.position = next;

        // 깔끔히 끝나면 감속 꼬리를 남겨 부드럽게 정착. 벽에 박으면 즉시 정지(꼬리=벽 슬라이드 잼).
        _velocity = wallHit ? Vector3.zero : _dashDir * dashSpeed;

        // 대시는 달리기급 소음 — 순간 충격(dashNoise)은 시작 시 1회, 여기선 지속 레벨만.
        NoiseManager.Instance?.SetMovementNoise(runNoiseLevel);
    }

    float SampleGroundHeight(Vector3 pos)
    {
        Vector3 origin = new Vector3(pos.x, 200f, pos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f, groundLayer, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        // ★ M3 fix: 레이 미스 시 현재 지면 높이 유지 (next.y += _groundOffset 후 드리프트 누적 방지)
        return transform.position.y - _groundOffset;
    }

    public void TakeDamage(float amount)
    {
        if (_currentHP <= 0f) return;   // 이미 사망 — 같은 프레임 다중 타격 시 OnPlayerDied 중복 발화 방지
        if (IsUntouchable) return;   // 대시 무적 OR 발도 돌진 무적 — 회피/돌진 구간은 안 잡힌다(충전 중은 무방비)
        _currentHP -= amount;
        PlayerCameraRig.Instance?.TriggerShake(damageShake);   // 피격 화면 펀치(발사보다 크게)
        OnPlayerDamaged?.Invoke(amount);   // 화면 히트 플래시(HudV2Controller가 구독) — 피해량으로 강타 판정
        // 피격 임팩트 — 짧은 히트스탑(Feel). 좀비 공격은 빈도 낮고 큰 피해라 스팸/스터터 없이 "맞았다" 충격만.
        MMTimeScaleEvent.Trigger(MMTimeScaleMethods.For, 0.05f, 0.015f, false, 0f, false);
        if (_currentHP <= 0f)
        {
            _currentHP = 0f;
            // 잡힌 채 죽으면 좀비를 풀어준다 — 좀비가 Grapple 상태로 고착되지 않게.
            if (_grappler != null)
            {
                var z = _grappler;
                _grappler = null;
                z.OnGrappleEnded(false);
            }
            OnPlayerDied?.Invoke();
        }
    }

    /// <summary>구급상자 제작 등으로 회복. maxHP를 넘지 않도록 클램프.</summary>
    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        _currentHP = Mathf.Min(MaxHP, _currentHP + amount);
    }

    /// <summary>강화 골격 카드로 MaxHPMult가 오를 때 호출. 늘어난 최대치만큼 현재 체력도 채워 즉시 체감되게 한다.</summary>
    public void RaiseMaxHP(float oldMult, float newMult)
    {
        if (_currentHP <= 0f) return;   // 사망 상태에선 최대치만 오르고 부활시키지 않는다
        float added = maxHP * (newMult - oldMult);
        if (added > 0f) _currentHP = Mathf.Min(MaxHP, _currentHP + added);
    }

    // ════════════════════════════════════════════════════════════
    //  발도 돌진 이동 채널 (KatanaController 호출 — 위치 단일 소유 + i-frame)
    // ════════════════════════════════════════════════════════════

    /// <summary>발도 돌진 시작 — 일반 이동 잠금 + 돌진 구간 무적(IsUntouchable). 충전 중은 켜지 않는다(무방비).
    /// KatanaController.StartLunge에서 호출. 잡힌 상태에선 무시(grapple 우선).</summary>
    public void BeginLungeMotion()
    {
        if (IsGrappled) return;
        _lungeMoveActive = true;
        _velocity = Vector3.zero;   // 돌진은 고정 속도 — 잔여 일반 속도 꼬리 제거
        _dashTimer = 0f;            // 대시와 동시 진행 방지(둘 다 위치 소유 시도 금지)
    }

    /// <summary>발도 돌진 한 스텝 — 요청 step을 WallGuardedStep+지면 샘플링으로 *PlayerController가* 이동시킨다.
    /// KatanaController.StepLunge가 실행순서 -10이라 UpdateMovement(순서 0)보다 먼저 호출 → 같은 프레임 즉시 반영.
    /// 반환: 요청 방향으로의 *전진 거리*(벽 슬라이드의 옆 성분 제외) — 벽에 막히면 0에 가깝다 → 호출자 벽정지 판정.</summary>
    public float MoveLungeStep(Vector3 worldStep)
    {
        if (!_lungeMoveActive) return 0f;
        worldStep.y = 0f;
        float reqDist = worldStep.magnitude;
        if (reqDist < 1e-5f) return 0f;
        Vector3 reqDir = worldStep / reqDist;

        Vector3 guarded = WallGuardedStep(worldStep);   // 벽 앞 클램프 + 벽면 슬라이드(일반 이동과 동일 가드)
        Vector3 next = transform.position + guarded;
        next.y = SampleGroundHeight(next) + _groundOffset;   // 지면 정렬(M-2 — 평면 이동의 지면 미정렬 해소)
        transform.position = next;

        // 전진 성분만 반환 — 벽면 슬라이드의 옆 이동을 진행으로 오인하지 않게(벽 만나면 전진≈0 → 돌진 정지).
        Vector3 guardedXZ = guarded; guardedXZ.y = 0f;
        return Mathf.Max(0f, Vector3.Dot(guardedXZ, reqDir));
    }

    /// <summary>발도 돌진 종료 — 일반 이동/무적 복귀. KatanaController가 돌진 완료·벽정지·하드캔슬 시 호출.</summary>
    public void EndLungeMotion()
    {
        _lungeMoveActive = false;
    }
}
