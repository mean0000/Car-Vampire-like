# 세션 핸드오프 — 임시 캐릭터 + 로코모션 애니메이션 파이프라인 (2026-06-08)

> 다음(클로드)이 이 문서만 읽고 이어갈 수 있게 쓴 핸드오프.
> 이 세션 큰 줄기: **(1) AI 리그(Player_Rigged) 스킨 웨이트 파탄 발견 → (2) 구매한 임시 캐릭터(SM_Casual_Male)로 교체 → (3) 로코모션 애니메이션 시스템 구축·검증·리뷰 완료.**
> ⚠️ 내일 작업(권총 모션 + 트윈스틱 조준)은 유저 지시로 **연기**. 이 문서 PART 3에 인계.

---

## PART 1 — 오늘 한 일

### 1-1. AI 리그 파탄 발견 → 임시 캐릭터 교체
- `Player_Rigged.fbx`(헤드리스 Blender AI 리깅 산출물) 검증 → **스킨 웨이트 90% 깨짐**(버텍스 대부분 미할당). 톱다운이라도 사용 불가 판정.
- 유저가 **"Stylized Casual Adventure Characters"** 팩을 임포트 → `Assets/JC_StylizedCasualCharacters/`.
- **★ SM_Casual_Male = 임시 캐릭터다. 주인공 아님.** 유저 명시: "주인공보단 임시 캐릭터야". 주인공 컨셉(얼굴 보이는 퇴폐미 한국 남자 + 테크웨어)은 별도로 유지(메모리 `project_character_pipeline.md`).
- `SM_Casual_Male.prefab` = 큐레이션된 캐주얼 복장(13 SMR), 아바타 `SM_Casual_MaleAvatar` Humanoid 유효+human 매핑 정상.

### 1-2. 로코모션 애니메이션 시스템 구축
- **애니메이션 소스** = `Assets/Jorjouto/ACS/Sample/SampleCharacter/Animation/AnimationLibrary_Unity_Standard.fbx` — **Humanoid 클립 46종**. 클립 이름에 **`Rig|` 프리픽스** 붙음(예: `Rig|Walk_Loop`).
  - ⚠️ loopTime 수정 시 `clips[i].name == "Walk_Loop"` 완전일치는 실패함 → `.Contains("Walk_Loop")` 써야 함. Walk_Loop / Jog_Fwd_Loop 둘에 loopTime=true 세팅 후 리임포트 완료.
- **컨트롤러** `Assets/_Project/Animations/PlayerLocomotion.controller`(신규):
  - float 파라미터 `Speed`.
  - 단일 스테이트 `Locomotion`(default) → 1D BlendTree(Simple1D, useAutomaticThresholds=false):
    - `Idle_Loop` @ 0
    - `Walk_Loop` @ 2.5
    - `Jog_Fwd_Loop` @ 5
    - `Sprint_Loop` @ 8.5
  - 임계값은 PlayerController 실측 속도(moveSpeed 5 / run 1.7배≈8.5 / crouch 0.5배≈2.5)에 맞춤.
- **드라이버** `Assets/_Project/Scripts/PlayerLocomotionAnimator.cs`(신규):
  - **비침습 설계.** PlayerController가 transform.position을 직접 옮기고(물리X·회전X) 루트를 절대 안 돌리므로, 이 드라이버는 추적대상(기본=부모=Player 루트)의 **평면(XZ) 위치 변화로 속도를 역산** → Animator `Speed`에 댐핑 적용.
  - **★ 회전은 이 컴포넌트가 붙은 CharacterVisual 자신에게만.** 루트를 돌리면 **카메라(Player 자식)까지 돈다** — 절대 금지.
  - 파라미터: speedDamp 0.1, maxSpeed 9(텔레포트 스파이크 클램프), turnSpeed 720, **moveThreshold 0.3 m/s**.

### 1-3. 씬 와이어링 (Greybox_ScanLit)
- `Assets/_Project/Scenes/Greybox_ScanLit.unity`:
  - CharacterVisual = SM_Casual_Male 프리팹으로 교체. localPosition (0,-1,0), 회전 identity, scale 1.
  - Animator.runtimeAnimatorController = PlayerLocomotion, applyRootMotion=false.
  - PlayerLocomotionAnimator 추가, moveThreshold 0.3.

