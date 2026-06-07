using DG.Tweening;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

public enum SpawnAnimation { None, RiseFromGround, RushFromSide }

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ZombieController : MonoBehaviour
{
    enum ZombieState { Idle, Investigate, Chase, Attack, Dead }

    [Header("Config")]
    [SerializeField] ZombieConfig _config;

    [Header("Ground")]
    [SerializeField] LayerMask groundLayer = -1;

    [Header("Separation")]
    [SerializeField] float separationRadius = 1.5f;
    [SerializeField] float separationStrength = 1.5f;
    [SerializeField] LayerMask zombieLayer;

    [Header("Detection")]
    [SerializeField] LayerMask obstacleMask;

    [Header("Kill Feedback")]
    [SerializeField] ParticleSystem killParticlePrefab;
    [SerializeField] AudioClip killSound;

    [Header("XP")]
    [SerializeField] GameObject xpOrbPrefab;

    // --- Runtime ---
    ZombieState _state = ZombieState.Idle;
    int _currentHP;
    float _attackCooldownTimer;
    float _investigateTimer;
    float _investigateLookTimer;
    Vector3 _investigateTarget;
    float _signalCooldownTimer;
    bool _hasSignaled;

    Transform _player;
    PlayerController _playerController;
    Rigidbody _rb;
    float _groundOffset;
    Vector3 _velocity;
    Vector3 _homePosition;
    bool _dead;
    bool _spawning;

    // 근접 피격 반응(코드 구동 — RB가 kinematic이라 물리력 대신 직접 변위)
    Vector3 _knockbackVel;     // 넉백 잔여 속도(감쇠)
    float _staggerTimer;       // 경직: AI 추격 일시정지
    const float KnockbackDamping = 12f;   // 넉백 감쇠율(클수록 빨리 멈춤)

    MMSpringScale _springScale;
    Animator _animator;
    static readonly int SpeedHash = Animator.StringToHash("Speed");

    // ──────────── Scan Tag (AI 위협 태깅 — 아웃라인) ────────────
    // 어그로/사망 상태 외부 노출. ScanPulseController가 태깅 대상 판별에 사용.
    public bool IsAggro => _state == ZombieState.Chase || _state == ZombieState.Attack;
    public bool IsDead => _dead || _state == ZombieState.Dead;

    // 아웃라인 표시는 renderingLayerMask의 비트로만 제어 — 물리 레이어(7)는 건드리지 않는다.
    const uint ScanTagBit = 1u << 1;   // "ScanTagged" 렌더링 레이어 인덱스 1
    Renderer[] _scanRenderers;
    bool _scanTagged;

    public void SetScanTagged(bool on)
    {
        if (_scanTagged == on) return;
        _scanTagged = on;
        if (_scanRenderers == null) return;
        for (int i = 0; i < _scanRenderers.Length; i++)
        {
            var r = _scanRenderers[i];
            if (r == null) continue;
            if (on) r.renderingLayerMask |= ScanTagBit;
            else    r.renderingLayerMask &= ~ScanTagBit;
        }
    }

    // ──────────── Lifecycle ────────────

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        var col = GetComponent<Collider>();
        _groundOffset = col.bounds.center.y - col.bounds.min.y;

        _springScale = GetComponent<MMSpringScale>();
        _animator = GetComponent<Animator>();
        _scanRenderers = GetComponentsInChildren<Renderer>(true);
    }

    void OnDestroy()
    {
        transform.DOKill();
    }

    /// <summary>스포너에서 Instantiate 직후 호출</summary>
    public void Init(Transform player, Vector3 homePosition)
    {
        _player = player;
        _playerController = player != null ? player.GetComponent<PlayerController>() : null;
        _homePosition = homePosition;
        _currentHP = _config != null ? _config.maxHP : 3;
        _state = ZombieState.Idle;
        _dead = false;
        _hasSignaled = false;

        // ★ 풀링 대비 전체 리셋 (현재 Instantiate/Destroy라 무해하지만, 풀 도입 시 오염 방지)
        _velocity = Vector3.zero;
        _knockbackVel = Vector3.zero;
        _staggerTimer = 0f;
        _spawning = false;
        _attackCooldownTimer = 0f;
        _investigateTimer = 0f;
        _investigateLookTimer = 0f;
        _signalCooldownTimer = 0f;
    }

    public void PlaySpawnAnimation(SpawnAnimation type, Vector3 rushTarget = default, bool hasRushTarget = false)
    {
        transform.localScale = Vector3.one;
        switch (type)
        {
            case SpawnAnimation.RiseFromGround:
                _spawning = true;
                transform.localScale = new Vector3(1f, 0f, 1f);
                transform.DOScaleY(1f, 0.4f)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() => _spawning = false);
                break;
            case SpawnAnimation.RushFromSide:
                _spawning = true;
                Vector3 dest = hasRushTarget ? rushTarget : transform.position;
                transform.DOMove(dest, 0.3f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => _spawning = false);
                break;
        }
    }

    // ──────────── Main Loop ────────────

    void FixedUpdate()
    {
        // ★ _config null 가드 추가 (SO 미연결 좀비가 NRE 내지 않도록)
        if (_dead || _spawning || _player == null || _config == null) return;

        UpdateTimers();
        UpdateStateMachine();
        UpdateMovement();
        UpdateAnimation();
    }

    // ★ 신규: 매 FixedUpdate에서 쿨다운 타이머 감소 (기존엔 signalCooldown이 안 줄어듦)
    void UpdateTimers()
    {
        if (_signalCooldownTimer > 0f)
        {
            _signalCooldownTimer -= Time.fixedDeltaTime;
            if (_signalCooldownTimer <= 0f)
                _hasSignaled = false;   // 쿨다운 만료 → 재소환 가능
        }

        if (_staggerTimer > 0f)
            _staggerTimer -= Time.fixedDeltaTime;
    }

    // ──────────── Detection ────────────

    bool CanSeePlayer()
    {
        if (_config == null || _player == null) return false;

        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        if (dist > _config.sightRange) return false;
        if (dist < 0.5f) return true;   // 초근접(겹침)은 각도 무의미
        if (Vector3.Angle(transform.forward, toPlayer) > _config.sightHalfAngle) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1f;
        if (Physics.Raycast(eyePos, toPlayer.normalized, dist, obstacleMask))
            return false;

        return true;
    }

    bool CanHearPlayer()
    {
        if (NoiseManager.Instance == null || _config == null || _player == null) return false;

        float radius = NoiseManager.Instance.HearingRadius * _config.hearingMultiplier;
        if (radius <= 0f) return false;

        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        // 장애물 감쇄
        if (Physics.Raycast(transform.position + Vector3.up, toPlayer.normalized, dist, obstacleMask))
            radius *= 0.5f;

        return dist <= radius;
    }

    // ──────────── State Machine ────────────

    void UpdateStateMachine()
    {
        bool canSee = CanSeePlayer();
        bool canHear = CanHearPlayer();
        bool chaseThreshold = NoiseManager.Instance != null && NoiseManager.Instance.IsChaseThreshold;

        switch (_state)
        {
            case ZombieState.Idle:
                if (canSee || (canHear && chaseThreshold))
                    EnterChase();
                else if (canHear)
                    EnterInvestigate(_player.position);
                break;

            case ZombieState.Investigate:
                _investigateTimer += Time.fixedDeltaTime;

                if (canSee || (canHear && chaseThreshold))
                {
                    EnterChase();
                    break;
                }

                Vector3 toTarget = _investigateTarget - transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude < 1f)
                {
                    _investigateLookTimer += Time.fixedDeltaTime;
                    if (_investigateLookTimer >= _config.investigateLookTime)
                    {
                        _state = ZombieState.Idle;
                        break;
                    }
                }
                else
                {
                    // ★ H-2: 타겟에서 멀어지면 둘러보기 타이머 리셋 (도착해야 카운트 시작)
                    _investigateLookTimer = 0f;
                }

                if (_investigateTimer >= _config.investigateTimeout)
                    _state = ZombieState.Idle;
                break;

            case ZombieState.Chase:
                float distToPlayer = FlatDistance(transform.position, _player.position);

                if (distToPlayer <= _config.attackRange)
                {
                    _state = ZombieState.Attack;
                    // ★ H-1: 진입 즉시 때리지 않도록 첫 공격 전 쿨다운(=윈드업) 부여
                    _attackCooldownTimer = _config.attackCooldown;
                    break;
                }
                break;

            case ZombieState.Attack:
                float dist = FlatDistance(transform.position, _player.position);
                if (dist > _config.attackRange * 1.2f)
                {
                    _state = ZombieState.Chase;
                    break;
                }

                _attackCooldownTimer -= Time.fixedDeltaTime;
                if (_attackCooldownTimer <= 0f)
                {
                    PerformAttack();
                    _attackCooldownTimer = _config.attackCooldown;
                }
                break;
        }
    }

    void EnterChase()
    {
        _state = ZombieState.Chase;
        _investigateTimer = 0f;
        _investigateLookTimer = 0f;

        // ★ Codex HIGH: 시그널을 Chase 진입 시점에 한 번 발동.
        // (이전엔 Chase 루프에서 attackRange 체크가 먼저라, 근접 진입 시 시그널이 누락됨)
        if (_config != null && _config.isSignalZombie && !_hasSignaled && _signalCooldownTimer <= 0f)
        {
            _hasSignaled = true;
            StartCoroutine(SignalCoroutine());
        }
    }

    void EnterInvestigate(Vector3 target)
    {
        _state = ZombieState.Investigate;
        _investigateTarget = target;
        _investigateTimer = 0f;
        _investigateLookTimer = 0f;
    }

    /// <summary>신호 좀비가 AlertTo()로 호출. 즉시 Chase.</summary>
    public void AlertTo(Vector3 targetPosition)
    {
        if (_dead || _state == ZombieState.Dead) return;
        if (_state == ZombieState.Chase || _state == ZombieState.Attack) return;
        EnterChase();
    }

    // ──────────── Movement ────────────

    void UpdateMovement()
    {
        Vector3 moveTarget;
        float stopDist;

        switch (_state)
        {
            case ZombieState.Investigate:
                moveTarget = _investigateTarget;
                stopDist = 0.5f;
                break;
            case ZombieState.Chase:
                moveTarget = _player.position;
                stopDist = _config.attackRange * 0.8f;
                break;
            case ZombieState.Attack:
                moveTarget = _player.position;
                stopDist = _config.attackRange * 0.8f;
                break;
            default: // Idle
                moveTarget = _homePosition;
                stopDist = 1f;
                break;
        }

        Vector3 toTarget = moveTarget - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        // 경직 중이면 추격 의지를 끊는다(넉백만 작용 → "맞고 밀려나는" 멈칫).
        bool staggered = _staggerTimer > 0f;

        Vector3 desiredDir = (!staggered && dist > stopDist) ? toTarget.normalized : Vector3.zero;
        if (!staggered) desiredDir += CalcSeparation() * separationStrength;
        desiredDir.y = 0f;

        float speed = _config != null ? _config.moveSpeed : 3f;
        float accel = _config != null ? _config.acceleration : 6f;

        Vector3 desiredVel = desiredDir.sqrMagnitude > 0.001f
            ? desiredDir.normalized * speed
            : Vector3.zero;

        _velocity = Vector3.Lerp(_velocity, desiredVel,
            1f - Mathf.Exp(-accel * Time.fixedDeltaTime));

        // 넉백은 AI 속도와 별개로 더해지고 지수 감쇠한다(kinematic이라 물리력 대신 직접 변위).
        Vector3 step = (_velocity + _knockbackVel) * Time.fixedDeltaTime;
        if (step.sqrMagnitude > 0.0000001f)
        {
            Vector3 nextPos = transform.position + step;
            nextPos.y = SampleTerrainHeight(nextPos) + _groundOffset;
            _rb.MovePosition(nextPos);

            // Chase/Attack에서는 플레이어를 바라봄, 그 외는 이동 방향
            Vector3 lookDir = (_state == ZombieState.Chase || _state == ZombieState.Attack)
                ? (_player.position - transform.position) : _velocity;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                _rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.fixedDeltaTime));
            }
        }

        _knockbackVel = Vector3.Lerp(_knockbackVel, Vector3.zero,
            1f - Mathf.Exp(-KnockbackDamping * Time.fixedDeltaTime));
    }

    void UpdateAnimation()
    {
        if (_animator != null)
            _animator.SetFloat(SpeedHash, _velocity.magnitude);
    }

    // ──────────── Combat ────────────

    void PerformAttack()
    {
        if (_playerController != null)
            _playerController.TakeDamage(_config.attackDamage);
        // TODO: 공격 애니메이션 트리거
    }

    /// <summary>원거리/DoT 등 일반 피해. 기본 죽음 연출(좀비 자체 파티클/사운드) 사용.</summary>
    public void TakeDamage(int amount)
    {
        if (_dead) return;

        _currentHP -= amount;

        if (_currentHP <= 0)
        {
            Die();
            return;
        }

        // 맞으면 작은 범프
        _springScale?.Bump(new Vector3(0.2f, -0.3f, 0.2f));

        // Idle/Investigate 상태에서 맞으면 → Chase
        if (_state == ZombieState.Idle || _state == ZombieState.Investigate)
            EnterChase();
    }

    /// <summary>
    /// 근접 타격. 데미지 + 넉백(코드 변위) + 경직 + 어그로. 사망 시 무기별 죽음 연출.
    /// 반환값 = 이 타격으로 죽었는지(호출자가 킬 사운드/카운트에 사용).
    /// </summary>
    public bool TakeMeleeHit(int damage, Vector3 attackerPos, float knockback, float stagger, WeaponLoadout.DeathStyle style)
    {
        if (_dead) return false;

        Vector3 dir = transform.position - attackerPos;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;

        _currentHP -= damage;

        if (_currentHP <= 0)
        {
            DieByWeapon(style, dir, knockback);
            return true;
        }

        // 생존: 넉백 + 경직 + 범프 + 어그로
        _knockbackVel = dir * knockback;
        if (stagger > 0f) _staggerTimer = Mathf.Max(_staggerTimer, stagger);
        _springScale?.Bump(new Vector3(0.25f, -0.35f, 0.25f));
        if (_state == ZombieState.Idle || _state == ZombieState.Investigate)
            EnterChase();
        return false;
    }

    void Die()
    {
        _dead = true;
        _state = ZombieState.Dead;
        SetScanTagged(false);   // 시체/래그돌에 아웃라인이 남지 않도록 즉시 태그 제거

        _springScale?.Bump(new Vector3(0.3f, -0.5f, 0.3f));
        MMTimeScaleEvent.Trigger(MMTimeScaleMethods.For, 0.03f, 0.05f, false, 0f, false);

        if (killParticlePrefab != null)
            Instantiate(killParticlePrefab, transform.position, Quaternion.identity);
        if (killSound != null)
            AudioSource.PlayClipAtPoint(killSound, transform.position);

        SpawnXPOrbs();
        CraftingSystem.Instance?.NotifyKill(transform.position);
        Destroy(gameObject);
    }

    /// <summary>무기별 죽음 연출(형태/멈춤/런치 차등). 시체를 넉백 방향으로 날린 뒤 제거.</summary>
    void DieByWeapon(WeaponLoadout.DeathStyle style, Vector3 launchDir, float force)
    {
        _dead = true;
        _state = ZombieState.Dead;
        SetScanTagged(false);   // 런치되는 시체에 아웃라인이 남지 않도록 즉시 태그 제거
        _knockbackVel = Vector3.zero;   // 런치 트윈이 변위를 전담

        SpawnXPOrbs();
        CraftingSystem.Instance?.NotifyKill(transform.position);

        // 히트스탑은 스윙당 1회만 내야 한다(다중킬 스택 방지) → MeleeAttacker.Swing이 소유.
        // 여기선 형태(squash)·런치만 무기별로 차등.
        Vector3 squash;
        float launchDist;
        if (style == WeaponLoadout.DeathStyle.Crunch)
        {
            // 쇠지렛대: 깊게 으스러지며 위로 팝 + 크게 날아감
            squash = new Vector3(0.45f, -0.6f, 0.45f);
            launchDist = Mathf.Max(0.6f, force * 0.12f);
        }
        else
        {
            // 방망이(Splat): 옆으로 납작하게 후려쳐 뒤로 미끄러짐
            squash = new Vector3(0.6f, -0.45f, 0.6f);
            launchDist = Mathf.Max(0.4f, force * 0.08f);
        }

        _springScale?.Bump(squash);
        if (killParticlePrefab != null)
            Instantiate(killParticlePrefab, transform.position, Quaternion.identity);

        // 시체 런치 후 제거. _dead=true라 AI/재타격이 차단되어 트윈과 충돌 없음.
        Vector3 launchTarget = transform.position + launchDir * launchDist;
        if (style == WeaponLoadout.DeathStyle.Crunch) launchTarget += Vector3.up * 0.5f;
        transform.DOKill();
        transform.DOMove(launchTarget, 0.25f).SetEase(Ease.OutQuad)
            .OnComplete(() => Destroy(gameObject));
        Destroy(gameObject, 0.6f);   // 트윈이 끊겨도 확실히 제거되는 안전망
    }

    void SpawnXPOrbs()
    {
        if (xpOrbPrefab == null || _player == null || _config == null) return;
        int count = Random.Range(_config.xpOrbCountMin, _config.xpOrbCountMax + 1);
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + Random.Range(-30f, 30f);
            Vector3 burstDir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var obj = Instantiate(xpOrbPrefab, transform.position, Quaternion.identity);
            var orb = obj.GetComponent<XPOrb>();
            if (orb != null) orb.Init(burstDir, _player);
        }
    }

    // ──────────── Signal Zombie ────────────

    System.Collections.IEnumerator SignalCoroutine()
    {
        yield return new WaitForSeconds(_config.signalDelay);

        // ★ 대기 중 좀비 사망/플레이어 소멸 가드
        if (_dead || _player == null) yield break;

        // 딜레이 동안 Chase가 풀렸으면 소환 취소.
        if (_state != ZombieState.Chase && _state != ZombieState.Attack)
        {
            _signalCooldownTimer = _config.signalCooldown;
            yield break;
        }

        // TODO: 시그널 파티클/사운드

        Collider[] nearby = Physics.OverlapSphere(transform.position, _config.signalRadius, zombieLayer);
        int summoned = 0;
        foreach (var col in nearby)
        {
            if (summoned >= _config.signalSummonCount) break;
            if (col.gameObject == gameObject) continue;

            var zombie = col.GetComponent<ZombieController>();
            if (zombie != null && !zombie._dead
                && (zombie._state == ZombieState.Idle || zombie._state == ZombieState.Investigate))
            {
                zombie.AlertTo(_player.position);
                summoned++;
            }
        }

        _signalCooldownTimer = _config.signalCooldown;   // ★ 하드코딩 제거. UpdateTimers()가 감소시킴
    }

    // ──────────── Utility (기존 코드 재사용) ────────────

    Vector3 CalcSeparation()
    {
        Vector3 sep = Vector3.zero;
        Collider[] nearby = Physics.OverlapSphere(transform.position, separationRadius, zombieLayer);
        foreach (var col in nearby)
        {
            if (col.gameObject == gameObject) continue;
            Vector3 away = transform.position - col.transform.position;
            away.y = 0f;
            float d = away.magnitude;
            if (d > 0.001f)
                sep += away.normalized * (separationRadius / d - 1f);
        }
        return sep;
    }

    float SampleTerrainHeight(Vector3 pos)
    {
        Vector3 origin = new Vector3(pos.x, 200f, pos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f,
                            groundLayer, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return pos.y;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
