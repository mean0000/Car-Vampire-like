# 플레이어 액션-조정 레일 (권위 문서)

> **2026-06-20 동결.** 플레이어 전투 스택의 구조 계약. **새 무기·새 공격/반격 액션을 추가할 때 반드시 이 문서를 먼저 읽고 따른다.** 이 계약을 어기면 "회피 후 공격 미발동" 류의 상태 desync 버그가 재발한다(아래 §9 이력 참조).
>
> 권위 동급: [[2026-06-10-camera-system]] · [[2026-06-09-postprocessing-core-design]]. 게임감/주스 캐넌은 [[feedback_animation_agent_principle]](애니가 진실)·[[feedback_player_self_cancel_canon]](플레이어 self-cancel).

---

## 0. 한 줄 요약

**플레이어가 "공격 커밋 중인가(busy=이동 잠금)"는 무기 코드의 낙관적 플래그가 아니라 *Animator가 실제로 액션 클립을 재생 중인가*가 소유한다.** 코드는 액션을 *요청*하고, Animator 상태가 *진실*이며, busy·잠금은 그 진실에서 도출된다. 이것이 "애니가 진실" 캐넌의 전투 레벨 완성이다.

---

## 1. 왜 이 레일이 존재하나 (해결한 문제)

### 증상
"회피(대시) 직후 좌클릭하면 공격/반격이 정확하게 발동하지 않는다." 카운터(반격) 추가 시 같은 버그가 콤보에서도 재현 — **패턴 실패**.

### 근본 원인 (1차 감사 E=3.5/10, FAIL)
무기 코드가 *입력*만 보고 액션을 낙관적으로 커밋(`_step=1`/`_countering=true`/busy)한 뒤 Animator엔 "알리고 기대만" 했다. Animator가 못 받으면(잘못된 상태·AnyState 경쟁·timeScale 정지) **두 상태 머신(무기 `_step` ↔ Animator 상태)이 갈라졌다.** 코드는 공격하는데 애니가 안 나오거나(또는 busy 고착), 그 반대.

구체적으로 4개 desync 지점:
1. 콤보 진입이 `Locomotion→Combo1` 전용 → 대시 직후(Animator가 Dash 상태)엔 진입 불가.
2. 시간 도메인 분열 — 히트스탑(timeScale=0) 중 `PlayerMotor.Tick`/`PlayerAnimatorDriver.Tick`이 `dt<=0`로 즉시 return해 Dash bool·상태가 안 풀리는데, 카운터 창은 unscaled로 계속 흐름.
3. 반격이 Animator 진입 확인 없이 `_countering=true` 커밋 → 진입 실패 시 입력 묵살 고착.
4. AnyState `Dash > Counter` 우선순위 경쟁(Dash bool 미정리).

---

## 2. 핵심 원칙 (불변식)

1. **위치 단일 소유 = `PlayerMotor`.** 모든 이동(입력 이동·대시 버스트·공격 루트모션)은 `PlayerMotor`가 `transform.position`에 쓴다. 다른 누구도 직접 안 쓴다. (루트모션은 `PlayerAnimatorDriver.OnAnimatorMove → PlayerMotor.ApplyRootStep` 경유.)
2. **애니가 진실 = 타이밍·busy는 Animator/클립이 소유.** 타격/캔슬창/종료는 클립 AnimationEvent(`OnAttackHit`/`OnComboWindow`/`OnComboEnd`). busy는 Animator 상태 태그(아래 §3).
3. **busy는 도출, 커밋 아님.** `WeaponBehaviour.IsBusy = 액션 유예 ∨ Animator가 "Action" 상태`. 코드 진행 플래그(`_step`/`_countering`)는 *진행 로직*(어느 단·반격 분기)만 담당하고 busy를 직접 정하지 않는다.
4. **플레이어 self-cancel.** 회피(대시)는 최우선 입력 — 진행 중 공격을 즉시 캔슬(하드컷). 단 회피·반격이 공격을 이기고, 공격은 회피를 못 끊는다(대시는 커밋).

---

## 3. 액션-조정 레일 — 동작 방식

