using UnityEngine;

/// <summary>
/// 플레이어 스택의 단일 오케스트레이터. 매 프레임 입력을 수집해 하위 컴포넌트를
/// Aim→Motor→Weapon→Animator 순서로 명시 호출한다(Script Execution Order 암묵 의존 제거 — codex 권고).
/// 조준을 이동보다 먼저 확정해, 정지 대시 방향 폴백·공격 방향이 같은 프레임의 최신 aim을 본다.
///
/// 무기는 런 시작 시 1택으로 자식에 주입된다(현재는 씬 배치 무기를 GetComponentInChildren로 집음 —
/// 런 매니저 연결은 추후). 무기/애니 드라이버는 없어도(null) 이동·조준은 독립 동작한다.
/// </summary>
[RequireComponent(typeof(PlayerMotor), typeof(PlayerAim))]
public class PlayerBrain : MonoBehaviour
{
    [SerializeField] KeyCode dashKey = KeyCode.Space;

    PlayerMotor _motor;
    PlayerAim _aim;
    WeaponBehaviour _weapon;
    PlayerAnimatorDriver _animator;
    PlayerFootsteps _footsteps;
    PlayerHealth _health;

    void Awake()
    {
        _motor = GetComponent<PlayerMotor>();
        _aim = GetComponent<PlayerAim>();
        _animator = GetComponentInChildren<PlayerAnimatorDriver>();
        _weapon = GetComponentInChildren<WeaponBehaviour>();
        _footsteps = GetComponentInChildren<PlayerFootsteps>();   // 발소리(자족 — 없어도 무해)
        _health = GetComponentInChildren<PlayerHealth>();
        _weapon?.Initialize(transform, _animator);
        // ★[비활성화 2026-06-20] 패링→Tumbling 애니 전환 — 복잡도 대비 효율 낮아 주석처리. 회피=Step 대시 유지.
        // if (_health != null && _animator != null) _health.Parried += _animator.TriggerTumbling;
        // ★패링 성공 → 무기 반격 창 오픈(카타나=Skill02 카운터). 창 안에 좌클릭하면 반격 발동.
        if (_health != null && _weapon != null) _health.Parried += _weapon.ArmCounter;
    }

    void OnDestroy()
    {
        _weapon?.Cleanup();
        // [비활성화] if (_health != null && _animator != null) _health.Parried -= _animator.TriggerTumbling;
        if (_health != null && _weapon != null) _health.Parried -= _weapon.ArmCounter;
    }

    void Update()
    {
        var input = ReadInput();
        _aim.Tick(input);                          // 1) 조준 방향 먼저 확정
        // ★회피 최우선: 대시 입력 + 충전 있으면 진행 중 공격을 즉시 캔슬하고, 같은 프레임 공격 입력도 무효화한다
        //   (공격/이동 도중에도 바로 회피, dash+좌클릭 동시 입력 시 회피가 이김 — self-cancel 캐넌, Stab 권고-1).
        if (input.dashDown && _motor.CanDash)
        {
            _weapon?.Cancel();
            input.primaryDown = false;             // 같은 프레임 좌클릭이 콤보를 재시작해 대시를 씹는 것 방지
        }
        _weapon?.Tick(input, _aim.Direction);      // 2) 공격 — 상태(IsBusy)를 먼저 확정(캔슬됐으면 idle)
        bool busy = _weapon != null && _weapon.IsBusy;
        _motor.Tick(input, _aim.Direction, busy);  // 3) 이동·대시 — 공격 커밋 중(busy)이면 Motor 입력이동 양보
        _animator?.SetAttacking(busy);             //    공격 중엔 루트모션이 위치를 주도(OnAnimatorMove)
        _animator?.Tick();                         // 4) 상태 → 애니 파라미터 반영
        _footsteps?.Tick();                        // 5) 발소리 — 확정된 위치(이동·루트모션) 이후 거리 적산
    }

    /// <summary>레거시 Input에서만 읽는 유일 지점. New Input System 전환 시 이 메서드만 교체(codex 권고:
    /// 입력 격리 — Motor/Aim/Weapon은 PlayerInputState만 보므로 무수정).</summary>
    PlayerInputState ReadInput() => new PlayerInputState
    {
        move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
        aimScreen = Input.mousePosition,
        dashDown = Input.GetKeyDown(dashKey),
        primaryDown = Input.GetMouseButtonDown(0),
        primaryHeld = Input.GetMouseButton(0),
        primaryUp = Input.GetMouseButtonUp(0),
        secondaryDown = Input.GetMouseButtonDown(1),
        secondaryHeld = Input.GetMouseButton(1),
        secondaryUp = Input.GetMouseButtonUp(1),
    };
}
