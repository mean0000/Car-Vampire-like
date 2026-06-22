---
name: katana-skill01-pattern
description: 카타나 RMB 스킬(Skill01) QA(2026-06-20): 카운터 미러 정확성·쿨다운·대시억제·3액션 상호작용 검증
metadata:
  type: project
---

## 결과 요약: Critical 0 / High 2 / Medium 3 — PASS

카운터 미러는 정확하다(BeginSkill/EndSkill/워치독/자가치유/OnHitFrame분기/OnComboEnd분기/Cancel 전항목 동형).

## ★H-1: Skill01 클립 OnComboEnd 미심심 + skillMaxDuration 미검증 = 소프트락
- `skillMaxDuration` 기본값 3.5s는 Counter(2.58s+여유)에서 그대로 복사한 값
- Animation 에이전트가 Skill01 클립에 OnComboEnd 이벤트를 심지 않으면 _skilling 영구 고착 → 3.5s 조작불능
- **필수 후속:** Animation 에이전트 Skill01 클립 길이 확정 후 Inspector skillMaxDuration = 클립길이+여유 검증
- [[parry-counter-pattern]] H-1과 동일 표면(카운터 클립 OnComboEnd 누락 소프트락)

## ★H-2: 자가치유 else-if 체인 — _skilling/_countering 동시 고착 시 카운터 잔존
- KatanaWeapon.cs 171행: `if (_skilling) EndSkill(); else if (_countering) EndCounter(); else ResetCombo()`
- 정상 흐름에서 동시 true 불가(IsBusy 교차 차단)이나 이론 경로 닫으려면 독립 if로:
  ```csharp
  if (!IsBusy)
  {
      if (_skilling) EndSkill();
      if (_countering) EndCounter();
      if (_step > 0) ResetCombo();
  }
  ```
- 2행 수정, 비용 최소

## M-1: 대시 후 RMB 버퍼 없음 (의도적 설계 — 플레이테스트 후 판단)
- LMB는 `_bufferedAttack`으로 보존, RMB는 소거(PlayerBrain 65행 "대시 커밋 — 끝난 뒤 RMB로" 주석)
- 플레이테스트에서 "대시→스킬 씹힘" 피드백 시 `_bufferedSkill` 도입 검토

## M-2: _hitDone 3액션 공유 — 묵시적 불변식
- IsBusy 교차 차단으로 _skilling/_countering 동시 true 불가 → 공유 안전
- else-if 우선순위(스킬→카운터→콤보)에 주석 없음 → 3번째 스킬 추가 시 혼란 위험

## M-3: skillCooldown SerializeField 기본값 0f
- Tooltip에 "VFX 테스트용"으로 의도 명시됨
- 밸런스 확정 시 **프리팹 Inspector 직접 수정 필수** (씬 저장값이 코드 default를 이김 — 이 프로젝트 기록된 함정)

## 검증된 안전 전제
- 스킬 진행 중 LMB: 183행 `_countering || _skilling` 가드 → BeginCounter/BeginCombo 진입 불가
- 콤보/카운터 진행 중 RMB: IsBusy=true → BeginSkill 진입 불가
- 대시 시작/진행 중 RMB: Brain 55/65행 secondaryDown=false 소거
- TriggerSkill() Dash bool 정리: AnyState 경쟁 해소(Counter와 동형)
- OnHitFrame 분기 순서: 스킬 → 카운터 → 콤보 (동시 진입 불가이므로 순서 무관하나 스킬 우선)
- Cancel(): _skilling/_countering/_hitDone 모두 리셋, 쿨다운은 소비 유지(의도 — 회피로 끊어도 쿨다운 환불 안 함)
