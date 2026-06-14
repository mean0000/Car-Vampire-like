---
name: caniathrox-clip-kit
description: Caniathrox(개형) 클립별 실측 루트모션 값 — "_RM" 접미사가 루트모션 보장 아님. Animator 스텝 실측이 유일한 진실
metadata:
  type: project
---

# Caniathrox 클립 킷 — 실측 루트모션 (2026-06-13)

경로: `Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 8/Caniathrox/FBX Files/`
generic rig, rootMotionBoneName=root, animationType=2. Avatar guid `bdda1685fe869bf498117879ab0ea21c`.

## ★ 핵심 함정: "_RM" 접미사 ≠ 루트모션 있음. AnimationUtility.GetEditorCurve(RootT.z)는 거짓(전부 0 반환).
유일한 진실: **프리팹 인스턴스 + 단일클립 컨트롤러 + applyRootMotion=true로 Animator.Update(dt) 스텝 후 transform delta 측정.**

| 클립 | guid | len | 전진(z) | 상승(maxY) | 용도 |
|---|---|---|---|---|---|
| Run_RM | d058822fc145dec41a49982d252e6ef5 | 0.600s(loop아님) | **2.457m/cyc** | 0 | 접근 로코모션 |
| **Jump_RM** | b751ac5964e60c744bd28e9114b55810 | 0.833s | **4.673m** | **0.278m** | ★진짜 도약(덮침) |
| JumpBite_RM | 463af4bc9ccab6b4f9ad80ff380c4117 | 0.833s | **0(제자리!)** | 0 | 제자리 물기 |
| BiteForward_RM | 2db73d511566ae048b694977ec339c2d | 0.833s | 1.328m | 0 | 짧은 전진 물기 |
| Spit | 3e494b3b497026246b7d856aff3609c4 | 0.667s | 0 | 0 | 제자리 뱉기 |
| IdleAngry | b83ee0ec08fcde34abfa4183098ec63f | 1.867s | 0 | 0 | 휴지 |

전부 fileID 1827226128182048838.

## ★ 옛 코드(CaniathroxAttackDemo v3)의 오기: "JumpBite_RM 전진4.67m·상승0.28m"는 틀림 — 그건 **Jump_RM** 값이다.
JumpBite_RM은 실제 제자리(0,0,0). 옛 코드는 JumpBite_RM을 참조하면서 Jump_RM 수치를 주석에 박고, 코드 포물선으로 위치를 창작해 "모션 없이 떠오르는 점프"를 만들었다(프로젝트 사고 #2 재현). 도약=덮침 의도면 **Jump_RM**을 써라.

## 종 신체특성 확인: 4족 도약 = 짧고 앞으로 덮침. Jump_RM(전진4.67m·상승0.28m)이 정확히 이 프로파일(낮은 무게중심, 높이 안 뜨고 전진). [[project_animation_inplace_gotchas]]의 in-place 함정과 연결.

## ★Jump_RM Y곡선 프레임별 실측 (2026-06-13, 50프레임@60fps, len0.833s)
- Y: f0~6(0~0.1s) 지면 → f9 상승시작 → **f20(0.333s) 정점 0.2775m** → f30(0.5s) 착지 0 → 이후 질질. **대칭 포물선이 "개구리 폴짝"의 정체.**
- 윈드업 없음: f0부터 이미 전진(dZ 0.10), 폭발 순간 없이 균일 가속 → "팍" 대비 부재.
- 착지 후 꼬리: f30~50(0.333s, 전체 40%)이 dZ 0.015로 거의 정지 = "달려들기" 예리함 죽임.
- **포즈 골격(SK_Caniathrox에 SampleAnimation, 측면): f0=머리숙이고 앞다리 모은 웅크림(응축 적합), f0.40=몸 펴고 앞으로 덮침.** 한 클립에 응축→덮침 내장, 단 사이 Y가 끼어 폴짝.

## ★파생 RM 클립 (Jump_RM 복제 + import 오버라이드, 원본 보존). 경로: `Assets/_Project/Animations/CaniathroxRM/`
| 클립 | guid | len | 전진/상승 | 만든 법 |
|---|---|---|---|---|
| **JumpLunge_RM** | 11a41d8daabec194498d92e48d3d7d71 | 0.833s | **4.673m / 0.000m** | Jump_RM 복제, clipAnimations[0] **lockRootHeightY=true**(Y bake) + keepOriginalPositionXZ=true(Z 보존). 위로 안 뜨는 낮은 돌진 |
| **JumpCoil** | b86d67dd3242c294089908e39242db3e | **0.167s** | 0 / 0 (제자리) | Jump(noRM) 복제, firstFrame0/lastFrame5(@30fps 응축구간만), lockRootPositionXZ+lockRootHeightY. "모았다가" 응축 |

**★Y bake 방법(import 데이터 레벨, 코드 아님 — 헌법 안전):** FBX 복제(`AssetDatabase.CopyAsset`)→ModelImporter.clipAnimations[0]에 `lockRootHeightY=true, keepOriginalPositionY=false`(Y억제) + `keepOriginalPositionXZ=true`(전진보존)→SaveAndReimport. 원본 .meta 안 건드림. 검증=Animator 스텝 재실측(maxY 0 확인).
SampleAnimation은 Y 루트모션 미반영(raw)이라 포즈 캡처엔 높이차 안 보임 — Y 검증은 반드시 Animator 스텝으로([[measure-rootmotion-by-stepping]]).
