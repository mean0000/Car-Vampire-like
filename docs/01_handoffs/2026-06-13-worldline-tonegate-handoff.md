# 핸드오프: 월드 라인 + 톤게이트 + 런 루프 — 맵 세션

> **세션**: 2026-06-13. 맵/월드 축 세션. **런 루프 코어(Phase 1) 구현 → 모듈러 디스트릭트 설계 → 화이트박스(반려) → 실내/실외 방향 → ★몬스터 톤게이트 해소 → 에셋 구매 판정**. 끝에 *월드 아트 라인 분기(A/B)*가 유저 판정 대기로 남음.
> **상위 연계**: [[2026-06-13-monster-tonegate-resolution]](톤게이트 로그) · [[2026-06-13-run-loop-system-architecture]](런 루프 설계) · [[2026-06-13-modular-district-assembly-design]](모듈 라이브러리) · [[2026-06-13-topdown-map-reference-research]](레퍼런스 권위) · [[reference_owned_assets]](보유 에셋)

---

## 1. 이번 세션 한 일 (요약 + 포인터)

| 영역 | 결과 | 권위 문서 |
|---|---|---|
| **런 루프 코어** | 7파일 클린 구현(스테이징→Assets) + 2중 리뷰 통과 + **Phase2 진입**(컴파일 OK·기능테스트 19/19 PASS·SO 3종 생성) | [[2026-06-13-run-loop-system-architecture]], [[project_2026_06_13_runloop_phase1_built]] |
| **런 구조·압박** | 러시안룰렛 할당량(N 이상개체/시간 T → 퇴근가능 → 퇴근 or 한 탕 더·잠김), 압박=전투감×밀도디렉터×위협레벨 | [[project_2026_06_13_run_structure_pressure_engine]] |
| **맵 구조** | 모듈 라이브러리(M1~M8/X1~X3/W) + 6불변식(동심·비대칭추출3·디제틱·랜드마크·다경로·가독성). P4=**수동 시드 3장 먼저** | [[2026-06-13-modular-district-assembly-design]] |
| **레퍼런스** | Duckov(인-런맵/추출/콘)·Shape of Dreams(전투/런/연출)·DRG:S·Megabonk·Hades II | [[2026-06-13-topdown-map-reference-research]], [[project_2026_06_13_map_reference_anchors]] |
| **액션 앵커** | 처단·돌파·매복·대시 (좀보이드/택티컬 폐기, 시야콘=매복발생기+전방학살) | [[project_2026_06_13_action_processing_anchor]] |
| **★톤게이트** | **게임거리 통과** — Synty 월드+PBR 괴수 코히어런트. 클로즈업 툰 유예, 진짜 후속=가독성(림) | [[2026-06-13-monster-tonegate-resolution]] |
| **에셋 판정** | Woodland Apocalypse Bundle=보류(POLYGON≠Toon·우드랜드≠도심·선행팩). Office Pack $15=안전 | 동 로그 §5 |

## 2. ★미결정 — 유저 판정 대기 (다음 세션 *첫* 분기)

### (가장 큰) 월드 아트 라인
- **(A) 현 방향 유지** [기본 권고] — 보유 **Toon City** 도심 골격 + **$15 Office Pack**(코퍼릿 모듈러 실내, 유일 빈칸). 톤게이트 통과, 재작업 0, 최저 비용. → 바로 맵 빌드.
- **(B) POLYGON Apocalypse 전환 검토** — 그리티 종말 무드(검역벽·붕괴도시)가 진짜로 당기면, **괴수+주인공을 POLYGON Apocalypse(도심 base) vs Toon City 비교 캡처** → 눈 판정. 전환 시 $30/월 Synty 구독이 단품보다 유리. ⚠️톤게이트 재오픈 + 보유 골격 폐기의 *토대 결정*.

> A면 즉시 빌드 진입, B면 비교 캡처가 첫 작업. **이 분기 전엔 맵 골격 빌드 시작 금지**(라인이 골격을 결정).

### 부수
- **$15 Office Pack 구매** = 유저 액션(A 확정 시 안전). 신규 도심팩은 Toon City 중복이라 사지 말 것.

## 3. 다음 큐 (우선순위)

