---
name: 2026-06-14 자연스러운 끌림 맵 컨셉 4종
description: 첫 바이옴(폐허도심) PULL 맵 4종 설계(입체교차로/집하장/분화구/분수광장). 추천=집하장(왈칵 reveal). 끌림4겹·동심원LV·데스트랩0·단일LZ 공통 불변식. Fountain/Monument 프리팹 owned 없음 함정
type: project
---

첫 바이옴(폐허 도심) **자연스러운 끌림(PULL) 맵 컨셉 4종** 설계 완료. 경로 = `docs/02_logs/2026-06-14-natural-pull-map-concepts.md`. 산출 = 설계 문서 1개(유저 판정 대기, 에디터/MCP 미사용). 디제틱 정체성·명명은 Story 별도 파일(나는 공간/끌림 합성만).

**권위 = `docs/00_authority/2026-06-14-natural-pull-doctrine.md`**(루프 정정: 서바이버즈 농성 15분+·할당량N+무한스폰·중도탈출불가·escalation, 타르코프 추출 ❌·하드타임아웃 ❌). 끌림 4겹 = ①상시중력(틸트 솟은 발광 비콘) ②리딩라인(코어 수렴) ③콘 릴레이 공개(글린트→엘리트윤곽→코어잭팟, 젤다 사선) ④뽕 약속이 미끼. **시드 성격 = 리딩라인이 비콘을 어떻게 드러내나(공개 드라마).**

**4종(각 다른 비콘·공개드라마·순환 — 닮음 회피):**
1. **입체교차로**(시드B 블록): 비콘=무너진 십자 고가 스택+박힌 Fulgurodonte 램 엘리트+발광 웅덩이. 공개=블록 가림→드러냄. 순환=8자+사분면.
2. **집하장**(신규 회랑): 비콘=크레인 15m+ 보급등(최고 가시성)+본사 보급 잭팟+Fulguro 경비. 공개=**회랑 코너 왈칵(최강 드라마)**. 순환=격자 회랑. ★추천.
3. **분화구**(시드C 나선): 비콘=**함몰** 발광 크레이터+CrustaspikanLarvae(보스 예고)+안개 솟음. 공개=나선 점층(궁금증 최대). 순환=동심 고도 나선.
4. **분수광장**(시드A 광장): 비콘=단상+Kupolojuve 공중 군집. 공개=방사 정조준(또렷 기준선). 순환=링 카이팅. 광역 학살 뽕.

**★가장 강한 추천 = ② 집하장.** Why: 독트린 §2.4 "최강 드라마(건물 가렸다 골목끝 왈칵)" 정면 직격 + 크레인 15m+가 CI-6(키큰비콘=상시/지상미끼=콘공개) 가장 깨끗이 분리 + 본사 보급=메타경제 디제틱 정합 + 키트 한톤(산업 야드) 완비 비콘치환 불요. 유보=NavMesh 회랑 데스트랩/밀도 위험 4종 중 최대(V3·walkable 최우선).

**공통 불변식 CI-1~6**: 끌림4겹 / 동심원 LV(외곽LV1·중간LV2~3·코어LV4~5 잭팟) / 카이팅루프+카빙레인+데스트랩0(포켓≥2출구) / **단일 추출 LZ**(외곽, 할당량N 점등 끝맺음 — 추출구3종 비대칭은 독트린이 폐기) / 디제틱 스폰출처(정면팝인❌) / 키큰비콘 상시·지상미끼 콘공개.

**★프리팹 함정(Glob 2026-06-14)**: **Fountain/Monument owned 미존재. Statue는 PolygonGeneric에만 = §3 배제 위반.** → 분수광장 단상 = `SM_Prop_GuardTower_01`(BR 수직)/`SM_Bld_WaterTank_01`·`SM_Bld_SmokeStack_01`(CN)+발광풀로 치환(Story 정체성 합의 필요). 검증 실재 비콘: 고가 `SM_Env_Bridge_Broken_01`+`SM_Env_Bridge_Support_01`(BR), 크레인 `SM_Bld_Crane_01`·`SM_Veh_Crane_01`+`SM_Prop_Crane_Section_01`(CN), 함몰 `SM_Env_Dirt_Hole_01`+`SM_Env_Dirt_Slope_*`(CN), 컨테이너 `SM_Prop_Container_01`(BR)·`SM_Prop_Shipping_Container_01`(CN), `SM_Prop_Floodlights_01`(CN 발광등). ⚠️Synty 프리팹 경로 = `Assets/Synty/Polygon*`(Glob는 절대경로 줘야 잡힘, cwd=_img 기준 상대는 0건).

**Owner 경계**: lv=비콘 위치/높이/실루엣·리딩라인 기하·릴레이 비트·순환·동심원LV·키트선별·NavMesh포인트·시드값. Gameplay=시드 씬빌드·SpawnMarker·디렉터/스태거/풀사이즈·LZ점등 런타임·NavMesh베이크+off-mesh link. artist=발광/웅덩이/분수/안개 셰이더(telegraph·킬버스트 재활용). Story=정체성/명명.

**리스크**: R1 쫄깃함=게이트0 전투감 선행 필수(맵=무대 전투감=배우). R4 분화구 다층NavMesh=off-mesh link 필수·경사≤30°·바닥탈출≥2(1차는 얕은단차). R5 4종=수동 시드 MVP 컨셉(모듈설계 P4 "수동시드 먼저"), 진짜 조립기 아님.

관련: [[project_2026_06_13_modular_assembly_design]], [[project_2026_06_13_district_design]]
