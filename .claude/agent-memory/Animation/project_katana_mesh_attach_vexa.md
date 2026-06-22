---
name: katana-mesh-attach-vexa
description: Frank 카타나 메시를 Vexa 오른손에 장착한 방법·그립값·BladeTip 좌표·바이크 추출 레시피 (06-19 시공)
metadata:
  type: project
---

Frank 카타나 메시를 Vexa(Synty Sidekick rig) 오른손에 장착 완료 — 슬래시 VFX 트레일 선결 작업. 씬 `Assets/_PlayerStackTest.unity`.

**Why:** Vexa가 빈손으로 카타나 휘두름. 칼날 끝 transform이 있어야 스윙 궤적에 트레일을 붙임.

**무엇을 어디에:**
- 메시 = `Frank_Katana_Skin.FBX` 안의 **`Sword_Mesh`** SkinnedMesh(246 verts, 2bones=Weapon_Sword+Weapon_Blade). ★카타나는 캐릭터 스킨FBX에 서브메시로 들었고, `Sword_Mesh`만 쥔 칼(다른 후보=Sword_Dummy=칼집칼·Sword_Case_Mesh=칼집, 제외).
- standalone 카타나 FBX 없음 → **BakeMesh로 정적 추출**: 인스턴스화→`smr.BakeMesh`→정점을 `Weapon_Sword` 본 로컬공간으로 변환(`wSword.worldToLocalMatrix * smr.transform.localToWorldMatrix`)→저장 `Assets/_Project/Meshes/Frank_Katana_Static.asset`. 카타나=강체라 스키닝 불필요, 정적 MeshFilter/MeshRenderer로 충분.
- 부착 본 = `animator.GetBoneTransform(HumanBodyBones.RightHand)` = **`Base HumanRPalm`**(Synty Sidekick 네이밍, Hand→Palm 리맵). Frank 본은 `hand_r`(Unreal식)이라 이름 다름 — 절대 추측 금지.
- 재질 = `SG_Frank_Katana_Sword`, `SG_Frank_Katana_Blade`(submesh 순서대로).

**★그립 오프셋 = IDENTITY (localPos 0·localRot 0·scale 1).** Frank 바인드포즈를 Weapon_Sword 로컬공간에 구워서 팔름에 붙이면, Synty Humanoid 리타겟이 팔름 본을 Frank hand_r에 충분히 정렬시켜 **오프셋 0으로 그립이 정확**(주먹이 손잡이 감쌈·칼날 아래로·뜸/관통 0, 캡처검증). 튜닝 오프셋 추가 안 함(괜히 망침). ★이건 Sidekick rig 한정 — 다른 캐릭이면 재캡처 필요.

**BladeTip:** `Katana_Mesh` 자식, localPos **(-0.94, 0, 0)** = 칼끝. mesh 로컬 long-axis=X, 칼날은 -X(min -0.94)·손잡이는 +X(max 0.40). 트레일 앵커는 여기.

**★SampleAnimation 캡처 함정 재확인:** 에디트모드 `clip.SampleAnimation`으로 공격포즈 샘플하면 본은 움직이는데 **본에 부모된 자식(칼)의 렌더가 stale**(칼이 허공에 뜬 것처럼 보임=거짓). 검증=DATA로: 모든 frac에서 `katana.world == palm.TransformPoint(localPos)` match=True 확인했고 BladeTip이 스윙 호를 그림(tip world가 frac별로 광역 이동). 강체 자식은 런타임에 본을 매프레임 따라감(코드 0). [[feedback_editmode_capture_one_pose_per_invoke]]

**유저 플레이 게이트:** 쥔 모양 최종 미적·실제 애니 중 따라오는 손맛은 유저 눈. 구조 정합(손에 붙음·날 방향·칼끝 마커)은 검증 완료.
