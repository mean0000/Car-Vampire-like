---
name: parry-counter-pattern
description: 패링 반격(카운터) QA (2026-06-20): Counter 상태 종료=ExitTime전용(OnComboEnd 없음)→_countering 소프트락 위험·콤보중 Counter AnyState 가로채기 코드↔컨트롤러 불일치·_lockedFace counter종료후 미해제·재진입시 ArmCounter중복·패링중패링 _counterTimer덮어쓰기. Critical 0 / High 3
metadata:
  type: project
---

## 2026-06-20 패링 반격(KatanaWeapon counter / KatanaMelee.controller) QA 결과

### 컨트롤러 구조 (검증됨)
- AnyState→Counter: Counter 트리거, HasExitTime=false, TransitionDuration=0 — 어느 상태에서든 즉시 전환 가능
- Counter→Locomotion: **ExitTime=0.9, HasExitTime=1, Conditions=[]** — OnComboEnd AnimationEvent가 아니라 ExitTime으로만 종료
- Counter 트리거는 AnyState에 달려 있어 Combo1/2/3 재생 중에도 발동됨 (코드에서 _step==0 조건으로 막고 있음)

### H-1 (HIGH): Counter 종료 = ExitTime 전용 → _countering 소프트락 위험
- Counter 상태는 OnComboEnd AnimationEvent가 없고 ExitTime=0.9 자동전환만 있음
- 그러나 KatanaWeapon.OnComboEnd가 `_countering` 해제를 담당 — 이 이벤트는 콤보 클립의 AnimationEvent이므로 Counter 클립에 동일한 이벤트가 없으면 OnComboEnd가 발화하지 않음
- 컨트롤러는 ExitTime 후 Locomotion으로 자동복귀하지만 코드 `_countering=true`는 영구 유지 → IsBusy=true, 이동/공격/모든 입력 영구 잠금
- 픽스: Counter 클립에 OnComboEnd AnimationEvent 심기 OR 코드에 _counterTimer 기반 폴백 타임아웃

### H-2 (HIGH): _lockedFace counter 종료 후 미해제
- BeginCounter→TriggerCounter→SetCombo 미호출 → _lockedFace는 TriggerCounter에서 잠기지만 카운터 종료(OnComboEnd) 시 SetCombo(0)이 `_lockedFace=Vector3.zero`를 실행하므로 조건적으로 해제됨
- 그러나 H-1(OnComboEnd 미발화)이 터지면 _lockedFace도 영구 잔존 → 카운터 후 평시에도 몸이 반격 방향으로 고정

### H-3 (HIGH): ArmCounter 재진입 — 패링 중 패링 시 _counterTimer 갱신
- Parried 이벤트→ArmCounter가 `_counterTimer = counterWindow` 단순 대입
- 슬로모 중 적이 다시 맞으면 (같은 대시 창 내에서 _parryFiredThisDash가 이미 true이므로 사실상 불가)
- 단, ParrySlowMotion이 timeScale을 낮추는 중 플레이어가 다시 대시해 Parried가 재발화하면 _counterTimer가 갱신됨 — 이미 열린 창 위에 새 창이 덮어씌워지는 것은 무해(counterWindow 이내 재발화니 시간이 늘 뿐)
- 실제 문제는 없으나 의도적 디자인인지 확인 권고

### M-1 (MEDIUM): Tick 분기 — 콤보 진행 중(_step>=1) counterTimer 오픈 케이스 입력 소멸
- BeginCounter 조건: `_counterTimer > 0f && _step == 0`
- _step>=1 콤보 진행 중 패링 성공 → _counterTimer 설정 → 입력해도 `_step >= 1` 분기로 버퍼에 쌓임 → 카운터 발동 불가
- 의도적 설계일 수 있으나(콤보 중 카운터 봉쇄) 문서화 없음; _counterTimer는 unscaled로 흘러 콤보 종료 후 잔여 시간 안에 클릭하면 카운터 발동 — 실제로는 동작하므로 플레이어 입장에서 혼란 없을 수 있음

### M-2 (MEDIUM): DoHit 공통화 — counterKnockback=0f 폴백 없음
- DoCounterHit: `counterDamage > 0 ? counterDamage : 1` 가드 있음
- counterKnockback은 폴백 없이 그대로 전달 — 0이면 넉백 0(기능적으론 무해, 하지만 콤보와 일관성 없음)

### 안전 확인 (검증됨)
- Parried 구독: PlayerBrain Awake/OnDestroy 완전 대칭
- Initialize 재진입 가드: ComboWindow/ComboEnd 해제→재구독 정상
- Cancel()이 _countering=true, _counterTimer=0 모두 리셋 — 회피 가로채기 정상
- _hitDone: BeginCounter에서 false 세팅 → DoCounterHit 1회 가드 정상
- Counter 트리거가 컨트롤러에 없으면 SetTrigger 무음(안전)
- counterRange/counterArcHalf/counterDamage 기본값: Inspector 기본값 사용 시 동작하는 값(3m/70°/12)

**Why:** Counter 클립에 OnComboEnd 이벤트가 없으면 코드 _countering 영구 잠금 — 실제 플레이에서 카운터 후 입력 전체 소프트락 발생  
**How to apply:** 차기 리뷰에서 Counter 클립 AnimationEvent 설치 여부 최우선 확인
