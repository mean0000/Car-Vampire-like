---
name: frank-native-visual-swap
description: 임시 Vexa 비주얼을 Frank_Katana_Skin 네이티브 캐릭터로 교체 — Frank는 자체 스키닝된 카타나 보유(Vexa와 다름), 재포인트 4개·FX메시 비활성 함정
metadata:
  type: project
---

플레이어 비주얼을 임시 Vexa(Generic→Humanoid 리타겟)에서 **Frank_Katana_Skin.FBX 네이티브**로 교체(2026-06-21). 씬 `Assets/_PlayerStackTest.unity`. 로직 스택(PlayerBrain/Motor/Aim/Health/입력)은 무변.

**Why:** 리타겟 아티팩트 0 + 정합↑. Frank avatar=`Frank_Katana_SkinAvatar`(isValid·isHuman). KatanaMelee.controller(우리 전투 로직)는 그대로 얹힘.

**How to apply (재현 시):**
- **★Frank는 카타나를 자체 보유** — `Sword_Mesh`(SkinnedMeshRenderer)가 `Weapon_Sword` 본(오른손 `R_Hand_Weapon` 아래)에 스킨됨. 스윙 시 손과 함께 움직인다. → Vexa처럼 BakeMesh로 정적 칼 만들 필요 **없음**([[project_katana_mesh_attach_vexa]]는 Vexa 한정 회피책이었다).
- **weaponAnchor = `Weapon_Sword` 본**(블레이드 오리엔트·손에 위치). 칼끝은 `Weapon_Sword` 로컬 -X 방향(blade bounds: hilt +0.33 → tip -0.87). BladeTip은 Weapon_Sword 자식 localPos `(-0.9,0,0)`(Vexa의 -0.94와 동형).
- **재포인트 4(모두 비주얼 자식 가리킴):** ①KatanaWeapon.weaponAnchor ②PlayerAttackVfx.weaponAnchor → 둘 다 Weapon_Sword. ③WeaponTrailController.trail → 신규 BladeTip TrailRenderer(설정: time0.25/startWidth0.15/endWidth0/minVtxDist0.02/View/Stretch, mat=`Assets/_Project/Materials/WeaponTrail.mat`). ④PlayerAttackVfx.comboSet=`Katana_Cham_ComboAttackSet`(데이터 SO — 비주얼 무관, 유지).
- **비주얼 자식 컴포넌트 3:** Animator(avatar=Frank, ctrl=KatanaMelee, applyRootMotion=false[직렬화]·updateMode Normal·culling CullUpdateTransforms) + PlayerAnimatorDriver + PlayerAfterimage. ★PlayerAnimatorDriver.Awake()가 applyRootMotion을 런타임에 true로 덮는다(직렬화 false여도 무관). AnimationEvent(OnAttackHit/OnComboWindow/OnComboEnd/OnFootstep)는 Animator와 같은 GO의 PlayerAnimatorDriver가 받으니 드라이버는 반드시 Animator GO에.
- **★Frank FX 메시 비활성 필수:** `FX_Slash_R`·`Quick_Slash`·`R_Shash_FX`=데모 슬래시 메시(우리 자체 VFX와 중복). 안 끄면 ①데모 슬래시 중복 ②PlayerAfterimage가 GetComponentsInChildren<SMR>로 자동수집해 고스트에 구워짐. 유지=`Frank_Mesh_Unity`(몸)·`Mesh_Pants`·`Sword_Mesh`(손 칼)·`Sword_Case_Mesh`+`Sword_Dummy`(등 칼집, 코스메틱).
- **자동 발견 컴포넌트(수동 재포인트 불필요):** PlayerAfterimage(자식 SMR 자동수집)·PlayerAttackVfx/WeaponTrailController(GetComponentInChildren로 driver/weapon 탐색). 비주얼 자식에 있기만 하면 됨.

**검증:** 콘솔 에러 0(에디트+플레이), Animator isHuman·avatarValid·런타임 applyRootMotion=true, Layer0 클립 재생 중, 미싱 컴포넌트 0. 손맛/그립 미세정합=유저 플레이 게이트.
