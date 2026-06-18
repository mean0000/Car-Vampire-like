---
name: vexa-humanoid-katana-base
description: Vexa Generic→Humanoid conversion + KatanaMelee.controller (param contract + 3-state combo + AnimationEvents) + foot-roll finding. Player katana base + combo built 2026-06-18.
metadata:
  type: project
---

# Vexa Humanoid + Katana melee base (built 2026-06-18)

임시 플레이어 캐릭터 = Vexa, 카타나 애니 = Frank_Slash_Pack. 코드 스택(`PlayerAnimatorDriver`·`KatanaWeapon`·구 `KatanaController`)은 오케스트레이터 소유 — Animation은 안 건드림(파일 파티션).

## Step1 — Vexa Generic→Humanoid 변환 (PASS, 구조 깨끗)
- `Assets/Vefects/Stylized Female Character - Vexa/Meshes/Vefects_Vexa.fbx` = 원래 Generic(animationType 2). `animationType=Human` + `avatarSetup=CreateFromThisModel` + SaveAndReimport로 변환.
- 생성 아바타 `Vefects_VexaAvatar`: **isValid=True isHuman=True**, 49 human bones(풀바디+손가락), 85 skeleton entries, 20개 core+key 본 전부 매핑(UpperChest·양 Shoulder 포함).
- **Vexa 골격명 = 자체 네이밍**("Base HumanPelvis"/"Base HumanLThigh") — Frank의 언리얼 마네킹(pelvis/thigh_l)과 **다른 스켈레톤**. 그래서 Humanoid muscle-space 리타겟이 필요하고, 그래서 작동함(둘 다 같은 humanName 머슬로 수렴).
- ★팔 자동매핑 함정: Vexa 팔=Upperarm1/2/3·Forearm1/2/3·**Palm**·digits. 오토맵 `Hand→Forearm2` 오인 → **수정=`LeftHand→Base HumanLPalm`, `RightHand→Base HumanRPalm`**.
- **Vexa 바인드 = 완벽 대칭**. NewKatana 왼발-꺾임 소스결함([[retarget-foot-asymmetry-fix]])은 Vexa엔 **없음**.

## Step2 — Frank 카타나 클립 리타겟 (PASS 구조 / 1 watch-item)
- Frank 클립 = 전부 Humanoid muscle clip(공유 Frank 아바타 guid c544be36). 리임포트 불필요 — Vexa 아바타 Animator에서 런타임 머슬 리타겟.
- ★★**In_Place 폴더엔 공격/콤보 클립 없음** — 공격·콤보·Evade는 **Root_Motion 폴더에만**. 톱다운 정답=Root_Motion 클립 쓰되 **player Animator applyRootMotion=false라 root커브 폐기→제자리 스트라이크**. 코드가 위치 소유([[katana-player-controller]]).
- ★**watch-item = idle 왼발 상수 roll** L≈0.20 vs R≈0.10. 출처=Frank 소스클립/공유아바타(Vexa 바인드 무죄). **미수정 — 유저 눈 판정 후 투자**. 45도/15m 톱다운서 안 보일 가능성 큼.

## Step3 — KatanaMelee.controller (파라미터 계약 + 콤보 3상태)
- 경로 `Assets/_Project/Animation/KatanaMelee.controller`.
- ★**파라미터 계약(코드 구동)**: `Speed`(float)·`MoveX`(float)·`MoveY`(float)·`Attack`(trigger, **미사용이지만 보존** — 구 코드 폴백)·`Dash`(bool)·**`ComboStep`(int, 2026-06-18 추가)**.
- **Locomotion**(default) = 2D FreeformDirectional 블렌드트리(px=MoveX py=MoveY). ★blendParameter Float 필수([[blendtree-param-must-be-float]]).
- **Dash** = Evade_F RM 단일 상태. AnyState→Dash `[If Dash]` CUT. (콤보 작업서 미터치 — AnyState엔 Dash만 남음.)
- ★★**콤보 3상태(2026-06-18)**: `Combo1`(S1_Combo01_01, 1.0s/60f)·`Combo2`(_02, 1.133s/68f)·`Combo3`(_03, 1.05s/63f). writeDefaults=false. **구 Attack 단일상태(S1_Attack01)는 제거**(AnyState→Attack도 제거). 전이 전부 **CUT(hasExitTime=false, dur=0)**:
  - `Locomotion→Combo1 [ComboStep==1]` · `Combo1→Combo2 [==2]` · `Combo2→Combo3 [==3]`
  - `Combo1→Loco [==0]` · `Combo2→Loco [==0]` · `Combo3→Loco [==0]`
  - 즉 코드가 ComboStep을 1/2/3로 올려 진행, 0으로 리셋해 복귀. Exit Time 없음=코드가 흐름 소유(셀프캔슬 플레이어 캐넌 [[player_self_cancel_canon]]). 헌법상 정체성 동작이지만 **플레이어는 self-cancel 허용**(몬스터 commit-lock과 다름).

