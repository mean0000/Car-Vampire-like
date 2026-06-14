---
name: measure-rootmotion-by-stepping
description: generic rig 루트모션 실측은 Animator.Update 스텝뿐. 정적 커브 읽기·"_RM" 이름·옛 주석을 신뢰하지 마라
metadata:
  type: feedback
---

루트모션 거리/높이를 알아야 하면 **반드시 Animator를 실제로 돌려 transform delta를 측정**한다. 정적 `AnimationUtility.GetEditorCurve(RootT.z)`, 클립 이름의 "_RM" 접미사, 옛 코드 주석의 수치를 진실로 믿지 마라.

**Why:** generic rig에서 RootT 커브가 0으로 읽혀도 Animator는 실제 루트모션을 만든다(반대로 "_RM" 이름인데 루트모션 0인 클립도 있음 — Caniathrox JumpBite_RM). 정적 읽기/이름/옛주석을 믿고 만든 게 프로젝트 사고 #2(코드 포물선이 클립 도약을 덮어씀, "모션 없이 떠오르는 점프"). 제2원칙: 클립이 진실, 측정으로만 확인.

**How to apply:** MCP RunCommand로 (1)프리팹 인스턴스 (2)단일클립 임시 AnimatorController (3)applyRootMotion=true·cullingMode=AlwaysAnimate (4)Rebind→Update(0)→루프 Update(1/60f) (5)transform.position delta 누적. System.Reflection 금지(하니스 즉사), 디스크 캡처로 시각검증. [[caniathrox-clip-kit]]에 실측표.
