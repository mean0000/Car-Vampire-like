---
name: pounce-grammar
description: 포식자 "모았다가 팍 달려들기" 3박자 연출 문법 — 응축(느린 별도클립)+발사(빠른 Y억제 돌진)+ExitTime 자동연결. 위로뜨는 Y가 "개구리"의 정체
metadata:
  type: feedback
---

포식자 도약을 "개구리 폴짝"이 아니라 "모았다가 팍 달려들기(pounce)"로 만드는 3박자 문법:

**1. 모았다가 (Anticipation)** = 짧은 in-place 응축 클립을 **느린 state speed**(예 0.6)로 재생. 제자리(루트모션 0)에서 무게 모으는 웅크림. 도약 클립의 첫 ~16% 구간(발사 직전 코일 자세)을 잘라 만들면 됨(별도 Roar 등 안 끌어와도 도약 시작이 곧 충전 자세).
**2. 팍 (Explosion)** = 응축(느림)과 발사(빠른 state speed 예 1.3)의 **타이밍 대비**가 "팍"을 만든다. 같은 클립 내 speed 변조 아님 — 별개 상태의 state speed 차이. 정적 상수라 코드가 매프레임 안 긁음(헌법 안전).
**3. 낮게 깔린 돌진 (Low travel)** = 발사 클립의 **위로 뜨는 Y를 import에서 bake 억제**(lockRootHeightY), 전진(Z)만 보존. ★위로 뜨는 Y 포물선이 "개구리"의 정체 — 4족 포식자 도약은 높이 안 뜨고 앞으로 덮침.

**연결**: 응축→발사는 **ExitTime 1.0 무조건 CUT**(드라이버 트리거 아님). 응축이 끝나는 순간 자동 발사 → "모았다가 팍"이 끊김 없는 한 호흡. 드라이버는 진입 트리거 1발만(가는 곳이 발사 대신 응축으로 바뀜, 로직 0변경).

**Why:** 유저 직접 플레이 피드백(2026-06-13) "점프가 개구리처럼 폴짝, 모았다가 팍 달려드는 느낌으로". Jump_RM 실측 진단=①위로뜨는 대칭 Y포물선(정점 0.278m@0.333s) ②윈드업 없이 균일가속(팍 대비 부재) ③착지 후 40% 질질끄는 꼬리. 셋 다 "개구리"의 원인.
**How to apply:** 다른 종 도약/돌진 연출에도 재사용. 응축=짧은 in-place 느린클립, 발사=빠른 Y억제 돌진, 연결=ExitTime CUT. 전부 클립/상태/import 레벨(코드가 위치·포즈 안 만듦). 검증=Animator 풀시퀀스 스텝으로 IsInTransition=False(클립 안 섞임)·발사 maxY 0 확인. 모션 느낌·speed값 최종판정=유저 플레이. [[caniathrox-attack-statemachine]] [[caniathrox-clip-kit]] [[transition-patterns]]