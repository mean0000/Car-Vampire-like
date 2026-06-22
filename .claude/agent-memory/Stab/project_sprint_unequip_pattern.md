---
name: sprint-unequip-pattern
description: 달리기 납도(Unequip layer1)+이동지향 facing QA(2026-06-21 재설계 v2): 이전 H-1/H-2/H-3 모두 Closed. 신규=activeSelf가드역전(High)·Layer1루트모션섞임(M)·OnEnable미리셋(M)
metadata:
  type: project
---

## 변경 이력

### v1 (2026-06-21 최초 구현)
PlayerInputState.sprintHeld, PlayerBrain.sprintKey, PlayerMotor.sprintSpeed=9f,
PlayerAnimatorDriver 납도 연출(HideWeapon/ShowWeapon 이벤트 기반).

**H-1/H-2/H-3 미수정 상태로 게이트 통과 실패.**

### v2 (2026-06-21 재설계)
HideWeapon/ShowWeapon 이벤트 기반 경로 제거 → 매 Tick 말미 상태식으로 단일화.

```csharp
bool hide = sprinting && _sheathed;
if (weaponMesh != null && weaponMesh.gameObject.activeSelf == hide)
    weaponMesh.gameObject.SetActive(!hide);
```

---

## 이전 결함 해소 확인

### H-1 영구 숨김 → Closed
`sprinting=false` 순간 `hide = false && _sheathed = false`로 강제.
이벤트(`OnWeaponSheathed`) 늦게 발화돼도 그 시점 `sprinting=false` → `hide=false`. 경쟁 구조 없음.

### H-2 트리거 스팸 → Closed
`IsSprinting` = 입력 기반(`sprintHeld && move.sqrMagnitude > 0.0001f`), 속도 임계 오실레이션 없음.
`if (sprinting && !_wasSprinting)` 엣지 + `ResetTrigger+SetTrigger` 쌍으로 1회만 발화.

### H-3 달리기 후 즉시 공격 칼 없음 → Closed
공격 시작 프레임: `Weapon.Tick` → `IsBusy=true` → `Motor.Tick(locked=true)` → `_sprinting=false` → `Animator.Tick` → `sprinting=false` → `hide=false` → 칼 보임.
Brain 호출 순서(`Weapon→Motor→Animator`)가 동일 프레임 내에서 정합을 보장.

---

## 신규 발견 (v2 리뷰)

### H-1 (High) — activeSelf 가드 역전: 동작은 맞으나 유지보수 혼동 위험
```csharp
// 현재: activeSelf==hide 일 때 SetActive(!hide) — 직관에 반함
if (weaponMesh != null && weaponMesh.gameObject.activeSelf == hide)
    weaponMesh.gameObject.SetActive(!hide);
// 권장: 의도 직접 표현
bool shouldBeVisible = !hide;
if (weaponMesh != null && weaponMesh.gameObject.activeSelf != shouldBeVisible)
    weaponMesh.gameObject.SetActive(shouldBeVisible);
```
수정자가 `==hide`를 `!=hide`로 혼동하거나 SetActive 인수를 잘못 읽으면 즉시 반전 버그.
현재 동작은 정확하다 — 리팩터 권장, 긴급 아님.

### M-1 (Medium) — OnAnimatorMove에서 Layer1 루트모션 섞임 가능성
공격 시작 프레임에 Unequip 클립이 Layer1에서 아직 재생 중이고 weight=1이면
`_animator.deltaPosition`에 Layer1 루트모션이 포함됨.
Unequip 클립이 상체 In_Place(XZ 변위=0)이면 무해. Animation 에이전트 확인 필요.

### M-2 (Medium) — OnEnable 플래그 미리셋
`_wasSprinting`, `_sheathed`, `_unequipEntered`가 OnEnable에서 리셋 안 됨.
첫 Tick의 `if(!sprinting)` 리셋이 실질 방어하나 명시적 리셋 권장:
```csharp
void OnEnable() {
    if (moveSource != null) _lastPos = moveSource.position;
    _wasSprinting = false; _sheathed = false; _unequipEntered = false;
}
```

### L-1 (Low) — Shift 연타 Unequip 반복 재시작 (flicker)
`!_wasSprinting` 엣지가 입력 기반이라 Shift 연타 시 매번 트리거.
stuck-state 없음(매번 `_sheathed=false` 리셋), 시각적 flicker만. 실용 플레이에서 드묾.

---

## 안전 확인 (v2)
- 대시 중 `IsSprinting=false` 보장: Motor `_dashTimer>0` early-return 전 `_sprinting=false` 리셋.
- 공격 중 `IsSprinting=false`: `locked=true` → Motor early-return 경로에서 `_sprinting` 설정 라인 미도달.
- weaponMesh null 가드 완전.
- Layer1 weight 매 프레임 `inUnequip?1:0` → T포즈 freeze 없음.
- `IsActionPlaying` = Layer0 "Action" 태그만 → Layer1 영향 0.
- 컴파일: `OnWeaponSheathed()` 시그니처 정합, StringToHash 충돌 없음.

## Critical 0 / High 1(유지보수) / Medium 2 / Low 1
