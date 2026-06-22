# 엔진 결정 + 아트/카메라 북극성 (확정)

> **상태: 확정 (2026-06-21)** · 방향 동결. 엔진 논쟁 재개 금지 — 재론하려면 본 문서의 기각 근거를 정면 반박하는 새 사실이 있어야 함.
> 관련 메모리: `project_2026_06_21_engine_stay_unity_ruiner_ref` · 관련 권위: [[2026-06-19-artdir-realistic-confirmed]] 계열(Processed Realism), [[shader-direction]]

---

## 0. 한 줄 결정

**엔진 = Unity (URP) 유지.** 언리얼 전환 검토 후 **유저가 기각.** 룩/이동/카메라의 겨냥점(북극성) = **Ruiner / Ruiner 2**. 진짜 작업 변수 = 엔진이 아니라 **라이팅·포스트·셰이더·에셋 큐레이션 크래프트**(= 유저 TA 진로 그 자체).

유저 최종 발언: *"내가 볼때 그냥 내 실력 부족인 것 같네, 그냥 유니티로 잘 해볼게."* → 엔진이 갭이 아님을 유저 스스로 확인하고 Unity 유지로 착지.

---

## 1. 결정: Unity 유지 / 언리얼 기각 — 근거 (검증됨)

| 근거 | 내용 |
|---|---|
| **엔진은 갭이 아님** | **V Rising = Unity HDRP(+DOTS) 아이소메트릭 액션, 출시 히트작**(1주 100만장, PS5 이식). "리얼 + 아이소 + Unity 출시"의 존재증명. Book of the Dead·Enemies = Unity 포토리얼 천장(둘 다 Unity 데모팀, HDRP). → "Unity가 그 색/리얼 못 낸다"는 거짓. 천장은 언리얼과 같은 데 있음. |
| **매몰 투자** | Protofactor 몬스터 30+종(URP 변환 완료)·Synty Sidekick·Vexa·RPG Mecanim 카타나 애니·COZY Pro·Feel·DOTween·Vefects·커스텀 셰이더(SlashArc/ThreatArc/전투 시스템) — **전부 Unity 전용. 언리얼 가면 0원 + 커스텀 코드 전부 손실.** |
| **에셋 병목은 엔진 무관** | 리얼 환경 에셋 병목은 어느 엔진이든 유저 몫. 언리얼 무료 Megascans가 *오히려* 환경엔 유리하나, 그게 매몰+코드손실을 정당화하지 못함. Fab은 크로스엔진이지만 엔진 종속물(플러그인·프리팹/머티리얼·컴포넌트 셋업·코드)은 안 넘어감. |
| **솔로 도달선** | Ruiner 풀 포토리얼은 스튜디오(Reikon, 수년)의 산물 — 솔로 불필요. 목표는 *읽히는 따뜻한 버전* = 이미 동결된 "Processed Realism + 가독성=보스"([[2026-06-19-artdir-realistic-confirmed]]) 선. 그 선까진 솔로 라이팅/포스트로 도달. |

### 미채택 / 보류
- **HDRP** — 미채택. (URP 유지. HDRP 이주는 코드·게임플레이·애니는 살아남으나 머티리얼/셰이더/라이팅 전부 재작업이라 비용 큼. 필요해지면 별도 결정.)
- **언리얼 전환** — 기각.
- **벽타기/파쿠르 1인칭 무브 직역** — 탑다운에 불가(수직 가독성 없음). → 대시-슬로모-주스가 그 "흐름감"의 탑다운 번역본.

---

## 2. 북극성: Ruiner / Ruiner 2

유저가 영상에서 "배경부터 전부 내가 원하던 스타일"로 지목. **겨냥점으로 박아두고 한 걸음씩 접근** — 도달 못 해도 그 방향으로 당겨지는 것만으로 게임이 좋아짐. (Ruiner = Unreal 제작이나, 본 결정대로 그 *룩*은 Unity로 추적.)

핵심 시그니처: 사이버펑크 탑다운/아이소 + 만족스러운 근접 + 충전식/체인 대시 + 불릿타임 슬로모 + 네온(레드/시안/마젠타) + 딥섀도우 + 따뜻한 헤이즈 + 짙은 파티클.

---

## 3. "포근한 노을" 기술 분해 (전부 URP 가능)

유저 타깃 = Ruiner 2 잔해 거리의 *낮은 노을 햇빛 + 길고 부드러운 그림자 + 먼지 헤이즈* "포근함". 4겹의 합이며 4개 다 URP 기능:

1. **낮은 각도 따뜻한 디렉셔널 라이트** — 색온도 + 낮은 angle
2. **길고 부드러운 그림자** — URP Soft Shadows + Shadow Distance
3. **따뜻한 컬러그레이드** — Volume: Tonemapping + Color Adjustments (앰버로 통일)
4. **★먼지 헤이즈/포그 (포근함의 정체)** — URP Fog(따뜻한 색) + 더스트 파티클
5. **★그림자 속 따뜻한 바운스** — baked GI 또는 **APV**(Greybox_ScanLit에서 작동 확인)

