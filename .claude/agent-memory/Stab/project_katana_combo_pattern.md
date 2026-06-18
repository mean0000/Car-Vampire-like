---
name: katana-combo-pattern
description: 카타나 콤보 3단(KatanaWeapon+PlayerAnimatorDriver ComboStep int) QA — 핵심 hazard + 안전 전제 (2026-06-18)
metadata:
  type: project
---

## 아키텍처 전제

- **입력 흐름**: primaryDown → `_step==0`이면 BeginCombo, `_step>0`이면 _buffered=true(_bufferTimer 0.35s)
- **윈도우**: AnimationEvent OnComboWindow → `_windowOpen=true`. Tick에서 `_windowOpen && _buffered && _step<comboMax` → Advance
- **종료**: AnimationEvent OnComboEnd → `_step=0` 복귀. CUT 전환으로 Advance 후엔 이전 클립 OnComboEnd가 안 온다는 AnimatorController 보장에 의존
- **클립 타이밍(기준값)**: Combo1 타격=0.367s, 윈도우=0.483s, 갭=0.116s

---

## 확인된 버그/위험 패턴

### H-1 — KatanaWeapon 파괴 시 ComboWindow/ComboEnd 좀비 리스너
- KatanaWeapon에 `protected override void OnDestroy()` 미구현
- `WeaponBehaviour.OnDestroy`는 `base.Cleanup()`만 호출(=AttackHit만 해제)
- ComboWindow/ComboEnd는 KatanaWeapon.Cleanup에 있으나 OnDestroy 경로에서 미호출
- Brain.OnDestroy → `_weapon?.Cleanup()` 경로는 안전하나, KatanaWeapon이 Brain보다 먼저 파괴될 때 누락
- **픽스**: KatanaWeapon에 `protected override void OnDestroy() { ... AnimatorDriver.ComboWindow -= ...; AnimatorDriver.ComboEnd -= ...; base.OnDestroy(); }` 추가

### H-2 — Initialize 재진입 시 ComboWindow/ComboEnd 이중 구독
- base.Initialize는 AttackHit 재진입 가드 있으나, KatanaWeapon의 ComboWindow/ComboEnd += 는 가드 없음
- 멱등 동작이나 구독 카운트 누적 → 새 핸들러 추가 시 이중 발화 버그로 변질
- **픽스**: Initialize 앞에 `if (AnimatorDriver != null) { AnimatorDriver.ComboWindow -= OnComboWindow; AnimatorDriver.ComboEnd -= OnComboEnd; }` 추가

### M-1 — OnComboEnd 경합: CUT 전환 후 지연 발화
- Advance 후 이전 클립 OnComboEnd가 같은 프레임 지연 발화 → `_step=0`으로 진행 중 콤보 즉시 종료 가능
- 세대 카운터(generation) 없이 AnimatorController 구현만 방어선
- **픽스**: `_expectedStep` 세대 카운터 추가, OnComboEnd에서 `if (_step != _expectedStep) return;` 가드

### M-2 — 입력 버퍼 0.35s vs 얼리 클릭 씹힘 구간 0.133s
- BeginCombo 직후 t=0 ~ t=0.133s(=0.483-0.35) 구간 클릭은 버퍼 만료 후 윈도우가 열림 → 씹힘
- "2단이 안 나온다" 체감 버그로 연타 플레이 스타일에서 노출
- **픽스 옵션 A**: inputBufferTime을 0.50s+ 로 늘리기
- **픽스 옵션 B**: 버퍼를 `_lastClickTime = Time.time` 기반으로 교체(윈도우 시점에 `Time.time - _lastClickTime < inputBufferTime` 판정)

### M-3 — AttackHit 폴백 타이머 제거 → 클립 CUT 시 타격 누락 무방비
- WeaponBehaviour 주석이 명시적으로 폴백 요구, 그러나 KatanaWeapon 콤보 버전에서 미구현
- 타격(0.367s) < 윈도우(0.483s) 순서가 클립에서 보장되면 CUT 시 타격 항상 완료 → 안전하나 클립 수정 시 무음 깨짐

---

## 안전 확인된 사항

- **primaryDown 이중 경로 불가**: GetMouseButtonDown 한 프레임 1회 + if/else if 배타 구조
- **_hitDone 가드**: OnHitFrame `!_hitDone` + Advance에서 false 리셋. 동단 이중 타격 불가
- **_step 범위**: 0~comboMax 상한(Advance 진입 조건 `_step < comboMax`), 음수 불가
- **comboMax [Min(1)]**: Inspector 하한 보장
- **_hitThisSwing.Clear()**: DoSwingHit 진입 즉시 → 동단 다중 콜라이더 중복 타격 방어
- **OverlapSphere 버퍼 64 + n==length 경고**: 이전 M-2 수정 완료
- **base.Initialize AttackHit 재진입 가드**: 존재. OK
- **timeScale=0**: AnimationEvent가 발화 안 되므로 콤보 자동 정지. Tick의 버퍼 감쇠도 정지(일시정지 중 클릭이 재개 후 즉시 소비 가능 — 에지)

---

## 검증이 필요한 AnimatorController 전제

- Combo1→2→3 전환이 CUT(transition duration=0, no exit time) 으로 구현되어야 함
- OnComboEnd AnimationEvent가 클립의 몇 normalizedTime에 배치되었는지 — M-1 경합 위험도 결정
- 타격 이벤트(OnAttackHit)가 항상 윈도우(OnComboWindow)보다 앞에 배치되어야 M-3 안전
