---
name: project-player-dash-rootmotion
description: 플레이어 대시를 코드 속도버스트→Step 클립 루트모션으로 전환 — 거리=클립 정확, 8방향 클립선택, i-frame 창 트래킹
metadata:
  type: project
---

플레이어 대시(회피)를 코드 속도버스트(dashSpeed×dt≈5m)에서 **Frank Step 클립 루트모션 구동**으로 전환(06-20). 거리=클립 정확(유저 명시 요구).

**Step 클립 루트모션 실측(averageSpeed×len, BakeRot ON 후 불변 검증):**
- `Step_F`(guid 5acbe88aa9dc5c14787617a5f8cd173e): 0.417s, +Z 4.485 m/s → **1.869m** 전진
- `Step_B`(64cf2bad0925cc84492c94df8dbc8aea): 0.417s, −Z 3.364 m/s → **1.402m**
- `Step_L`(ddcd63843efc5f24a99a16f01f51c487): 0.417s, −X 3.364 m/s → **1.402m**
- `Step_R`(df099cf69b9198047afa2f242539229c): 0.417s, +X 3.364 m/s → **1.402m**
- (옛 velocity 대시 ~5m보다 짧은 도약스텝 — 의도된 짧은 회피)

**아키텍처(공격 루트모션 패턴 정확히 미러):** 위치=PlayerMotor 단일소유. 비주얼 Animator는 자식.
- `PlayerMotor`: 대시중 `Tick`은 타이머만 감쇠하고 위치 양보(공격 locked와 동일). `StartDash`가 대시방향을 **facing(=aimDir) 프레임에 투영·카디널 스냅**해 `_dashLocalX/_dashLocalY`(우+X/전진+Y) 산출 → 어느 Step 클립인지. `UpdateDash`/`dashSpeed` 제거.
- `ApplyRootStep`(공격·대시 공용): 대시창엔 이게 위치 무버(클립 deltaPosition 적용, WallGuardedStep 통과). `_dashAppliedThisFrame` 가드=프레임당 1회 적용(이중쓰기 방지). 벽에 막혀 변위 96%↓면 대시 조기종료.
- `PlayerAnimatorDriver`: `OnAnimatorMove`가 `_attacking || IsDashing`일 때 `ApplyRootStep` 피드(기존엔 `_attacking`만). 대시 **시작 엣지**(`DashStartedThisFrame`)에 `DashX/DashY` 1회 잠금(진행중 고정=한 동작 한 클립). `Dash` bool 엣지로 상태 진입/복귀.
- `PlayerMotor.IsDashing` 보존(orchestrator 잔상 VFX가 폴링).

**i-frame/busy 창=클립 길이 트래킹:** `dashDuration` 의미 변경 — "거리"가 아니라 **i-frame+이동잠금 유지 시간**(=Step 길이 0.42s). 이 창 전체 `IsInvulnerable`. 거리는 클립이, 창은 타이머가 소유.

**컨트롤러(KatanaMelee.controller):** Dash 상태 motion=Evade_F 단일 → **2D SimpleDirectional 블렌드트리**(DashX/DashY, 4 Step 클립 F@(0,1)/B@(0,-1)/L@(-1,0)/R@(1,0)). 카디널 스냅이라 항상 1클립 100%=블렌드 뭉갬 없음. Any→Dash(Dash If true, dur0 CUT), Dash→Locomotion(Dash IfNot, exit0.9 + 무조건 exit0.95, dur0 CUT). 플레이어 self-cancel canon=하드컷 허용.

**Step 클립 import(공격과 매칭):** C# API `lockRoot*`=Bake Into Pose 토글. lockRootRotation=ON(BakeRot, facing 코드소유)·lockRootHeightY=ON(grounded)·**lockRootPositionXZ=OFF**(XZ 루트모션=대시거리 보존). loopTime=OFF. ★BakeRot ON 후 XZ 거리 불변 검증완료(translation은 안 바뀜, 프레임만). clipAnimations in-place 변형(rebuild 금지=길이팽창)로 안전 reimport. rig 미스매치 경고=표준 비치명.

**방향→클립 직관:** 비주얼이 aimDir 향한 채 Step_F(+Z local) 재생 → 루트모션이 aimDir로 회전 = 전진 회피. Step_L(−X local)=몸기준 좌측 회피. 클립선택이 방향을 인코딩, facing은 조준 고정 = 8방향 다 가능(조준 보며 회피).

**미해결/게이트:** orchestrator의 `PlayerAfterimage.cs` 중복정의(Scripts/ + Scripts/Player/ 두 파일)로 게임 어셈블리 컴파일 에러 3건 — 내 코드 아님, orchestrator 도메인(내 IsDashing API는 보존). 모션 속도감/거리 손맛=유저 플레이 게이트. Stab는 orchestrator가 돌림.