### 3.1 Animator 상태 태그 `"Action"`
컨트롤러 `Assets/_Project/Animation/KatanaMelee.controller`에서 **공격/반격 상태에 State Tag `"Action"`**:
- `Combo1` / `Combo2` / `Combo3` / `Counter` → 태그 `Action`
- `Locomotion` / `Dash` / `Tumbling` → **빈칸**(이동·회피는 자기 메커니즘이 잠금을 처리, Action 아님)

### 3.2 `PlayerAnimatorDriver.IsActionPlaying`
```
현재 상태가 IsTag("Action")  OR  (전이 중 AND 다음 상태가 IsTag("Action"))
```
전이 중 다음 상태도 보는 것이 요청→진입 1~2프레임 갭을 메운다.

### 3.3 `WeaponBehaviour` (공유 레일 — 모든 무기 베이스)
- **템플릿 `Tick()`** — 베이스가 액션 유예(`_actionGrace`)를 `Time.deltaTime`로 감쇠한 뒤 구체 무기 `OnTick()` 호출. **구체 무기가 유예 처리를 빼먹을 수 없다**(레일을 자동으로 탄다).
- **`BeginAction()`** — 액션 애니를 요청하는 순간 호출. 유예(`actionPendingGrace`=0.12s)를 켜 busy가 *즉시* true → Animator 진입 전 이동 누수 방지. 이후 `IsActionPlaying`이 busy를 이어받는다.
- **`IsBusy = _actionGrace>0 || AnimatorDriver.IsActionPlaying`** — busy의 단일 진실.
- **`Cancel()`** — 유예를 끈다(캔슬 즉시 busy 해제). 구체 무기가 override하면 `base.Cancel()` 필수.

### 3.4 자가치유 (failure mode 자동 복구)
- **busy 자가치유:** 액션 진입 실패 시 유예 0.12s 만료 + `IsActionPlaying=false` → `IsBusy=false` 자동. (이동 잠금 해제)
- **진행 플래그 자가치유:** `KatanaWeapon.OnTick`에서 `!IsBusy && (_step>0 || _countering)`이면 진입 실패로 보고 `EndCounter()`/`ResetCombo()` 호출 + **에디터 경고**(`"Action"` 태그 누락 런타임 안전망). `_countering`이 입력을 묵살(최대 워치독 3.5s)하는 것까지 복구.

---

## 4. ★ 새 무기/액션 추가 가이드 (OCP 확장점 — 작업 시 이대로)

> **이게 핵심 참고 절차다.** 새 무기(대검/권총/드론)나 기존 무기의 새 공격을 붙일 때:

1. **무기 코드:** `WeaponBehaviour`를 상속한 새 클래스(`GreatswordWeapon : WeaponBehaviour` 등). `Tick`이 아니라 **`OnTick`을 override**(템플릿 메서드 — 베이스가 유예를 처리). `IsBusy`는 **override하지 마라**(베이스 레일 사용). 추가 커밋 상태가 꼭 필요하면 override 후 `base.IsBusy`와 OR.
2. **액션 요청 시 `BeginAction()` 호출** — 공격/특수/반격을 시작하는 모든 지점에서(콤보 시작·단 전환·반격 등). 이게 진입 유예를 켠다.
3. **Animator 상태에 태그 `"Action"` 필수** — 새 공격/반격 상태(클립) 전부. **누락 시 0.12s 뒤 이동 누수**(자가치유 에디터 경고가 잡아주지만, 안 다는 게 정답).
4. **Animator 진입은 AnyState로** — 새 액션 상태는 `AnyState→상태` 전환(하드컷: HasExitTime off, Duration 0, **CanTransitionToSelf off**)로 진입시켜 어느 상태에서든 즉발. `Locomotion→상태`만 두면 대시 직후 진입 불가(이 버그의 1번 원인).
5. **AnyState 우선순위:** `[0]Dash > [1]Tumbling > [2]Counter > [3]Combo`. 회피·반격이 공격보다 위(self-cancel 캐넌). 새 액션은 의미에 맞는 위치에.
6. **타이밍은 클립 AnimationEvent로** — 타격=`OnAttackHit`, 캔슬창=`OnComboWindow`, 종료=`OnComboEnd`(없으면 busy/진행이 안 닫힘). 코드 타이머로 타이밍 만들지 마라(애니가 진실).
7. **Cancel override 시 `base.Cancel()` 호출**(유예 해제).

