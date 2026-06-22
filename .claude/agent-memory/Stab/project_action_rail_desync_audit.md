---
name: action-rail-desync-audit
description: 공유 액션-조정 레일 구현 E 재점수 감사 (2026-06-20). IsBusy=Animator추종 구조, 버퍼 dead code, 태그 계약 안전망 부재.
metadata:
  type: project
---

## 감사 결과 (2026-06-20)

### 구현 대상 레일
- `WeaponBehaviour.IsBusy = _actionGrace > 0 || IsActionPlaying` (Animator 실상태 기반)
- `BeginAction()` = 진입 유예 0.12s (scaled) 켬
- `PlayerAnimatorDriver.IsActionPlaying` = 현재 OR 전이 중 다음 상태의 "Action" 태그 감지
- `PlayerBrain` 버퍼: 대시 중 좌클릭 → 대시 종료 첫 프레임 재주입
- `TriggerCounter()` 내 Dash bool 즉시 끔 (AnyState 경쟁 해소)
- 컨트롤러 Combo1/2/3/Counter = `m_Tag: Action` 실재 확인

### E 재점수: 7.5/10 (이전 3.5~4 → PASS)

**구조적 High 없음.** 이전 E=3.5의 근본원인(busy=코드 내부 상태, Animator desync)이 제거됨.

### 닫힌 것
1. desync: IsBusy가 Animator 실상태를 추종 → 회피→공격 흐름 구조적 안전
2. 자가치유: 진입 실패 시 유예 만료 + IsActionPlaying=false → busy 자동 해제
3. 콤보 연속성: Combo1→2→3 전이 중 IsActionPlaying 끊김 없음 (전이 중 Next 상태 포함)
4. AnyState→Combo1 vs 내부 전이 경쟁: Unity 평가 순서상 상태 내부 전이 우선 → 안전
5. 버퍼 재주입 1회: `else if` 구조로 재주입 프레임에 정확히 1회 발화
6. 대시 연타 버퍼 폐기: `dashDown && CanDash` 분기에서 즉시 폐기

### 새 이슈 (기존에 없던 것)
- **M-1** (`PlayerBrain` line 75): `_bufferTimer` 감쇠 경로 dead code. 대시 진행 중 타이머가 감쇠되지 않음 (IsDashing=true 분기에서 else if가 먼저 처리). attackBufferTime=0.3s가 실질적으로 무의미. 현재 dashDuration=0.13s라 실 영향 없음.
- **L-1** (`PlayerAnimatorDriver.IsActionPlaying`): 미래 액션 상태에 "Action" 태그 누락 시 silent fail → 유예 0.12s 후 busy 해제(이동 누수). 에디터 경고 없음. 계약은 코멘트에만 존재.
- **L-2** (`WeaponBehaviour.Tick()` line 44): `_actionGrace -= Time.deltaTime` (scaled). ParrySlowMotion 중 유예가 실시간보다 길어짐. Animator도 같이 느려지므로 비율 유지 — 실 desync 없음. 단 슬로모 깊을수록 유예가 과도하게 길어질 수 있음 (timeScale=0.1이면 유예=1.2s 실시간).

### 메모 패턴
- 이 프로젝트에서 OnComboEnd 지연발화 경합 방어선(_lastAdvanceTime < 0.1f)은 비슷한 구조인 `_lastAdvanceTime` 시각 비교로 구현됨 — 제네레이션 카운터 방식이 아닌 Time.time 기반이라 정밀도 이슈 없음 (현재 프레임 내 발화 차이, 0.1s 관용)
- `ComboStep` 파라미터는 Integer (m_Type: 3), `Counter`/`Tumbling`/`Attack`은 Trigger (m_Type: 9)로 확인됨

**Why:** 이전 E 감사는 구조적 desync(낙관적 커밋)가 핵심. 이번 레일이 그 근본을 Animator 추종으로 교체.
**How to apply:** 다음 무기/액션 추가 시 Animator 상태에 "Action" 태그 누락이 이 레일의 가장 큰 회귀 위험. 새 무기 연결 체크리스트에 반드시 포함.
