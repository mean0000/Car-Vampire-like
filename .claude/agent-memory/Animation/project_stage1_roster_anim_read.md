---
name: project-stage1-roster-anim-read
description: 1차 스테이지 9종 애니 도메인 판독 (2026-06-14) — 프리뷰 실측 신체분류·클립킷 인벤토리·틀 재활용 맵·배역표 이견. 구현 전 "읽고 판단" 결론.
metadata:
  type: project
---

1차 스테이지(폐허 도심) 9종 확정. 애니 도메인에서 프리뷰 이미지 실측(Protofactor Vol.2)+클립 인벤토리(`Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol N/...`)로 판독. **구현 전 단계.** 배역표=LevelDesign 공간역할 제안일 뿐, 신체 진실은 프리뷰가 결정.

**Why:** 유저가 "배역표 역할을 꼭 따를 필요 없다, 신체가 믿기는가는 애니 도메인"이라 명시. 배역표 일부가 프리뷰와 충돌(아래 §이견).
**How to apply:** 각 종 상태머신 짤 때 이 신체분류+클립킷을 1차 근거로. 배역 동사가 신체와 안 맞으면 이 문서의 대안 우선.

## ★프리뷰 실측 신체분류 (눈으로 확인 — 배역표 텍스트 아님)
| 종 | 실측 신체 | 무게/기동 | 시그니처 클립(에셋 보유) |
|---|---|---|---|
| Lacercharias | **저자세 이형**(거대 입+뭉툭 앞다리, 수직 거의 0) | 낮음·빠름 | ★Roll 시스템(GoToRoll→Roll→RollToBiteAttack→RollToIdle)+BiteAttack. 굴러서 무는 종 |
| Venodonte | **저자세 다족 절지**(게-사마귀, 눈자루, 긴 앞다리) | 낮음·정착사격 | (구현완료) 3AcidShotCombo. 브레이스-사격 자세 |
| Caniathrox | **저자세 4족**(넓은 스탠스, 앞으로 입) | 낮음·쾌속 | (구현완료) JumpBite/JumpLunge. 포식자 돌진 |
| Kupolojuve(=Kupolobrach Juvenile) | **부유 해파리-돔**(가시 종 모양 갓+촉수다리+발광 코어) | 부유·맥동 | ★진짜 비행킷(Fly* 다수)+DashSpikeAttack+ElectroShot/FlyElectroShot+FlyClawsAttack+JumpClawsAttack+Falling. **날갯짓 아님 = 젤리 맥동 부유** |
| Dimaxillosaurus | **직립 2족 슬렌더**(긴 갈고리 클로 팔) | 중간·리치 | (구현중) 클로월 이즈4분할 |
| Venosaur | **묵직 헌치드 2족/수각**(근육질, 앞으로 클로, 넓은 다리) | 무겁·둔중 | 2HitComboClaws+directional claws+Bite+JumpClaws+Roar+Run+Spit. Dimax보다 무거운 브루저 |
| Fulgurodonte | **★저자세 다족 절지**(거미/게형, 전기파랑 가시, 갈라진 다리) — ★직립 아님! | 낮음·돌진 | RamAttack_RM(시그니처)+클로콤보+ElectroBlast+Spit+Roar+**OutOfTheGround**(매장 등장) |
| Carcinoptera | **진짜 날개 갑각**(말벌-게, 막날개 2장+긴 게다리+주둥이) | 비행·다이브 | Fly*+FlyBiteAttack+JumpBiteAttack_RM+ClawsAttack+지상 Crawl+Falling. **진짜 날갯짓 비행** |
| CrustaspikanLarvae | **헌치드 너클워커 4족**(무거운 앞팔+엄니+등 가시) | 무겁·저돌 | JumpBite_RM+BiteForward_RM+Roar+GetOutOfTheGround. 고릴라형 너클차저 |
| (성체)Crustaspikan | **거대 직립 타이탄 브루트**(거대 어깨+게집게 팔) | 거대·느린예고 | 2HandsSmash+2HitCombo+L/RHandSmash+FootStomp+**ThrowRock/UnearthRock/WalkWithRock**(투석 시퀀스)+SpitterShot+Roar1/2. ★약점 split 메시(SM_CrustaspikanWeakPointSplit) |

## ★★배역표 이견 (프리뷰가 배역표를 반박한 곳 — 중요)
1. **Fulgurodonte = "직립 거대 비콘 윤곽" 주장 = 거짓.** 프리뷰는 **저자세 다족 절지(거미/게형)**. 디제틱 doc("직립 앵커②=인간 잔존, 멀리서 솟은 직립 실루엣 비콘")은 신체와 충돌. 램은 맞음(RamAttack_RM 보유)이나 **낮게 깔려 돌진하는 절지 램**이지 직립 황소 램이 아님. → 코어 비콘 윤곽은 Fulgurodonte로 못 세움. **성체 Crustaspikan(진짜 직립 타이탄)이 비콘 적임자.** Story/LevelDesign에 신체 정정 통보 필요.
2. **Kupolojuve 전격 = "대시 스파이크/전기샷" — 신체는 날갯짓 비행이 아니라 부유 해파리.** 다이브 공격(DashSpikeAttack)은 맞으나 "비행 하라서"의 기동은 날개가 아니라 **맥동 부유 + 돔 아래 코어 방전**. 텔레그래프=돔 갓이 비콘, 발광 코어가 전격 차징. 평면 다이브는 클립(DashSpikeAttack_RM) 그대로 가능.
3. **Lacercharias = "저자세 롤링 돌진" — 배역표 맞음, 단 에셋이 진짜 Roll 상태머신 보유.** 단순 Crawl이 아니라 GoToRoll→Roll(구르는 이동)→RollToBiteAttack(굴러와 물기) 시퀀스. fodder지만 **고유 굴림 문법** = 첫 적인데 의외로 애니 흐름이 특수(상태 전이 4단). 배역 "쓸어담는 손맛"엔 맞음.
4. **Venosaur vs Dimaxillosaurus 무게 분리 = 신체로 자연 정당화.** Dimax=슬렌더 리치 클로(빠른 휙), Venosaur=묵직 헌치드(둔중 콤보). 배역 "호위/물량 살"엔 맞으나 단독으로도 위협적 신체 → "단독 약함" 강제는 수치(HP/AI)로, 애니는 묵직 브루저로.

