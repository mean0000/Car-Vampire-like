# AI 3D 에셋 생성 도구 + API/MCP + Unity 파이프라인 전수 조사

**작성:** 2026-06-14 (웹조사: WebSearch/WebFetch, 6월 현재 기준)
**목적:** 스타일라이즈드 로우폴리 **탑뷰(top-down) Unity URP 게임**의 맵 콘텐츠(소품·건물·모듈러 키트 조각)를 AI로 쉽게 양산해서 깔기.
**범위 한계:** 가격/스펙은 벤더 페이지·서드파티 비교글에서 수집. **추정과 사실을 분리 표기**했고, 출처는 각 절 끝 + 문서 맨 아래에 링크.

---

## 0. 우리가 이미 가진 것 (출발점)

메모리 `project_local_hunyuan3d_pipeline.md` 기준:

- **Hunyuan3D-2 로컬 서버** — `C:\Users\pc\AI3D`, 4070 Ti SUPER(16GB)에서 image→mesh 무료·무제한. shape-only(텍스처 OFF 필수, 빌드툴 부재). 단일뷰 + **멀티뷰(-2mv)** 둘 다 작동 확인.
- **Blender MCP** (ahujasid/blender-mcp 계열) — `generate_hunyuan3d_model`, `generate_hyper3d_model_via_text/images`, Polyhaven·Sketchfab 검색/다운로드, `execute_blender_code`. (Rodin 무료키는 `API_INSUFFICIENT_FUNDS`로 고갈됨 = 사실.)
- 산출물 = raw 고밀도 메시 + floater. **로우폴리 voxel-remesh는 디테일 파괴(사용자 거부 기록)** → face-preserving decimate만.

**핵심 결론(미리):** 우리가 가진 것은 *캐릭터/괴수 단발 메시*용으로 검증됨. **맵 콘텐츠(모듈러·반복·일관성)는 단발 생성형의 약점 구간** — 여기는 도구 선택이 달라진다 (§4, §5).

---

## 1. 도구별 상세 (이미지/텍스트→3D)

### Meshy
- **①생성:** 텍스트→3D, 이미지→3D, 멀티이미지→3D, 텍스처링/리텍스처, 리메시, **오토리깅 + 500+ 애니 프리셋**.
- **②API/MCP:** REST API 있음(Pro $20/mo부터 API 접근). **공식 MCP 서버 `@meshy-ai/meshy-mcp-server`** (npm, github.com/meshy-dev/meshy-mcp-server) — `meshy_text_to_3d`, `_image_to_3d`, `_remesh`, `_retexture`, `_rig`, `_animate` 등. 비공식 MCP도 여럿. **공식 Unity 플러그인** 존재.
- **③가격:** Free(200 cr/월) / Pro $20(1,000 cr) / Studio(서드파티 표기 $30, 4,000 cr) / Max·Enterprise. 텍스처 모델 1개 ≈ **$0.40**(1,000gen 기준). API는 pay-before-you-go 크레딧.
- **④로우폴리 적합도:** 양호. 게임용으로 널리 쓰임, 폴리 타깃·리메시 제공. (사실)
- **⑤URP 임포트:** GLB/FBX, 공식 Unity 플러그인으로 가장 매끈. SOC2/ISO 인증 = 스튜디오 친화.
- **⑥탑뷰 맵 적합도:** 소품 단발은 좋음. **모듈러 일관성은 약함**(단발 생성형 공통).
- **⑦자동화:** MCP + REST + Unity 플러그인 = 자동화 3박자 가장 완비.

