---
name: anim-clip-wiring
description: Mixamo 클립 방향 실측 분류(averageSpeed/averageAngularSpeed), 아크 클립 Bake XZ 함정, 컨트롤러 스왑 파라미터 재주입 패턴
metadata:
  type: project
---

2026-06-13 무기 스탠스(PistolLocomotion) 세션 실측.

**Why:** Mixamo 팩 클립은 파일명으로 좌/우를 구분 못 하고("strafe" vs "strafe (2)"), 임포트 기본값이 프로젝트 인플레이스 컨벤션과 어긋난 채 들어온다. 감으로 배치하면 블렌드트리 좌우가 뒤집힌다.

**How to apply:**
- **방향 분류는 RunCommand 실측**: `AnimationClip.averageSpeed`(x부호: +=우) / `averageAngularSpeed`(+=시계방향=우회전, 단위 rad/s 추정 — 0.5~1.3 범위). 스트레이프 좌우는 avgSpeed.x로, 아크 페어 좌우는 각속도 부호로 판별. ⚠️아크 페어의 **선속도 평균은 좌우 판별에 못 씀** — 양쪽 아크가 같은 부호로 측정됨(권총 팩 실측: walk arc 둘 다 x음수, backward arc 둘 다 x양수).
- **아크 클립 Bake XZ 함정**: Pistol 팩 아크 8종 전부 `loopBlendPositionXZ: 1`(Bake Into Pose XZ ON)로 들어옴 — applyRootMotion=false에서 슬라이드+스냅백. 비아크 클립은 0. 팩 단위로 임포트 세팅 일괄 신뢰 금지, 클립별 grep 점검. 프로젝트 컨벤션: loopTime 1 / loopBlend* 0 / keepOriginalPositionY 1 / keepOriginalPositionXZ 1.
- **runtimeAnimatorController 스왑 = 파라미터 전부 리셋**: 스왑 직전 `GetFloat`(목표값 아닌 댐핑된 현재값)을 캡처해 직후 `SetFloat` 재주입. 실측: 재주입으로 S=0.908→0.828(정상 댐핑 1스텝)로 연속, 미재주입이면 0 스냅. bool 엣지 폴링(_wasReloading류)도 스왑 시 false로 리셋 동기화할 것.
- **컨트롤러 YAML 손제작은 기존 컨트롤러 복제가 안전**: RifleLocomotion 내부 fileID를 그대로 재사용(파일 내 유일성만 요구됨), guid·클립 참조만 교체. Unity 로드 시 params/layers/mask 검증은 RunCommand에서 `AnimatorController` 캐스팅으로 즉시 확인 가능.
- 무기→스탠스 매핑은 PlayerCombat의 기존 `_gunClass`(GunSfx.GunClass, ApplyRanged에서 ClassifyGun으로 갱신) 노출 한 줄이면 충분 — 새 enum 만들 필요 없음. [[playmode-verification-tricks]]
- **컨트롤러 상태 추가는 AnimatorController API가 YAML 손제작보다 안전 (2026-06-13 Fire 상태 실측)**: RunCommand에서 `LoadAssetAtPath<AnimatorController>` → `layers[i].stateMachine`(레이어 배열은 복사본이지만 stateMachine은 에셋 참조라 직접 수정됨) → `AddState/AddTransition/AddCondition` + SetDirty/SaveAssets. 클립은 `LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(a=>!a.name.Contains("__preview"))`로 — fileID 추측 불필요. **전환 우선순위는 `state.transitions` 배열 순서 = 평가 순서**: 조건이 부분집합 관계면(Reload→Empty가 Reload==false 단독, Reload→Fire가 +Firing) 구체적인 쪽을 배열 재할당으로 앞에 둬야 죽은 전환이 안 된다.
- **임포트 API 매핑**: ModelImporter에서 컨벤션 = `loopTime=true, loopPose=false(loopBlend), lockRootRotation/HeightY/PositionXZ=false(loopBlend*=Bake OFF), keepOriginalOrientation=false, keepOriginalPositionY/XZ=true` + `animationType=Human, avatarSetup=CreateFromThisModel`. `clipAnimations` 비어있으면 `defaultClipAnimations`로 시드 후 재할당.
- **발사 모션 구동 = IsFiringSustained 접근자**: `_burstShots > 0 && Time.time - _lastShotTime < 0.15f` — `_burstShots>0` 가드가 게임 시작 직후(Time.time≈0, _lastShotTime=0) 오탐을 막는다. 미존재 파라미터 SetBool은 콘솔 워닝 — 스탠스별 파라미터 차이는 스왑 시 1회 `animator.parameters` 스캔 캐시로 가드(매 프레임 조회는 배열 할당).
- **상태 배속 파라미터 배선 (2026-06-13 Dash 실측)**: `state.speedParameter="DashSpeed"; state.speedParameterActive=true` — speed multiplier를 파라미터로 거는 공식 API. AnyState 전환은 `sm.AddAnyStateTransition(state)` + `canTransitionToSelf=false` 필수(조건 bool이 수 프레임 true 유지되는 구동에서 자기 재진입 방지).
- **씬 더티 없이 프리팹 조립 = 프리뷰 씬 (2026-06-13 실측)**: 병렬 세션이 씬 소유 중일 때 `EditorSceneManager.NewPreviewScene()` → `ObjectFactory.CreateGameObject(previewScene, HideFlags.None, name, typeof(...))` + `PrefabUtility.InstantiatePrefab(prefab, previewScene)`(중첩 참조 보존) → `SaveAsPrefabAsset` → `ClosePreviewScene`. 열린 씬 isDirty에 일절 안 닿음. SerializeField 배선은 `SerializedObject.FindProperty(...).objectReferenceValue`(리플렉션 불요).
- **⚠️RuntimeInitializeOnLoadMethod(AfterSceneLoad)는 앱 기동 시 1회만 발화** — 런타임 씬 전환으로는 재실행 안 됨. 씬마다 재부착이 필요하면 SceneManager.sceneLoaded 구독이 필요(PlayerHandWeapon 부트스트랩에 이 한계 존재, 보고됨).
- **StartCoroutine 첫 반복은 동기 실행**: 스탠스 스왑 검증 코루틴이 첫 yield 전에 구 컨트롤러를 샘플링해 래치 오염(PistolLocomotion인데 hasFiringParam=True로 보임). 스왑 검증은 `yield return null` 한 번 뒤부터 샘플링할 것.
