---
name: lab-movement-knobs
description: 룩 랩 이동 속도 노브 — 플레이어 걷기/질주 2단, 적 접근속도. 걷기<적<질주 관계. Run_RM 네이티브 속도 실측
metadata:
  type: project
---

# 룩 랩 이동 속도 노브 (2026-06-13)

유저 직접 플레이 피드백: "적 이속이 플레이어 걷기보단 빠르고 전력질주보단 느렸으면."

**플레이어** (`LabPlayerController.cs`, 룩 랩 전용 — 게임 PlayerController와 무관):
- `walkSpeed` = 5.5 m/s (기본)
- `sprintSpeed` = 9.0 m/s (Shift 홀드, 좌/우 Shift)
- 입력 폴링 Update(`_sprinting`), 소비 FixedUpdate. SmoothDamp 가감속(accelTime 0.08).

**적 접근** (`CaniathroxChaser.cs`):
- `approachSpeed` = 7.0 m/s. **걷기(5.5) < 적(7.0) < 질주(9.0)** — 걸으면 따라붙고 질주해야 떨군다.
- 구현: Run_RM 네이티브 **4.0942 m/s**(2.4565m / 0.600s, Animator 스텝 실측)에 배율. `modelAnimator.speed = approachSpeed / RunNativeSpeed` 를 **Approach 상태에서만**. ★Update 맨 위 매 프레임 `speed=1f` 단일 리셋 후 Approach에서만 올림(배율 누수 방지). 발도 비례 가속이라 미끄러짐 최소.

전부 시작 노브값 — 유저 플레이 튜닝 대기. [[caniathrox-attack-statemachine]]
