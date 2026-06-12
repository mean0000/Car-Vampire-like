using DG.Tweening;
using MoreMountains.Feedbacks;
using UnityEngine;

public enum SpawnAnimation { None, RiseFromGround, RushFromSide }

/// <summary>3계층 탄 판정 결과(combat-texture-foundation §6.2). 풀히트/스침/그레이즈.</summary>
public enum BulletHitTier { Full, Graze, NearMiss }

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ZombieController : MonoBehaviour
{
    // 게이트0 인식 모델(combat-texture-foundation §2): Dormant→Alert→Chase 최소 골격 + 기존 Investigate/Idle 유지.
    // 공격은 단발 타격이 아니라 윈드업→런지→grapple 사이클(§6.5) — 옛 Attack 상태를 대체.
    enum ZombieState { Dormant, Idle, Investigate, Alert, Chase, Windup, Lunge, Grapple, Recover, Dead }

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

    [Header("Combat Cues (게이트0 — 비우면 무음)")]
    [Tooltip("Chase 진입 순간 그르렁 — 주변 좀비를 Investigate로 깨우는 사운드 이벤트의 가청 큐.")]
    [SerializeField] AudioClip gruntSound;
    [Tooltip("런지 윈드업 시작 사운드 큐 — '온다'를 귀로 먼저 알린다.")]
    [SerializeField] AudioClip windupSound;

    [Header("XP")]
    [SerializeField] GameObject xpOrbPrefab;

    // --- Runtime ---
    ZombieState _state = ZombieState.Dormant;
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
    Collider _collider;
    float _groundOffset;
    Vector3 _velocity;
    Vector3 _homePosition;
    bool _dead;
    bool _spawning;   // 스폰 연출 중 — 상태 정의상 Dormant(마이그레이션 체크리스트 ③)

    // 근접 피격 반응(코드 구동 — RB가 kinematic이라 물리력 대신 직접 변위)
    Vector3 _knockbackVel;     // 넉백 잔여 속도(감쇠)
    float _staggerTimer;       // 경직: AI 추격 일시정지 (flinch/stagger 공용 — 길이만 다름)
    const float KnockbackDamping = 12f;   // 넉백 감쇠율(클수록 빨리 멈춤)

    // ── 게이트0: 인식(확률 누적) ──
    float _detectGauge;        // 0~1. alertThreshold↑=Alert, 1.0=Chase
    float _perceptTimer;       // 다음 인식 틱까지(개체별 위상 분산)
    float _sightVar = 1f;      // 개체 분산(±senseVariance) — 눈 밝은 놈/장님
    float _hearVar = 1f;
    Vector3 _lastNoticedPos;   // Alert가 식었을 때 Investigate로 갈 마지막 인지 위치
    float _alertStareTimer;    // Alert 응시 잔여(끝나면 느린 접근)
    float _gruntCooldownTimer; // 그르렁 재방출 쿨다운(Chase 재진입 스팸 방지)

    // ── 게이트0: 피격 사다리 ──
    int _comboHits;            // 윈도우 내 풀히트 누적
    float _poiseWindowTimer;   // 누적 유지 윈도우
    float _resistMult = 1f;    // knockdown마다 문턱 상승(스턴락 방지)
    float _resistDecayTimer;
    float _downTimer;          // >0 = 넘어져 있음(행동 불능 + 받는 피해 증가)
    bool _gettingUp;           // 기상 연출 중복 방지

    // ── 게이트0: 히트스탑(피격자만 — 전역 timeScale 금지) ──
    float _hitStopTimer;

    // ── 게이트0: 런지/grapple ──
    float _windupTimer;
    float _lungeTimer;
    float _recoverTimer;
    Vector3 _lungeDir;         // 윈드업 종료 순간 고정(커밋 — 회피 보상)

    // 킬 피드백 클램프(§6.2) — 시간창당 1회. 대량 킬 시 오디오 폭주 방지. 수치는 CombatFeelConfig가 권위지만
    // 좀비는 SO 미접근 경로(DoT 등)도 있어 마지막 적용값을 static으로 공유한다.
    static float s_lastKillFeedbackTime = -999f;
    static float s_killFeedbackWindow = 0.15f;
    const float KillPunchIn = 0.18f;   // 킬 카메라 펀치인 크기(m) — 카메라 명세 §충격
    const float ConvergedKillPunchIn = 0.32f;   // 수렴샷 킬 펀치인 — "저격 킬"의 카메라 무게(합산 캡 0.5m 안)

    /// <summary>PlayerCombat이 CombatFeelConfig 값으로 1회 동기화.</summary>
    public static void SetKillFeedbackWindow(float window) => s_killFeedbackWindow = Mathf.Max(0f, window);
    static bool TryConsumeKillFeedback()
    {
        if (Time.unscaledTime - s_lastKillFeedbackTime < s_killFeedbackWindow) return false;
        s_lastKillFeedbackTime = Time.unscaledTime;
        return true;
    }

    MMSpringScale _springScale;
    MaterialPropertyBlock _flashMpb;   // 피격 흰 점멸(볼트 A) — MPB라 머티리얼 무손상
    float _hitFlashTimer;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    Animator _animator;
    Transform _visual;         // 리그 루트("Visual" 자식) — 윈드업 젖힘/넉다운 기울임 대상
    bool _hasAttackTrigger;    // Animator에 "Attack" 트리거가 있으면 클립, 없으면 코드 기울임 폴백
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int AttackHash = Animator.StringToHash("Attack");

    // ──────────── Scan Tag (AI 위협 태깅 — 아웃라인) ────────────
    // 어그로/사망 상태 외부 노출. ScanPulseController가 태깅 대상 판별에 사용.
    public bool IsAggro => _state >= ZombieState.Chase && _state <= ZombieState.Recover;
    public bool IsDead => _dead || _state == ZombieState.Dead;
    /// <summary>Investigate~Alert 구간(어그로 전 의심 단계) — TensionAudioDirector의 Alert 판정용(B-005a).</summary>
    public bool IsAlerted => _state == ZombieState.Investigate || _state == ZombieState.Alert;
    /// <summary>공격 사이클(Windup/Lunge/Grapple) — 시야 게이트 공정성 바닥(공격 중 실물 강제 공개)용.</summary>
    public bool IsAttacking => _state == ZombieState.Windup || _state == ZombieState.Lunge || _state == ZombieState.Grapple;
    /// <summary>설정 이동속도(m/s) — 스폰 가드 TTC 계산용(런타임 속도 아님). config 부재 시 코드 기본 3.</summary>
    public float MoveSpeed => _config != null ? _config.moveSpeed : 3f;

    // 전투 질감 랩 관측용 — 상태/설정 가시화(에디터 자동화·디버그 HUD가 읽는다. 게임 로직에서 사용 금지).
    public string DebugState => _downTimer > 0f ? "Downed" : _state.ToString();
    public bool HasConfig => _config != null;

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

        _collider = GetComponent<Collider>();
        // 피벗→콜라이더 바닥 거리. 리그 피벗=발(콜라이더 center y>0)이라 center 기준이 아닌 피벗 기준이어야 접지됨
        _groundOffset = transform.position.y - _collider.bounds.min.y;

        _springScale = GetComponent<MMSpringScale>();
        _animator = GetComponent<Animator>();
        _visual = transform.Find("Visual");
        _scanRenderers = GetComponentsInChildren<Renderer>(true);

        // Animator에 Attack 트리거가 와이어링돼 있으면 클립으로, 없으면 코드 젖힘으로 윈드업 표현.
        if (_animator != null && _animator.runtimeAnimatorController != null)
            foreach (var p in _animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.nameHash == AttackHash)
                { _hasAttackTrigger = true; break; }
    }

    void Start()
    {
        // 씬 손배치 좀비 자가 부트스트랩 — 스포너 경로는 Instantiate 직후 Init이 호출되지만,
        // 사전배치 개체는 호출처가 없어 _player가 비면 FixedUpdate 가드에 걸려 영구 동결된다.
        // (Instantiate 경로는 Start 전에 Init이 이미 끝나므로 이 폴백을 타지 않는다.)
        if (_player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) Init(pc.transform, transform.position);
        }
    }

    void OnDestroy()
    {
        transform.DOKill();
        if (_visual != null) _visual.DOKill();
        // grapple 중 파괴(씬 언로드 등) — 플레이어가 영구 잡힘 상태로 남지 않게 해제.
        if (_state == ZombieState.Grapple && _playerController != null)
            _playerController.ReleaseGrapple(this);
    }

    /// <summary>스포너에서 Instantiate 직후 호출</summary>
    public void Init(Transform player, Vector3 homePosition)
    {
        _player = player;
        _playerController = player != null ? player.GetComponent<PlayerController>() : null;
        _homePosition = homePosition;
        _currentHP = _config != null ? _config.maxHP : 3;
        // 배치 인구 기본 = Dormant(combat-texture-foundation §4.1) — 발각 전엔 정지.
        _state = ZombieState.Dormant;
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
        _detectGauge = 0f;
        _comboHits = 0;
        _resistMult = 1f;
        _downTimer = 0f;
        _gettingUp = false;
        _hitStopTimer = 0f;
        _gruntCooldownTimer = 0f;

        // 개체 분산: 같은 무리에도 눈 밝은 놈/장님이 섞여 "안전거리를 확신할 수 없음"을 만든다(PZ Random).
        float v = _config != null ? _config.senseVariance : 0.3f;
        _sightVar = 1f + Random.Range(-v, v);
        _hearVar = 1f + Random.Range(-v, v);
        // 인식 틱 위상 분산(스태거드 업데이트) — 전 인구가 같은 프레임에 판정하지 않게.
        _perceptTimer = Random.value * (_config != null ? _config.perceptionTickInterval : 0.2f);
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
        // 피격 흰 점멸 해제 — 사망/히트스탑 가드보다 먼저(점멸이 시체에 영구 잔류하지 않게).
        if (_hitFlashTimer > 0f)
        {
            _hitFlashTimer -= Time.fixedDeltaTime;
            if (_hitFlashTimer <= 0f && _scanRenderers != null)
                foreach (var r in _scanRenderers) if (r != null) r.SetPropertyBlock(null);
        }

        // ★ _config null 가드 추가 (SO 미연결 좀비가 NRE 내지 않도록)
        if (_dead || _spawning || _player == null || _config == null) return;

        // 히트스탑(피격자만): 애니·이동·판단 전부 정지. 공격자·카메라·세계는 계속 흐른다(사쿠라이).
        if (_hitStopTimer > 0f)
        {
            _hitStopTimer -= Time.fixedDeltaTime;
            if (_hitStopTimer <= 0f && _animator != null) _animator.speed = BaseAnimSpeed();
            return;
        }

        UpdateTimers();

        // 넘어짐: 행동 불능. 넉백 잔여만 흘려보내고 기상 대기.
        if (_downTimer > 0f)
        {
            UpdateKnockdown();
            return;
        }

        TickPerception();
        UpdateStateMachine();

        if (_state == ZombieState.Lunge) UpdateLungeMovement();
        else UpdateMovement();

        UpdateAnimation();
    }

    // ★ 매 FixedUpdate에서 쿨다운 타이머 감소
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

        if (_attackCooldownTimer > 0f)
            _attackCooldownTimer -= Time.fixedDeltaTime;

        if (_gruntCooldownTimer > 0f)
            _gruntCooldownTimer -= Time.fixedDeltaTime;

        // 피격 사다리 누적 윈도우/내성 감쇠
        if (_poiseWindowTimer > 0f)
        {
            _poiseWindowTimer -= Time.fixedDeltaTime;
            if (_poiseWindowTimer <= 0f) _comboHits = 0;
        }
        if (_resistMult > 1f)
        {
            _resistDecayTimer -= Time.fixedDeltaTime;
            if (_resistDecayTimer <= 0f) _resistMult = 1f;
        }
    }

    // ──────────── Detection (게이트0 — 틱당 확률 누적, 이진 판정 금지) ────────────

    /// <summary>시야 원뿔+가림 판정(확률 없는 기하). 성공 시 0(밀착)~1(시야 한계) 거리 비율 반환.</summary>
    bool PlayerInSightCone(out float dist01)
    {
        dist01 = 1f;
        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        float maxRange = _config.sightRange * _sightVar;
        if (dist > maxRange) return false;
        dist01 = Mathf.Clamp01(dist / Mathf.Max(0.01f, maxRange));
        if (dist < 0.5f) return true;   // 초근접(겹침)은 각도 무의미
        if (Vector3.Angle(transform.forward, toPlayer) > _config.sightHalfAngle) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1f;
        if (Physics.Raycast(eyePos, toPlayer.normalized, dist, obstacleMask))
            return false;

        return true;
    }

    bool CanHearPlayer()
    {
        if (NoiseManager.Instance == null || _player == null) return false;

        float radius = NoiseManager.Instance.HearingRadius * _config.hearingMultiplier * _hearVar;
        if (radius <= 0f) return false;

        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        if (dist > radius) return false;   // 반경 밖이면 레이캐스트 자체를 생략(스태거드 틱 + 조기 탈출)

        // 장애물 감쇄
        if (Physics.Raycast(transform.position + Vector3.up, toPlayer.normalized, dist, obstacleMask))
            radius *= 0.5f;

        return dist <= radius;
    }

    /// <summary>
    /// 인식 틱(스태거드 — perceptionTickInterval 주기 + 거리 LOD). 시야는 틱당 확률 누적 게이지로:
    /// 일부 누적 = Alert(응시 텔레그래프), 만충 = Chase. 소리는 Investigate로만(떼어내기 보장 — §3.2 철칙).
    /// Chase 이후 상태에선 판정하지 않는다(이미 확정).
    /// </summary>
    void TickPerception()
    {
        if (_state >= ZombieState.Chase) return;

        _perceptTimer -= Time.fixedDeltaTime;
        if (_perceptTimer > 0f) return;

        float flatDist = FlatDistance(transform.position, _player.position);
        float interval = _config.perceptionTickInterval * (flatDist > 25f ? 3f : 1f);   // 거리 LOD
        _perceptTimer = interval;

        // 시야: 원뿔 안이면 틱당 확률 롤 → 성공 시 게이지 누적. 멀수록·플레이어가 조용할수록 낮다.
        if (PlayerInSightCone(out float dist01))
        {
            float exposure = _playerController != null ? _playerController.SightExposureMult : 1f;
            float p = _config.detectBaseChance * _sightVar
                      * Mathf.Lerp(1f, 0.25f, dist01)   // 거리 폴오프
                      * exposure;                        // 이동/사격이 노출을 키운다
            if (Random.value < p)
            {
                _detectGauge += _config.detectGaugePerTick;
                _lastNoticedPos = _player.position;
            }
        }
        else
        {
            _detectGauge = Mathf.Max(0f, _detectGauge - _config.detectDecayPerSec * interval);
        }

        // 게이지 전이
        if (_detectGauge >= 1f)
        {
            EnterChase();
            return;
        }
        if (_detectGauge >= _config.alertThreshold && _state != ZombieState.Alert)
        {
            EnterAlert();
            return;
        }
        if (_state == ZombieState.Alert && _detectGauge < _config.alertThreshold * 0.5f)
        {
            // 식었다 — 마지막 인지 지점을 보러 간다("뭐였지?").
            EnterInvestigate(_lastNoticedPos);
            return;
        }

        // 청각: 어떤 크기의 소리든 Investigate까지만(시야 확정 없이는 Chase 금지 — 소리 사다리 철칙).
        if (_state != ZombieState.Alert && CanHearPlayer())
            EnterInvestigate(_player.position);
    }

    // ──────────── State Machine ────────────

    void UpdateStateMachine()
    {
        switch (_state)
        {
            case ZombieState.Dormant:
                // 정지. 전이는 전부 TickPerception(시야/청각)과 피격이 만든다.
                break;

            case ZombieState.Investigate:
                _investigateTimer += Time.fixedDeltaTime;

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

            case ZombieState.Alert:
                // 멈춰 응시(텔레그래프) → 응시 끝나면 느린 접근. 게이지 전이는 TickPerception 소관.
                if (_alertStareTimer > 0f)
                    _alertStareTimer -= Time.fixedDeltaTime;
                break;

            case ZombieState.Chase:
                if (_attackCooldownTimer <= 0f
                    && FlatDistance(transform.position, _player.position) <= _config.lungeRange)
                    EnterWindup();
                break;

            case ZombieState.Windup:
                _windupTimer -= Time.fixedDeltaTime;
                if (_windupTimer <= 0f) EnterLunge();
                break;

            case ZombieState.Lunge:
                // 이동·접촉은 UpdateLungeMovement에서. 여기선 타이머만.
                _lungeTimer -= Time.fixedDeltaTime;
                if (_lungeTimer <= 0f) EnterRecover();   // 빗나감 — 후딜(회피 보상)
                break;

            case ZombieState.Grapple:
                // 플레이어가 탈출/타임아웃을 판정해 OnGrappleEnded/OnGrappleTimeout으로 끝낸다.
                break;

            case ZombieState.Recover:
                _recoverTimer -= Time.fixedDeltaTime;
                if (_recoverTimer <= 0f)
                {
                    _state = ZombieState.Chase;
                    _attackCooldownTimer = _config.attackCooldown;
                }
                break;
        }
    }

    void EnterAlert()
    {
        _state = ZombieState.Alert;
        _alertStareTimer = _config.alertStareTime;
        _investigateTimer = 0f;
        _investigateLookTimer = 0f;
    }

    void EnterChase()
    {
        bool wasAttackCycle = _state >= ZombieState.Chase && _state != ZombieState.Dead;
        _state = ZombieState.Chase;
        _detectGauge = 1f;
        _investigateTimer = 0f;
        _investigateLookTimer = 0f;

        // 그르렁 전파(§2.2): Chase 진입 순간 작은 반경 사운드 — 듣는 좀비는 Investigate로만 승급.
        // 무선 브로드캐스트식 그룹 어그로 금지 = "한 마리씩 떼어내기"의 수학적 보장.
        if (!wasAttackCycle && _gruntCooldownTimer <= 0f)
        {
            _gruntCooldownTimer = 5f;
            EmitGrunt();
        }

        // ★ Codex HIGH: 시그널을 Chase 진입 시점에 한 번 발동.
        // (이전엔 Chase 루프에서 attackRange 체크가 먼저라, 근접 진입 시 시그널이 누락됨)
        if (_config != null && _config.isSignalZombie && !_hasSignaled && _signalCooldownTimer <= 0f)
        {
            _hasSignaled = true;
            StartCoroutine(SignalCoroutine());
        }
    }

    // ── 절차 그르렁 폴백(B-005a) — gruntSound 미할당 좀비 공용 클립 1장 ──
    // 70~110Hz 사인 스윕 다운 + 진폭 거칠기(30Hz 변조) + 저역 노이즈 = "으르렁" 근사.
    static AudioClip s_proceduralGrunt;
    static AudioClip ProceduralGrunt
    {
        get
        {
            if (s_proceduralGrunt != null) return s_proceduralGrunt;   // 플레이 종료로 파괴되면 재생성
            const int sr = 44100;
            const float dur = 0.6f;
            int len = (int)(sr * dur);
            float[] data = new float[len];
            var rng = new System.Random(7707);
            double phase = 0.0;
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                float u = i / (float)(len - 1);
                float freq = Mathf.Lerp(110f, 70f, u);              // 스윕 다운 — 목 깊은 데로 내려가는 으르렁
                phase += 2.0 * System.Math.PI * freq / sr;          // 스윕은 위상 적분(주파수 직대입은 글리치)
                float growl = (float)System.Math.Sin(phase);
                float rough = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 30f * t);   // 진폭 거칠기
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.05f * (white - lp);                          // 저역 노이즈(숨 섞임)
                // 어택 50ms / 릴리즈 마지막 30% — 양끝 클릭 방지
                float env = Mathf.Clamp01(u / 0.08f) * (u > 0.7f ? (1f - u) / 0.3f : 1f);
                data[i] = (growl * rough * 0.7f + lp * 0.3f) * env;
            }
            s_proceduralGrunt = AudioClip.Create("ProceduralGrunt", len, 1, sr, false);
            s_proceduralGrunt.SetData(data, 0);
            return s_proceduralGrunt;
        }
    }

    void EmitGrunt()
    {
        if (_config == null) return;
        // 클립 미할당이어도 절차 폴백으로 소리를 낸다 — 리스너 분리 검증(좌우 정위 게이트)에 필수(B-005a).
        SfxOneShot.Play(gruntSound != null ? gruntSound : ProceduralGrunt, transform.position);

        Collider[] nearby = Physics.OverlapSphere(transform.position, _config.gruntRadius, zombieLayer);
        foreach (var col in nearby)
        {
            if (col.gameObject == gameObject) continue;
            var z = col.GetComponent<ZombieController>();   // 좀비 콜라이더=루트(시그널 코드와 동일) — 부모 탐색 비용 제거
            if (z == null || z == this || z._dead) continue;
            // Dormant/Idle만 깨운다(Investigate로). 이미 더 높은 상태는 건드리지 않는다.
            if (z._state == ZombieState.Dormant || z._state == ZombieState.Idle)
                z.EnterInvestigate(transform.position);   // 소리의 근원(그르렁 낸 좀비)으로 걸어온다
        }
    }

    void EnterInvestigate(Vector3 target)
    {
        _state = ZombieState.Investigate;
        _investigateTarget = target;
        _investigateTimer = 0f;
        _investigateLookTimer = 0f;
    }

    void EnterWindup()
    {
        _state = ZombieState.Windup;
        _windupTimer = _config.lungeWindup;

        if (windupSound != null)
            SfxOneShot.Play(windupSound, transform.position);

        // 윈드업 텔레그래프: Attack 클립이 와이어링돼 있으면 그걸, 없으면 상체 젖힘 코드 폴백.
        if (_hasAttackTrigger && _animator != null) _animator.SetTrigger(AttackHash);
        else if (_visual != null)
        {
            _visual.DOKill();
            _visual.DOLocalRotate(new Vector3(-22f, 0f, 0f), _config.lungeWindup * 0.8f)
                   .SetEase(Ease.OutQuad);
        }
    }

    void EnterLunge()
    {
        _state = ZombieState.Lunge;
        _lungeTimer = _config.lungeDuration;
        // 방향은 이 순간 고정(커밋) — 윈드업 동안 옆으로 빠지면 허공에 돌진한다(회피 보상).
        Vector3 d = _player.position - transform.position;
        d.y = 0f;
        _lungeDir = d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;

        if (_visual != null)
        {
            _visual.DOKill();
            _visual.DOLocalRotate(new Vector3(14f, 0f, 0f), 0.08f);   // 젖혔다가 앞으로 덮침
        }
    }

    void EnterRecover()
    {
        _state = ZombieState.Recover;
        _recoverTimer = _config.lungeRecover;
        ResetVisualPose();
    }

    void ResetVisualPose()
    {
        if (_visual == null) return;
        _visual.DOKill();
        _visual.DOLocalRotate(Vector3.zero, 0.2f).SetEase(Ease.OutQuad);
    }

    /// <summary>신호 좀비가 AlertTo()로 호출. 즉시 Chase. (제어 계열 예외 — 그르렁 전파 규칙의 유일한 우회)</summary>
    public void AlertTo(Vector3 targetPosition)
    {
        if (_dead || _state == ZombieState.Dead || _config == null) return;   // config 없는 룩데브 조각상 가드(NRE 실측)
        if (_state >= ZombieState.Chase) return;
        EnterChase();
    }

    // ──────────── Movement ────────────

    void UpdateMovement()
    {
        Vector3 moveTarget;
        float stopDist;
        float speedMult = 1f;

        switch (_state)
        {
            case ZombieState.Investigate:
                moveTarget = _investigateTarget;
                stopDist = 0.5f;
                break;
            case ZombieState.Alert:
                // 응시 중엔 제자리(텔레그래프 — "쟤가 날 봤나?"), 응시 후 느린 접근.
                moveTarget = _player.position;
                stopDist = _config.lungeRange;
                speedMult = _alertStareTimer > 0f ? 0f : _config.alertApproachSpeedMult;
                break;
            case ZombieState.Chase:
                moveTarget = _player.position;
                stopDist = _config.lungeRange * 0.8f;
                break;
            case ZombieState.Idle:
                moveTarget = _homePosition;
                stopDist = 1f;
                break;
            default: // Dormant/Windup/Grapple/Recover — 제자리
                moveTarget = transform.position;
                stopDist = 0.5f;
                break;
        }

        Vector3 toTarget = moveTarget - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        // 경직 중이면 추격 의지를 끊는다(넉백만 작용 → "맞고 밀려나는" 멈칫).
        bool staggered = _staggerTimer > 0f;

        Vector3 desiredDir = (!staggered && speedMult > 0f && dist > stopDist) ? toTarget.normalized : Vector3.zero;
        if (!staggered && _state != ZombieState.Dormant) desiredDir += CalcSeparation() * separationStrength;
        desiredDir.y = 0f;

        float speed = (_config != null ? _config.moveSpeed : 3f) * Mathf.Max(0.01f, speedMult);
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
        }

        // Alert/Chase/Windup/Grapple에서는 플레이어를 바라봄, 그 외는 이동 방향
        bool facePlayer = _state == ZombieState.Alert || _state == ZombieState.Chase
                       || _state == ZombieState.Windup || _state == ZombieState.Grapple;
        Vector3 lookDir = facePlayer ? (_player.position - transform.position) : _velocity;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            _rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.fixedDeltaTime));
        }

        _knockbackVel = Vector3.Lerp(_knockbackVel, Vector3.zero,
            1f - Mathf.Exp(-KnockbackDamping * Time.fixedDeltaTime));
    }

    /// <summary>런지 전용 이동 — 고정 방향 돌진. 벽에 막히면 즉시 후딜, 접촉하면 grapple.</summary>
    void UpdateLungeMovement()
    {
        float step = _config.lungeSpeed * Time.fixedDeltaTime;

        // 벽 통과 방지
        Vector3 origin = transform.position + Vector3.up * 1f;
        if (Physics.Raycast(origin, _lungeDir, step + 0.4f, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            EnterRecover();
            return;
        }

        Vector3 nextPos = transform.position + _lungeDir * step;
        nextPos.y = SampleTerrainHeight(nextPos) + _groundOffset;
        _rb.MovePosition(nextPos);
        _rb.MoveRotation(Quaternion.LookRotation(_lungeDir, Vector3.up));
        _velocity = _lungeDir * _config.lungeSpeed;   // 애니 Speed 파라미터용

        // 접촉 판정 → grapple 시도
        if (FlatDistance(nextPos, _player.position) <= _config.grappleContactRadius)
            ResolveLungeContact();
    }

    void ResolveLungeContact()
    {
        bool grabbed = _playerController != null
                    && _playerController.TryBeginGrapple(this, _config.grappleHold);
        if (grabbed)
        {
            _state = ZombieState.Grapple;
            _velocity = Vector3.zero;
            // 잡는 순간 한 입 — 근접 피격은 HP로 아프게(§6.5 유저 판정: 싱크 비용 아님).
            _playerController.TakeDamage(_config.grappleDamage);
            ResetVisualPose();
        }
        else
        {
            // 대시 무적(회피 성공)이면 헛손질 — 피해 없음. 이미 다른 좀비에게 잡혀 있으면 타격만.
            if (_playerController != null && !_playerController.IsUntouchable)
                _playerController.TakeDamage(_config.attackDamage);
            EnterRecover();
        }
    }

    /// <summary>플레이어가 탈출(연타 성공)했거나 외부 요인(플레이어 사망 등)으로 grapple이 끝남.</summary>
    public void OnGrappleEnded(bool escaped)
    {
        if (_state != ZombieState.Grapple) return;
        if (escaped)
        {
            // 밀쳐냄: 경직 + 뒤로 넉백 — 탈출의 손맛.
            _staggerTimer = Mathf.Max(_staggerTimer, _config.grappleEscapeStagger);
            Vector3 away = transform.position - _player.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f)
                _knockbackVel = away.normalized * _config.grappleEscapeKnockback;
        }
        EnterRecover();
    }

    /// <summary>grapple 홀드 타임아웃 — 탈출 실패의 청구서: 한 입 더 물고 놓아준다.</summary>
    public void OnGrappleTimeout()
    {
        if (_state != ZombieState.Grapple) return;
        if (_playerController != null)
            _playerController.TakeDamage(_config.grappleDamage);
        EnterRecover();
    }

    /// <summary>기본 애니 재생속도 — 어그로(Chase~Recover) 중에는 가속(연출 패스). 히트스탑/킬 프리즈의 0은 별도 경로가 소유.</summary>
    float BaseAnimSpeed() => IsAggro ? _config.chaseAnimSpeedMult : 1f;

    void UpdateAnimation()
    {
        if (_animator != null)
        {
            _animator.SetFloat(SpeedHash, _velocity.magnitude);
            // 히트스탑 중/사망 시에는 절대 덮어쓰지 않는다(킬 프리즈 0 보호) — FixedUpdate 가드가 막지만 이중 방어.
            if (!_dead && _hitStopTimer <= 0f)
                _animator.speed = BaseAnimSpeed();
        }
    }

    // ──────────── Combat — 피격 사다리 (§6.2) ────────────

    /// <summary>피격자 전용 히트스탑 — 애니·이동·판단 정지. 전역 timeScale을 만지지 않는다.</summary>
    void ApplyHitStop(float duration)
    {
        if (duration <= 0f) return;
        _hitStopTimer = Mathf.Max(_hitStopTimer, duration);
        if (_animator != null) _animator.speed = 0f;
    }

    /// <summary>flinch: 모든 풀히트가 보장하는 짧은 경직. 한 발 = 적 행동 1회 거부(윈드업 취소).</summary>
    void Flinch()
    {
        _staggerTimer = Mathf.Max(_staggerTimer, _config.flinchDuration);
        if (_state == ZombieState.Windup) CancelAttackCycle();
    }

    /// <summary>stagger: 사다리 2단 — 더 긴 정지. 런지(커밋 상태)도 끊는다.</summary>
    void Stagger()
    {
        _staggerTimer = Mathf.Max(_staggerTimer, _config.staggerDuration);
        if (_state == ZombieState.Windup || _state == ZombieState.Lunge) CancelAttackCycle();
        if (_state == ZombieState.Grapple) ForceReleaseGrapple();
    }

    /// <summary>knockdown: 넘어짐 + 기상. 다운 중 받는 피해 증가(추가타 보상).</summary>
    void Knockdown()
    {
        _downTimer = _config.knockdownDuration;
        _gettingUp = false;
        if (_state == ZombieState.Windup || _state == ZombieState.Lunge) CancelAttackCycle();
        if (_state == ZombieState.Grapple) ForceReleaseGrapple();
        _velocity = Vector3.zero;

        if (_visual != null)
        {
            _visual.DOKill();
            _visual.DOLocalRotate(new Vector3(-78f, 0f, 0f), 0.15f).SetEase(Ease.OutQuad);
        }
    }

    void UpdateKnockdown()
    {
        _downTimer -= Time.fixedDeltaTime;

        // 넉백 잔여만 흘려보낸다(쓰러진 채 미끄러짐).
        Vector3 step = _knockbackVel * Time.fixedDeltaTime;
        if (step.sqrMagnitude > 0.0000001f)
        {
            Vector3 nextPos = transform.position + step;
            nextPos.y = SampleTerrainHeight(nextPos) + _groundOffset;
            _rb.MovePosition(nextPos);
        }
        _knockbackVel = Vector3.Lerp(_knockbackVel, Vector3.zero,
            1f - Mathf.Exp(-KnockbackDamping * Time.fixedDeltaTime));

        // 기상 연출은 다운 끝나기 직전에 시작 — 일어나는 모션이 보여야 "다시 온다"가 읽힌다.
        if (!_gettingUp && _downTimer <= 0.25f)
        {
            _gettingUp = true;
            ResetVisualPose();
        }
        if (_downTimer <= 0f)
        {
            // 일어난 직후 짧은 비틀 — 즉시 풀스피드 추격으로 튀지 않게.
            _staggerTimer = Mathf.Max(_staggerTimer, 0.3f);
            if (_state < ZombieState.Chase) EnterChase();   // 맞고 넘어졌으면 일어나선 무조건 어그로
        }
    }

    void CancelAttackCycle()
    {
        _state = ZombieState.Chase;
        _attackCooldownTimer = _config.attackCooldown;
        ResetVisualPose();
    }

    void ForceReleaseGrapple()
    {
        if (_playerController != null) _playerController.ReleaseGrapple(this);
        _state = ZombieState.Chase;
        _attackCooldownTimer = _config.attackCooldown;
    }

    /// <summary>피격 흰 점멸(볼트 A) — "박혔다"의 최저비용·최고가독 신호(로우폴리 표준). 생존 피격에만(사망은 스펙터클 소관).</summary>
    void HitFlash(float duration = 0.07f)
    {
        if (_scanRenderers == null) return;
        _hitFlashTimer = duration;
        if (_flashMpb == null) { _flashMpb = new MaterialPropertyBlock(); _flashMpb.SetColor(BaseColorId, new Color(2.6f, 2.6f, 2.6f, 1f)); }
        foreach (var r in _scanRenderers) if (r != null) r.SetPropertyBlock(_flashMpb);
    }

    /// <summary>풀히트의 사다리 누적: 윈도우 내 N번째 히트가 flinch→stagger→knockdown으로 승급. 내성으로 스턴락 방지.</summary>
    void ApplySolidReaction()
    {
        _comboHits++;
        _poiseWindowTimer = _config.poiseWindow;
        _resistDecayTimer = _config.resistDecayTime;

        int stagAt = Mathf.CeilToInt(_config.staggerAtHit * _resistMult);
        // SO 설정 실수(stag ≥ down)나 반올림으로 문턱이 역전돼도 사다리 순서를 보장(리뷰 반영).
        int downAt = Mathf.Max(Mathf.CeilToInt(_config.knockdownAtHit * _resistMult), stagAt + 1);

        if (_comboHits >= downAt)
        {
            _comboHits = 0;
            _resistMult *= _config.resistGrowth;
            Knockdown();
        }
        else if (_comboHits == stagAt) Stagger();
        else Flinch();
    }

    /// <summary>방향/넉백 없는 일반 피해(DoT 등). 기본 죽음 연출 사용(비산 방향은 전방 반대 폴백).</summary>
    public void TakeDamage(int amount) => TakeBulletHit(amount, Vector3.zero, 0f, 0f, BulletHitTier.NearMiss, false);

    /// <summary>
    /// 원거리(총알) 피해 — 3계층 판정 결과를 받는다(§6.2).
    /// Full: 풀데미지+넉백+사다리. Graze(스침): flinch만. NearMiss(그레이즈): 데미지만(+호출자가 굴린 비틀).
    /// hitStop은 피격자 전용(전역 timeScale 금지). 사망 시 킬 히트스탑은 내부에서 증폭.
    /// </summary>
    public void TakeBulletHit(int amount, Vector3 hitDir, float knockback, float hitStop, BulletHitTier tier, bool nearMissFlinch, bool converged = false)
    {
        if (_dead || _config == null) return;   // SO 미연결 좀비 NRE 가드(FixedUpdate와 동일 — 리뷰 반영)

        if (_downTimer > 0f)
            amount = Mathf.Max(1, Mathf.RoundToInt(amount * _config.downedDamageMult));   // 다운 추가타 보너스

        // 보장 원샷(B-009 수정 2026-06-11): 수렴샷 풀히트 = 비엘리트 확정 킬. 데미지 배수가 아니라 "보장" —
        // 저격의 1순위는 확실성(확률적 죽음=저격 아님). 희소성은 의식 비용(0.85s 노출+이동 둔화)×전장 밀도가
        // 담보한다(pm 판정). 엘리트 도입 시 여기서 면역(대크리) 분기.
        if (converged && tier == BulletHitTier.Full)
            amount = Mathf.Max(amount, _currentHP);

        _currentHP -= amount;

        if (_currentHP <= 0)
        {
            float stop = hitStop * 2.5f;                      // 킬샷: 일반 30~50ms → 80~120ms급 프리즈
            if (converged) stop = Mathf.Max(stop, 0.12f);     // 수렴 킬은 프리즈 연장 — "한 발의 무게"
            Die(hitDir, stop, converged);
            return;
        }

        // 넉백은 풀히트만 — 스침/그레이즈가 호드를 밀면 3계층의 "질감 차이"가 사라진다.
        if (tier == BulletHitTier.Full && knockback > 0f)
        {
            Vector3 d = hitDir; d.y = 0f;
            if (d.sqrMagnitude > 0.0001f)
            {
                _knockbackVel += d.normalized * knockback;
                float maxKb = knockback * 2f;
                if (_knockbackVel.sqrMagnitude > maxKb * maxKb)
                    _knockbackVel = _knockbackVel.normalized * maxKb;
            }
        }

        switch (tier)
        {
            case BulletHitTier.Full:
                ApplyHitStop(hitStop);
                ApplySolidReaction();
                // 볼트 A(탄착 확실성): 흰 점멸 + 피격 스쿼시 — 15m 부감의 화폐는 실루엣 변위(교과서 2원칙).
                HitFlash();
                _springScale?.Bump(new Vector3(0.16f, -0.2f, 0.16f));
                break;
            case BulletHitTier.Graze:
                ApplyHitStop(hitStop * 0.5f);
                Flinch();
                break;
            case BulletHitTier.NearMiss:
                if (nearMissFlinch) Flinch();   // 비틀 확률은 호출자(CombatFeelConfig)가 굴림
                break;
        }

        // 피격 = 발각 확정. 어디서 맞았는지 모를 수는 없다(낮은 상태에서만 승급).
        if (_state < ZombieState.Chase) EnterChase();
    }

    /// <summary>
    /// 근접 타격. 데미지 + 넉백(코드 변위) + 사다리 + 피격자 히트스탑. 사망 시 무기별 죽음 연출.
    /// 반환값 = 이 타격으로 죽었는지(호출자가 킬 사운드/카운트에 사용).
    /// </summary>
    public bool TakeMeleeHit(int damage, Vector3 attackerPos, float knockback, float stagger, WeaponLoadout.DeathStyle style, float hitStop = 0f)
    {
        if (_dead || _config == null) return false;   // SO 미연결 좀비 NRE 가드(리뷰 반영)

        Vector3 dir = transform.position - attackerPos;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;

        if (_downTimer > 0f)
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * _config.downedDamageMult));

        _currentHP -= damage;

        if (_currentHP <= 0)
        {
            DieByWeapon(style, dir, knockback, hitStop * 1.5f);
            return true;
        }

        // 생존: 넉백 + 무기별 경직 + 사다리 + 피격자 히트스탑 (스케일 범프 제거 — 맞을 때 크기 변화 없음)
        _knockbackVel = dir * knockback;
        if (stagger > 0f) _staggerTimer = Mathf.Max(_staggerTimer, stagger);
        ApplyHitStop(hitStop);
        ApplySolidReaction();
        HitFlash();   // 볼트 A — 근접도 동일한 "박혔다" 신호
        if (_state < ZombieState.Chase) EnterChase();
        return false;
    }

    /// <summary>
    /// 사망 — 죽음의 스펙터클(디자인 나침반 §3.1): corpseStop 프리즈(킬 히트스탑 — 피격자만)
    /// → 머리/모자 팝(터짐) → 쓰러짐 → 시체 잔류 → 시안 와해(디스폰=정화). ZombieDeathFX가 잔류·와해 관리.
    /// 전역 MMTimeScale 제거(combat-texture-foundation §6.2 통합 경로) — 세계는 계속 흐른다.
    /// </summary>
    void Die(Vector3 hitDir, float corpseStop = 0f, bool converged = false)
    {
        _dead = true;
        _state = ZombieState.Dead;
        SetScanTagged(false);   // 시체/래그돌에 아웃라인이 남지 않도록 즉시 태그 제거
        if (_playerController != null) _playerController.ReleaseGrapple(this);   // grapple 중 사망 — 플레이어 즉시 해방
        if (_collider != null) _collider.enabled = false;   // 잔류 시체가 탄을 먹지 않게
        if (_animator != null) _animator.speed = 0f;        // 킬 프레임 프리즈

        _springScale?.Bump(new Vector3(0.3f, -0.5f, 0.3f));

        if (killParticlePrefab != null)
            Instantiate(killParticlePrefab, transform.position, Quaternion.identity);
        // 킬 피드백(사운드+카메라 펀치인)은 시간창당 1회 클램프 — 대량 정화에서 발작 방지(§6.2).
        // killSound 미할당 좀비도 창을 소비한다(의도 — 펀치인은 모든 킬의 권리, 창은 공유 예산).
        if (TryConsumeKillFeedback())
        {
            if (killSound != null) SfxOneShot.Play(killSound, transform.position);
            if (_player != null)
                PlayerCameraRig.Instance?.TriggerPunchIn(transform.position - _player.position,
                    converged ? ConvergedKillPunchIn : KillPunchIn);
        }
        ZombieDeathFX.PlayKillRing(transform.position, converged);   // "죽였다"의 확인 신호 — 수렴 킬은 대형 2연속

        SpawnXPOrbs();
        CraftingSystem.Instance?.NotifyKill(transform.position);
        RunStats.Instance?.AddKill();
        HarvestStrain();

        Vector3 dir = hitDir; dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = -transform.forward;
        StartCoroutine(DeathSpectacle(dir.normalized, Mathf.Max(0f, corpseStop), converged));
        Destroy(gameObject, 12f);   // 스펙터클 경로가 끊겨도(트윈/코루틴 중단) 확실히 제거되는 안전망
    }

    /// <summary>킬 프리즈 → 머리 팝 → 쓰러짐 → 시체 등록(잔류·와해는 ZombieDeathFX 소관).</summary>
    System.Collections.IEnumerator DeathSpectacle(Vector3 dir, float corpseStop, bool converged = false)
    {
        // 실시간 대기 — 전역 히트스탑(timeScale 0.05)이 걸려도 프리즈 길이가 설계값을 지킨다(리뷰 F-1).
        if (corpseStop > 0f) yield return new WaitForSecondsRealtime(corpseStop);
        ZombieDeathFX.PopHead(this, dir, converged);
        LieDown(dir);
        ZombieDeathFX.RegisterCorpse(this);
    }

    /// <summary>쓰러짐 — 타격 방향으로 살짝 밀리며 눕는다(시체 잔류의 시작).</summary>
    void LieDown(Vector3 dir)
    {
        if (_visual != null)
        {
            _visual.DOKill();
            _visual.DOLocalRotate(new Vector3(-85f, 0f, 0f), 0.35f).SetEase(Ease.OutQuad);
        }
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            transform.DOKill();
            transform.DOMove(transform.position + dir.normalized * 0.4f, 0.3f).SetEase(Ease.OutQuad);
        }
    }

    /// <summary>무기별 죽음 연출(형태/멈춤/런치 차등). 킬 프리즈 후 시체를 넉백 방향으로 날린 뒤 제거.</summary>
    void DieByWeapon(WeaponLoadout.DeathStyle style, Vector3 launchDir, float force, float corpseStop = 0f)
    {
        _dead = true;
        _state = ZombieState.Dead;
        SetScanTagged(false);   // 런치되는 시체에 아웃라인이 남지 않도록 즉시 태그 제거
        if (_playerController != null) _playerController.ReleaseGrapple(this);
        if (_collider != null) _collider.enabled = false;
        if (_animator != null) _animator.speed = 0f;   // 킬 프레임 프리즈(피격자만 — 세계는 흐른다)
        _knockbackVel = Vector3.zero;   // 런치 트윈이 변위를 전담

        SpawnXPOrbs();
        CraftingSystem.Instance?.NotifyKill(transform.position);
        RunStats.Instance?.AddKill();
        HarvestStrain();

        // 형태(squash)·런치만 무기별로 차등. 멈춤감은 피격자 프리즈(corpseStop) — 전역 슬로모 아님.
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
        // 근접 킬도 같은 클램프 창에서 카메라 펀치인(사운드는 MeleeSfx 소관이라 여기선 카메라만).
        if (TryConsumeKillFeedback() && _player != null)
            PlayerCameraRig.Instance?.TriggerPunchIn(transform.position - _player.position, KillPunchIn);
        ZombieDeathFX.PlayKillRing(transform.position);

        // 킬 프리즈(corpseStop) 동안 멈췄다가 런치 — "맞는 순간 멈추고, 풀리며 날아간다".
        // 런치가 끝나면 죽음의 스펙터클(머리 팝→쓰러짐→시체 잔류→와해)로 합류.
        Vector3 launchTarget = transform.position + launchDir * launchDist;
        if (style == WeaponLoadout.DeathStyle.Crunch) launchTarget += Vector3.up * 0.5f;
        transform.DOKill();
        transform.DOMove(launchTarget, 0.25f).SetEase(Ease.OutQuad)
            .SetDelay(Mathf.Max(0f, corpseStop))
            .OnComplete(() =>
            {
                if (this == null) return;
                ZombieDeathFX.PopHead(this, launchDir);
                LieDown(launchDir);
                ZombieDeathFX.RegisterCorpse(this);
            });
        Destroy(gameObject, 12f);   // 스펙터클 경로가 끊겨도 확실히 제거되는 안전망
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

    /// <summary>익스트랙션 루프: 사망 시 strain 수확. config에 지정이 없으면 RunManager 기본(생체 1성).
    /// E-001: 깊이 가중(랩 A+1/B+2/C+3) — LabDepthStrain이 씬에 없으면 기존대로 1.</summary>
    void HarvestStrain()
    {
        if (Run.RunHarvest.Instance == null) return;
        var def = _config != null ? _config.strainDrop : null;
        if (def == null && Meta.MetaProgress.Instance != null)
            def = Meta.MetaProgress.Instance.BioStrain;
        if (def != null)
        {
            int weight = Run.LabDepthStrain.WeightAt(transform.position);
            Run.StrainHarvestFX.OnZombiePurged(def, weight, transform.position);
        }
    }

    // ──────────── Signal Zombie ────────────

    System.Collections.IEnumerator SignalCoroutine()
    {
        yield return new WaitForSeconds(_config.signalDelay);

        // ★ 대기 중 좀비 사망/플레이어 소멸 가드
        if (_dead || _player == null) yield break;

        // 딜레이 동안 Chase(공격 사이클 포함)가 풀렸으면 소환 취소.
        if (_state < ZombieState.Chase || _state == ZombieState.Dead)
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
            if (zombie != null && !zombie._dead && zombie._state < ZombieState.Chase)
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
