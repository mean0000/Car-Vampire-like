---
name: frank-katana-kit-and-vexa-rig
description: Frank Slash Pack 카타나 클립 인벤토리·3변형(In_Place/Root_Motion/8Way) + Vexa는 Generic(Humanoid 클립 못 얹음) vs Sidekick은 이미 게이트 통과 — 카타나 슬라이스 rig 결정
metadata:
  type: project
---

# Frank 카타나 킷 + Vexa/Sidekick rig 상황 (2026-06-18 계획 조사)

ZombieCrush가 조준형 로그라이트 뱀서로 피벗, 카타나=첫 수직 슬라이스. 플레이어 애니 스택 재구성 계획 시 디스크 실측.

## ★rig 결정 (가장 큰 판정 포인트)
- **Frank 카타나 클립 = 전부 Humanoid** (`animationType: 3`, 완전한 human[] 매핑, 언리얼 마네킹 본명 pelvis/calf_l/foot_l/hand_l + ik_foot_* + Weapon_Sword/Weapon_Blade 본). 공유 아바타 guid `c544be3651b5d0442a6ed780add0aa36`(avatarSetup=2 CopyFromOtherAvatar, FBX sub-asset fileID 9000000, 독립 .asset 아님).
- **Humanoid 클립은 Humanoid 타깃에만 얹힌다(muscle-space).** 클립이 본 커브가 아니라 머슬 커브를 운반 → Generic 리그엔 원리적으로 못 올림.
- **Vexa(`Vefects_Vexa.fbx`) = Generic** (`animationType: 2`, human[]·skeleton[] 빈 배열, avatarSetup=0, 아바타 없음). Frank 쓰려면 **반드시 Humanoid 재임포트+Avatar 생성(CreateFromThisModel)** 필요. Vexa 자체 데모 애니(SwordSlash_Vexa 등)는 안 씀 → Humanoid 변환해도 무관(서로 독립).
- **Sidekick(Starter_02)은 이미 Humanoid + Frank 리타겟 게이트 PASS**([[frank-slash-retarget-gate]]). 즉 Vexa는 애니 관점 한 발 뒤. **Sidekick으로 슬라이스 완성 후 룩만 메시 교체(Humanoid끼리 컨트롤러 그대로 호환)가 작업량 최소** — 유저 확인 대상(Q0).

## Frank 카타나 클립 인벤토리
경로: `Assets/Frank_Slash_Pack/Assets/Animations/Frank_SlashPack_Katana/FBX_Animation/`. 클립은 **FBX 내부 sub-asset**(.anim 파일 아님 — Glob `*.anim` 0건). 도큐: `Documentation/Frank_Katana_Motion_List.txt`(125 Motion+8 Velocity).
- **3 변형 폴더:** `In_Place/`(XZ 스트립, keepOriginalPositionXZ=0) · `Root_Motion/`(루트 전진 살아있음) · `Root_Motion_8Way/`(8방향 속도 RM). **★톱다운=In_Place** (위치는 코드 소유, [[katana-player-controller]] applyRootMotion=false).
- **스탠스 3종:** Stance1/2/3 각각 Idle + 전이(Stance1_to_2/3, Stance2_to_1 등). 카타나는 자세별 다른 공격 세트.
- **공격:** S1/S2/S3 × Attack01/02/03 (단발) + Combo: **통짜**(S1_Combo01_All 128프레임 3타) **또는 분리 3클립**(S1_Combo01_01/_02/_03). S1Combo 1~3, S2Combo 1~2, S3Combo 1.
- **스킬:** S1/S2/S3 Skill01/02 (광역·특수).
- **로코모션:** 8Way Walk/Run F/B/L/R/FL/FR/BL/BR (+ S2 변형, GuardWalk 변형). Run01/02, Walk, Walk_Faster, Step_F/B/L/R, Evade_F/B/L/R, Tumbling_F/B/L/R.
- **피격/사망:** Hit01/02/03, Hit_Knockback/Knockdown(+Loop), Getup01/02, Die01/02/03. + 별도 `Frank_SlashPack_Critical_Skills/Damages_*`(High/Mid/Low/Strong/Stun/Wall/Air/KO/Parrying/Bound, Damages_List.txt).
- **장비:** Stance1_Equip, Unequip(_Idle/_Run). Jump_01/02 + Jump_ZeroHeight.
- **부족 클립 0** — 카타나 슬라이스에 필요한 모션 다 있음. **단 발도(iai/draw-cut) 전용 클립 없음** — 가장 가까운 건 Step_F(전진)·S2_Attack01(찌르기성). 발도 돌진은 코드(MoveLungeStep)가 전진하므로 클립은 **제자리 찌르기/베기로 읽히는 짧은 모션** 필요(이동 클립 쓰면 코드 순간이동과 충돌).
- **무기별 동일 네이밍 컨벤션** = AnimatorOverrideController 1:1 매핑 용이(Katana↔GreatSword↔Spear 등, GreatSword 모션 리스트 확인됨). **단 Frank는 근접 위주 — 권총/드론 총기 모션은 별도 에셋 조달 필요(미확인).**

