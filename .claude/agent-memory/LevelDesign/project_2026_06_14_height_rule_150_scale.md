---
name: 2026-06-14 높이 규칙(단일 전투 평면) + 150 척도 정합 + 1차 빌드 스펙
description: PULL 4종 맵 컨셉 A·B·C 정합(단일평면·150척도·시그니처)+집하장/분수광장 1차 빌드 스펙. 옛 고지사격·다층카이팅 전면 철회
metadata:
  type: project
---

2026-06-14 자연끌림 PULL 맵 컨셉 4종을 A·B·C로 정합 + 1차 빌드 스펙 2맵 산출. 컨셉 모문서 = `docs/02_logs/2026-06-14-natural-pull-map-concepts.md`(갱신), 빌드 스펙 = `docs/02_logs/2026-06-14-first-stage-build-spec.md`(신규). 권위 = `docs/00_authority/2026-06-14-natural-pull-doctrine.md` §3 ★높이 규칙.

**A. ★높이 규칙 = 단일 전투 평면 (독트린 §3 신설, 1순위 권위).**
**Why:** 옥상 저격수·층 등반 = 탑뷰 조준 모델 붕괴·근접 못 닿음·카이팅 깨짐·수용성↓·멀티 navmesh 솔로 매몰·장르 이탈(유저 객관 판정, 시각=`_img/2026-06-14-height-rule.png`).
**How to apply:** 모든 적·플레이어·조준 = 한 지상 평면. 높이 허용 = ①비콘 실루엣/끌림(올라가지 않음, 코어 엘리트·보상은 *발밑* 평면) ②비행체(평면 다이브/평면 ThreatArc 텔레그래프) ③얕은 단차·경사 ≤30°(한 navmesh)만. **컨테이너/고가/크레인/단상 윗면 = 워크어블 차단(실루엣·오클루전 전용).** 옛 컨셉의 고지 사격수·다층 카이팅·고도차 떨굼은 *전부 철회*. 잔여 표현 발견 시 = 버그. ⇒ 분화구는 깊은 나선·off-mesh link 폐기, **얕은 보울 ≤30° 단일 navmesh**로 교체(점층은 *잔해 차폐*로 보존, 고도 아님). off-mesh link 불요해짐.

**B. 사이즈 = 150×150 m (옛 90~100 전면 교체).**
**Why:** 카메라 가시 ≈Ø30m(반경15) → 코어가 림에서 ~3카메라 = "여정" 성립(시각=`_img/2026-06-14-map-scale.png`). 100이면 코어가 늘 화면 안→릴레이 공개 무의미.
**How to apply:** 원점=맵중심(0,0). 밴드 반경: 코어 LV4~5 r0~22 / 중간 LV2~3 r22~50 / 외곽 LV1 r55~74 / 봉쇄벽 r74. 카이팅 링 = 림 r62 / 중간 r36 / 코어 r13. 릴레이 비트 간격 ≈ 한 카메라(15~18m). Synty 10m 모듈 그리드, 비-타일 프롭 5m/자유(obstacle 통과 ≥3m).

**C. 맵별 몬스터 시그니처 (높이 규칙 통과 — §5.5 표 확정).** 공통 LV밴드: 외곽 LV1(Lacercharias 산발·Venodonte **지상** 견제) / 중간 LV2~3(Caniathrox·Kupolojuve·Dimaxillosaurus) / 코어 LV4~5(Fulgurodonte+Venosaur·Crustaspikan 유생). 집하장=Fulguro 램 코어+Venodonte 컨테이너 *뒤* 지상 엄폐+Kupolojuve 평면 다이브. 분수광장=Fulguro 개활 램 or Carcinoptera 공중정예(평면 해소)+Kupolojuve 군집 비콘. 고가잔무=Fulguro 박힌 램. 발원격리=Crustaspikan 유생 분출.

**1차 빌드 스펙 = 집하장 + 분수광장 2맵**(공개 드라마 양극: 왈칵 vs 트임). 둘 다 150 그리드 좌표·릴레이 비트 ①②③ 좌표·카이팅 링·데스트랩0·스폰 마커 role·단일 LZ·NavMesh 체크·ASCII 톱다운·owner 경계표. 트리거 = **proximity(비트 반경)**, 시간 기반 아님(콘 공개 동기). 집하장 #1위험=회랑 데스트랩/walkable 밀도(동시 alive 회랑당 ≤6 의도). 분수광장 #1위험=개활 60-agent path 스파이크→스태거 필수. 입체교차로·발원격리는 검증 후 2차.

**프리팹 전부 Glob 재확인(2026-06-14, owned ✓):** 크레인 `SM_Bld_Crane_01`(CN)·`SM_Prop_Crane_Section_01`·`SM_Prop_Floodlights_01`(CN), 컨테이너 `SM_Prop_Container_01`(BR)·`SM_Prop_Shipping_Container_01`(CN), 도로 `SM_Env_Road_Cross_01`·`_Straight_01~03`·`_Corner_01/02`(BR), 바닥 `SM_Bld_Concrete_Floor_01~04`·`SM_Bld_Concrete_Slab_04`(CN), LZ `SM_Env_Port_Concrete_Slab_01`(BR), 봉쇄벽 `SM_Env_Port_Wall_01/02`·`_Corner_01`(BR), 단상 치환 `SM_Prop_GuardTower_01`(BR)/`SM_Bld_WaterTank_01`·`SM_Bld_SmokeStack_01`(CN). ⚠️**Fountain/Monument owned 미존재·Statue=PolygonGeneric 배제** → 분수광장 단상=치환(Story 합의 필요, P2). ⚠️Glob 절대경로 `C:/Users/pc/ZombieCrush/Assets/**` 줘야 잡힘(cwd=_img 상대는 0건).

**미결/리스크:** R1=쫄깃함은 이 2맵으로 판정 불가(게이트0 전투감 선행 필수, 맵=무대 전투감=배우). 분수광장 비콘 치환 Story 합의(P2). 발광/안개=artist 셰이더(telegraph 재활용).

관련: [[project_2026_06_14_natural_pull_concepts]]