### Tripo (Tripo3D / VAST AI)
- **①생성:** 텍스트/단일/멀티이미지→3D, **스타일 변환(LEGO/voxel/Minecraft/cartoon/clay)**, 오토리깅.
- **②API/MCP:** REST(WaveSpeed·PoYo·3DAIStudio 등 다수 게이트웨이). **공식 MCP `VAST-AI-Research/tripo-mcp`**(alpha, 15 tools, Tripo Blender 애드온 연동, Claude 호환).
- **③가격:** 크레딧 $0.01/cr, **가입 시 무료 2,000 cr**. Pro ≈ $15.9/mo(3,000 cr). v2.5 image-to-3D ≈ $0.30/run~. **P1 로우폴리 변종**: 텍스처無 $0.28/gen, 텍스처有 $0.35/gen.
- **④로우폴리 적합도:** ★ **스타일라이즈드 게임 에셋 최강 후보** — voxel/cartoon/clay 스타일 변환이 우리 룩에 직결. (사실: "stylized game assets에 best" 평가 다수)
- **⑤URP 임포트:** GLB(하위 티어). 매끈.
- **⑥탑뷰 맵 적합도:** 스타일 일관성(같은 카툰 톤)은 Meshy보다 유리. 단, 치수 정합은 여전히 단발 한계.
- **⑦자동화:** 공식 MCP + 저렴한 크레딧 + 넉넉한 무료티어 = 실험·양산 둘 다 좋음.

### Rodin / Hyper3D (Deemos)
- **①생성:** 이미지/텍스트→3D(10B 파라미터). Gen-2.5는 sculpt급 디테일, **High-Poly Quads**, baked normals, 멀티이미지.
- **②API/MCP:** REST API(Business $120/mo부터, 120–240 RPM). **Blender MCP에 내장**(우리 보유 — MAIN_SITE/FAL_AI 모드).
- **③가격:** Free=pay-as-you-go **$1.5/credit**(사적 에셋 10개). Creator $30/mo(~60 모델, API 없음). **Business $120/mo(~416 모델, API)**. Enterprise=온프렘.
- **④로우폴리 적합도:** 품질 1위지만 **고폴리·고비용** — 멀리서 보는 맵 소품엔 과잉. (평가: 최고 품질·가장 느림 2–3분)
- **⑤URP 임포트:** GLB/OBJ/FBX/USDZ/STL, quads 지원이 retopo에 유리.
- **⑥탑뷰 맵 적합도:** ✕ 비용 대비 부적합(탑뷰는 디테일이 안 보임). 히어로 에셋·클로즈업용.
- **⑦자동화:** API는 Business 티어 락. **무료키 고갈로 현재 우리 MCP 경로 막힘**(사실, 메모리 기록).

### Tencent Hunyuan3D (우리 로컬 보유)
- **①생성:** 텍스트/이미지→3D, 2단(DiT geometry + Paint texture). 2mini/2mv/3.0/3.1. **face count 40K–1.5M 타깃 가능 = 로우폴리도 지원**(클라우드 API 기준).
- **②API/MCP:** Tencent Cloud REST(`hunyuan.intl.tencentcloudapi.com`, TC3 서명) + 3DAIStudio 게이트웨이. **로컬 self-host API + Blender MCP**(우리 구성). 오픈소스(가중치 공개).
- **③가격:** **로컬 = 무료·무제한**(자체 GPU). 클라우드 API는 종량.
- **④로우폴리 적합도:** raw는 고밀도. 우리 경험상 **voxel-remesh는 디테일 파괴** → 클라우드의 face-count 타깃 또는 face-preserving decimate 필요.
- **⑤URP 임포트:** GLB. 로컬은 **텍스처 OFF**라 Unity에서 머티리얼 별도(우리는 셀셰이드라 오히려 무관).
- **⑥탑뷰 맵 적합도:** 무료라 양산엔 매력적이나, **모듈러 정합·일관성은 동일한 단발 한계**.
- **⑦자동화:** 이미 MCP 연결됨. 비용 0이 최대 강점.