1. **월드 라인 A/B 판정**(§2) — 다른 모든 맵 작업의 선행.
2. **맵 골격 재빌드** — 반려된 화이트박스(90×90m, 너무 작고 조잡)를 **Duckov 스케일(수백 m)**로, **에이전트(LevelDesign/Gameplay) 통해** 재시공. 실외 골격 + 실내 포켓(지붕 없는 폐건물 + 오피스 실내). 모듈 라이브러리 빌드 + 수동 시드 3장(P4). *직접 MCP로 급조 ❌ — 반려 교훈.*
3. **몬스터 가독성** — 어두운 괴수가 어두운 노면에 묻힘 → **MonsterActor 림/이미시브**(툰 아님). [[2026-06-13-art-pivot-shader-direction]] §2a. artist 위임.
4. **런 루프 Phase 2 통합** — 9 지점 배선, **★H-1 최우선**(OperationTimer 이중구독 즉사 — 구독자 정확히 1개, RunManager sweep 제거). 전투감(게이트0)·병렬 전투 세션 합류 시. [[project_2026_06_13_runloop_phase1_built]].
5. **전투 측 1줄**(병렬 세션) — `ZombieController.Die/DieByWeapon` → `CombatEvents.RaiseAnomalyProcessed(threatTier)`.
6. **클로즈업 툰**(유예) — 컷인/정산/타이틀 기능 만들 때 *몬스터별 하이브리드*(아웃라인 전체+램프 매트만+전기 PBR유지)로. `MonsterToon.shader` 보관됨.

## 4. ★함정 (carry-forward)

- **MCP Camera_Capture = 죽은 프레임** → 디스크 렌더(`RenderPipeline.SubmitRenderRequest`)만. ([[project_graphics_verification_loop]])
- **RunCommand 내 System.Reflection = 하니스 즉사** → SerializedObject/공개 접근자/Type.GetType. ([[project_unity_mcp_reflection_gotcha]])
- **OperationTimer.OnExpired 이중구독(H-1)** = 커밋 플레이어 시계0 즉사. 통합 시 불변식=구독자 1.
- **병렬 세션이 에디터 공유** — isPlaying 가드, 타 세션 dirty 씬 저장 후 전환.
- **MCP AnimatorController 디스크 영속화** — 메모리만 저장됨, SaveAssets+ForceUpdate+재로드 검증.
- **★신규 — POLYGON ≠ Toon 라인** — Synty 내 다른 시리즈. 섞으면 내부 톤 불일치(월드 라인 결정 시 주의).
- **★신규 — 툰이 괴수를 어둡게 짓누름** — 클로즈업 툰 재시도 시 실제 Synty 거리 + 노출 정상 + 램프 재튜닝(검은 잉크화 방지). 흰 보이드 캡처 금지.
- **Protofactor 마젠타** → 메뉴 `Tools/ZombieCrush/Convert Protofactor Materials to URP`.

## 5. 산출물 인덱스 (미커밋)

- **톤게이트**: `Assets/_Project/Shaders/MonsterToon.shader`, `Assets/_Project/Scripts/Editor/MonsterToonToneGate.cs`, `docs/captures/2026-06-13-monster-toon-tonegate/`(11컷, ★판정근거=`_WIDE_*`)
- **런 루프**: `Assets/_Project/Scripts/` — `CombatEvents.cs`, `Run/{QuotaRunController,ThreatLevel,ThreatProfile,QuotaTierConfig}.cs`, `Upgrade/{RunUpgradeDef,RunUpgradeCatalog}.cs` + SO 3종 `Assets/_Project/Setting/Run/`
- **맵**: `Assets/_Project/Scenes/District_Whitebox_01.unity`(반려 — 재빌드 대상), `docs/captures/2026-06-13-district-whitebox/`
- **설계 로그**: `docs/02_logs/2026-06-13-*.md`(run-loop·modular-district·reference-research·map-architecture)

## 6. 상태 스냅샷

- **런 루프**: Phase 1 완료(코어 머신·SO·기능테스트 PASS). Phase 2(통합 배선) 대기 — 전투감 합류 게이트.
- **맵**: 설계(모듈 라이브러리·불변식) 완료, 빌드 미착수(라인 분기 대기). 화이트박스 1차 반려.
- **톤게이트**: 코어 통과(닫힘). 가독성 후속 + 클로즈업 유예.
- **전투**: 병렬 세션 소유(Caniathrox 완성, Venodonte 다음). CombatEvents 1줄 합의 대기.
- **그래픽**: 베이스라인 v1 잠금([[project_2026_06_10_graphics_baseline]]). 신규 그래픽 작업 동결 유지(가독성 림만 예외).
