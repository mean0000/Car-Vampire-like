---
name: player-anim-conventions
description: 플레이어 애니메이터 규약(Speed/MoveX/MoveY) + Mixamo 팩 임포트 컨벤션(Bake Into Pose 전부 OFF=인플레이스) — 2026-06-12 Ch44/RifleLocomotion 기준
metadata:
  type: project
---

플레이어 비주얼 = `Ch44_nonPBR`(유저 확정 기준 캐릭터, 2026-06-12), 구동 규약 = `PlayerLocomotionAnimator`가 float 3개만 쓴다: **Speed(0=idle/1=walk/2=run), MoveX/MoveY(조준 프레임 투영, 단위원)**. 발사/재장전/사망 애니 파라미터는 아직 코드에 없음(Pro Rifle Pack에 발사/재장전 클립 자체가 없음 — 알려진 갭).

**Why:** 컨트롤러를 새로 만들 때마다 이 규약에 맞추면 코드 무수정. PistolLocomotion → RifleLocomotion 모두 동일 패턴(루트 1D Speed, 자식 2D FreeformDirectional 8방향+센터 idle).

**How to apply:**
- Mixamo 팩 임포트 컨벤션(권총 팩 선례, 신규 팩 전체 적용됨): Humanoid+자체아바타, 로코모션/idle/홀드=loopTime 1, **loopBlend(Bake Into Pose) 전부 0**(applyRootMotion=false에서 인플레이스 조건), keepOriginalPositionY/XZ=1, keepOriginalOrientation=0. 클립명은 전부 "mixamo.com"이라 이름 검색 무용 — FBX 경로로 LoadAllAssetsAtPath.
- Ch44 머티리얼: FBX 임베디드 → `Ch44_Textures/`(타일 1001/1002×Diffuse/Normal 등) 추출 + `Ch44_Materials/M_Ch44_*` URP Lit 외부 리맵 완료. 디퓨즈가 원래 다크 톤이라 "어둡게 보임"은 텍스처 정상.
- 비주얼 교체 시 CharacterVisual의 추가 컴포넌트(PlayerLocomotionAnimator·PlayerAfterimage)는 `ComponentUtility.CopyComponent/PasteComponentAsNew`로 옮기면 씬 참조(aimSource 등) 보존됨.