### CSM.ai (Common Sense Machines — **2026-01 Google 인수**)
- **①생성:** 이미지/멀티뷰/텍스트→3D, **image-to-Kit**(킷 분해). GLB/OBJ/USDZ.
- **②API/MCP:** REST API 있음. 업계 관측상 bulk API 확장 예상(미확정).
- **③가격:** 크레딧형, Free(가입 1cr) / Maker(100cr/월) / Creative Pro(400cr/월), 추가 $0.20/cr. 웹 Pro $20/mo.
- **④~⑦:** **Google 인수 후 로드맵 불확실 → 프로덕션 채택 위험**(서드파티 경고). 보류 권고.

### Luma (Genie / Ray)
- **①생성:** 텍스트→3D(Genie), 비디오/NeRF 캡처. Ray3.14 = 1080p·4x·3x 저비용.
- **②API/MCP:** Luma API(AIMLAPI 등 경유). 전용 3D MCP는 미확인.
- **③가격:** Lite $9.99/mo~. 씬당 ~$1(캡처).
- **④~⑥:** 비주얼은 인상적이나 **게임엔 retopo 필요**, 포토리얼 지향 → 우리 스타일라이즈드와 결 다름. 부적합.

### Stability AI (SF3D / Stable Fast 3D)
- **①생성:** 이미지→3D, **~0.5초(최속)**. GLB(알베도만, PBR 없음).
- **③가격:** ~$0.07–0.10/gen.
- **④~⑦:** 속도 압도적이나 디테일·PBR 약함. **대량 프로토타입 스캐터엔 의외로 후보**(빠르고 쌈), 단 품질 하한.

### Kaedim
- **①생성:** 컨셉아트→**사람이 리뷰한** 프로덕션 3D(수일 소요). 외주 대비 90%↓.
- **②~③:** 스튜디오 파이프라인용, 셀프서브 API/MCP보다 서비스형. **솔로·자동화엔 부적합**(턴어라운드 느림).

### Alpha3D
- 텍스트/이미지→3D 커머스 지향. 2025–26 비교글에서 게임 에셋 주류 추천에서 빠짐. **탑뷰 게임 맵엔 우선순위 낮음.** (정보 빈약 = 추정)

**§1 출처:** Meshy 가격/MCP/Unity, Tripo 가격/MCP/스타일, Hyper3D 가격, Hunyuan 스펙, CSM 인수/가격, Luma, 3DAIStudio 종합 — 아래 출처 목록 참조.

---

## 2. Sloyd (파라메트릭 — 모듈러 맵 조각의 핵심 후보) ★

별도 절로 뺀 이유: **단발 생성형이 아니라 절차적(procedural) 파라메트릭** 이라 우리 핵심 질문("모듈러 반복 조각 일관성")의 정답에 가장 가깝다.

- **①생성:** 텍스트 프롬프트 + **파라메트릭 편집(슬라이더/토글)**. 정교하게 만든 procedural generator 기반 → 출력이 **이미 최적화·game-ready**(UV·LOD·클린 메시).
- **②API/MCP:** **Unity SDK**(클라이언트사이드/런타임 생성, ms 단위) + 서버 API. 전용 3D-생성 MCP는 미확인(MCP 없음).
- **③가격:** Plus $15/mo(웹앱·플러그인, **무제한 생성**). SDK ~$324/yr, API ~$480/yr. ⚠️ **API 클라이언트 모집 일시 중단**("after September 2025 다시 확인" 표기 — 현재 재개 여부 미확인 = 사실/주의). 비교 기준 텍스처 모델 1개 ≈ **$0.015**(1,000모델/월) = Meshy의 ~1/33.
- **④로우폴리 적합도:** ★ generator가 본래 로우폴리·게임 지향. (사실)
- **⑤URP 임포트:** game-ready 메시, Unity SDK 직임포트(런타임 포함).
- **⑥탑뷰 맵 적합도:** ★★ **파라메트릭 = 같은 generator로 치수·비율 일관 변형** → 건물/벽/도로 변종을 정합 보장하며 양산. **모듈러 키트의 정답에 가장 근접.** 단, 카탈로그에 있는 카테고리에 한정(임의 무엇이든 ✕).
- **⑦자동화:** 슬라이더 변주 + (재개 시) API 배치. **storage=파라미터만 저장**이라 변종 폭발에 강함.

