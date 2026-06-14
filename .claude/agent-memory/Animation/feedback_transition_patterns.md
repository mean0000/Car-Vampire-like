---
name: transition-patterns
description: 자연스러웠던/위험했던 전이 패턴 — CUT vs 블렌드 경계, 비루프 로코모션 자기루프 함정
metadata:
  type: feedback
---

상태머신 전이 설계 원칙(검증된 것):

**블렌드(crossfade) 허용 = 로코모션 이음새 단 한 곳:** Idle↔Run 같은 속도 이음새만 짧은 블렌드(0.10~0.15s). 그 외 전부 CUT(duration=0).
- **Why:** 제0원칙 — 정체성 동작(공격·도약·물기·스핏) 재생 중 다른 클립이 섞이면 "애니 도중 다른 애니"(유저 명시 금지, 사고#1). 검증질문 "이 프레임에 두 클립 섞이나"가 곧 합격선.
- **How to apply:** 정체성 동작 진입 전이는 dur0 컷 또는 ExitTime 후 컷. 검증은 캡처 프레임에서 `anim.IsInTransition(0)==False` 확인.

**비루프 로코모션 클립 함정:** "_RM" run/walk 클립이 isLooping=False면 한 번 재생 후 얼어붙어 루트모션 정지(Caniathrox Run_RM=2.46m 후 멈춤). 접근/순찰을 지속하려면 **상태 자기루프 전이**(ExitTime≈0.98, dur0, 지속조건)로 클립을 재시작.
- **Why:** 벤더 클립 import의 loopTime을 켜면 원본 에셋 수정(금지). 자기루프 전이가 에셋 안 건드리고 로코모션을 지속시키는 정석.
- **How to apply:** 로코모션 상태에 self-transition 추가, 공격 트리거 전이를 자기루프보다 위 순서에 둬 우선권 보장(트리거 즉시 발동, 자기루프는 ExitTime에만).

**WriteDefaults=true 유지**(상태 간 본 포즈 누수 방지, 단일 레이어 풀바디면 안전). [[caniathrox-attack-statemachine]]

**한 클립을 두 상태로 SPLIT = "부분만 배속"의 유일한 정석 (Dimax 클로월 2026-06-14):** 유저가 "잘라내지 말고 *뒷부분만 빠르게 재생*"을 원할 때. 한 state.speed는 클립 전체 균일이라 한 상태로는 불가 → 같은 take에서 두 ModelImporterClipAnimation(frame 범위만 다름, 예 Swing 0~22 / Recovery 22~35)을 만들어 각 상태에 다른 speed(1.0 / 3.0)를 준다.
- **Why:** ①트림(끝 잘라내기)은 동작을 *버려* 루트모션 거리손실+시간단축으로 같은 speed인데 전체가 빨라짐(유저 "회수만"과 어긋남, Dimax v5 3.94m/s 사고). split은 *전부 재생*하되 회수 구간만 압축 → 거리 100% 보존(Swing+Recovery 루트모션 합=풀클립). ②cycleOffset로 한 상태가 뒷부분만 재생은 불가(비루프 클립은 end-frame 서브레인지 개념 없음) — sub-clip import가 유일하게 깔끔.
- **연속성 보장:** 같은 take라 Swing.lastFrame == Recovery.firstFrame = *비트-동일 포즈* → Swing→Recovery 전이가 CUT(dur0, ExitTime~0.99)여도 포즈 점프 0. crossfade가 아니라 한 동작의 분할이라 제0원칙 위반 아님(검증: enter-to-enter 루트Z 연속 + IsInTransition 순간만).
- **How to apply:** 분할점 = 실측(SampleAnimation 손/무기 본 전방 reach가 peak 지나 중립으로 *귀환 시작*하는 frame — 팔로스루 끝 경계). 이벤트(히트)는 *타격이 든 쪽 sub-clip*에 정규화 재계산(컨택절대frame/분할frame수). 빌드스크립트 const(SplitFrame·두 speed)에 박아 durable. 속도점프(1.0→3.0 경계 "탁")는 정지캡처 판정불가 → 유저 ▶(어색하면 RecoverySpeed↓ 또는 분할점↑).