→ **새 액션 = "Action 태그 + AnyState 진입 + BeginAction() + 클립 이벤트"**. busy·잠금·자가치유는 코드 수정 0으로 자동 편입.

---

## 5. 플레이어 스택 구조 (파이프라인)

`PlayerBrain.Update()` 매 프레임 명시 순서(Script Execution Order 의존 제거):
```
ReadInput → Aim.Tick → [회피 최우선 캔슬 + 입력 버퍼] → Weapon.Tick → busy=Weapon.IsBusy
          → Motor.Tick(busy) → Animator.SetAttacking(busy) → Animator.Tick → Footsteps.Tick
```
- **조준을 먼저** 확정 → 정지 대시 방향·공격 방향이 같은 프레임 최신 aim을 본다.
- **Weapon이 Motor보다 먼저** → 무기가 busy를 정해야 Motor가 그 busy로 이동을 양보.

| 컴포넌트 | 단일 책임 |
|---|---|
| `PlayerBrain` | 오케스트레이터(입력 수집·순서·입력 버퍼). 로직 없음. |
| `PlayerInputState` | 한 프레임 입력 스냅샷(입력 소스 격리 — New Input System 전환 시 Brain만 교체). |
| `PlayerAim` | 마우스→지면 평면 조준 방향. |
| `PlayerMotor` | **위치 단일 소유.** 이동(질량감 가속/감속)·대시(자유방향·i-frame)·벽가드·지면추종. |
| `WeaponBehaviour` (베이스) | 액션 레일(유예·busy·BeginAction·Cancel) + AnimationEvent 구독. |
| `KatanaWeapon` | 콤보 3단 진행·반격 카운터·히트 판정. |
| `ComboAttackSet` (SO) | 단별 판정(히트박스)+비주얼(슬래시) 데이터 단일 진실. |
| `PlayerAnimatorDriver` | 로코모션 파라미터 구동 + 루트모션 적용 + AnimationEvent 릴레이 + `IsActionPlaying`. |
| `PlayerHealth` | 허트박스·HP·무적 게이트(`IDamageable`)·퍼펙트 회피(`Parried`). |
| `ParrySlowMotion` | 패링 보상(히트스탑+슬로모, `timeScale` 단일 소유). |
| `PlayerAfterimage` | 대시 잔상 VFX. |

---

## 6. 회피→공격→패링→반격 흐름

- **회피(대시):** `dashDown + CanDash` → `Weapon.Cancel()`(진행 공격 하드컷) + 같은 프레임 좌클릭 무효. 대시는 커밋(공격이 못 끊음).
- **입력 버퍼:** 대시 *진행 중* 좌클릭은 버리지 말고 기억(`_bufferedAttack`) → 대시 끝나는 첫 프레임 재주입. 만료 타이머 없음(대시는 짧고, 재대시 시 폐기). "눌렀는데 안 나감" 제거의 핵심.
- **패링(퍼펙트 회피):** 대시 시작 후 `perfectDodgeWindow`(0.15s) 내에 적 공격이 닿으면 `PlayerHealth.Parried` 발화 → ① `ParrySlowMotion`(히트스탑+슬로모) ② `KatanaWeapon.ArmCounter`(반격 입력창 0.6s 오픈).
- **반격(카운터):** 반격창 안에 좌클릭 → `BeginCounter` → `Skill02` 반격(콤보 대신). `TriggerCounter`가 Dash bool을 즉시 꺼 AnyState 경쟁 해소.

---

## 7. 시간 도메인 표 (혼재는 의도적 — 새로 만들 때 이 표를 보고 정렬)

| 타이머 | 도메인 | 이유 |
|---|---|---|
| `_actionGrace` (유예) | **scaled** | Animator와 같은 축 — 슬로모 중 진입까지 함께 늘어져 정확히 브리지. |
| `_counterTimer` (반격 창) | **unscaled** | 플레이어 반응은 실시간 — 슬로모가 창을 안 늘리거나 줄임(관대). |
| `_counterFallbackTimer` (워치독) | scaled | 클립 재생 시간과 정렬(백스톱). |
| 대시 입력 버퍼 | (타이머 없음) | 대시 끝에 재주입 — 만료 불필요(슬로모 만료 엣지 제거). |
| 콤보 입력 버퍼 `_buffered` | scaled | 콤보 단 연결 타이밍. |