> **함정:** 대부분의 Unity 노을 시도가 밋밋한 이유 = ①따뜻한 헤이즈/포그 누락 ②바운스 GI 누락. 햇빛만 주황으로 칠하면 "노란 조명"이 됨. 포근함 = 빛이 *공기와 그림자에 스며든* 것.
> **유일 약점:** 진짜 볼류메트릭 갓레이(먼지 사이 빛줄기) = HDRP 네이티브, URP는 커스텀/에셋 필요. 단 타깃 프레임은 갓레이보다 헤이즈에 가까워 포그+더스트로 90% 충당. **COZY Pro 보유** = 노을 헤이즈/시간대에 직접 도움.

---

## 4. 카메라/이동 기법 스펙

탑다운에서 속도감/흐름감을 만드는 검증된 기법. **나(오케스트레이터) + Codex 독립 리서치가 수렴한 항목 = 신뢰 / 발산 = 유저 판정.**

### 수렴 (둘 다 독립 지목 → 신뢰, 우선 구현)
1. **조준/대시 방향 카메라 리드** — 화면 중심을 진행 방향으로 5~12% 앞당김 (Hotline Miami). 탑다운 속도감의 근본.
2. **대시 *적중* 에만 카메라 임펄스** — 상시 흔들기 ❌, 적중/벽충돌/피니시 순간만 짧고 강하게 (Ruiner).
3. **페이즈 전환용 줌** — 평시 가독성 유지, 엘리트 결투/패링/피니시에만 순간 줌인, 웨이브 클리어 시 줌아웃 (Furi).
4. **쉐이크 계층화** — 일반 베기 미세 / 강공격 중간 / 피니시·폭발·보스 경직 강함 (Nuclear Throne). 모든 동작에 같은 쉐이크 = 피로.

### 발산 (Codex만 추가 — 유저 판정 대상)
- **Mr. Shifty** — 아슬아슬 회피/퍼펙트 슬라이드 시 0.25~0.5초 슬로모 **+ 대시 쿨 일부 환급**. 우리 대시캔슬 리듬에 직결.
- **Ape Out** — 처치 체인 시 쉐이크보다 **카메라 리커버리 속도 + 사운드/플래시 빈도**를 올림.

> ⚠️ 모든 줌/슬로모는 뱀서류 호드 밀집전에서 가독성 깨지지 않게 **0.3초 이하 이벤트성**으로. 모션블러는 **대시 순간에만**(전역 ❌ — 호드 가독성 보호, "가독성=보스" 게이트 준수).

---

## 5. 순서대로 작업 계획 (Sequenced Roadmap)

> 유저 요청: *"일은 순서대로 해야 하니까."* 아래는 이 방향을 게임에 얹는 순서. 각 단계는 산출물(캡처/빌드)로 검증 후 다음으로.

**Phase A — 무드 증명 (현재 다음 액션)**
- A-1. 잔해 씬(`Top_Down_Post-Apocalyptic_Pack/TD_Demo`)에 골든아워 무드 패스 1장 캡처 — §3 레시피(따뜻 디렉셔널 + 롱섀도우 + 앰버 그레이드 + 헤이즈 + APV 바운스).
- A-2. JudgeCam → PNG 렌더([[graphics-verification-loop]] 경로, MCP 캡처는 캐시라 RunCommand). **게이트 = 시각 비트(vc+Codex 대조). 최종 미적 판정 = 유저.**
- A-3. 결과 → "됐다" 자신감 / "안 됐다" = 정확한 부족 지점 = 다음 학습 타깃.

**Phase B — 카메라 속도감 레이어**
- B-1. 조준/이동 방향 카메라 리드 (Cinemachine).
- B-2. 대시 적중 카메라 임펄스 + 쉐이크 계층화 (Feel 보유).
- B-3. 대시 한정 모션블러.

**Phase C — 슬로모/주스 (발산 항목 유저 판정 후)**
- C-1. 퍼펙트 회피/슬라이드 슬로모 + 대시 쿨 환급 (Mr. Shifty식, 유저 OK 시).
- C-2. 페이즈 전환 줌(패링/피니시).

> 본 계획은 진행 중인 트랙([[block-grid-map-model]] 맵, 카타나 하데스풍 전투)과 병행 — 무드/카메라는 그 위에 얹는 레이어.

---

## 6. 참조 (검증된 출처)

- V Rising = Unity HDRP: https://unity.com/resources/stunlock-studios-v-rising
- Unity 포토리얼 데모(천장): https://unity.com/demos/unity-originals · https://unity.com/demos/enemies
- Hardspace: Shipbreaker = Unity HDRP: https://unity.com/case-study/shipbreaker
- Ruiner = Unreal Engine 4: https://www.unrealengine.com/en-US/developer-interviews/revving-the-engine-ruiner
- Ruiner 2 발표: https://gameinformer.com/2026/03/05/ruiner-2-announced-and-it-features-co-op-with-up-to-3-players
- 마켓플레이스(Fab/Megascans, Unity vs Unreal 에셋): https://support.fab.com/s/article/Fab-Transition-FAQs
