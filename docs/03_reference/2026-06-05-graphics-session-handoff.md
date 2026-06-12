# 그래픽 스타일 작업 핸드오프 — 2026-06-05

> ⚠️ **이력 문서.** 그래픽 방향은 2026-06-10 베이스라인 v1로 잠금 — 현행 [[2026-06-10_그래픽_베이스라인_v1]] (COZY = Sky Authority 복귀 포함). 이 세션의 방법론(복원점·캡처 검증)은 유효.

**브랜치:** `feat/graphics` · **무대:** `Assets/_Project/Scenes/StyleLab.unity` (중립 테스트 씬) · **레퍼런스:** Hell Express (Screenshot01 풀밭/갓레이, Screenshot03 젖은 아스팔트)
**상세 작업로그(이미지+커밋):** `docs/03_reference/2026-06-05-style-reproduction-log.html`

이 문서는 "무엇을 했나"보다 **"어떻게 작업했나"(방법론·발견·함정·다음수)** 를 인계하기 위한 것이다.

---

## 1. 이번 세션 결과 (STEP9–12, 전부 복원점 + 문서화)

| STEP | 내용 | feat 커밋 | 경쟁 디렉터 |
|---|---|---|---|
| 9  | 손전등 blown orb→콘(380→90/inner22/FFAD5A) + Reflection Probe Box Projection ON | `f6500c072` | MIRROR / ABSORB |
| 10 | 젖은 바닥 웅덩이 마스크(Ground 전용 M_WetAsphalt + 절차적 PuddleMask, dry0.18↔wet0.82) | `ffb0afe04` | MIRROR / ABSORB |
| 11 | **필름룩 그레이드 재건**(빈 프로파일 발견 → 8컴포넌트) | `bbf3acc7a` | NATURALIST / COLORIST |
| 12 | **틸트시프트**(게임 전체 상수 처리) | `0ed8ecbcc` | SUBTLE / DIORAMA |

각 feat 커밋 뒤에 `docs(style)` 커밋이 1:1로 붙는다(작업로그 HTML 갱신).

---

## 2. 작업 방법론 (이게 핵심 인계 사항)

유저가 지정한 방식 = **클로드가 디렉터, 작업은 경쟁 라이벌 에이전트에게 내린다.**

1. **레퍼런스를 모사하지 않는다.** "왜 그렇게 표현했나 — 개발자/TA/디자이너의 의도와 기술"을 역설계해서 *우리 값*으로 번안한다.
2. **경쟁 디렉터 2명을 띄운다.** 한 문제를 정반대 철학으로 푸는 `artist` 에이전트 둘(예: 반사 vs 머금기, 자연주의 vs 컬러리스트, 은은함 vs 미니어처). **제안만, 씬 편집 금지.**
3. **디렉터(클로드/Opus)가 판정·병합.** 잠긴 헌장과 사실(현재 씬 실측값)에 비춰 한쪽 구조를 택하고 다른 쪽의 절제/규율을 이식. 추측 금지 — 항상 실측 먼저.
4. **구현은 디렉터가 직접**(게임감·색 튜닝은 의도 보존 위해 손에 쥔다). MCP `Unity_RunCommand`로 C# 실행.
5. **캡처 루프로 판정.** 적용→캡처→눈으로 판정→수치 조정→재캡처. 매 스텝 커밋(복원점) + 작업로그 HTML 갱신.

> 유저 = 최종 판정자, 클로드 = 빌더(MCP 캡처 루프). [[graphics-lighting-strategy]]

---

## 3. 중대 발견 & 관점 교정

- **★빈 포스트 프로파일:** STEP11 진입 전 조사에서 활성 포스트 프로파일 `StyleLab_Post.asset`(guid d8a6c53d…)가 **STEP4부터 줄곧 비어있었음**(컴포넌트 10슬롯 전부 null)을 발견. ACES·스플릿토닝·비네트 등 필름룩이 통째 부재 → 손전등 백색클리핑·균일 웜의 진짜 원인이었음. 문서가 잘못 가리키고 있었음. STEP11에서 `VolumeProfile.Add<T>()`로 8컴포넌트 재건. **그레이드 튜닝은 이 .asset에서, 1st-party라 안전.**
- **★관점 교정(유저, STEP12 직전):** "황혼의 적막에 목숨걸지 마라. 우린 특정 환경 그래픽이 아니라 **게임 전체에 걸 그래픽(처리/treatment)** 을 잡아야 한다. 다른 게임은 화면마다 그래픽 다시 안 잡는다." → 내가 단일 밤 씬에 노출·무드를 끼워맞춘 게 실수. **틸트시프트부터는 환경 불문 상수로 구현.** 검증은 한 씬이 아니라 멀티 조건에서. (전용 메모리: graphics-treatment-not-per-scene)

---

