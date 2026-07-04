using UnityEngine;

/// <summary>
/// 플레이어 스택의 단일 오케스트레이터. 매 프레임 입력을 수집해 하위 컴포넌트를
/// Aim→Weapon→Motor→Animator 순서로 명시 호출한다(Script Execution Order 암묵 의존 제거 — codex 권고).
/// (조준을 먼저 확정 → 무기가 IsBusy를 정해야 Motor가 그 busy로 이동을 양보하므로 Weapon이 Motor보다 먼저.)
/// 조준을 이동보다 먼저 확정해, 정지 대시 방향 폴백·공격 방향이 같은 프레임의 최신 aim을 본다.
///
/// 무기는 런 시작 시 1택으로 자식에 주입된다(현재는 씬 배치 무기를 GetComponentInChildren로 집음 —
/// 런 매니저 연결은 추후). 무기/애니 드라이버는 없어도(null) 이동·조준은 독립 동작한다.
/// </summary>
[RequireComponent(typeof(PlayerMotor), typeof(PlayerAim))]
public class PlayerBrain : MonoBehaviour
{
    [SerializeField] KeyCode dashKey = KeyCode.Space;
    [SerializeField] KeyCode sprintKey = KeyCode.LeftShift;   // ★달리기 — 누르고 있는 동안 스프린트

    bool _bufferedAttack;   // 대시 중 좌클릭 기억 → 대시 끝 첫 프레임에 대시 베기로 주입(만료 없음 — 대시는 짧고 재대시 시 폐기)

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
        // ★패링 성공 → 무기 반격 창 오픈(카타나=Skill02 카운터). 창 안에 좌클릭하면 반격 발동.
        if (_health != null && _weapon != null) _health.Parried += _weapon.ArmCounter;
    }

    void OnDestroy()
    {
        _weapon?.Cleanup();
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
            // ★대시와 같은 프레임 좌클릭(코드)도 대시 베기로 예약 — 이 프레임엔 Motor가 아직 안 틱해 IsDashing=false라
            //   아래 버퍼 분기가 못 잡는다(Codex P2-a). 여기서 직접 적립(새 대시라 이전 버퍼는 이번 입력으로 갱신·폐기).
            _bufferedAttack = input.primaryDown;
            input.primaryDown = false;             // 같은 프레임 좌클릭이 콤보를 재시작해 대시를 씹는 것 방지(대시 베기로 보존됨)
            input.secondaryDown = false;           // 같은 프레임 우클릭(스킬)도 무효 — 회피가 이김
        }
        // ★대시 커밋 보호 + 입력 버퍼: 대시 진행 중 좌클릭은 버리지 말고 기억 → 대시 끝나는 즉시 재주입.
        //   대시는 커밋(공격이 못 끊음)이지만 입력은 보존해 "눌렀는데 안 나감"을 없앤다 = 회피→공격 흐름의 핵심.
        //   만료 타이머 없음: 대시는 짧고(끝나면 즉시 재주입), 재대시 시 폐기되므로 영구 잔존 불가(슬로모 중 만료 엣지도 제거 — Codex L).
        if (_motor.IsDashing)
        {
            if (input.primaryDown) _bufferedAttack = true;   // ★대시 중 좌클릭 = 대시 베기 예약(대시 끝에 발동)
            input.primaryDown = false;             // 대시 중엔 콤보 시작 보류(대시 끝나고 대시 베기로 주입)
            input.secondaryDown = false;           // 대시 중엔 스킬 발동 보류(대시 커밋 — 끝난 뒤 RMB로)
        }
        else if (_bufferedAttack)
        {
            input.dashAttack = true;               // ★대시 끝난 첫 프레임 — 버퍼된 입력을 대시 베기로 주입(일반 콤보 아님)
            input.primaryDown = false;             // 같은 프레임 우연한 새 좌클릭이 Combo1을 먼저 시작해 대시 베기를 씹는 것 방지(Codex P2-b — 버퍼 우선)
            _bufferedAttack = false;
        }

        _weapon?.Tick(input, _aim.Direction);      // 2) 공격 — 상태(IsBusy)를 먼저 확정(캔슬됐으면 idle)
        bool busy = _weapon != null && _weapon.IsBusy;
        _motor.Tick(input, _aim.Direction, busy,   // 3) 이동·대시 — 공격 커밋 중(busy)이면 Motor 입력이동 양보
                    _weapon != null ? _weapon.ActionMoveMult : 0f);   //    ★무빙 평타(07-04): 무기가 허용한 배율만큼은 걷기 유지(콤보만, 무기 소유 OCP)
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
        sprintHeld = Input.GetKey(sprintKey),
        primaryDown = Input.GetMouseButtonDown(0),
        primaryHeld = Input.GetMouseButton(0),
        primaryUp = Input.GetMouseButtonUp(0),
        secondaryDown = Input.GetMouseButtonDown(1),
        secondaryHeld = Input.GetMouseButton(1),
        secondaryUp = Input.GetMouseButtonUp(1),
    };
}
