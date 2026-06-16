---
name: retarget-foot-asymmetry-fix
description: One-foot retarget asymmetry (sole rolls on one side) = source FBX's cocked A-pose foot frozen as muscle-zero by CreateFromThisModel; fix = symmetrize that leg's T-pose bind
metadata:
  type: project
---

# 한쪽 발만 어긋나는 리타게팅 비대칭 — 근본원인과 수정법 (2026-06-16)

플레이어 Synty Sidekick(Starter_02) Walk 리타게팅에서 **왼발만 발바닥이 옆으로 구르는(roll)** 문제. 유저 Play로 "한쪽은 맞는데 한쪽이 안 맞아".

**대상:** `Assets/_Project/Animations/NewKatana/Walk_Loop_F_0.fbx`(클립 "Take 001"), `Walk_Combat_Loop_F_0.fbx`(클립 "AS_Walk_Combat_Loop_F_0_Seq"). 둘 다 Unreal 마네킹 본명(foot_l/foot_r, calf_l, thigh_l, ball_l). animationType=Human, avatarSetup=CreateFromThisModel(각자 자기 아바타).

## 근본원인 (확정, 기하로 증명)
소스 FBX의 **왼발이 A-포즈/바인드에서 ~50°+ 꺾여 있음**(오른발은 중립). `CreateFromThisModel`이 그 꺾인 왼발을 muscle-zero(휴머노이드 기준 포즈)로 구워버림 → 리타게팅 모든 프레임이 그 상수 오프셋을 운반 → 타깃(Starter_02)에서 왼쪽 발바닥이 스트라이드 내내 일정하게 구름.
- 검증: 스켈레톤 바인드(T-pose) 모델공간 발 up 벡터 — R foot up=(0,-1,0) 깨끗 / L foot up=(0.21,0.75,0.62) 50°+ 꺾임.
- **클립 자체는 정상**(자기 native rig에 SampleAnimation하면 양발 대칭·평평). 오직 리타게팅에서만 드러남.
- **타깃 아바타(Starter_02)는 무죄**: 같은 타깃에 Jorjouto Walk 리타게팅하면 양발 완벽 대칭(L/R sole roll x ±0.04). 소스 쪽 결함.

## 진단 기법 (재사용 — 소스냐 타깃이냐 가르는 결정적 테스트)
1. 타깃 rig에 의심 클립 + **known-good 클립**(다른 스켈레톤이어도 Humanoid면 됨) 둘 다 SampleAnimation → known-good이 대칭이면 타깃 무죄=소스 결함.
2. 의심 클립을 **자기 native source rig**(FBX 프리팹 인스턴스화)에 SampleAnimation → 거기서 대칭이면 클립 커브는 정상=아바타 바인드 결함.
3. 척도 = **발 local-up.x를 캐릭터 루트 기준으로** (`root.InverseTransformDirection(foot.up).x`). 평평한 발바닥이면 ≈0, 옆으로 구르면 절대값↑. 스트라이드 내내 *상수 오프셋*이면 리타게팅/바인드 아티팩트(진짜 보행 roll은 게이트와 함께 진동).
4. ⚠️ euler 직접 비교는 함정 — 좌/우 본 로컬 rest 축이 미러라 오염됨. up 벡터 또는 delta-from-rest 각도로 봐라.
5. ⚠️ `HumanPoseHandler.GetHumanPose`는 edit모드에서 **rest pose만 반환**(SampleAnimation이 루트를 안 구동) — muscle값 읽기 헛수고. 본 transform은 갱신됨(SkinnedMesh 표면만 안 갱신 = 이 프로젝트 알려진 함정).

## 수정법 (검증됨)
소스 FBX의 **ModelImporter.humanDescription.skeleton[]** (= T-pose 바인드)에서 결함 다리 체인을 반대편의 모델공간 미러로 교체.
- ⚠️ **naive 컴포넌트 미러 (x,-y,-z,w)를 로컬 회전에 직접 적용 금지** — Unreal 좌우 본은 깨끗한 X-반사 관계가 아님(thigh는 ~180° 롤). 3번 시도해 발이 뒤집힘.
- ✅ **정답 = 모델공간(FK)에서 미러 후 로컬로 역산.** 체인을 위에서 아래로(thigh→calf→foot) 처리, 각 좌측 본의 target_model = mirrorX(대응 우측 본 current_model), 그 다음 `newLocal = inverse(parent_model) * target_model`. 부모 갱신을 반영해 FK 재계산하며 진행.
- 작업 좌표계 일관성 필수: 전부 skeleton[] 바인드 프레임에서. 프리팹 localRotation(A-pose)과 skeleton[](T-pose)은 **다른 프레임** — 섞으면 ~180° 깨짐.

## 결과 (정지캡처 검증, 최종 모션판정=유저 Play)
- 왼발 sole roll x 최대: **0.32(원본) → 0.20(발만) → 0.17(다리체인 전체)**. R=0.10에 수렴. 발바닥 방향 z도 -0.4~-0.75(오배향)에서 +0.83~1.0(평평·아래)으로 교정.
- 잔차 L0.17 vs R0.10 = 미세. 원천=소스 보행 자체의 약한 비대칭 + 안 건드린 twist본(calf_twist_01_l/thigh_twist_01_l, upper-leg-twist 기준). twist본은 불안정 위험 커서 의도적으로 안 건드림(수익 적고 재파손 위험 큼).
- 발 컨택은 그대로(SmashHit식 이벤트 무관). toe는 언매핑 유지(부감 15~20m서 안 보임=미적 무관, 유저 사전 합의).

## 함정 메모
- NewKatana 폴더는 **git 미추적(??)** — 실패 시 git revert 불가, 원본값을 미리 로깅해 복원해야 함. (원본 좌측 바인드 quat: thigh_l(0.140,0.024,-0.212,0.967) calf_l(-0.043,0.043,0.335,0.940) foot_l(-0.011,0.004,0.115,0.993))
- RunCommand에서 **System.Reflection으로 AvatarSetupTool 접근 시 하니스 즉사**(NRE) — Enforce T-Pose를 스크립트로 못 부름. 그래서 skeleton[] 직접 미러로 동등 효과를 냄.
- 매 humanDescription 쓰기 후 `imp.SaveAndReimport()` 필요(디스크 영속화). 토우 언매핑 import warning은 무해한 노이즈.
- 검증 척도/기법은 [[project_animation_inplace_gotchas]]와 같은 "MCP 정지캡처 한계" 계열.
