---
name: parry-iframe-separation
description: 패링/i-frame 분리(옵션 A) QA — DashStartTime 기반 리셋 레이스·perfectDodgeWindow 상한 미제약·unscaledTime 주석 오류
metadata:
  type: project
---

## PlayerHealth 패링 분리(옵션 A) 구현 — 2026-06-21

### 배경
PlayerMotor에 dashCancelIframeScale(기본 0.5)이 추가되어 캔슬 대시 시 i-frame이 줄었다.
전 게이트 H-1(패링창이 i-frame 밖이면 보상·무적 없이 피격)·H-2(_parryFiredThisDash 리셋 실패)의 처방으로
패링을 IsInvulnerable 블록 밖(맨 앞) + 타이밍 기반(unscaledTime age)으로 분리했다.

### H-1·H-2 해소 여부
- H-1(캔슬 대시 패링 무음): 해소됨. 패링 분기가 IsInvulnerable과 완전 독립.
- H-2(_parryFiredThisDash 리셋 실패): 해소됨. DashStartTime 변화 감지 기반, i-frame 0이어도 동작.

### 신규 발견 이슈

**H-1(신규) — 리셋-판정 프레임 레이스 (★수정 필요)**
- PlayerHealth.Update에서 _parryFiredThisDash를 리셋하는 타이밍이 PlayerBrain.Update(→Motor.Tick → _dashStartTime 갱신)보다 먼저 실행되는 프레임에,
  새 대시 시작과 동시에 TakeHit(물리 콜백)가 오면 _parryFiredThisDash가 이전 대시의 true를 유지 → 패링 누락.
- Script Execution Order 미지정 상태.
- 수정: 리셋 로직을 Update에서 TakeHit 진입부로 이동(리셋+판정 원자적 실행).
  ```csharp
  // TakeHit 패링 블록 앞에 삽입, Update 리셋 블록 삭제
  if (_motor != null && _motor.DashStartTime != _lastDashStart)
  {
      _parryFiredThisDash = false;
      _lastDashStart = _motor.DashStartTime;
  }
  ```

**M-1 — perfectDodgeWindow 상한 미제약**
- perfectDodgeWindow > iframeDuration 으로 Inspector 설정 시 i-frame 종료 후에도 패링 발화 가능.
  DashInvulnerable 플래그(bool 고정)만 체크하고 _iframeTimer > 0 조건이 없기 때문.
- 수정: Awake에 경고 추가.
  ```csharp
  if (_motor != null && perfectDodgeWindow > _motor.IframeDuration + 0.01f)
      Debug.LogWarning("[PlayerHealth] perfectDodgeWindow > iframeDuration", this);
  ```
  (IframeDuration 프로퍼티가 없으면 추가 필요 또는 PlayerHealth에 별도 상한 Tooltip으로 처리)

**M-2 — DashStartTime 주석 오류**
- PlayerMotor.cs L71 주석: `마지막 대시 시작 시각(Time.time)` → 실제는 `Time.unscaledTime`.
  TakeHit와 다른 시스템이 Time.time과 비교하는 코드 작성 시 조용한 버그.

### 안전 확인
- 3분기(창 내 패링 / i-frame 꼬리 회피 / i-frame 후 피격) 기존과 동일 유지됨.
- 출처 분리(hitIframe vs DashStartTime): 정상 분리.
- 초기값(-999f) 처리: 정상.
- 대시당 1회 보장: 정상.
- unscaledTime 일관성(Motor·Health·ParrySlowMotion): 정합.
- ParrySlowMotion 구독 대칭, Restore() OnDisable: 정상.
- PlayerBrain Parried → ArmCounter 구독 대칭: 정상.

### 결론
Critical 0 / High 1(신규 레이스) / Medium 2 / Low 2
전 H-1·H-2 근본 해소됨. 신규 H-1 레이스는 2줄 이동으로 완전 제거 가능.
