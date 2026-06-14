---
name: venodonte-clip-kit
description: Venodonte(LV1 산성 사수, 저자세 절지류) 클립별 실측 루트모션 + 머리 스러스트 발사 프레임. 사격은 in-place, 이동은 Crawl
metadata:
  type: project
---

# Venodonte 클립 킷 — 실측 (2026-06-13)

경로: `Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 11/Venodonte/`
generic rig, animationType=2, rootMotionBoneName=root, Avatar guid `6bc540f1b67e45e4a93e4a9adca2930f`. 전 클립 fileID `1827226128182048838`.
★프리팹 `Venodonte_Tint1.prefab`은 **Animator가 루트 GameObject에**(Caniathrox와 다름 — 거기선 자식). GetComponentInChildren<Animator>()로 잡으면 둘 다 OK.
바디=주황-갈색 저자세 절지류(만디블·꼬리·다족·더듬이). 위협색=레드오렌지(텍스처에 이미 따뜻함). 4틴트 차이는 albedo 텍스처뿐(BaseColor=흰=텍스처구동).

## ★ 종 특성: 사격은 제자리, 이동은 포복(Crawl). Run/Walk 없음.
| 클립 | guid | len | 전진 | maxY | 용도 |
|---|---|---|---|---|---|
| Idle | a71a7e8822de6544b8ed123fc31b8ceb | 2.000s | 0 | 0 | 휴지(Idle 상태) |
| Taunt | e12a66bd890e9914c872e28aeeb0bb67 | 3.333s | 0 | 0 | ★조준 윈드업(Aim) — 머리 들어올림 |
| AcidShot(단발) | 970da2de833e012419c5b196fe18cc76 | 0.667s | 0 | 0 | 단발 사격(보존, 미사용) |
| **3AcidShotCombo** | 2f7fc70362968e54c97a1ca8373cb825 | **1.333s**(40f@30fps) | **0(제자리)** | 0 | ★산성샷 3연(Fire 상태) |
| **CrawlForward_RM** | 77b257a519308e44db0af5eb2901c2a6 | 0.533s | **1.567m/cyc** | 0 | 이동(Reposition). 네이티브 **2.940m/s** |

★사격 클립이 in-place(전진 0)인 게 정답 — 사수는 발 심고 쏜다(헌법: 코드가 위치 안 잡아도 됨). 이동만 Crawl 루트모션.
CrawlForward 네이티브 2.940m/s = Caniathrox Run_RM(4.094)보다 느림 → 저자세 크리처라 moveSpeed 시작값 3.0(거의 네이티브).

## ★머리 스러스트 발사 프레임 (Animator 스텝 + FangUpperLeft 본 model-z 추적)
3AcidShotCombo는 **머리 전방 스러스트 3회**가 클립에 내장 — 각 정점 = 글롭 1발 발사 순간:
- 스러스트 정점: norm **0.225 / 0.425 / 0.625** (= t 0.300/0.567/0.833s). 실간격 ≈ 0.267s(스펙 0.15s보다 느림=클립이 진실).
- f0~3 머리 뒤로 빼고(웜업) → f9 첫 스러스트 → f10-11 리코일 → f17 둘째 → f25 셋째 → f26~40 정착.
단발 AcidShot 스러스트 정점: norm **0.450**(t 0.300s).
Taunt: 머리 z 0.26→0.51·y 0.49→0.63로 **들어올림**(norm 0.45 정점) → 좋은 조준 텔레그래프. 3.33s라 Aim에서 speed 3.3·ExitTime 0.45로 압축(~0.47s 윈드업).

## ★발사 타이밍 = AnimationEvent (코드 타이머 아님 — 헌법 제2원칙)
3발 발사를 코드 타이머로 쏘면 위반. 대신 **클립 events:에 FireAcidGlob(int) 3개**를 스러스트 정점에 박음.
- 클론 클립(원본 .meta 보존): `Assets/_Project/Animations/VenodonteRM/Venodonte@3AcidShotCombo.fbx` guid **41840e2735ce4e66bac7870496c6132b**, `@AcidShot.fbx` guid **67f663945799473d8bffc70bf7df20db**.
- ★FBX 클론은 `AssetDatabase.CopyAsset` MCP 에러("User interactions") → **Bash로 .fbx 파일복사 + 새 guid .meta 직접 작성**. 원본 .meta 안 건드림(JumpLunge_RM 전례).
- ★events `time:`은 정규화값으로 쓴다(임포터가 ×clip.length로 초 변환). 0.225 → 0.300s. 직접 초로 쓰면 ×1.333 더 스케일됨(함정).
- SendMessage 도달: events는 **Animator와 같은 GameObject의 컴포넌트** 메서드를 부름. Venodonte는 Animator가 루트=드라이버도 루트 → 도달 OK.
[[venodonte-attack-statemachine]] [[projectile-pool-pattern]] [[animevent-fire-timing]] [[measure-rootmotion-by-stepping]]
