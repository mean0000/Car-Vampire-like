---
name: katana-combo-pattern
description: 카타나 콤보 3단(KatanaWeapon+PlayerAnimatorDriver ComboStep int) QA — 핵심 hazard + 안전 전제 (2026-06-18 초기, 2026-06-21 RestartCombo+comboLoopCooldown 추가 업데이트)
metadata:
  type: project
---

## 아키텍처 전제

- **입력 흐름**: primaryDown → `_step==0`이면 BeginCombo, `_step>0`이면 _buffered=true(inputBufferTime=0.5s)
- **윈도우**: AnimationEvent OnComboWindow → `_windowOpen=true`. Tick에서 `_windowOpen && _buffered && _step >= 1` → `_step < comboMax`면 Advance, else RestartCombo(회수 캔슬 신규)
- **종료**: AnimationEvent OnComboEnd → `_step=0` 복귀. CUT 전환으로 Advance 후엔 이전 클립 OnComboEnd가 안 온다는 AnimatorController 보장에 의존
- **클립 타이밍(기준값)**: Combo1 타격=0.367s, 윈도우=0.483s, 갭=0.116s. 클립 전체는 ~1.0s 이상(0.1s 가드 창과 여유)

## KatanaMelee.controller 실측 (2026-06-21 디스크 확인)

- **AnyState 전이 3개**: [0]Dash=true→Dash / [1]Counter트리거→Counter / [2]ComboStep==1→Combo1
- **ComboStep==1 조건**: ConditionMode=6(Equals), threshold=1 — 즉 SetInteger(ComboStep,1)이 Combo3에서 AnyState→Combo1 하드컷을 발동시킨다. CanTransitionToSelf=0.
- **Combo3→Combo1 직접 전이 없음**: Combo3는 ComboStep==0만 전이 조건. RestartCombo는 AnyState 경로를 사용.
- **Skill01 파라미터 없음**: 컨트롤러에 Speed/MoveX/MoveY/Attack/Dash/ComboStep/DashX/DashY/Counter만 있음. TriggerSkill()=무음 무동작(기존 알려진 미연결 상태).
- **모든 Action 클립 태그 확인**: Combo1/Combo2/Combo3/Counter 모두 m_Tag: Action. Locomotion/Dash는 빈칸.

---

## 확인된 버그/위험 패턴

### H-1 — KatanaWeapon 파괴 시 ComboWindow/ComboEnd 좀비 리스너
- KatanaWeapon에 `protected override void OnDestroy()` 미구현
- WeaponBehaviour.OnDestroy는 base.Cleanup()만 호출(=AttackHit만 해제)
- ComboWindow/ComboEnd는 KatanaWeapon.Cleanup에 있으나 OnDestroy 경로에서 미호출
- Brain.OnDestroy → `_weapon?.Cleanup()` 경로는 안전하나, KatanaWeapon이 Brain보다 먼저 파괴될 때 누락
- **픽스**: KatanaWeapon에 `protected override void OnDestroy() { ... AnimatorDriver.ComboWindow -= ...; AnimatorDriver.ComboEnd -= ...; base.OnDestroy(); }` 추가

### H-2 — Initialize 재진입 시 ComboWindow/ComboEnd 이중 구독 (2026-06-21 코드에서 수정 확인됨)
- Initialize()에 `if (AnimatorDriver != null) { AnimatorDriver.ComboWindow -= OnComboWindow; AnimatorDriver.ComboEnd -= OnComboEnd; }` 선행 해제 추가됨 — 수정 완료.

### M-1(기존) — OnComboEnd 경합: CUT 전환 후 지연 발화
- `_lastAdvanceTime = Time.time` + OnComboEnd에서 `< 0.1f` 가드로 방어
- RestartCombo도 동일하게 `_lastAdvanceTime = Time.time` 설정해 Combo3 하드컷 후 지연 OnComboEnd 차단 — 가드 정합.
- 0.1s 창: 슬로모 중에도 Combo1 클립(~1.0s)의 정상 OnComboEnd는 여유 있게 통과. 안전.

### M-1(신규 2026-06-21) — RestartCombo: Combo3 클립 OnComboWindow vs OnComboEnd 순서 미확인
- RestartCombo는 `_windowOpen=true`인 상태(OnComboWindow 이후)에서만 발동되어야 한다.
- Combo3 클립에서 `OnComboEnd`가 `OnComboWindow`보다 먼저 또는 같은 프레임에 발화하면 → `ResetCombo()`가 `_step=0`, `_windowOpen=false`로 닫아 RestartCombo 조건(`_step >= 1`)이 false가 됨 → 회수 캔슬 무음 동작 안 함.
- **필수 검증**: Combo3 클립 AnimationEvent 타임라인에서 OnComboWindow normalized time < OnComboEnd normalized time 확인.

