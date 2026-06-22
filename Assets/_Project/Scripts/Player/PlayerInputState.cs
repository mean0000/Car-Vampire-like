using UnityEngine;

/// <summary>
/// 한 프레임의 플레이어 입력 스냅샷. Unity Input API는 <see cref="PlayerBrain"/>.ReadInput()에서만
/// 읽고, 모든 로직(Motor/Aim/Weapon)은 이 값만 본다 — 입력 소스 격리.
/// 레거시 Input ↔ New Input System 전환 시 Brain 한 곳만 교체하면 되고, Motor/Aim/Weapon은 무수정.
/// </summary>
public struct PlayerInputState
{
    public Vector2 move;        // 이동 입력(x=좌우 A/D, y=전후 W/S, 정규화 전 −1..1)
    public Vector2 aimScreen;   // 조준 스크린 좌표(마우스). 게임패드는 우스틱→가상 커서로 채움(추후)

    public bool dashDown;       // 대시 눌림 엣지
    public bool dashAttack;     // ★대시 베기 — PlayerBrain이 '대시 중 좌클릭'을 대시 끝 프레임에 이 플래그로 주입(파생 입력)
    public bool sprintHeld;     // ★달리기(Shift) 홀드 — 누르고 있는 동안 스프린트 속도

    public bool primaryDown;    // 주공격(LMB) 눌림 엣지
    public bool primaryHeld;    // 주공격 홀드
    public bool primaryUp;      // 주공격 뗌 엣지

    public bool secondaryDown;  // 보조(RMB) 눌림 엣지
    public bool secondaryHeld;  // 보조 홀드
    public bool secondaryUp;    // 보조 뗌 엣지(거합 발도 릴리스 등 — 홀드 후 놓는 순간이 입력)
}