**한계:** generator 라이브러리에 없는 형태는 못 뽑음(생성형의 "무엇이든"과 트레이드오프). API 모집 중단 상태 확인 필요.

**§2 출처:** Sloyd SDK/API 페이지, Sloyd 가격 비교 블로그.

---

## 3. 텍스처/머티리얼 AI

### Scenario (Scenario.gg / scenario.com)
- **생성:** 2D 게임 에셋 + **PBR 텍스처 맵(albedo/normal/roughness/metallic) 초 단위**, **스타일 학습(자기 레퍼로 모델 파인튜닝)** → 아트스타일 일관.
- **API/플러그인:** API-first, **공식 Unity 플러그인**(scenario-labs/Scenario-Unity).
- **가격:** $20–200/mo.
- **적합도:** ★ 우리가 **셀셰이드/스타일라이즈드**라면 — 스타일 학습으로 *프로젝트 톤에 맞는* 텍스처·트림시트·타일 텍스처를 일관 양산. Hunyuan 로컬이 텍스처 OFF라 **텍스처 보충처로 궁합 좋음**. (사실)

### Layer.ai
- 게임 에셋·텍스처 AI(스타일 일관 지향). 2026 비교에서 Scenario와 경쟁군. 정보 빈약(추정) — 우선순위는 Scenario 다음.

### 기타 PBR 생성
- Meshy/Tripo/Hunyuan 자체 텍스처링, 3DAIStudio 텍스처 툴 번들 등. URP는 **Metallic workflow**로 세팅 권장(§6).

**§3 출처:** Scenario 텍스처 블로그, Scenario-Unity github.

---

## 4. 도구 요약표

| 도구 | 생성물 | API | MCP | 가격(요지) | 로우폴리 | URP임포트 | 탑뷰맵 | 자동화 |
|---|---|---|---|---|---|---|---|---|
| **Sloyd** ★모듈러 | 파라메트릭 메시 | ○(모집중단?) | ✕ | $15/mo 무제한 / API $480yr | ★★ | ★★(Unity SDK) | **★★** | ★(파라변주) |
| **Tripo** ★스타일 | 메시+스타일변환+리그 | ○ | **○ 공식** | $0.01/cr, 무료2000 | ★★ | ★★ | ★ | ★★ |
| **Meshy** | 메시+텍스+리그+애니 | ○ | **○ 공식** | Free200 / Pro$20 | ★ | ★★(공식플러그인) | ★ | ★★ |
| **Hunyuan3D** (보유) | 메시(+텍) | ○ | **○ 연결됨** | **로컬 무료** | △(remesh주의) | ★(텍 별도) | ★ | ★(MCP) |
| **Rodin/Hyper3D** | 고폴리 메시 | ○($120) | ○(키고갈) | $1.5/cr, Biz$120 | ✕(과잉) | ★★ | ✕ | △ |
| **Stability SF3D** | 메시(알베도) | ○ | △ | ~$0.08/gen | △ | ★ | △(대량proto) | ★ |
| **CSM.ai** | 메시+Kit분해 | ○ | △ | $0.2/cr | ★ | ★ | ★ | 보류(인수) |
| **Luma** | 메시/NeRF | ○ | ✕ | $9.99/mo~ | ✕(retopo) | △ | ✕ | △ |
| **Kaedim** | 사람리뷰 메시 | 서비스형 | ✕ | 스튜디오가 | ★ | ★ | △ | ✕(느림) |
| **Scenario** (텍스처) | PBR텍스처+2D | ○ | △ | $20–200 | — | ★★(플러그인) | (텍보충) | ★ |

(★★=강함, ★=좋음, △=조건부, ✕=부적합 / 추정 포함)

---

## 5. 핵심 질문 답변

