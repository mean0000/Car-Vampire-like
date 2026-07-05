using UnityEngine;

/// <summary>
/// 비주얼 캐릭터(Humanoid Animator)에 붙는다. 두 역할:
///  ① 구동: 매 프레임 Motor/Aim 상태 → 로코모션 파라미터(Speed/MoveX/MoveY/Dash).
///     facing(조준)과 movement(이동)를 분리 — 마우스로 앞을 보며 S로 뒷걸음, D로 우측 스트레이프.
///  ② 릴레이: 공격 클립의 AnimationEvent(<see cref="OnAttackHit"/>)를 받아 <see cref="AttackHit"/>로
///     무기에 전달한다(애니가 진실 — 타격 타이밍은 클립이 소유).
///
/// 파라미터 규약은 Animation 에이전트의 컨트롤러와 고정 합의: Speed/MoveX/MoveY(float), Attack(trigger), Dash(bool).
/// 루트는 안 돌린다(자식 카메라 보호) — 회전은 이 비주얼 transform에만 적용한다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimatorDriver : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("이동을 추적할 대상. 비우면 부모(플레이어 루트). Aim/Motor는 이 대상의 부모 체인에서 찾는다.")]
    [SerializeField] Transform moveSource;

    [Header("Speed Mapping (m/s → blend)")]
    [SerializeField] float walkSpeedRef = 5f;
    [SerializeField] float runSpeedRef = 8.5f;
    [Tooltip("측정 속도 상한 — 스폰/텔레포트 점프로 인한 블렌드 오버슈트 방지.")]
    [SerializeField] float maxSpeed = 12f;
    [Tooltip("이 속도(m/s) 미만이면 정지로 간주 — 방향(MoveX/Y)을 0으로(회전은 계속).")]
    [SerializeField] float moveThreshold = 0.3f;

    [Header("Damping")]
    [SerializeField] float speedDamp = 0.1f;
    [SerializeField] float dirDamp = 0.08f;
    [Tooltip("★이동 시 몸 회전 속도(도/초) — 낮을수록 부드럽게 돌고(각짐↓·둔함↑), 높을수록 즉각 스냅. WASD 8방향의 '딱딱한 방향 전환'을 이걸로 완화한다. 공격/대시 중엔 무시(즉시 잠금 — 런지/회피 크리스프 유지).")]
    [SerializeField] float faceTurnRate = 600f;

    [Header("★상하체 완전 분리 (07-04 — SoD/RUINER식, 상체 공격이 하체에 무영향)")]
    // ★상체 레이어 웨이트 구동(07-04 위상 디싱크 확정 수정, Fix B): 두 로코모션 클럭(상체 UB_Loco·하체 Base Locomotion)이
    //   동시에 weight>0이 되는 상황을 아예 없앤다 — 걷기 정상상태에선 상체를 Base가 구동(weight 0)해 다리와 같은 클럭 = 위상 자동 정합.
    //     · Base가 상체 소유(액션/대시)      → 0 즉시 (Base 전신이 상체까지 보임, 위상 정합)
    //     · 콤보 진행                        → 1 즉시 (크리스프 진입)
    //     · 그 외(콤보 직후 복귀 + 걷기)      → 0으로 이즈아웃(comboLayerBlendTime) — 정적 콤보-끝-포즈 → Base 라이브 로코모션 블렌드
    //   콤보 종료 시 컨트롤러는 Combo 상태가 마지막 프레임을 홀드(Combo→UB_Loco 전이 제거)하고, 웨이트 이즈아웃이 그 *정적* 포즈를
    //   Base 로코모션으로 블렌드한다. 정적 포즈라 클럭이 안 끼어 위상 무관 = 디싱크 원천 소멸. 단일 블렌드라 복귀 튐도 없다.
    [Tooltip("★콤보→걷기 복귀 웨이트 이즈아웃 시간(초). 콤보 종료 후 상체 레이어를 이 시간에 걸쳐 1→0으로 내려 " +
             "정적 콤보-끝-포즈를 Base 라이브 로코모션으로 블렌드한다(단일 블렌드=튐 없음). 짧을수록 크리스프, 길수록 부드럽게 안착.")]
    [SerializeField] float comboLayerBlendTime = 0.12f;
    [Tooltip("★콤보 스텝인(Day2 '밀어넣고 멈춤' 전진 루트모션) 억제 — 완전 분리에선 기본 ON(true). " +
             "이유: 정지 콤보에서 다리가 idle 스탠스인데 루트모션이 전진시키면 발이 미끄러진다(브리프 §2). " +
             "★현 아키텍처(콤보=UpperBody 마스크 레이어, Root 제외)에선 콤보가 애초에 전진 루트모션을 delta에 싣지 않아 이 값과 무관하게 스텝인이 없다. " +
             "이 노브는 억제 경로를 명시적으로 남겨둔 것이며, Day2 스텝인의 진짜 부활은 콤보를 Root 포함 레이어로 되돌리는 컨트롤러 변경이 필요하다(그럴 경우 다리가 다시 콤보 골반을 따라간다).")]
    [SerializeField] bool suppressComboStepIn = true;

    public enum FacingMode { FaceMovement, FaceMouse, Hybrid }
    [Header("Facing Mode (비교용 — 플레이 중 F키 순환)")]
    [Tooltip("몸이 뭘 보나: FaceMovement=이동 방향(하데스) / FaceMouse=마우스(트윈스틱) / Hybrid=질주는 이동·그 외는 조준. 직접 비교용.")]
    [SerializeField] FacingMode facingMode = FacingMode.Hybrid;
    [Tooltip("facingMode 순환 키.")]
    [SerializeField] KeyCode cycleFacingKey = KeyCode.F;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveYHash = Animator.StringToHash("MoveY");
    static readonly int ComboStepHash = Animator.StringToHash("ComboStep");
    static readonly int AttackHash = Animator.StringToHash("Attack");   // ★콤보 1단 진입 트리거(ANY→Combo1, CanTransitionToSelf) — 홀드된 Combo에서 재시작 가능
    static readonly int DashHash = Animator.StringToHash("Dash");
    static readonly int DashXHash = Animator.StringToHash("DashX");
    static readonly int DashYHash = Animator.StringToHash("DashY");
    // ★액션 트리거 해시(Counter/Skill01/SkillCharge/SkillCancel/DashAttack)는 더 이상 여기 하드코딩하지 않는다 —
    //   WeaponActionSet.animator(SO)가 이름을 소유하고 WeaponAnimatorData.Resolve()가 해시를 캐시(2026-07-05 슬롯화).

    Animator _animator;
    PlayerAim _aim;
    PlayerMotor _motor;
    Vector3 _lastPos;
    bool _wasDashing;
    bool _attacking;   // 공격 커밋 중 = 루트모션을 위치로 적용(PlayerBrain이 매 프레임 갱신)
    Vector3 _lockedFace;   // 콤보 단 시작 시 잠근 facing — 공격 중 몸/런지 방향 고정(단 사이엔 재캡처)
    int _comboLayer = -1;       // ★상체 콤보 오버라이드 레이어("UpperBodyCombo") 인덱스(컨트롤러에 없으면 -1 → 웨이트 구동 스킵, 무해)
    bool _comboActive;          // ★콤보(평타) 진행 중인가 — 스텝인 억제 게이트의 소스(SetCombo가 설정). 반격/스킬/대시베기는 false(Base 전신).
                                //   ★웨이트 게이트는 더 이상 이 값이 아니라 "Base가 상체를 소유하는가"(LayerHasActionTag(0)‖IsDashing)로 구동(07-04 튐 수정).
    bool _suppressStepIn;       // ★스텝인 억제 스냅샷(Stab H-1) — Tick 시점에 확정. OnAnimatorMove가 라이브 _comboActive 대신 이걸 읽어,
                                //   같은 프레임 후반 애니 이벤트(OnComboEnd→SetCombo(0))가 게이트를 뒤집는 레이스 차단.
    float _comboWeight;         // ★상체 레이어 웨이트 상태(Fix B) — MoveTowards 이즈아웃이 프레임 간 값을 이어가려면 필요. SetLayerWeight의 최근값과 동치.

    /// <summary>공격 클립 타격 정점(AnimationEvent OnAttackHit)이 발화 → 무기가 구독해 판정.</summary>
    public event System.Action<int> AttackHit;
    /// <summary>캔슬 윈도우 시작(AnimationEvent OnComboWindow) — 다음 콤보 단 입력이 먹히기 시작.</summary>
    public event System.Action ComboWindow;
    /// <summary>공격 클립 끝(AnimationEvent OnComboEnd) — 다음 단 안 갔으면 콤보 종료.</summary>
    public event System.Action ComboEnd;
    /// <summary>로코모션 클립 발 디딤(AnimationEvent OnFootstep) — PlayerFootsteps가 구독해 발소리.</summary>
    public event System.Action Footstep;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        // 공격 클립은 루트모션(클립에 박힌 거리만큼 전진) — deltaPosition을 받으려면 true.
        // 단 자동적용(자식만 이동)은 OnAnimatorMove 정의로 차단되고, 우리가 공격 중에만 루트(부모)에 수동 적용한다.
        // 로코모션 클립은 In_Place(루트≈0)라 비공격 프레임의 delta는 무시한다.
        _animator.applyRootMotion = true;
        // ★상하체 완전 분리: 상체 콤보 오버라이드 레이어 인덱스 캐시(이름으로 — 레이어 재정렬에 견고). 없으면 -1.
        _comboLayer = _animator.GetLayerIndex("UpperBodyCombo");
        // 배선 실패를 무음 강등으로 두지 않는다(_motor/_aim 정책과 일관, Stab M-1) — 레이어 못 찾으면 상체 콤보 오버라이드가 조용히 죽는다.
        if (_comboLayer < 0)
            Debug.LogError("[PlayerAnimatorDriver] UpperBodyCombo 레이어 못 찾음 — 상체 콤보 오버라이드 비활성.", this);
        if (moveSource == null) moveSource = transform.parent;
        if (moveSource != null)
        {
            _aim = moveSource.GetComponentInParent<PlayerAim>();
            _motor = moveSource.GetComponentInParent<PlayerMotor>();
        }
        // 배선 실패를 무음으로 두지 않는다 — _motor 없으면 공격 루트모션이 조용히 죽으므로 즉시 노출(Codex).
        if (_motor == null)
            Debug.LogError("[PlayerAnimatorDriver] PlayerMotor를 부모 체인에서 못 찾음 — 공격 루트모션이 적용되지 않는다. 프리팹 계층(비주얼 자식이 PlayerMotor 하위인지) 확인.", this);
        // _aim도 대칭으로 노출(Stab 게이트) — 없으면 Hybrid facing이 조준을 못 써 transform.forward로 무음 폴백된다.
        if (_aim == null)
            Debug.LogError("[PlayerAnimatorDriver] PlayerAim을 부모 체인에서 못 찾음 — Hybrid facing이 조준 방향 대신 transform.forward로 폴백. 계층 확인.", this);
    }

    void OnEnable()
    {
        if (moveSource != null) _lastPos = moveSource.position;
    }

    // 비교용 하니스 HUD — 현재 facing 모드 표시(고른 뒤 OnGUI/enum/순환 전부 제거).
    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(14f, 10f, 760f, 40f), $"[{cycleFacingKey}] Facing: {facingMode}  (FaceMovement / FaceMouse / Hybrid 순환)", style);
    }

    /// <summary>PlayerBrain이 매 프레임 마지막에 호출.</summary>
    public void Tick()
    {
        if (moveSource == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 비교용 하니스: 플레이 중 facing 모드 순환(고른 뒤 떼어낼 임시 코드)
        if (Input.GetKeyDown(cycleFacingKey))
            facingMode = (FacingMode)(((int)facingMode + 1) % 3);

        // 평면(XZ) 변위만 — 지면 추종 y 변화는 로코모션과 무관.
        Vector3 cur = moveSource.position;
        Vector3 delta = cur - _lastPos; _lastPos = cur; delta.y = 0f;

        // 스폰/텔레포트로 루트가 순간이동하면 가짜 변위로 방향이 튄다 — 기준만 리셋하고 스킵.
        float rawSpeed = delta.magnitude / dt;
        if (rawSpeed > maxSpeed * 3f) return;
        float speed = Mathf.Min(rawSpeed, maxSpeed);
        bool moving = speed > moveThreshold;

        // facing 3안 비교(F키 순환). 공격 중엔 항상 단 시작 잠근 조준(_lockedFace). 비주얼(this)만 회전 — 루트는 안 돈다.
        Vector3 aimFace = (_aim != null && _aim.Direction.sqrMagnitude > 0.0001f) ? _aim.Direction : transform.forward;
        Vector3 moveFace = moving ? delta.normalized : transform.forward;
        Vector3 face;
        if (_attacking && _lockedFace.sqrMagnitude > 0.0001f)
            face = _lockedFace;
        else if (facingMode == FacingMode.FaceMovement)
            face = moveFace;                                                     // 이동 방향(하데스식)
        else if (facingMode == FacingMode.FaceMouse)
            face = aimFace;                                                      // 마우스 조준(트윈스틱)
        else
            face = (_motor != null && _motor.IsSprinting) ? moveFace : aimFace; // 하이브리드: 질주=이동, 그 외=조준
        face.y = 0f;
        if (face.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(face.normalized, Vector3.up);
            // ★몸 회전 스무딩 — 이동 facing은 turnRate로 스르륵 돈다(WASD 8방향이라도 각진 스냅 제거).
            //   공격 잠금(_attacking)·대시 중엔 즉시 스냅(런지/회피 방향 크리스프 유지). faceTurnRate 크게=옛 즉시동작 복원.
            bool snap = _attacking || (_motor != null && _motor.IsDashing);
            transform.rotation = snap
                ? targetRot
                : Quaternion.RotateTowards(transform.rotation, targetRot, faceTurnRate * dt);
        }

        // movement = facing 프레임 투영 → MoveX(우측 스트레이프) / MoveY(전진 +, 뒷걸음 −)
        Vector3 fwd = transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);   // 좌수계: up×fwd = right
        Vector3 moveDir = moving ? delta.normalized : Vector3.zero;
        float moveX = Vector3.Dot(moveDir, right);
        float moveY = Vector3.Dot(moveDir, fwd);

        // 8방향 스냅(딱딱) — 이동 방향을 가장 가까운 45도로 양자화해 블렌드트리에서 한 클립만 100% 활성한다.
        // 대각선 입력 시 F+R+FR 가중 블렌드로 애매해지던 것을 제거(이동 자체는 연속, 애니 방향만 8단계).
        if (moving)
        {
            float ang = Mathf.Atan2(moveX, moveY);                       // 이동 방향각(rad)
            ang = Mathf.Round(ang / (Mathf.PI / 4f)) * (Mathf.PI / 4f);  // 45도 단위 반올림
            moveX = Mathf.Sin(ang);
            moveY = Mathf.Cos(ang);
        }

        // speed 블렌드 0(idle)→1(walk)→2(run)
        float blend = speed <= walkSpeedRef
            ? (walkSpeedRef > 0f ? speed / walkSpeedRef : 0f)
            : 1f + (speed - walkSpeedRef) / Mathf.Max(0.01f, runSpeedRef - walkSpeedRef);
        blend = Mathf.Clamp(blend, 0f, 2f);

        _animator.SetFloat(SpeedHash, blend, speedDamp, dt);
        _animator.SetFloat(MoveXHash, moveX, dirDamp, dt);
        _animator.SetFloat(MoveYHash, moveY, dirDamp, dt);

        // ★상하체 완전 분리 + 위상 디싱크 확정 수정(07-04 Fix B) — 상체 콤보 레이어(UpperBodyCombo, UpperBody 마스크)의 웨이트를
        //   세 갈래로 구동해, 두 로코모션 클럭(상체 UB_Loco·하체 Base Locomotion)이 동시에 weight>0이 되는 상황을 아예 없앤다.
        //     ① Base가 상체 소유(액션 태그 or 대시)  → 0 즉시. Counter/Skill01*/DashAttack/Dash가 상체까지 보이고, 위상은 Base 소유라 정합.
        //     ② 콤보 진행(_comboActive)             → 1 즉시. 베기 상체가 크리스프하게 켜진다.
        //     ③ 그 외(콤보 직후 복귀 + 걷기 정상상태) → 0으로 이즈아웃(comboLayerBlendTime). 컨트롤러가 Combo 상태를 홀드(Combo→UB_Loco
        //        전이 제거)하므로 이 이즈아웃은 *정적* 콤보-끝-포즈를 Base 라이브 로코모션으로 블렌드한다. 정적 포즈=클럭 없음=위상 무관.
        //   왜 위상 정합인가: 걷기 정상상태에서 weight가 0에 도달하면 상체를 Base가 구동(다리와 동일 클럭) → 팔-다리 위상 자동 일치.
        //   구버전(v3)은 걷기 중 weight를 상수 1로 둬 UB_Loco가 Base와 독립 클럭 → 콤보 직후 팔-다리 위상이 영구 어긋났다(디싱크). 이게 그 수정.
        //   P2b(Base 액션 종료 후 스냅): 액션 중 weight=0이었고 종료 후에도 ③이 MoveTowards(0→0)라 0 유지 → 0→1 스냅 없음 = UB_Loco 드리프트 위상 팝 없음.
        //   ⚠️Base는 콤보 중 Locomotion 상태에 머문다(컨트롤러가 Combo 상태를 상체 레이어로 이관 — Base엔 ComboStep 전이 없음).
        _suppressStepIn = _comboActive && suppressComboStepIn;   // ★프레임-초 스냅샷(Stab H-1) — 콤보 전체 스텝인 억제. 애니 이벤트가 _comboActive 뒤집어도 이번 프레임 게이트 불변.
        if (_comboLayer >= 0)
        {
            // LayerHasActionTag(0)은 이미 IsActionPlaying(busy/이동잠금)이 쓰는 검증된 술어 — Counter/Skill01*/DashAttack 전부 "Action" 태그.
            bool baseOwnsUpperBody = LayerHasActionTag(0) || (_motor != null && _motor.IsDashing);
            if (baseOwnsUpperBody)      _comboWeight = 0f;                 // ① Base 전신 소유 → 즉시 0(위상 정합)
            else if (_comboActive)      _comboWeight = 1f;                 // ② 콤보 진행 → 즉시 1(크리스프)
            else                        _comboWeight = Mathf.MoveTowards(  // ③ 복귀/걷기 → 0으로 이즈아웃(정적 콤보-끝-포즈 → Base 로코모션)
                                            _comboWeight, 0f, dt / Mathf.Max(0.0001f, comboLayerBlendTime));
            _animator.SetLayerWeight(_comboLayer, _comboWeight);
        }

        // 대시: 시작 엣지에서 방향(어느 Step 클립)을 잠그고, bool 엣지로 상태 진입/복귀를 구동한다.
        // DashX/DashY = facing 프레임의 카디널 방향(우=+X, 전진=+Y) → 블렌드트리가 한 Step 클립을 100% 고른다.
        // 한 동작=한 클립: 대시 진행 중 이 값은 고정(매 프레임 갱신 안 함) — 모션 정체성 보존.
        if (_motor != null)
        {
            if (_motor.DashStartedThisFrame)
            {
                _animator.SetFloat(DashXHash, _motor.DashLocalX);
                _animator.SetFloat(DashYHash, _motor.DashLocalY);
                // ★대시=즉각 캐넌(Stab M-1): 대시 시작 프레임엔 위 웨이트 게이트가 이미 _motor.IsDashing=true를 읽어 상체 레이어를
                //   0으로 스냅한다(StartDash가 _dashTimer 세팅→IsDashing 즉시 true, 드라이버 Tick은 모터 뒤에 돈다). 별도 스냅 불필요.
            }
            bool dashAnim = _motor.IsDashing;
            if (dashAnim != _wasDashing) { _animator.SetBool(DashHash, dashAnim); _wasDashing = dashAnim; }
        }
        // ★달리기 무기 처리: 별도 코드 없음 — run 티어(Speed=2)는 walk 티어와 동일한 S2_Run 8way(무기 OUT) 세트를
        //   m_TimeScale 1.35로 더 빠르게 돌린다(스프린트=빠른 런 + 발슬라이드 완화). 무기 상태가 walk와 통일되어 임계서
        //   칼 깜빡임 없음. (구 Run_Stance3 단일 클립은 loop=0이라 스프린트 중 1회 후 freeze → 폐기.)
    }

    /// <summary>콤보 단 설정(0=idle, 1/2/3) — AnimatorController가 ComboStep으로 Combo 상태를 전환한다.
    /// 단 시작(step≥1) = 이 단의 facing 잠금 지점(이후 마우스를 돌려도 몸/런지 방향은 이 순간 조준에 고정).</summary>
    public void SetCombo(int step)
    {
        _comboActive = step >= 1;   // ★상하체 분리 게이트의 단일 소스 — 콤보만 하체 오버라이드/스텝인-억제 대상(반격/스킬/대시베기 제외)
        if (step >= 1)
        {
            if (_aim != null && _aim.Direction.sqrMagnitude > 0.0001f)
                _lockedFace = _aim.Direction;
        }
        else _lockedFace = Vector3.zero;   // 콤보 종료 — 잔존 잠금 제거(self-cancel 경로에서 옛 방향 재사용 방지)
        _animator.SetInteger(ComboStepHash, step);
        // ★콤보 1단 진입 = Attack 트리거로 ANY→Combo1 발화(컨트롤러 CanTransitionToSelf=1). Combo→UB_Loco 전이를 제거해
        //   콤보 종료 시 상체 레이어가 직전 Combo 상태의 마지막 프레임을 홀드하므로, 다음 콤보는 그 홀드된 상태에서 자기 자신으로
        //   재진입해야 한다(단발-후-단발이 흔한 경로). ComboStep==1 int 조건은 재생 내내 참이라 CanTransitionToSelf와 함께 쓰면
        //   매 프레임 재발화(프레임0 동결)하므로, 소비형 트리거를 쓴다(1회 발화 후 자동 소진). 1단에서만 발화 — 연계 2/3단은
        //   ComboStep==2/3 전이가 담당하고, BeginCombo가 SetCombo(1)을 콤보 시작에만 호출하므로 재생 중 재발화가 없다.
        if (step == 1) _animator.SetTrigger(AttackHash);
    }

    /// <summary>공격 커밋 여부 — true인 프레임에만 루트모션 변위를 루트로 적용(PlayerBrain이 busy로 갱신).</summary>
    public void SetAttacking(bool attacking) => _attacking = attacking;

    /// <summary>★레일: 지금 '액션' 클립(공격/반격)이 실제로 재생 중인가 — busy의 단일 진실 소스(애니가 진실).
    /// 컨트롤러에서 Combo1/2/3·Counter 상태에 "Action" 태그를 단다. 새 무기/액션은 상태에 같은 태그만 달면
    /// 코드 수정 없이 busy/잠금에 자동 편입(OCP 확장점). 전이 중이면 다음 상태도 봐서 진입 1프레임 갭을 메운다.
    /// ★REQUIRED: 모든 '공격/반격' 액션 상태에 Animator State Tag "Action" 필수. 누락 시 유예(0.12s) 뒤 busy가
    ///   풀려 이동 누수 — 단 KatanaWeapon의 진입 실패 자가치유가 에디터 경고로 잡아준다(런타임 안전망).</summary>
    public bool IsActionPlaying
    {
        get
        {
            if (_animator == null) return false;
            // Base 레이어(0)=Counter/Skill01*/DashAttack(전신 액션, 끝나면 Locomotion으로 전이 → 태그가 정확히 풀린다).
            if (LayerHasActionTag(0)) return true;
            // 상체 콤보 레이어=Combo1/2/3. ★Fix B로 Combo→UB_Loco 전이를 제거해 콤보 종료 후 Combo 상태를 마지막 프레임에
            //   홀드하므로, 태그만 보면 busy가 영구 true가 된다 → '실제 재생 중'일 때만 busy로 친다(홀드=끝=busy 아님).
            if (_comboLayer >= 0 && ComboLayerActivelyPlaying()) return true;
            return false;
        }
    }

    /// <summary>한 레이어의 현재(또는 전이 중 도착) 상태가 "Action" 태그인가 — 요청→진입 갭의 busy 누수 방지.</summary>
    bool LayerHasActionTag(int layer)
    {
        if (_animator.GetCurrentAnimatorStateInfo(layer).IsTag("Action")) return true;
        if (_animator.IsInTransition(layer) && _animator.GetNextAnimatorStateInfo(layer).IsTag("Action")) return true;
        return false;
    }

    /// <summary>★상체 콤보 레이어가 지금 '재생 중'인가 — 홀드된(끝난)·중단된(캔슬된) Combo는 제외(Fix B).
    /// Combo→UB_Loco 전이를 제거해 콤보 종료/캔슬 후 Combo 상태가 강제 종료 없이 남으므로, Action 태그만으로는
    /// busy가 영구/장시간 true가 된다. 구 설계는 Combo→UB_Loco 전이가 태그를 풀어 busy를 해제했고, 이 메서드가 그 역할을 대체한다.
    /// 두 겹으로 판정한다:
    ///  ① _comboActive 게이트(Stab P0) — 대시캔슬/자가캔슬이 스윙 도중 Cancel()→SetCombo(0)으로 _comboActive를 끈다.
    ///     이 게이트가 없으면 중단된 클립이 자연 종료(normalizedTime≥1)될 때까지 busy가 붙어 대시 직후 이동이 얼어붙는다
    ///     (대시캔슬=이 무기의 코어 캐넌 경로). _comboActive가 콤보 논리 종료(정상 종료·캔슬 공통)를 정확히 반영한다.
    ///  ② normalizedTime 안전망 — _comboActive가 (이벤트 누락 등으로) 고착돼도 Animator가 끝났으면(≥1) busy 해제 → KatanaWeapon 자가치유 발화.</summary>
    bool ComboLayerActivelyPlaying()
    {
        if (!_comboActive) return false;   // ① 논리 종료(정상/캔슬 공통) — 대시캔슬 busy-freeze 방지
        var cur = _animator.GetCurrentAnimatorStateInfo(_comboLayer);
        if (cur.IsTag("Action") && cur.normalizedTime < 1f) return true;   // ② 진행 중(마지막 프레임 홀드 전) — 고착 시 안전망
        if (_animator.IsInTransition(_comboLayer) && _animator.GetNextAnimatorStateInfo(_comboLayer).IsTag("Action")) return true;  // 진입 전이 중
        return false;
    }

    /// <summary>★액션 진입(반격/스킬/대시베기 공통) — 데이터 주도 트리거(2026-07-05 슬롯화). 트리거 이름은
    /// <see cref="WeaponActionSet"/>.animator(SO)가 소유하고 해시는 Resolve 캐시 — 구 TriggerCounter/TriggerSkill/
    /// TriggerDashAttack의 일반화(동작 보존). 액션은 공격이라 IsBusy(=_attacking)로 루트모션이 적용된다(별도 게이트 불필요).
    /// 몸 facing을 현재 조준에 잠가 히트박스 _aimDir과 통일(콤보 SetCombo의 잠금과 동형). 종료 시 SetCombo(0)이 잠금 해제.
    /// ★AnyState 경쟁 해소: 직전 대시의 Dash bool이 남아 있으면 Any→Dash(우선순위 0)가 액션 전이를 이긴다 —
    /// 요청 시 Dash bool을 즉시 꺼 확실히 진입(엣지 추적도 동기화). hash 0(이름 미설정)이면 무동작(안전).</summary>
    public void TriggerAction(int triggerHash)
    {
        if (_animator == null || triggerHash == 0) return;
        if (_aim != null && _aim.Direction.sqrMagnitude > 0.0001f)
            _lockedFace = _aim.Direction;
        _animator.SetBool(DashHash, false);
        _wasDashing = false;
        _animator.SetTrigger(triggerHash);
    }

    /// <summary>★차징 윈드업(RMB 누름) — 구 TriggerSkillCharge의 일반화. 윈드업(프레임 0→70)을 재생한 뒤 프레임 70
    /// (차징완료 포즈)에서 홀드한다. 발동(베기)은 릴리스 때 <see cref="TriggerAction"/>이 별도로 낸다(윈드업 재재생 없음).
    /// facing 잠금 + Dash bool 정리(AnyState 경쟁 해소)는 TriggerAction과 동형.
    /// cancelHash 잔류 가드: 대시-취소가 쏜 취소 트리거가 AnyState→Dash에 밀려 미소비로 남으면 새 차징을 즉시
    /// 취소시킴 → 새 차징 전 클리어. chargeHash 0이면 무동작(안전).</summary>
    public void TriggerCharge(int chargeHash, int cancelHash)
    {
        if (_animator == null || chargeHash == 0) return;
        if (_aim != null && _aim.Direction.sqrMagnitude > 0.0001f)
            _lockedFace = _aim.Direction;
        _animator.SetBool(DashHash, false);
        _wasDashing = false;
        if (cancelHash != 0) _animator.ResetTrigger(cancelHash);
        _animator.SetTrigger(chargeHash);
    }

    /// <summary>★차징 취소(최소차징 전 릴리스 = 불발 / 하드컷 복귀) — 구 TriggerSkillCancel의 일반화.
    /// 컨트롤러 Charge/Hold→Locomotion 트리거로 윈드업/홀드만 idle로 되돌린다(베기 미발동). 차징 시작이 윈드업
    /// 트리거를 쐈는데 불발 시 홀드에 고착되는 소프트락을 막는다. hash 0이면 무동작(안전).</summary>
    public void TriggerActionCancel(int cancelHash)
    {
        if (_animator == null || cancelHash == 0) return;
        _animator.SetTrigger(cancelHash);
    }

    /// <summary>★차징 윈드업(Skill01Charge) 재생 중인가 — ChargePhantomEmitter가 *윈드업에만* 팬텀 방출하도록 읽음(홀드/베기 제외).
    /// Base Layer(0) 현재 상태명으로 판정. 액션 진입 전이가 전부 CUT(0)이라 윈드업 0→70 동안만 true, 프레임70 홀드 진입 시 false.</summary>
    public bool IsInSkillChargeWindup => _animator != null && _animator.GetCurrentAnimatorStateInfo(0).IsName("Skill01Charge");

    /// <summary>공격·대시 클립의 루트모션을 루트(PlayerMotor)로 넘긴다 — 애니가 진실(전진/회피 거리는 클립이 소유).
    /// 공격 커밋 중이거나 대시 창 동안만 적용한다. 그 외(In_Place 로코모션, delta≈0)는 무시해
    /// PlayerMotor 이동과 충돌하지 않는다. 자동 루트모션은 비주얼 자식만 옮기므로 쓰지 않고, 이 수동 경로로
    /// 부모 루트를 옮긴다. 위치 단일 소유는 PlayerMotor.ApplyRootStep(같은 프레임 이중 적용 가드 내장).</summary>
    void OnAnimatorMove()
    {
        if (_motor == null) return;
        if (!_attacking && !_motor.IsDashing) return;   // 공격 커밋 중이거나 대시 창 동안만 루트모션을 위치로 적용
        // ★차징 윈드업/게이더링 홀드는 제자리 — 루트모션 억제. 이유 둘: ①게이더링 홀드(Skill01Hold)는 매 루프
        //   사이클마다 클립을 전진 재생해 루트모션이 누적(실측 ~0.26m/s, 홀드 3s=0.78m 슬라이드)되므로 차단해야
        //   "기 모으며 제자리"가 된다. ②윈드업(Skill01Charge) 제자리화로 팬텀 출발점이 고정된다.
        //   클립은 윈드업/홀드/베기 공유라 bake로 분리 불가 → 상태별 억제(코드가 위치를 *만들지* 않고 클립 변위를 *억제만* — 헌법 부합).
        //   베기(Skill01Strike) 런지는 정상 적용(아래 경로). 이름 판정은 기존 IsInSkillChargeWindup과 동형 결속.
        var st = _animator.GetCurrentAnimatorStateInfo(0);
        if (st.IsName("Skill01Charge") || st.IsName("Skill01Hold")) return;
        // ★상하체 완전 분리(07-04 v2): 콤보 스텝인(전진 루트모션) 억제 — 완전 분리에선 정지 콤보 다리가 idle 스탠스라
        //   전진 루트모션이 발을 미끄러뜨린다(브리프 §2). 그래서 콤보 전체 스텝인 억제(_suppressStepIn = _comboActive && suppressComboStepIn).
        //   ★단 현 아키텍처(콤보=UpperBody 마스크 레이어, Root 제외)에선 콤보가 애초에 delta에 전진을 싣지 않아 억제 여부와 무관하게 delta≈0.
        //   반격/스킬/대시베기(_comboActive=false)는 Base 전신(Root 포함)이라 그 커밋 런지가 정상 적용된다.
        //   ★스냅샷 판독(Stab H-1) — 라이브 _comboActive를 읽으면 종료 프레임 애니 이벤트(OnComboEnd)가 게이트를 풀어
        //   마지막 프레임 루트 delta가 새어 나간다(ClearActionMove와 거울상 레이스).
        if (_suppressStepIn) return;
        _motor.ApplyRootStep(_animator.deltaPosition);
    }

    // ── AnimationEvent 수신(함수명 고정 — Animation 에이전트가 이 이름으로 클립에 심는다) ──
    /// <summary>타격 정점 — 무기 판정으로 릴레이.</summary>
    public void OnAttackHit(int hitFrameIndex) => AttackHit?.Invoke(hitFrameIndex);
    /// <summary>캔슬 윈도우 시작 — 다음 콤보 단 입력이 먹히기 시작.</summary>
    public void OnComboWindow() => ComboWindow?.Invoke();
    /// <summary>공격 클립 끝 — 다음 단 안 갔으면 콤보 종료(idle 복귀).</summary>
    public void OnComboEnd() => ComboEnd?.Invoke();
    /// <summary>로코모션 클립 발 디딤 프레임 — 발소리로 릴레이(디바운스는 구독자가).</summary>
    public void OnFootstep() => Footstep?.Invoke();
}