### 1-4. 검증 + 리뷰
- **검증:** 오프스크린 SkinnedMeshRenderer 스키닝이 안 flush돼서 디스크 렌더가 T-pose로 나오던 함정 → `forceMatrixRecalculationPerRender=true` + `updateWhenOffscreen=true` + **double `cam.Render()`** 로 해결. `_anim3_jog.png` 에서 올바른 달리기 스트라이드 확인. (그 전에 본 회전값 직접 읽기로 리타깃 자체는 정상임을 증명.)
- **Stab 리뷰 [HIGH] 반영:** moveThreshold를 프레임당 변위(m/frame)와 비교하던 걸 **속도(m/s) 비교로 수정** → 프레임레이트 독립. 씬 인스턴스 직렬화값도 0.05→0.3으로 갱신.
- **Codex 리뷰:** MINOR(moveSource 파괴/재할당)만 지적 → 임시 캐릭터엔 과한 방어라 미반영(CharacterVisual은 함께 파괴되는 자식, 런타임 재할당 없음).
- 콘솔 에러 0.

---

## PART 2 — 지금 상태

- **임시 캐릭터 = SM_Casual_Male 프리팹** (CharacterVisual로 Greybox_ScanLit에 박힘). 주인공 아님.
- **로코모션 = 완성·검증·리뷰 끝.** Idle/Walk/Jog/Sprint 1D 블렌드 + 이동방향으로 비주얼 회전. 정지 시 마지막 방향 유지.
- 작업 씬 = `Greybox_ScanLit.unity` (APV 라이팅 실험 씬).

---

## PART 3 — 내일 작업 (연기됨): 권총 모션 + 트윈스틱 조준

유저 지시: **"내가 마우스는 앞을 보고 캐릭터는 뒤로가게 눌렀을 때, 캐릭터가 앞을 보면서 뒷걸음 치는 모션 — 이게 있어야 탑뷰에서 자연스러울 것 같아."**

### 핵심 사실 (grep 전수조사 완료)
- AnimationLibrary에 **전진 로코모션만 존재**: Walk_Loop, Walk_Formal_Loop, Jog_Fwd_Loop, Sprint_Loop, Crouch_Fwd_Loop, Swim_Fwd_Loop.
- 정지 권총 클립: Pistol_Idle_Loop, Pistol_Aim_Up/Neutral/Down, Pistol_Shoot, Pistol_Reload.
- **❌ 프로젝트 어디에도 뒷걸음(backpedal)·스트레이프(strafe)·좌우 이동 클립 없음.**

### 요청의 두 부분 분리
| 부분 | 지금 공짜로 되나 | 필요한 것 |
|---|---|---|
| **얼굴 방향**(앞=마우스 조준 보기) | ✅ 가능 | 드라이버 회전을 "이동방향"→"마우스 조준방향"으로 전환 |
| **뒷걸음 다리**(다리는 뒤로) | ❌ 불가 | 전용 클립 필요(현재 없음) |

### 두 경로 (유저 결정 대기)
- **Path A — Mixamo 무료 권총 세트 받기:** Pistol Idle / Walk / Walk Back / Strafe Left / Strafe Right → **2D 블렌드(Fwd/Back/L/R)** + 상체 권총 레이어. 가장 자연스러움. 다운로드·리타깃 작업 필요.
- **Path B — 지금 있는 걸로 즉시:** 조준방향으로 얼굴 회전 + Pistol_Aim 상체 레이어 + Pistol_Shoot 트리거 + 뒷걸음은 **Jog 역재생 임시방편**.
- **내 추천: B로 지금 손맛 확인 → A로 업그레이드.**

### 시작 지점
- 드라이버(`PlayerLocomotionAnimator.cs`)의 회전 로직이 현재 `delta.normalized`(이동방향) 추종 → 트윈스틱은 여기를 마우스 월드포지션 조준으로 바꾸고, 다리 블렌드를 2D로 확장하는 게 핵심.
- 트윈스틱은 **얼굴(facing)과 이동(movement)을 분리**해야 함 → 1D Speed 블렌드로는 부족, 2D 방향 블렌드 필요.

---

## 부록 — 함정 메모
- RunCommand 하니스: 클래스는 반드시 `internal class CommandScript : IRunCommand`. AssetDatabase 작업 중 "User interactions are not supported" 에러 나면 재시도(DeleteAsset 빼면 통과한 사례).
- ModelImporterAnimationType 3 = Humanoid.
- 진단 PNG들(`_scanlit_*.png`, `_anim*.png`)은 리포 루트에 잔존 — 커밋 제외. 정리 가능.
