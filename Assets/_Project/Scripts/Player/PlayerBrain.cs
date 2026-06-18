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

    void Awake()
    {
        _motor = GetComponent<PlayerMotor>();
        _aim = GetComponent<PlayerAim>();
        _animator = GetComponentInChildren<PlayerAnimatorDriver>();
        _weapon = GetComponentInChildren<WeaponBehaviour>();
        _weapon?.Initialize(transform, _animator);
    }

    void OnDestroy() => _weapon?.Cleanup();

    void Update()
    {
        var input = ReadInput();
        _aim.Tick(input);                          // 1) 조준 방향 먼저 확정
        _weapon?.Tick(input, _aim.Direction);      // 2) 공격 — 상태(IsBusy)를 먼저 확정
        bool busy = _weapon != null && _weapon.IsBusy;
        _motor.Tick(input, _aim.Direction, busy);  // 3) 이동·대시 — 공격 커밋 중(busy)이면 잠금(제자리 공격)
        _animator?.Tick();                         // 4) 상태 → 애니 파라미터 반영
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