### M-2 — 입력 버퍼 0.5s vs 얼리 클릭 씹힘 구간
- inputBufferTime=0.5s(기존 0.35s에서 늘어남, Stab M-2 대응). 씹힘 구간이 0으로 수렴.
- OnComboWindow=0.483s < inputBufferTime=0.5s → 1단 직후 즉시 클릭도 버퍼에 저장됨.

### M-3 — AttackHit 폴백 타이머 제거 → 클립 CUT 시 타격 누락
- 타격(0.367s) < 윈도우(0.483s) 순서가 클립에서 보장되면 안전. 클립 수정 시 무음 깨짐 위험.

---

## comboLoopCooldown QA (2026-06-21)

### 타이밍 정합 (PASS)
- `_comboLoopCdTimer = comboLoopCooldown` 세팅 위치: OnHitFrame에서 `_step >= comboMax` + `_hitDone=true` + `DoSwingHit()` 직후 → 타격과 원자적으로 묶임.
- 차단 구간: 캔슬창(0.334s) 열린 후 쿨다운 만료(0.216+0.25=0.466s)까지 → 0.334~0.466 차단, 0.466~0.755 허용. 정합.
- Advance(1→2, 2→3)은 `_comboLoopCdTimer` 미접촉 → 콤보 진행 무영향. 확인.

### ★M-1(신규) — comboLoopCooldown 상한 미제약: 버퍼 만료 선행 시 재시작 무음 소멸
- **조건**: `comboLoopCooldown > inputBufferTime(0.5f) - Combo3타격시각(≈0.22f) ≒ 0.28f`
- 버퍼가 T=0.5s에 만료, 쿨다운이 T>0.5s에 만료 → 쿨다운 해제 후 버퍼 없어 RestartCombo 불발 → "눌렀는데 콤보 재시작 없음"
- 현재 기본값 0.25f에서는 미발생. Inspector 조정 시 함정.
- **픽스**: `[SerializeField, Min(0f)]` → `[SerializeField, Range(0f, 0.27f)]` 또는 Tooltip에 상한 ≈0.28f 명시, 또는 쿨다운 세팅 시 버퍼 연장(`if (_buffered) _bufferTimer = Mathf.Max(_bufferTimer, comboLoopCooldown + 0.05f)`).

### 안전 확인
- Cancel()의 `_comboLoopCdTimer=0f` 리셋: 대시 후 BeginCombo()는 `_comboLoopCdTimer` 미검사라 즉시 허용 — 의도 정합(idle 재시작은 쿨다운 미적용).
- 자가치유(ResetCombo)가 `_comboLoopCdTimer`를 초기화 안 해도 다음 3타에서 덮어쓰여 안전.
- comboMax=1 엣지: 1단 타격에서 쿨다운 세팅 + RestartCombo 경로 정상.
- 시간 도메인: scaled dt로 클립 재생과 동일 축. 슬로모 중 일관.

---

## RestartCombo 안전 확인 (2026-06-21)

- **단일 발동**: _buffered, _windowOpen 리셋 → 같은 프레임 이중 진입 불가.
- **IsBusy 연속성**: BeginAction()이 _actionGrace=0.12s 설정 → 자가치유 reconcile 오발 없음.
- **_startCdTimer 미설정 무해**: _step=1이므로 다음 입력은 _buffered 경로로 가고 _startCdTimer 검사 분기에 도달하지 않음.
- **카운터/스킬 중 차단**: primaryDown 분기 첫 줄 `if (_countering || _skilling)` 가드가 막음. _windowOpen 경로도 BeginCounter에서 _windowOpen=false로 닫힘.
- **CanTransitionToSelf=0**: AnyState→Combo1이 자기 전이 금지 → Combo1 재생 중 또 RestartCombo 발동 불가(구조상 _step=1 < comboMax=3이므로 Advance 분기로 감).
- **컴파일 오류 없음**: 심볼 전부 해결. RestartCombo 정의(L240-250), 호출(L209) 확인.

---

## 안전 확인된 사항

- **primaryDown 이중 경로 불가**: GetMouseButtonDown 한 프레임 1회 + if/else if 배타 구조
- **_hitDone 가드**: OnHitFrame `!_hitDone` + Advance/RestartCombo에서 false 리셋. 동단 이중 타격 불가
- **_step 범위**: 0~comboMax 상한, 음수 불가. comboMax [Min(1)] Inspector 하한 보장.
- **_hitThisSwing.Clear()**: DoSwingHit 진입 즉시 → 동단 다중 콜라이더 중복 타격 방어
- **base.Initialize AttackHit 재진입 가드**: 존재.

---

## 검증이 필요한 AnimatorController 전제

- **★Combo3 클립 AnimationEvent 순서**: OnComboWindow < OnComboEnd (normalized time 기준) 필수. 뒤바뀌면 RestartCombo 무음 동작 안 함.
- Combo1→2→3 전환이 CUT(transition duration=0, no exit time)으로 구현됨 — 디스크 확인완료.
- OnAttackHit이 항상 OnComboWindow보다 앞에 배치되어야 M-3 안전.