## ★틀 재활용 맵 (기존 3틀 → 신규 6종)
- **클로 브루저 틀(Dimax 이즈4분할)** → **Venosaur**(거의 직접 재활용, 더 무겁게 speed 낮춤·2HitComboClaws 보유), **Carcinoptera 지상 폴백**(ClawsAttack).
- **브루트 스매시 틀(Crassorrid 3분할 Windup/Strike/Recovery)** → **Crustaspikan 성체**(2HandsSmash·HandSmash·FootStomp 다수, 거의 1:1. 단 ThrowRock=원거리 시퀀스는 신규 분할 필요), **CrustaspikanLarvae**(축소판, JumpBite는 Caniathrox 도약틀).
- **돌진 틀(Caniathrox JumpLunge/Coil)** → **Fulgurodonte RamAttack**(저자세 절지 램 = 돌진틀 변형, 단 무게 더), **CrustaspikanLarvae JumpBite**(너클 도약).
- **원거리 사수 틀(Venodonte AcidShot AnimationEvent 3연)** → **Fulgurodonte Spit/ElectroBlast**, **Kupolojuve ElectroShot/FlyElectroShot**, **Crustaspikan SpitterShot**. AnimationEvent 발사 패턴 그대로.
- **신규 틀 필요(기존에 없음)**:
  - **비행 틀**(Kupolojuve·Carcinoptera) — 그림자 앵커+평면 다이브+호버 Idle. 비행 2종이 공유. ★최우선 신규 R&D.
  - **롤 틀**(Lacercharias) — GoToRoll→Roll 루프→RollToBite 전이. 단순하나 고유.
  - **투석 틀**(Crustaspikan ThrowRock) — UnearthRock→WalkWithRock→ThrowRock 다단 시퀀스(원거리지만 모션이 김).

## ★애니 난이도/리스크 순위 (신규 6종)
1. **Carcinoptera (최고난도)** — 진짜 날갯짓 비행 + 평면 다이브 + 그림자 앵커(카메라 크기왜곡 보정) + 지상 Crawl 폴백 전환. 비행 틀의 원형. 클립은 풍부.
2. **Crustaspikan 성체 (보스급)** — 다중 공격 셀렉션 AI 인터페이스(어느 공격?)+투석 다단 시퀀스+약점 split 메시 연동. Crassorrid 틀로 스매시는 쉽지만 보스 패턴 총량이 큼.
3. **Kupolojuve** — 부유 맥동 Idle(고유)+다이브+전격. Carcinoptera보다 단순(작고 다이브 단순)이나 비행 신체 첫 도입.
4. **Fulgurodonte** — RamAttack(돌진틀 변형)+OutOfTheGround 등장+ElectroBlast. 신체가 절지라 직립 전제 깨고 다시 봐야. 중간 난이도.
5. **Lacercharias** — Roll 상태머신만 새로 짜면 끝. fodder라 단순. 낮은 난이도.
6. **Venosaur (최저난도)** — Dimax 클로 틀 거의 직접 재활용. 무게 노브만 낮추고 클립 교체. 가장 빠름.

## 클립 부족 리스크 = **없음** (9종 전부 풍부)
9종 모두 Idle/Walk/Run/GetHit(방향4)/Death/Turn/시그니처공격 풀세트 보유. 부족 종 0. 단 **점검 필요(루트모션 Y/in-place 실측)**:
- **비행 2종 Fly*_RM의 Y 루트모션** — 평면 규칙 위해 Y bake 확인(상공 체류 금지). DashSpikeAttack_RM/JumpBiteAttack_RM 다이브 궤적 실측 선행.
- **Lacercharias Roll_RM** — 구르는 이동거리/사이클 실측(루프 이동량).
- **Fulgurodonte RamAttack_RM** — 램 전진거리·벽 그로기 정지 프레임.
- **Crustaspikan ThrowRock** — 투석 릴리스 프레임(AnimationEvent), WalkWithRock 이동량.
- 측정은 항상 Animator 스텝/SampleAnimation([[feedback_measure_rootmotion_by_stepping]], 정적커브 거짓).

## 네이밍 주의
배역표 "Kupolojuve" = 디스크 폴더 "Kupolobrach Juvenile", 클립 prefix는 **Kupolojuve@**(에셋 내부 통일). 성체 Kupolobrach는 후반/2차 보스(가족 계보 다름).

연동: [[project_dimaxillosaurus_clip_kit]](클로틀)·[[project_crassorrid_clip_kit]](브루트틀)·[[project_caniathrox_clip_kit]](돌진틀)·[[project_venodonte_clip_kit]](사수틀)·[[feedback_pounce_grammar]]·[[feedback_measure_rootmotion_by_stepping]]
