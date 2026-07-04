---
name: katana-combo-pattern
description: 카타나 콤보 3단(KatanaWeapon+PlayerAnimatorDriver ComboStep int) QA — 핵심 hazard + 안전 전제 (2026-06-18 초기, 2026-06-21 RestartCombo+comboLoopCooldown 추가, ★2026-07-04 RestartCombo/comboLoopCooldown 코드에서 완전 제거 확인+3세그 스트라이크스냅 리타임 리뷰 추가)
metadata:
  type: project
---

## ★2026-07-04 갱신 — RestartCombo/comboLoopCooldown 폐기 확인 (메모리 스테일 정정)

아래 "comboLoopCooldown QA"·"RestartCombo 안전 확인" 섹션(2026-06-21 작성)은 **더 이상 코드에 없음** —
`grep -r "RestartCombo|comboLoopCooldown" Assets/` 전체 0건(2026-07-04 확인). 현재 KatanaWeapon.cs 설계:
- **피니셔(comboMax=3)는 클릭으로 재시작 불가** — `_step < comboMax` 가드가 Advance/이동캔슬 양쪽에 있어 3단에서 버퍼 입력은 그냥 감쇠 소멸(회수는 대시로만 캔슬, 자연종료=OnComboEnd→ResetCombo).
- 무한 연타 억제는 인공 쿨다운이 아니라 "3타 회수 + 대시캔슬 리듬"(공격→대시→공격) 캐넌으로 대체(주석 명시).
- 위 구식 섹션은 참고용으로만 남기고 **재적용 금지** — 다음에 이 파일을 읽는 세션은 이 갱신 노트를 신뢰할 것.

## ★2026-07-04 QA — 평타 3세그 리타임(스트라이크 스냅) 리뷰 결과

**대상**: `KatanaComboRetimer.cs`(2세그→3세그 개편, 이벤트를 소스 대신 상수에서 저작) + 리타임 클립 3종
(`S1_Combo01_01/02/03_Retimed.anim`) + `KatanaMelee.controller`(Combo2 m_Motion repoint). Critical/High **0**.

- **★과거 Combo2 이벤트갭 소프트락 클래스 — 수정 확인(검증완료, 재발 아님)**: 세 클립 모두 `m_Events`에
  정확히 3개(OnAttackHit int=1/2/3 → OnComboWindow → OnComboEnd), 시간순 hit<window<end 엄수 확인
  (파일 실측: C1 0.2694/0.3226/0.6340, C2 0.1572/0.2313/0.6895, C3 0.1489/0.2023/0.8344 — 오케 실측치와
  소수점 4자리까지 일치). 이전엔 Combo2 FBX 서브클립에 이벤트 0개라 소스-읽기 방식이면 OnComboEnd
  미발화 위험(자가치유로 결국 회복되나 그 스윙은 무피해+정지). 상수 저작 방식으로 원천 차단 확인.
- **가드 로직 무영향 확인**: `TimeMap.Map()`은 모든 구간 speed>0(1.25/2.2/1.4/1.0)이라 단조증가 보장 —
  이벤트 순서 역전 불가. `strikeStart = Clamp(hit-lead, 1e-4, win-1e-4)` 방어로 구간 길이 항상 양수(NaN/0나눔 불가).
- **캔슬창 타이밍 회귀 없음**: StrikeSpeed(2.2×)는 windup→hit 구간만 압축, 캔슬창(window→end)은
  RecoverySpeed(1.4×, 피니셔만 1.0×)로만 압축 — "스트라이크 가속이 캔슬창을 과도 단축"은 기각(구조상
  분리된 구간). 창 폭 C1 0.311s/C2 0.458s/C3 0.632s, 전부 inputBufferTime(0.5s) 대비 여유 있음.
- **Combo2 repoint 댕글링 없음**: 옛 FBX 서브클립 guid(`ebd5d44d967e97b46bc091fc4a362265`)로 project-wide
  grep(.controller/.overrideController/.asset/.unity/.prefab) 0건. Combo1=3291e7ea/Combo2=fda78cae(신규)/
  Combo3=702d3829, 전부 m_Tag: Action 보존, 컨트롤러의 CUT 전이(HasExitTime:0, TransitionDuration:0,
  ConditionMode:6 Equals) 구조 불변 확인.
- **위상(Fix B) 무회귀 확인**: `PlayerAnimatorDriver.ComboLayerActivelyPlaying()`의 `!_comboActive→return false`
  게이트 존재 확인(상하체분리 v4/FixB 픽스, 클립 길이와 무관하게 동작 — 대시캔슬 시 busy 해제는 여전히
  `_comboActive` 플래그 기반이지 normalizedTime 절대값 기반이 아니므로 클립이 짧아져도 안전).
- **Low(정보성, 수정 요구 안 함)**: ①피스와이즈 리샘플이 세그 경계(strikeStart, window) 2곳에 탄젠트
  스케일 불연속("코너")을 남김 — 리샘플 기법 고유 트레이드오프(2세그 구버전에도 있었음, 이번에 경계 1개
  늘어남), 시각적으로 튀면 Animation 에이전트가 앵커 키 주변 탄젠트 수동 스무딩 검토. ②Defs()에 hitNorm
  < windowNorm < endNorm 순서 assert 없음(현재값은 전부 정상이나 향후 오타 시 클램프가 살리되 스트라이크
  구간이 레이저-씬 얇아짐, 크래시는 안 남). ③KatanaMelee.controller diff가 매우 큼(수백 라인) — Unity의
  SetDirty+Save 시 전체 YAML 재직렬화(블록 재정렬+공백 정규화)가 원인, 실제 의미변경은 Combo2 m_Motion
  1줄뿐임을 diff 라인별 대조로 확인(공포성 diff, 버그 아님).
- **극단 케이스(로우, 사변적 — 방어 코드 요구 안 함)**: 0.3~0.6s급 프레임 히치(GC/씬로드 스톨)로 같은
  프레임에 OnComboWindow+OnComboEnd가 겹쳐 발화하면 ResetCombo()가 윈도우를 먼저 닫아버려 그 프레임의
  버퍼 입력이 체인 안 되고 소멸할 수 있음 — 소프트락 아님(자가치유·ResetCombo 정상 완주), 이번 리타임과
  무관한 이벤트-드리븐 Animator 일반론적 위험(현재 창 폭 0.31~0.63s면 발생 확률 극히 낮음, 재발 안건 아님).

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
