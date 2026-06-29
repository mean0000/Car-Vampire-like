---
name: measure-rootmotion-by-stepping
description: generic rig 루트모션 실측은 Animator.Update 스텝뿐. 정적 커브 읽기·"_RM" 이름·옛 주석을 신뢰하지 마라
metadata:
  type: feedback
---

루트모션 거리/높이를 알아야 하면 **반드시 Animator를 실제로 돌려 transform delta를 측정**한다. 정적 `AnimationUtility.GetEditorCurve(RootT.z)`, 클립 이름의 "_RM" 접미사, 옛 코드 주석의 수치를 진실로 믿지 마라.

**Why:** generic rig에서 RootT 커브가 0으로 읽혀도 Animator는 실제 루트모션을 만든다(반대로 "_RM" 이름인데 루트모션 0인 클립도 있음 — Caniathrox JumpBite_RM). 정적 읽기/이름/옛주석을 믿고 만든 게 프로젝트 사고 #2(코드 포물선이 클립 도약을 덮어씀, "모션 없이 떠오르는 점프"). 제2원칙: 클립이 진실, 측정으로만 확인.

**How to apply:** MCP RunCommand로 (1)프리팹 인스턴스 (2)단일클립 임시 AnimatorController (3)applyRootMotion=true·cullingMode=AlwaysAnimate (4)Rebind→Update(0)→루프 Update(1/60f) (5)transform.position delta 누적. System.Reflection 금지(하니스 즉사), 디스크 캡처로 시각검증. [[caniathrox-clip-kit]]에 실측표.

**★휴머노이드 클립 = `MotionT.z`가 forward 드라이버, `RootT.z` 아님 (2026-06-29 측정).** Combo1 전진 스텝인서 핸드오프가 정적커브 보고 "RootT.z가 전진구동"이라 단정했으나, 클론에 ramp ADD→측정: RootT.z=net 0(inert)·**MotionT.z=net 0.4282**(=진짜 레버). ★에디트모드 `Animator.Update`는 휴머노이드 루트모션을 transform에 미적용 → 측정도구는 `clip.SampleAnimation(go, t)` 후 `go.transform.position`(=런타임 deltaPosition 적분 등가). 정적 RootT/MotionT 커브 값으로 "어느 게 forward냐" 추측 금지 — SampleAnimation로 ADD-test 해서 어느 커브가 sampled root를 움직이나로 판별. [[project_katana_combo_retimer]] §06-29.

**★스텝핑은 *전이 발화*도 검증한다 (avatar 불요·플레이모드 불요, 2026-06-26):** 빈 GameObject+Animator+컨트롤러(아바타 없어도 됨 — 상태머신은 파라미터/시간으로 평가)에 `runtimeAnimatorController=ac; Update(0)` 후 `SetTrigger("X")`→`Update(dt)` 루프, 매 스텝 `GetCurrentAnimatorStateInfo(0)`의 IsName/normalizedTime을 읽으면 **트리거·ExitTime·transition.offset 전이가 실제로 발화하는지** 결정적으로 확인된다(상태 바뀌는 스텝 로깅). cycleOffset+exitTime 깨짐, "Strike가 Loco로 빠져나오나"(소프트락 여부), 홀드 freeze normalizedTime 전부 이걸로 잡았다. ★주의: 에디트모드 Update는 *전이/시간*은 정확하나 **AnimationEvent 발화는 신뢰 불가**(런타임 기능) — 이벤트는 클립 events 시간이 range 안인지(GetAnimationEvents)로 따로 확인. ★`Object.DestroyImmediate(go)`로 임시GO 정리(씬 dirty 신경, 저장 안 함). 로컬함수+continue 패턴은 RunCommand 리라이터가 NRE 내니 인라인 루프로.