## Step4 — AnimationEvent (콤보 3종 × 3클립, 2026-06-18)
- 각 콤보 클립에 **3 이벤트**: `OnAttackHit(int=0)`@타격정점 · `OnComboWindow()`@타격+~0.12s(캔슬윈도우 시작) · `OnComboEnd()`@~0.92(클립끝).
- **타격정점 = SampleAnimation 실측 RightHand hips-local peak**(아래 측정법). 임포트 결과(초):
  - Combo01_01(1.0s): Hit **0.367s**(norm0.367) · Window 0.483s · End 0.917s
  - Combo01_02(1.133s): Hit **0.233s**(norm0.206) · Window 0.367s · End 1.049s
  - Combo01_03(1.05s): Hit **0.216s**(norm0.206) · Window 0.350s · End 0.967s
  - ★Combo1은 윈드업 느림(타격 37%), Combo2/3은 스냅 후속타(타격 ~21%) — 후속타가 이전 모션 모멘텀 타고 빨리 착지(콤보 craft 맞음).
- 시그니처: `OnAttackHit(int hitFrameIndex)`(콤보 단발이라 0). messageOptions=**DontRequireReceiver**(릴레이 없어도 에러 안 남). 코드가 `OnAttackHit`/`OnComboWindow`/`OnComboEnd`+`SetCombo(int)` 추가 예정.
- 임포트 경로=`ModelImporterClipAnimation.events`+`SaveAndReimport`(durable, SetAnimationEvents는 reimport서 wipe). ★time에 **정규화값** 넣음→임포터가 ×length([[animevent-fire-timing]]).
- 비치명: Frank reimport마다 "Copied Avatar Rig mis-match"(hand_l/r 3-10mm) 경고 — Frank 팩 자체 avatar config 조건, 내 편집 무관.

## ★측정법 함정(2026-06-18, 신규) — 콤보 타격프레임 SampleAnimation으로만
- 목표: 칼 임팩트 순간 = 손/검 속도 정점 프레임. Frank 콤보 클립엔 events 0이라 직접 측정 필요.
- ❌ **SampleAnimation으로 비-휴머노이드 본(Weapon_Blade) 추적**=0 (Humanoid 클립은 49 머슬본만 운반, 무기본은 바인드 고정). → **휴머노이드 본(RightHand) 추적해야 함.**
- ❌ **edit-mode `Animator.Update(dt)`로 본 추적**=normalizedTime은 전진하지만 **본 트랜스폼이 안 써짐**(world pos 완전 고정). edit모드 Animator는 그래프만 평가, 스킨/본 flush 안 함([[editmode_capture_one_pose_per_invoke]] 동류).
- ✅ **정답=`clip.SampleAnimation(go, t)` 프레임마다**(호출당 1 정확 포즈). go=Vexa 인스턴스+Animator+Vexa아바타. **RightHand를 Hips-local로** 변환해 추적(루트/바디 드리프트 제거). 결과=깨끗한 단일 슬래시창(15-17 m/s peak, S1_Attack01의 21.4와 동급).
- ★MCP 함정 재확인: `AssetDatabase.DeleteAsset`·`CreateAnimatorControllerAtPath*` = 대화형 에러([[mcp-runcommand-interactive-trap]]). 측정용 임시 컨트롤러는 **in-memory `new AnimatorController()`**(디스크 경로 X)로 만들고 `DestroyImmediate`. 컨트롤러 편집은 LoadAssetAtPath+mutate+SetDirty+SaveAssets(delete-then-create 금지).
