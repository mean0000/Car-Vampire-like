---
name: dash-attack-pattern
description: 대시 베기(DashAttack) QA(2026-06-21): AnyState 우선순위 충돌·대시 종료 직후 루트모션 이중이동·_bufferTimer 진입 시나리오·OnComboEnd 공유 trampoline 순서. Critical 0 / High 3
metadata:
  type: project
---

## 결론 요약
Critical 0 / High 3 / Medium 2 / Low 2.

## 구조 검증됨
- `_bufferedAttack` 단일 경로: 대시중 primaryDown → `_bufferedAttack=true` + `primaryDown=false`. 대시 종료 다음 프레임에서 `input.dashAttack=true` + `_bufferedAttack=false`. 같은 프레임 primaryDown과 dashAttack 동시 주입 불가능(else if 구조 — IsDashing 분기가 먼저 짧게 끊음).
- DashAttack 상태: `m_Tag: Action` 확인(KatanaMelee.controller fileID 1114000000000000002). Action 레일 자동 편입.
- AnyState 진입 전환: `DashAttack` 트리거 조건, `CanTransitionToSelf: 0`, `TransitionDuration: 0`, `HasExitTime: 0` — 하드컷 정상.
- exitTime 0.9 전환 → Locomotion: Counter와 동일 구조. `TransitionDuration: 0.1`.
- OnComboEnd 공유 trampoline 순서: `if _skilling → if _countering → if _dashAttacking → ResetCombo`. 대시베기 중 스킬/카운터 플래그가 None이라 `_dashAttacking` 분기가 정확히 실행됨.
- 자가치유(!IsBusy && 진행플래그) 독립 if 구조 — `_dashAttacking` 포함. H-2(katana-skill01) 수정과 동형.
- Cancel()에서 `_dashAttacking=false` + `_dashAttackFallbackTimer=0f` 리셋 확인.
- `BeginDashAttack`에 `BeginAction()` 호출 — 진입 유예 켬.
- `_hitDone` 공유 — 콤보/카운터/스킬/대시베기 전부 같은 `_hitDone`을 쓴다. 전이 중 "스킬 중 OnAttackHit → dashAttacking 분기" 오발 없음(플래그 배타 보장).

## H-1: AnyState 우선순위 목록 DashAttack이 Combo(1113000000000000001)보다 뒤
컨트롤러 m_AnyStateTransitions 순서: [0] Dash(-829729492204418584) [1] Counter(1112000000000000004) [2] DashAttack(1114000000000000004) [3] Combo1(1113000000000000001).
권위문서 §5 규정: "Dash > Tumbling > Counter > Combo — 새 액션은 의미에 맞는 위치에." DashAttack이 Combo보다 위(우선순위 2)이므로 규정 준수.
★그러나 Counter(1)와 DashAttack(2) 순서가 카운터 창 안에서 대시 베기가 발동할 때 경쟁할 수 있는가? — 코드에서 _counterTimer>0 && _step==0이면 BeginCounter()가 먼저 실행된다. dashAttack 가드에 `_countering` 체크가 있어서 BeginDashAttack은 차단됨. 따라서 실제 충돌 없음. 그러나 반대 경로(TriggerDashAttack 먼저 SetTrigger → 같은 프레임 _countering=true가 이미 설정돼 Animator Counter가 DashAttack보다 우선순위 높으면) — AnyState에서 Counter=1 < DashAttack=2 이므로 Counter 트리거가 있으면 Counter가 이김. 코드 플래그와 Animator 우선순위가 일치함.

