# 2026-06-19 핸드오프 — 아트방향 동결(실사 Hell Express) + 바이옴 색온도 + 호드 가독성 게이트

**연결**: 권위 `docs/00_authority/2026-06-13-shader-direction.md`(상태헤더·§8 갱신됨) · 메모리 `project_2026_06_19_artdir_realistic_confirmed` · 직전 갈림길 `docs/01_handoffs/2026-06-14-monster-look-realism-vs-lowpoly-handoff.md` · `2026-06-18-monster-synty-lowpoly-look-handoff.md`(로우폴리 B = 파킹)

---

## TL;DR
오래 미결이던 **실사 vs 로우폴리** 아트방향 갈림길을 나·Codex·Challenger 3자 교차검토 후 **유저가 실사 A(Hell Express) 동결**. 이어 유저가 가져온 레퍼 **As One We Survive**(Unity·탑다운·스타일라이즈드3D 따뜻한 골든아워)와 Hell Express(청록)의 온도 충돌을 **바이옴당 지배색**으로 해소. 다음 작업 = **청록 도심 호드 가독성 게이트 캡처**(A 채택의 검증 조건, 재량 없음).

---

## 1. 동결된 결정 (이번 세션)

| # | 결정 | 근거 |
|---|---|---|
| 1 | **방향 = 실사 A (Hell Express)** | 3자교차 후 유저 판정. 로우폴리 B는 LOD/폴백만 잔존 |
| 2 | **가독성 = 아트디렉션의 보스 (재량 없음)** | Codex+Challenger 공동 조건. A의 유일 치명리스크 |
| 3 | **색온도 = 바이옴당 지배색** | 도심(데모)=청록 / 평원·황무지 후반=따뜻한 골든아워 |
| 4 | **기법 스택 = 라이팅+헤이즈+필믹포스트+데칼** (에셋 피델리티 아님) | As One We Survive가 탑다운 Unity에서 증명한 솔로 도달선 |

## 2. 3자 교차검토 결과 (왜 A인가)
- **순수 FIT(고밀도 가독성+테마)** = 나·Codex·Challenger **만장일치 B(로우폴리)**.
- **생산 비용** = A 우세(Protofactor 30종 PBR 네이티브 / B는 decimate+리리그+부위색+애니/VFX 재적용).
- **타이브레이크 발산** = Codex "약한 A(65%), 가독성 폭군화 조건" ↔ Challenger "A합의=매몰비용 가면, 못 버틴다, B로".
- **★수렴(견고 신호)** = 적대자(B변론)와 실용주의자(A변론)가 **반대편서 같은 중간 "Processed Realism"**에 착륙: Protofactor 메시 유지 + 머티리얼 단순화·외곽선/림·위협색 과장 + 깔끔한 로우폴리 월드 + **터미널 오버레이=통합자**. 순혈 50/50 플랫몬스터=함정(06-18 "하이폴리는 셰이더로 각 못 냄" 증명).
- **유저 최종 = A.** (무드 천장은 유저 권한, 셋 다 못 느낌.)

## 3. 색온도 — 바이옴당 지배색 (화해안, Hell Express 레퍼 doc §2-2 근거)
- **도심(데모/수직슬라이스) = 차가운 청록** → Hell Express 고딕-림보 공포.
- **평원·황무지 등 후반 = 따뜻한 골든아워** → As One We Survive 멜랑콜리.
- **기법은 공통.** 온도만 바이옴별로 갈림.
- ★**연쇄 = 정보색 적응:** 시안 정보는 *청록 도심선 묻힘* → 청록 구역은 정보를 **샤프 라인/형태로 분리 + 마젠타 오버드라이브 적극**(보색이라 폭발). 따뜻한 구역은 시안 유지.

## 4. "어느 정도 효과까지" — 레퍼 증거 (유저 질문 답)
- **Unity로 다크 시네마틱 도달 가능 증명:** GTFO(다크 협동 호러 익스트랙션, 우리 사촌)·Escape from Tarkov(리얼리즘 천장, 단 팀+커스텀툴)·Pacific Drive·The Forest.
- ⚠️ 우리 셰이더 레퍼 The Ascent는 **Unreal**이었음 → 유저 질문이 정확했던 이유.
- **★직격 증거 = As One We Survive** (`docs/03_reference/references/asonewesurvive_hero.jpg`): 탑다운+Unity+스타일라이즈드3D 포스트아포칼립스. **바닥 지형에 로우폴리 면이 보일 만큼 메시가 수수한데도 시네마틱** = 시네마틱 퀄이 *라이팅+헤이즈+그레이드+프롭/데칼*에서 나오지 에셋 피델리티가 아님을 증명. 우리 06-01 레퍼 doc §6-2와 일치, 솔로 도달선.
- **"언리얼 느낌" 4요소:** ①필믹 톤매핑(URP O) ②실시간 GI(HTrace SSGI/APV) ③볼류메트릭(COZY) ④고밀도 리얼 에셋(★안 쫓음 — Hell Express는 스타일라이즈드). HDRP 불필요, URP 확정 유지.
- **정직 한계:** AOWS는 프로모 히어로컷 1장(베스트케이스), 실제 게임플레이는 미검증. Tarkov/GTFO는 팀 산출물(바는 닿되 그 게임=솔로 아님).