### Q1. Hunyuan 로컬 + Blender MCP 위에 *뭘 더* 얹어야 맵 에셋 양산이 쉬워지나?

3가지를 권장(중복 아님, 역할 분담):

1. **Sloyd (모듈러 조각용)** — Hunyuan이 못 하는 *치수 정합·반복 변종*을 파라메트릭으로 메움. 건물/벽/도로/펜스 같은 키트가 여기 강점. ⚠️ API 모집 재개 여부 확인 필요(웹앱·Unity SDK는 가용).
2. **Tripo 공식 MCP (스타일 단발 소품용)** — 무료 2,000cr로 즉시 시작, **카툰/voxel/clay 스타일 변환**이 탑뷰 스타일라이즈드 룩에 직결. MCP라 Hunyuan과 같은 대화형 워크플로에 얹힘.
3. **Scenario (텍스처 보충)** — Hunyuan 로컬이 텍스처 OFF라 비는 자리. 스타일 학습으로 *우리 톤 고정* 텍스처를 Unity 플러그인으로 바로.

> Rodin은 무료키 고갈 + 탑뷰 과잉이라 **추가 투자 비권장**(히어로 클로즈업 필요 시에만 Business 고려).

### Q2. 모듈러/반복 맵 조각(건물·도로·벽)을 AI로 일관되게? — 단발 vs 파라메트릭

**핵심 진단:** 단발 image/text→3D(Hunyuan/Meshy/Tripo)는 매번 *치수·피벗·톤이 미세하게 달라* **모듈러 키트(스냅 정합)에 근본적으로 약함.** 모듈러는 "10cm 그리드에 딱 맞는 반복 조각"이 생명인데 생성형은 그 정합을 보장 못 한다.

- **최선 = 파라메트릭(Sloyd)** — 같은 generator의 슬라이더 변주라 비율·격자가 일관. 건물/벽/도로의 "같은 톤 다른 변종"에 정답.
- **차선 = 단발 생성 + 후처리 정규화** — Hunyuan/Tripo로 뽑고 **Blender MCP(`execute_blender_code`)로 batch 정규화**: 바운딩박스 그리드 스냅, 피벗을 바닥-중앙으로, 균일 스케일, floater 제거, face-preserving decimate. (우리가 이미 floater 정리 스크립트 보유 → 그리드 스냅 추가만.)
- **권장 하이브리드:** *키트 코어(반복 구조물)=Sloyd 파라메트릭*, *디테일 소품(차·쓰레기통·간판·잔해)=Tripo/Hunyuan 단발 + Blender MCP 정규화*. 두 줄기를 Unity에서 한 셀셰이드 머티리얼로 통일하면 톤 봉합.

### Q3. 추천 랭킹 (비용·품질·통합 종합)

1. **Sloyd** — 모듈러 맵 조각의 정답(파라메트릭 정합·무제한·게임레디·Unity SDK). 솔로·양산 친화. *유일 리스크: API 모집 재개 확인.*
2. **Tripo (공식 MCP)** — 스타일라이즈드 단발 소품 최강 + 무료 2,000cr + 저가 크레딧 + 우리 MCP 워크플로 직결.
3. **Hunyuan3D 로컬(보유 유지)** — 비용 0. 대량 단발 + Blender MCP 후처리 정규화 파이프의 엔진으로 계속.
4. **Scenario** — 텍스처/머티리얼 일관성 보충(셀셰이드 톤 고정).
5. **Meshy** — 오토리그/애니가 필요해질 때(괴수·NPC) 보강. 맵 콘텐츠 1순위는 아님.

---

## 6. Unity URP 임포트 메모 (공통 함정)