**규칙:** 사람 반응 창=unscaled. Animator/클립과 정렬돼야 하는 것=scaled.

---

## 8. 함정 / 반드시 알 것

- **`"Action"` 태그는 load-bearing.** 안 달면 busy가 유예(0.12s)밖에 못 잡아 이동 누수. (자가치유 에디터 경고가 잡지만 안 다는 게 정답.)
- **SerializeField 씬 덮어쓰기:** 새 직렬화 필드를 추가해도 씬에 박힌 값이 코드 default를 이긴다 — Inspector 확인. (이 레일의 `actionPendingGrace`는 신규라 씬에 없으면 0.12 default.)
- **위치 이중 소유 금지:** 이동기를 추가하면 반드시 `PlayerMotor` 경유. `ApplyRootStep`은 대시 중(`_dashAppliedThisFrame`/`_dashTimer`) 양보로 이중 적용을 막는다.
- **Tumbling 코드는 비활성 잔재.** `_tumbling`은 현재 절대 true가 안 됨(`TriggerTumbling` 본문 주석·`Parried→TriggerTumbling` 구독 주석). 정리 예정(§10). 새 작업 시 무시.
- **MonoBehaviour 수명주기 함정:** `WeaponBehaviour.Initialize`는 재진입 가드(이전 이벤트 구독 해제 선행). 구체 무기가 `OnDestroy` override 시 `base.OnDestroy()` 필수.

---

## 9. 게이트 이력 (이 레일의 검증)

- **1차 종합 감사(나+Stab+Codex 3중):** 기반(이동/조준/입력/데이터) 7~9 양호, **E(시스템 얽힘) 3.5~4 FAIL**(구조적 High), H(빌드) 4~6 약점.
- **레일 구현 후 재감사:** **E = Stab 7.5 / Codex 8.1, PASS.** "busy 낙관 커밋 desync" 구조적 High 제거 확인(양 리뷰어 독립 수렴). H = Tier-1 빌드위생으로 격리.
- **잔여(전부 Medium 이하):** §10.

---

## 10. 남은 작업 (미해결 — 작업 시 참고)

- **Tumbling 비활성 잔재 코드 제거** — `PlayerMotor`(`_tumbling`/`SetTumbling`/`IsTumbling`/Tick 분기)·`PlayerAnimatorDriver`(`_tumbling`/`TriggerTumbling`/tumbling Tick 로직/노브). 상태 모델 단순화(별도 외과 패스).
- **`ComboAttackSet` 미할당 폴백 수치(range 1.8/arc 50/dmg 3)가 코드 은닉** — Inspector 노출 or 미할당을 에러로(밸런스 가시성).
- **`PlayerHealth` 세이브 복원 진입점 부재** — 미래 세이브 시 `_hp` 복원 메서드 필요(현재 막다른 길은 아님).
- **asmdef 부재** — `_Project.Player`/`_Project.Debug` 분리(중기).
- **L-2:** `_actionGrace` scaled가 깊은 슬로모서 과장(진입 실패 시만 체감) — 정상 케이스엔 정확, 현 유지.
- **⚠️ 런타임 손맛 미검증:** 대시→좌클릭 즉발·1타 리셋 루프 부재·반격 발동 = **유저 플레이 판정**(정적 검증의 천장).

---

## 11. 관련 파일

| 파일 | 역할 |
|---|---|
| `Assets/_Project/Scripts/Player/WeaponBehaviour.cs` | ★레일 베이스(템플릿 Tick·BeginAction·IsBusy·Cancel) |
| `Assets/_Project/Scripts/Player/PlayerAnimatorDriver.cs` | `IsActionPlaying`·루트모션·이벤트 릴레이 |
| `Assets/_Project/Scripts/Player/KatanaWeapon.cs` | 콤보·반격·자가치유 reconcile |
| `Assets/_Project/Scripts/Player/PlayerBrain.cs` | 오케스트레이션·입력 버퍼 |
| `Assets/_Project/Scripts/Player/PlayerMotor.cs` | 위치 단일 소유·대시·i-frame |
| `Assets/_Project/Scripts/Player/PlayerHealth.cs` · `ParrySlowMotion.cs` | 패링·보상 |
| `Assets/_Project/Animation/KatanaMelee.controller` | "Action" 태그·AnyState 진입·전환 |