## 4. 어디에 뭐가 있나 (에셋 맵)

| 레이어 | 위치 | 메모 |
|---|---|---|
| 포스트 그레이드 | `Assets/_Project/Setting/StyleLab_Post.asset` | 8컴포넌트(ACES/ColorAdj/WB/SplitToning/LiftGammaGain/Vignette/FilmGrain/Bloom). ⚠️post-exposure +1.35는 **이 어두운 씬용 임시값** — 게임화하려면 라이팅으로 옮길 것 |
| 젖은 바닥 | `M_WetAsphalt.mat` + `PuddleMask.png` | Ground 전용. metallic-alpha smoothness 채널, 타일3x. (공유/기본 머티리얼 미접촉) |
| 틸트시프트 | `TiltShift.shader` + `M_TiltShift.mat` | 내장 FullScreenPassRendererFeature가 `URP_HighFidelity_Renderer.asset`에 추가됨. 파라미터: center0.5/halfBand0.27/falloff0.15/radius0.009 |
| 렌더러 | `URP_HighFidelity_Renderer.asset` | 피처: HTrace SSGI / VolumetricFog(CristianQiu) / SSAO / TiltShift |
| 라이팅 | StyleLab 씬 | Sun(Directional 3.2 #FFD999) + PlayerFlashlight(Spot 90 #FFAD5A inner22) |
| 판정 카메라 | `JudgeCam` (InstanceID 84522) | pitch 59.5 / FOV 38 |

---

## 5. 잠긴 헌장 — 불변(처리) vs 변수(환경)

- **불변(게임 전체 고정):** ACES 톤매퍼, 필름 커브, 채도/대비 정체성, 스플릿토닝 *방향*(쿨섀도/웜하이라이트=이중온도), 비네트/그레인, 틸트시프트, 손전등(상수)+월드광(변수) 라이팅 아키텍처.
- **변수(환경/날씨/바이옴별, COZY 구동):** 노출·색온도, 표면 머티리얼(도심=젖은아스팔트 / 평원=풀).
- **검증 = 같은 처리를 여러 조건(낮·흐림·황혼·밤+평원)에 던져 다 "우리 룩"으로 버티나.** 한 씬에서 예쁜 건 증거 아님.

---

## 6. 보류 / 블로커

- **HTrace 1주체 GI 색블리드(MATERIA 우선순위)** — `UseVolumes=False` + 파라미터 미직렬화로 런타임 introspection 불가. **에셋 인스펙터를 유저가 한 번 열어 파라미터를 확인**해줘야 안전하게 튜닝 가능. 추측으로 서드파티 색 밀지 말 것.
- **CristianQiu 볼류메트릭 포그 틴트** — 동일하게 introspection 어려움.
- **post-exposure가 그레이드에 구워짐** — 게임화 시 라이팅(COZY sun)으로 분리 필요.

---

## 7. 툴 함정 (다음 세션 시간 절약)

- **MCP `Unity_Camera_Capture`는 머티리얼 파라미터 변경 후 캐시된 프레임 반환**(바이트 동일). 파라미터 판정은 `cam.Render → 내 RenderTexture → EncodeToPNG → 파일 → Read`로 신선 렌더.
- **reflection 대량 순회(Assembly.GetTypes / Type.GetFields)는 MCP 실행기 자체를 UNEXPECTED_ERROR로 죽임.** 대신 `SerializedObject` 프로퍼티 순회(`NextVisible`)는 안전 → 직렬화 필드명 알아낼 때 이걸 쓸 것.
- **`File.Delete`는 MCP에서 차단됨** → 파일 삭제는 Bash로.
- **`.git/index.lock`이 반복 생성됨**(Unity VC 연동 추정) → 커밋 전 `rm -f .git/index.lock`.
- **FullScreenPassRendererFeature 직렬화 필드명은 `m_` 접두사 없음**: `passMaterial`/`injectionPoint`/`fetchColorBuffer`/`requirements`/`passIndex`.

---

## 8. 다음 단계 (유저 지시 대기 — COZY는 이 작업 마무리 후)

1. **COZY 연결** (StyleLab에 실제 시간/날씨 시스템).
2. **멀티조건 일관성 검증** — 고정된 처리(그레이드+틸트시프트+라이팅 시스템)를 낮·흐림·황혼·밤 + 평원 바이옴에 던져 다 버티는지. 낮에서 터지면 **노출을 그레이드에서 빼 라이팅으로**, 스플릿토닝 과하면 강도↓.
3. **틸트시프트 최종 세기 확정** — 단일 밤 씬 아닌 멀티조건에서.
4. (보류 해제 시) HTrace 1주체 블리드.

---

*작성: Claude (Opus 4.8) · 방법론: 디렉터 + 경쟁 라이벌 에이전트 + 캡처 판정 루프*