- **GLB가 표준 컨테이너**(geometry+계층+PBR 텍스처 1파일). Unity 6은 GLB/FBX 임포트 양호 — KhronosGroup **UnityGLTF**로 런타임 로드도 가능.
- **핑크(마젠타) = 셰이더 못 찾음 = 렌더파이프 불일치.** 임포트 전 RP 에셋 설정 → `Edit ▸ Render Pipeline ▸ URP ▸ Upgrade Project Materials`. (우리 메모리의 "마젠타 지뢰"와 동일 패턴.)
- **머티리얼 Workflow Mode = Metallic** 로 두면 PBR 정합 깔끔.
- **노멀 뒤집힘**(안쪽 면 보임 = Blender↔Unity 좌표계): Blender export 전 `Recalculate Outside`, Unity 임포트 후 `Generate Smooth Normals`.
- **Hunyuan 로컬은 텍스처 無** → 우리는 셀셰이드 NPR이라 오히려 머티리얼을 Unity에서 입히는 게 정상 경로(메모리 일치).
- **GLB 부모 transform 함정**(Y-up→Z-up empty 래핑)은 우리 메모리에 이미 기록 — batch 정규화 시 `clear parent + transform_apply`로 identity 통일.

**§6 출처:** neural4d Unity 임포트 가이드, yugma GLB→Unity, UnityGLTF, threedium.

---

## 출처 (Sources)

**가격/스펙 — 벤더:**
- Meshy 가격: https://www.meshy.ai/pricing , API: https://www.meshy.ai/api , docs: https://docs.meshy.ai/en/api/pricing
- Meshy 공식 MCP: https://github.com/meshy-dev/meshy-mcp-server
- Tripo 공식 MCP: https://github.com/VAST-AI-Research/tripo-mcp
- Hyper3D/Rodin 가격: https://hyper3d.ai/pricing , API 가이드: https://hyper3d.ai/blog/rodin-api-3d-generation
- Sloyd API/SDK: https://www.sloyd.ai/api , https://www.sloyd.ai/sdk , 가격비교: https://www.sloyd.ai/blog/3d-ai-price-comparison
- Scenario 텍스처: https://www.scenario.com/blog/ai-texture-generation , Unity 플러그인: https://github.com/scenario-labs/Scenario-Unity
- Hunyuan3D 오픈소스: https://github.com/Tencent-Hunyuan/Hunyuan3D-2 , Cloud API: https://www.tencentcloud.com/document/product/1284/75540

**종합 비교 — 서드파티:**
- 3DAIStudio "Best 3D Model Generation APIs 2026": https://www.3daistudio.com/blog/best-3d-model-generation-apis-2026
- Meshy "Best AI Tools for 3D Game Assets 2026": https://www.meshy.ai/blog/best-ai-tools-for-3d-game-assets
- Tripo 가격 가이드: https://lorphic.com/tripo-ai-pricing-explained-guide/
- CSM 가격/Google 인수: https://powerusers.ai/ai-tool/csm-ai/ , https://www.aicerts.ai/news/google-ai-acquisition-boosts-spatial-3d-strategy/
- Costbench Meshy: https://costbench.com/software/ai-3d-generation/meshy/

**MCP/Blender:**
- ahujasid/blender-mcp: https://github.com/ahujasid/blender-mcp , Hunyuan 통합: https://deepwiki.com/ahujasid/blender-mcp/3.4-hunyuan3d-integration
- RFingAdam/mcp-blender(218 tools): https://github.com/RFingAdam/mcp-blender/

**Unity 임포트:**
- neural4d: https://blog.neural4d.com/user-guide/how-to-import-3d-models-into-unity-6-fbx-glb-ai-workflow/
- yugma GLB→Unity: https://yugma.studio/blog/glb-export-unity-tutorial/
- UnityGLTF: https://github.com/KhronosGroup/UnityGLTF

**사실/추정 구분:** 가격 수치는 벤더 페이지 직접확인이 1차(Meshy/Hyper3D/Sloyd), 서드파티 비교글은 교차검증용. CSM Google 인수·Sloyd API 모집중단·Rodin 무료키 고갈은 사실. Alpha3D/Layer.ai 세부는 정보 빈약으로 추정 표기.