## 5. ▶ 다음 작업 = 청록 도심 호드 가독성 게이트 캡처 (최우선, 재량 없음)
권위 `shader-direction.md §8` 0번 게이트. **A 채택의 검증 조건** — 기존 "통과"는 저밀도 2컷뿐, 호드 미검증.

### 캡처 스펙
- **씬:** `Greybox_ScanLit`(다크무드+포스트 기보유, 최소 손) — 또는 `Greybox_ScanLit_v2`
- **팔레트:** 청록 베이스(COZY height fog teal) + 따뜻한 포인트라이트를 *처리/액션 순간*에만
- **호드:** Protofactor 30~40기 (LV1 쫄 + Caniathrox 섞음) + 루팅/출구/텔레그래프 더미
- **기법:** AOWS 스택(헤이즈+소프트섀도+SSAO/AO+필믹그레이드+데칼) on 수수한 메시
- **가독성 규율:** 밝은 실루엣(액터는 뜸)·적 클래스 색코딩·**림은 위협/엘리트 한정**(전개체 림=새 노이즈, Challenger 경고)·텔레그래프 프레임지배·포그/그레이드 절제

### 판정 (2축 동시)
1. **청록 Hell Express 시네마틱이 URP로 실제 나오나** (vs `references/hellexpress_header.jpg`, `he_ss0/1.jpg`)
2. **호드에서 위협 파싱 되나** (적/루팅/출구/텔레그래프가 안 묻히나)
- 방법: **디스크 렌더(JudgeCam→PNG→Read)** — MCP Camera_Capture 죽은프레임 함정 회피([[project_graphics_verification_loop]]). 벤치=GTFO/Hell Express 옆에 두고 **vc(VisualCritic)** 대조. **최종 미적 판정 = 유저 빌드.**

### 체크리스트 (다음 세션)
- [ ] Greybox_ScanLit에 청록 팔레트 세팅(COZY teal fog + 따뜻한 포인트라이트)
- [ ] Protofactor 30~40 호드 + 루팅/출구/텔레그래프 더미 배치
- [ ] AOWS 기법스택 적용(헤이즈/AO/필믹/데칼)
- [ ] 디스크 렌더 캡처 → vc 대조(레퍼 옆) → 유저 판정
- [ ] 가독성 미달 시 규율 재튜닝 후 재캡처 (게이트 통과까지 루프)

## 6. ⚠️ 미결 / 긴장
- **실사 A 동결 ↔ 같은 날 다른 세션의 몬스터 로우폴리화/발광 작업** 긴장(`project_2026_06_19_emissive_treatment_blender_workflow` 명시). A 동결이 우선 — 로우폴리 메시작업은 LOD/폴백 한정으로 재정렬 필요. (단 **부위 발광 트리트먼트**는 A에도 유효 = 어두운 바디+발광부위, 실사 괴수 §2a Emissive 정책과 합치)
- 호드 게이트 미통과 시 가독성 규율로 풀리는지 vs 방향 재고인지 = 캡처 후 판정.

## 7. 자산 위치
- 권위: `docs/00_authority/2026-06-13-shader-direction.md`
- 레퍼 이미지: `docs/03_reference/references/` — `asonewesurvive_hero.jpg`(신규), `hellexpress_header.jpg`, `he_ss0.jpg`, `he_ss1.jpg`, `theascent_01/02.jpg`(=UE, 참고주의)
- 셰이더 레퍼 8장: `docs/captures/2026-06-13-shader-direction-refs/`
- 셰이더: `Assets/_Project/Shaders/` (MonsterToon/MonsterFlatStylized=로우폴리 파킹, ActorRimLit/CharacterToon 계열=실사 A)
- 씬: `Greybox_ScanLit`, `Greybox_ScanLit_v2`
