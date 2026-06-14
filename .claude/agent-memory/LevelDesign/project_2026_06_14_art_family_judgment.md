---
name: 2026-06-14 도심 골격 아트 가족 판정 (셰이더 증거)
description: 폐허 도심 골격=POLYGON(BR+CN) 단일 가족. Toon City 배제(다른 셰이더+폐허0). Fountain은 owned지만 Toon이라 못씀. 셰이더 GUID 증거.
metadata:
  type: project
---

**도심 폐허 맵 골격 = POLYGON(PolygonBattleRoyale + PolygonConstruction) 단일 가족으로 통일.** Toon City는 1차 폐허 도심에서 배제. 산출=`docs/02_logs/2026-06-14-asset-driven-map-composition.md`.

**Why (셰이더 레벨 증거 — 실측, 추정 아님):**
- BR과 Construction은 **같은 셰이더 GUID `0730dae39bc73f34796280af9875ce14`** = 한 가족(섞어도 클래시 0). 빌드스펙의 BR+CN 혼용은 art-safe.
- Toon City는 **다른 셰이더 GUID `25e085ecbe5fe224db065ec60b95b24b`** + 에셋별 디퓨즈(`Building_1A_D`…~70개) vs POLYGON 단일 아틀라스(9개). 한 화면에 섞으면 라이팅 응답·채도가 따로 놂 = 유저 경고한 "조잡 클래시".
- **Toon City 폐허 어휘 0개**: grep(ruin|destroyed|damaged|broken|rubble|wreck) = 0 matches. 차량 25종 전부 멀쩡, 건물 90종 전부 신축, 도로 파손본 없음. 깨끗한 도시라 폐허를 *못 짬*.
- POLYGON BR 폐허 어휘 충분: `Road_Straight_Damaged_01~03`, Rubble 9종(Pile/Stone/Plank/Pebbles), `Bridge_Broken_01`, Destroyed 차량 5종. CN=ConcreteRebar_Wall·Concrete_Slab_Pile·Junk_Stack.

**How to apply:**
- 폐허 도심 맵 = POLYGON(BR+CN)만. Toon City/PBR(Top_Down_Post-Apoc) 프리팹 1개라도 씬 들어오면 클래시 → 즉시 제거.
- 깨끗한 바닥+어두운 라이팅만으론 폐허 아님(=초등학생 게임). **Damaged 도로·Rubble·Destroyed car를 동심 밴드에 적극 산포**해야 폐허로 읽힘.
- 임포트/구매 불요(POLYBOX City Pack·신규 구매 ❌). Top_Down_Post-Apoc PBR은 골격·채굴 모두 비권장(톤충돌).
- **기존 메모리 "Toon City=톤정합 메인 outdoor(06-13)" 정정**: 그건 셰이더/폐허 전수 감사 이전 판단. 폐허 도심엔 부적합. (깨끗한 현대 도시 후반 바이옴엔 *Toon City 단독*으로 재검토 가치 — 혼용은 항상 금지.)

관련: [[project_2026_06_14_height_rule_150_scale]], [[project_2026_06_14_natural_pull_concepts]]
