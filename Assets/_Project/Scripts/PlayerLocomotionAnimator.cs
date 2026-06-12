using UnityEngine;

/// <summary>
/// 톱다운 트윈스틱 플레이어의 비주얼 로코모션 드라이버.
///
/// 핵심: facing(어디를 보는가)과 movement(어디로 가는가)를 분리한다.
/// - facing  = 마우스 조준 방향(PlayerCombat.AimDirection). 정지해도 항상 조준을 향해 회전.
/// - movement= PlayerController가 옮기는 실제 변위. 조준 프레임에 투영해 (MoveX,MoveY)로 환산.
///   → 마우스는 앞을 보는데 S로 뒤로 가면 "앞 보며 뒷걸음", D면 "앞 보며 우측 스트레이프".
/// - speed   = 변위 크기(m/s)를 idle(0)/walk(1)/run(2) 블렌드 값으로 매핑.
///
/// PlayerController는 transform.position만 옮기고(물리X·회전X) 루트를 절대 안 돌린다.
/// 회전은 이 컴포넌트(CharacterVisual)에만 적용 — 루트를 돌리면 자식 카메라까지 돈다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerLocomotionAnimator : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("이동을 추적할 대상. 비우면 부모(Player 루트)를 사용.")]
    [SerializeField] Transform moveSource;
    [Tooltip("조준 방향 소스. 비우면 moveSource에서 PlayerCombat을 찾는다. 없으면 이동 방향으로 facing 폴백.")]
    [SerializeField] PlayerCombat aimSource;

    [Header("Speed Mapping (m/s → blend)")]
    [Tooltip("이 속도에서 Speed 블렌드=1(걷기).")]
    [SerializeField] float walkSpeedRef = 5f;
    [Tooltip("이 속도에서 Speed 블렌드=2(달리기).")]
    [SerializeField] float runSpeedRef = 8.5f;
    [Tooltip("측정 속도 상한 — 스폰/텔레포트 점프로 인한 블렌드 오버슈트 방지.")]
    [SerializeField] float maxSpeed = 12f;
    [Tooltip("이 속도(m/s) 미만이면 정지로 간주 — 방향(MoveX/Y)을 0으로(회전은 계속).")]
    [SerializeField] float moveThreshold = 0.3f;

    [Header("Damping")]
    [SerializeField] float speedDamp = 0.1f;
    [Tooltip("방향(MoveX/MoveY) 댐핑 — 급격한 방향 전환을 부드럽게.")]
    [SerializeField] float dirDamp = 0.08f;

    [Header("Stance Controllers (무기별 자세)")]
    [Tooltip("라이플/샷건 스탠스 컨트롤러. 비우면 스탠스 스왑 비활성(현재 컨트롤러 유지).")]
    [SerializeField] RuntimeAnimatorController rifleController;
    [Tooltip("권총(리볼버) 스탠스 컨트롤러. 비우면 스탠스 스왑 비활성.")]
    [SerializeField] RuntimeAnimatorController pistolController;

    [Header("Reload (UpperBody Layer)")]
    [Tooltip("장전 블렌드트리의 기준 클립 길이(초). Stand_Reload 길이 — Move_Reload는 트리 내 timeScale로 동일 길이 정규화됨.")]
    [SerializeField] float reloadClipLength = 3.6666667f;
    [Tooltip("장전 모션 재생 배속 상한. 게임 reloadTime이 클립보다 짧아도 이 배속까지만 가속 — " +
             "bool 구동이라 타이머 종료 시 모션이 조기 컷되어 '조작하다 마는' 그림이 되며, 그게 고배속 경련보다 낫다(유저 판정). " +
             "하한 보호는 없음 — reloadTime이 클립보다 훨씬 긴 저속 장전 무기는 기존식대로 자연히 느려진다.")]
    [SerializeField] float maxReloadAnimSpeed = 1.35f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveYHash = Animator.StringToHash("MoveY");
    static readonly int ReloadHash = Animator.StringToHash("Reload");
    static readonly int ReloadSpeedHash = Animator.StringToHash("ReloadSpeed");
    static readonly int FiringHash = Animator.StringToHash("Firing");

    Animator _animator;
    Vector3 _lastPos;
    bool _wasReloading;
    bool _wasFiring;
    bool _hasFiringParam;        // 현재 컨트롤러에 Firing 파라미터가 있는가 — 권총 스탠스엔 없음(발사 클립 부재)
    bool _firingParamChecked;    // 스탠스 스왑마다 1회 재검사(파라미터 조회는 배열 할당이라 매 프레임 금지)

    /// <summary>
    /// 무기 종류(PlayerCombat 폴링)에 따라 컨트롤러 스왑: 권총류=권총 스탠스, 그 외(라이플/샷건)=라이플 스탠스.
    /// runtimeAnimatorController 교체는 파라미터를 보존하지 않으므로, 스왑 직전 댐핑된 현재 값을 읽어
    /// 직후 그대로 재주입한다 — 이동 중 스왑에도 블렌드 연속성 유지(한 프레임 idle 스냅 방지).
    /// </summary>
    void ApplyStance()
    {
        if (aimSource == null || rifleController == null || pistolController == null) return;

        RuntimeAnimatorController desired =
            aimSource.CurrentGunClass == GunSfx.GunClass.Pistol ? pistolController : rifleController;
        if (_animator.runtimeAnimatorController == desired) return;

        // 스왑 직전의 댐핑된 실제 값(목표값이 아니라 현재 보간값)을 캡처.
        float speed = _animator.GetFloat(SpeedHash);
        float moveX = _animator.GetFloat(MoveXHash);
        float moveY = _animator.GetFloat(MoveYHash);

        _animator.runtimeAnimatorController = desired;

        _animator.SetFloat(SpeedHash, speed);
        _animator.SetFloat(MoveXHash, moveX);
        _animator.SetFloat(MoveYHash, moveY);

        // 새 컨트롤러의 Reload bool은 기본 false — 폴링 엣지 상태를 동기화해
        // 장전 중 스왑이어도(무기 교체가 _reloading을 리셋) 다음 폴링이 올바른 엣지를 본다.
        _wasReloading = false;
        _wasFiring = false;
        _firingParamChecked = false;   // 컨트롤러가 바뀌었으니 Firing 파라미터 존재 여부 재검사
    }

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _animator.applyRootMotion = false;   // 위치는 PlayerController가 주도, 애니는 표현용
        if (moveSource == null) moveSource = transform.parent;
        if (aimSource == null && moveSource != null) aimSource = moveSource.GetComponentInParent<PlayerCombat>();
    }

    void OnEnable()
    {
        if (moveSource != null) _lastPos = moveSource.position;
    }

    void Update()
    {
        if (moveSource == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        ApplyStance();

        // 평면(XZ) 변위만 — 지면 추종에 의한 y 변화는 로코모션과 무관.
        Vector3 cur = moveSource.position;
        Vector3 delta = cur - _lastPos;
        _lastPos = cur;
        delta.y = 0f;

        // 스폰/텔레포트/리스폰으로 루트가 순간이동하면 한 프레임 변위가 비물리적으로 커진다.
        // 그 가짜 변위로 방향(MoveX/Y)이 엉뚱하게 튀지 않도록, 비물리 속도면 기준점만 리셋하고 스킵.
        float rawSpeed = delta.magnitude / dt;
        if (rawSpeed > maxSpeed * 3f) return;   // _lastPos는 이미 cur로 갱신됨 — 다음 프레임부터 정상 측정

        float speed = Mathf.Min(rawSpeed, maxSpeed);
        bool moving = speed > moveThreshold;

        // --- facing: 조준 방향(없으면 이동 방향) ---
        Vector3 face;
        if (aimSource != null && aimSource.AimDirection.sqrMagnitude > 0.0001f)
            face = aimSource.AimDirection;
        else if (moving)
            face = delta.normalized;
        else
            face = transform.forward;   // 정지 + 조준원 없음 → 현재 방향 유지

        face.y = 0f;
        if (face.sqrMagnitude > 0.0001f)
        {
            face.Normalize();
            // 몸 회전은 조준(_aimDir)을 즉시 추종한다. 트레일링(지연)은 조준 쪽(PlayerCombat.aimResponsiveness)에서만
            // 만들어지므로, 몸은 그 "이미 지연된 조준"을 그대로 바라본다 → 몸·탄도가 한 박자로 정확히 일치.
            transform.rotation = Quaternion.LookRotation(face, Vector3.up);
        }

        // --- movement: 조준(facing) 프레임에 투영 → (MoveX 우측, MoveY 전방) ---
        Vector3 facingFwd = transform.forward; facingFwd.y = 0f;
        if (facingFwd.sqrMagnitude < 0.0001f) facingFwd = Vector3.forward;   // 루트가 수직을 보면 zero-벡터화 방지
        facingFwd.Normalize();
        Vector3 facingRight = Vector3.Cross(Vector3.up, facingFwd);   // 좌수계: up×fwd = right

        Vector3 moveDir = moving ? delta.normalized : Vector3.zero;
        float moveX = Vector3.Dot(moveDir, facingRight);   // + = 우측 스트레이프
        float moveY = Vector3.Dot(moveDir, facingFwd);     // + = 전진, - = 뒷걸음

        // --- speed 블렌드 매핑: 0(idle)→1(walk)→2(run) ---
        float blendSpeed;
        if (speed <= walkSpeedRef)
            blendSpeed = walkSpeedRef > 0f ? speed / walkSpeedRef : 0f;
        else
            blendSpeed = 1f + (speed - walkSpeedRef) / Mathf.Max(0.01f, runSpeedRef - walkSpeedRef);
        blendSpeed = Mathf.Clamp(blendSpeed, 0f, 2f);

        _animator.SetFloat(SpeedHash, blendSpeed, speedDamp, dt);
        _animator.SetFloat(MoveXHash, moveX, dirDamp, dt);
        _animator.SetFloat(MoveYHash, moveY, dirDamp, dt);

        // --- 상체 장전 레이어: PlayerCombat 재장전 상태 폴링(facing과 동일한 폴링 패턴, 직접 결합 없음) ---
        // bool 구동 — 정상 종료(타이머 만료)와 중단(무기 교체로 _reloading 리셋) 모두 같은 경로로 Empty 복귀.
        if (aimSource != null)
        {
            bool reloading = aimSource.IsReloading;
            if (reloading != _wasReloading)
            {
                if (reloading)
                {
                    // 모션 길이를 게임의 reloadTime에 종속 — 게임 수치가 권위, 애니가 따라간다.
                    // 단 가속은 maxReloadAnimSpeed까지만(예: 리볼버 1.1s → 3.33배속 경련 방지).
                    float dur = aimSource.ReloadDuration;
                    float animSpeed = dur > 0.01f
                        ? Mathf.Min(maxReloadAnimSpeed, reloadClipLength / dur)
                        : 1f;
                    _animator.SetFloat(ReloadSpeedHash, animSpeed);
                }
                _animator.SetBool(ReloadHash, reloading);
                _wasReloading = reloading;
            }

            // --- 상체 발사 레이어: 주발사 지속 상태 폴링(장전과 동일 패턴) ---
            // Firing 파라미터가 없는 스탠스(권총: 발사 클립 부재)에서는 세팅 자체를 생략 — 미존재 파라미터 SetBool 워닝 방지.
            if (!_firingParamChecked)
            {
                _hasFiringParam = false;
                foreach (var p in _animator.parameters)
                    if (p.nameHash == FiringHash) { _hasFiringParam = true; break; }
                _firingParamChecked = true;
            }
            if (_hasFiringParam)
            {
                bool firing = aimSource.IsFiringSustained;
                if (firing != _wasFiring)
                {
                    _animator.SetBool(FiringHash, firing);
                    _wasFiring = firing;
                }
            }
        }
    }
}