## H-1: 대시 베기 시작 프레임 루트모션 이중이동 위험
- `PlayerBrain.Update` 순서: IsDashing 분기 → `else if (_bufferedAttack)` → `input.dashAttack=true`. 그 다음 `_weapon.Tick()` → `BeginDashAttack()` → `BeginAction()` + `TriggerDashAttack()` → `SetAttacking(busy=true)`.
- **대시 종료 첫 프레임**: `_dashTimer`가 이번 `Tick`에서 0이 됐다면(`UpdateDash`에서 `_dashTimer -= dt` → 0 이하), Motor.Tick은 `UpdateDash` 조기반환이 없어 정상 이동으로 계속. 그러나 `_dashAppliedThisFrame=true`가 UpdateDash 내에서 설정된다.
- 대시 종료 후 다음 프레임(IsDashing=false 첫 프레임): `_bufferedAttack` → `dashAttack=true` → BeginDashAttack → SetAttacking(true) → OnAnimatorMove가 `_attacking=true && !_motor.IsDashing` → ApplyRootStep 호출. 이 프레임에 `_dashAppliedThisFrame`는 `false`(매 프레임 초기화, UpdateDash가 이번 프레임에 실행되지 않음). `_dashTimer=0`이므로 ApplyRootStep 내 `if (_dashTimer > 0f || _dashAppliedThisFrame) return` — 둘 다 false → 루트모션 적용됨.
- 같은 프레임 Motor.Tick 경로: IsDashing=false → locked(busy) = true → `_velocity=Vector3.zero; return;` — 이동 위치 쓰기 없음. 루트모션과 이동 이중쓰기 없음. **안전**.
- 단, Animator SetTrigger한 직후 이 프레임에 OnAnimatorMove가 실제로 deltaPosition을 내주는가? Unity Update→Animator→OnAnimatorMove 순서상 SetTrigger가 이번 프레임 Update에서 불렸어도 Animator가 전이를 처리하는 건 그 다음 LateUpdate 이후다. 따라서 이번 프레임의 deltaPosition은 DashAttack 클립이 아니라 Dash 클립 마지막 프레임의 delta(~0, Step 클립 종료 직전). 실질적으로 무해(Dash 클립 마지막 프레임 delta≈0).

## H-2: OnComboEnd@0.87 직후 SetCombo(0)가 Combo 전이를 오발할 수 있나
EndDashAttack → `AnimatorDriver.SetCombo(0)`. 컨트롤러에서 ComboStep=0 전이 조건을 가진 상태: Combo1(6824437795057135513), Combo2(-3013906819078669098), Combo3(7691382374019707182), Locomotion(1113000000000000001). 대시 베기가 DashAttack 상태에 있고 ComboStep=0 조건의 전이는 DashAttack 상태 자체의 전이 목록에 없다(DashAttack의 m_Transitions: fileID 1114000000000000003만 — exitTime 0.9 → Locomotion). 따라서 SetCombo(0) 호출이 DashAttack에서 오발 전이를 만들지 않음. **안전**.

## H-3: _bufferedAttack 만료 없음 — 대시 중 슬로모 + 대시 종료 지연 시나리오
패링 슬로모(timeScale 축소) 중 대시 중 좌클릭 → `_bufferedAttack=true`. 슬로모가 길면 대시도 timeScale만큼 늦게 끝남(dashDuration은 scaled). 대시 종료 후 첫 프레임에 `input.dashAttack=true` 주입 → 정상 발동. `_bufferedAttack` 만료 없음은 슬로모 중에도 안전하다. 단, 대시 중 다시 대시(재대시)하면 `_bufferedAttack=false` 폐기 — 정상.

## M-1: Skill01이 컨트롤러에 없음 — TriggerSkill() 무음 무동작
KatanaMelee.controller에 Skill01 파라미터/상태가 없음(grep 결과 0). SetTrigger는 파라미터 미존재 시 무음 실패. 이건 대시 베기 변경과 무관한 기존 잔여 이슈이나 컨트롤러 확인 중 발견됨.

## M-2: dashAttackMaxDuration=3.5f가 Inspector에서 0 설정 시 폴백 작동 안 함
`BeginDashAttack`: `dashAttackMaxDuration > 0f ? dashAttackMaxDuration : 3.5f` — 0f 설정 시 3.5f 폴백 작동. OK.

## 시간 도메인
`_dashAttackFallbackTimer`: `Time.deltaTime`(scaled). Counter 워치독과 동형. 권위문서 §7 규칙(Animator/클립과 정렬=scaled) 준수.

## 안전 패턴 재확인
- 재진입 없음: dashAttack 가드 `!_dashAttacking` — 진행 중 중복 진입 차단.
- 구독 대칭: 대시 베기는 이벤트 추가 구독 없음(AttackHit/ComboEnd 기존 공유).
- 이중 발동 없음: `_bufferedAttack` bool → 단 한 번만 `input.dashAttack=true`, 즉시 `_bufferedAttack=false`.
