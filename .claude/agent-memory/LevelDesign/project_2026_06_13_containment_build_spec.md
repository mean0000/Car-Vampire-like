---
name: 2026-06-13 ContainmentZone 그레이박스 빌드 스펙
description: 액션 돌파 앵커로 첫 맵 빌드 스펙 author 완료 — E1~E8 인카운터 스파인, 키트 실선별, 좌표/스폰표, 매복 포켓+대시 레인 신규 기하, NavMesh V1~V7
type: project
---

**2026-06-13 ContainmentZone 그레이박스 빌드 스펙 author 완료.** 경로 = `docs/02_logs/2026-06-13-containment-zone-graybox-build-spec.md`. 산출물 = 구현 가능 빌드 스펙(Gameplay 소비용).

**Why:** 이전 맵 제안([[project_2026_06_13_map_architecture_proposal]])의 택티컬 인카운터 framing이 액션 앵커([[2026-06-13-action-processing-anchor]])로 상위 교체됨. 아키텍처/구역/스코프는 유효, 인카운터 기제만 돌파·매복·대시로 재author.

**핵심 기하 교체(이전 제안 대비):**
- 회랑 폭 4~4.5m → **5~6m**(빡빡 위빙 → 관통 대시 카빙, 데스트랩 금지).
- Z2→Z3 초크 3m → **5m**(엿보는 병목 → 밀고 드는 돌파).
- **신규 기하 = 매복 포켓(콘 밖 측면/후방 알코브) + 대시 레인(관통 동선).** 이전 제안엔 둘 다 없었음.
- L-코너 = 발견 드라마 → **매복 트리거 + 콘 차단**. 소리사다리 폐기.
- 시야 콘 = 신중 도구 → **매복 발생기(뒤·측면) + 전방 학살 가시화**.

**확정 수치:**
- 시설 ~120×80m, X[-30,+30]/Z[0,120], Synty 벽 **4m 그리드 전제**(★실측 스냅은 Gameplay 첫 벽 인스턴스 측정 후 확정).
- E1 Z0(Lacercharias 2~3, 대시-관통 튜토) / E2~E3 Z1(측면 알코브 A1·A2 + Venodonte 산성탄 + E3 방입장 매복 3마리 상한) / E4~E5 Z2(Caniathrox 3+Kupolojuve 2 시차쇄도, 둘레 엄폐로 남측 등면 닫음) / E6~E7 Z3(Fulgurodonte 램 엘리트+Venosaur 호위 3+군체 4, 복귀각성=잔존 일괄 Chase) / E8 Z4(Crustaspikan 유생 2 디제틱 예고만, 본보스 미투입).
- 스태거 = Z1_E3 0.4s, Z2 0.3~0.5s, Z3_Swarm 0.3s.

**How to apply:** Gameplay 핸드오프 시 §10 owner 경계 표 그대로 전달. 키트 = PolygonConstruction(Concrete_Wall/ConcreteRebar_Wall/Concrete_Floor/Shipping_Container/Barrier/Roadblock/BarrelStack) + BattleRoyale(Road_*/Port_Wall/Cinderblock_Wall/Container) — 실재 확인됨. PolygonGeneric은 판타지/자연이라 도심 부적합. 측정 의존: 공간 가독성까지만 캡처 검증, 쫄깃함은 게이트0 전투감 후 플레이로만. 톤게이트 미통과(리스킨 가능성).

관련: [[project_2026_06_13_map_architecture_proposal]], [[2026-06-13-action-processing-anchor]]