## 카타나 콤보 = 모션 분기 아님 (메커닉 실측)
`KatanaController.cs` 콤보(참격)는 **쿨 단축(공속 가속)** 시스템 — 5단까지 *같은 평타*가 빨라질 뿐 다른 모션 분기 아님. 거합 평타도 단발. → 애니 매핑은 **평타1·발도1·참격파1 = 3클립**으로 단순화 가능(통짜/분리 콤보 클립 불필요). 단 "콤보=같은 베기 가속" 확정은 유저 판정(Q2).

## 현행 스택 상태 (재구성 출발점)
- `PlayerLocomotionAnimator.cs` = twin-stick 드라이버 완성형: applyRootMotion=false(Awake), facing(조준)≠movement(이동)을 조준 프레임 투영(MoveX 우측/MoveY 전방/Speed idle0~run2), 무기별 스탠스 스왑(ApplyStance, 블렌드값 보존 무끊김), reload+firing 상체레이어, dash 베이스레이어. **카타나 베이스 컨트롤러 템플릿 = 이거.**
- `KatanaController.cs` = 순수 C# 헬퍼(PlayerCombat 소유, Animator GO에 안 붙음 → AnimationEvent 릴레이 필요). 트리거 계약 KatanaLight/Lunge/Wave. **★2026-06-16 카타나 공격 애니·슬래시 VFX 전부 제거됨(메커닉만, 라인 328·367·497·517 "비주얼 없음"). 이번 작업=Frank 클립으로 공격 상태 재도입.**
- 타격=현재 코드 즉시 발동(입력→SwingFan). 애니 재도입 시 평타 정점 AnimationEvent로 옮길지(동작=타격, 윈드업 지연=손맛 변화) vs 즉시 유지(반응 빠름)는 유저 판정(Q4, 단발이라 규약 강제 아님). 통짜 콤보 클립 쓰면 다단=이벤트 필수([[animevent-fire-timing]]).

## AnimatorOverrideController = 무기 "바로바로" 정답 (조건부)
- Override는 **상태 추가/삭제 못 함** — 베이스 슬롯 클립만 교체. 총 "장전" 상태가 베이스에 없으면 못 만듦.
- **해결: 베이스를 superset 또는 2-tier.** 근접 베이스(카타나·대검: Loco+Dash+Attack1/2/3)와 원거리 베이스(권총·드론: Loco+Dash+Fire+Reload레이어) 분리, Loco/Dash 구조만 공유. ApplyStance가 이미 rifle/pistol 별도 컨트롤러 스왑 = 이 패턴과 일관. ★blendParameter는 반드시 Float([[blendtree-param-must-be-float]]).
