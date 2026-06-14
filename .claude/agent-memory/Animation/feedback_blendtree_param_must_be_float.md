---
name: feedback-blendtree-param-must-be-float
description: AnimatorController 코드 빌드 시 BlendTree blendParameter는 반드시 Float. Bool로 만들면 런타임 "not float type" 에러 + 블렌드 무효. 두 번 물림.
metadata:
  type: feedback
---

코드로 AnimatorController를 빌드할 때 **BlendTree의 blendParameter(또는 blendParameterY)는 반드시 `AnimatorControllerParameterType.Float`로 AddParameter** 한다. Bool/Int로 만들면 런타임에 `BlendTree "X" uses parameter "Y" which is not float type` 에러가 나고 블렌드가 동작 안 한다.

**Why:** Simple1D/2D 블렌드 트리는 연속 가중에 Float 임계값을 쓴다. SetBool/SetFloat 호출 코드가 "되는 것처럼" 보여도(SetFloat은 Bool 파라미터에 무시됨) 블렌드 자체가 깨진다. ZombieCrush에서 두 번 물림: Caniathrox(원래 isRunning Bool→Float 교정), Dimaxillosaurus(컨트롤러 *재빌드* 때 에디터 스크립트가 isRunning을 Bool로 되돌려 회귀 — [[project_telegraph_driver_dimax]]). 오케스트레이터가 인스펙터로 Float 고쳐도 **에디터 빌드 스크립트가 Bool로 만들면 재빌드마다 회귀**한다 → 빌드 스크립트의 AddParameter를 Float로 고치는 게 진짜 수정.

**How to apply:** 상태머신을 코드(`DimaxillosaurusLabSetup` 등 에디터 빌더)로 (재)생성할 때, 블렌드 파라미터로 쓰는 이름(isRunning/speed/dir 등)은 AddParameter에서 Float로. 검증: 빌드 직후 `ctrl.parameters` 타입을 로그로 찍어 Float 확인 + 더미 prefab으로 Approach 블렌드를 Animator 스텝 돌려 에러 안 나는지 확인(정적 그래프 점검만으론 "not float" 런타임 에러를 못 잡는다). 트랜지션 *조건*(If isMoving)은 Bool로 둬도 무방 — 문제는 블렌드 트리가 *읽는* 파라미터만.
