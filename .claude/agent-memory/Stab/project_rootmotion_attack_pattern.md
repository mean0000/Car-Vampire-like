---
name: rootmotion-attack-pattern
description: 공격 루트모션(ApplyRootStep) QA — 대시 종료 프레임 이중 이동 H-2 + 조준 방향 추종 H-1 + 재진입 가드 통과 확인
metadata:
  type: project
---

## 2026-06-19 공격 루트모션 적용 QA (3파일: PlayerMotor/PlayerAnimatorDriver/PlayerBrain)

### 아키텍처 확정 사항 (검증됨)
- 위치 소유 = PlayerMotor 단일. 공격 중 Motor.Tick은 locked(L85) 조기리턴으로 위치 양보.
- applyRootMotion=true + OnAnimatorMove 수동 경로 — 자동적용(자식만 이동) 차단 후 부모 루트에 수동 적용.
- _attacking 플래그: PlayerBrain.Update()에서 SetAttacking 후 Animator 페이즈(OnAnimatorMove)에서 읽음 — 동일 프레임 일관성 보장됨.
- Initialize 재진입 가드: WeaponBehaviour + KatanaWeapon 양쪽 선해제 패턴 정상 구현 (Stab H-1/H-2 반영됨).

### H-1: 공격 중 런지 방향이 매 프레임 조준 추종 (미확정 설계 판단)
- 원인: _animator.Tick()에서 비주얼 자식 rotation을 현재 조준으로 갱신 → Animator 페이즈 deltaPosition이 그 rotation 기준으로 계산됨.
- 결과: 공격 모션 중 마우스 회전 → 런지 방향 꺾임. 히트박스(_aimDir = AnimationEvent 시점)와 런지 방향 불일치 가능.
- Fix A (방향 잠금): 공격 시작 프레임에 _lockedFacing 캡처, 공격 중 비주얼 rotation 고정.
- Fix B (히트박스 동기): 방향 추종이 의도라면 DoSwingHit도 최신 _aimDir 쓰도록 통일.
- 유저 판정 필요.

### H-2: 대시 종료 프레임 이중 transform.position 쓰기 (버그)
- 원인: UpdateDash에서 _dashTimer -= dt → ≤0이면 position 이동 후 return. 동일 프레임에 ApplyRootStep 가드(_dashTimer>0)가 이미 0이 된 _dashTimer를 보고 통과 → 두 번째 이동.
- 재현: 대시 진행 중 좌클릭(BeginCombo) → 대시 종료 프레임에 (대시 이동 + 런지 첫 프레임) 합산.
- Fix: `_dashEndedThisFrame` bool 플래그 — Motor.Tick 진입 시 false 리셋, UpdateDash에서 종료 감지 시 true, ApplyRootStep에서 `|| _dashEndedThisFrame` 추가.

### 검증됨 (오진 없음)
- 공격+대시 동시: Motor.Tick L85(locked)이 대시 시작 코드(L90) 도달 전 return → 공격 중 대시 불가. 안전.
- 종료 프레임 deltaPosition 누출: _attacking=false 후 OnAnimatorMove return → 안전.
- null/생명주기: Awake 완료 후 Update 루프 시작이므로 _motor null 없음. _attacking 가드 2중.
- _velocity=0 (ApplyRootStep): 대시→공격 전환 시 관성 소멸은 Motor.Tick L85 locked와 동일 처리로 일관성 있음. 손맛 판단은 라이브 확인.

### 반복 패턴 기록
- "종료 프레임 타이밍" 버그: 상태 감소(timer -= dt)와 그 결과 0이 된 시점을 같은 프레임에 다른 경로가 읽는 패턴 → UpdateDash처럼 타이머 감소와 가드 체크가 분리된 곳은 항상 "종료 프레임에 두 경로 동시 활성화"를 점검.
- applyRootMotion=true + OnAnimatorMove 수동 경로: deltaPosition 방향 기준 = Animator 오브젝트의 현재 rotation (Update에서 갱신된 이후). 방향 잠금이 필요하면 명시적으로 캐시해야 함.

**Critical 0 / High 2 (H-2 버그, H-1 설계 미확정)**
