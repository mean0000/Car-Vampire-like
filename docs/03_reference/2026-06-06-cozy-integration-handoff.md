# COZY 연결 후 화해 작업 핸드오프 — 2026-06-06

**브랜치:** `feat/graphics` · **무대:** `Assets/_Project/Scenes/StyleLab.unity`
**선행 문서:** `docs/03_reference/2026-06-05-graphics-session-handoff.md` (그래픽 처리 헌장·에셋맵·방법론)
**오늘 캡처:** `_capture_cozy_dered.png` (저장소 루트, de-red 적용 직후 JudgeCam 신선 렌더)

전 세션에서 잡은 그래픽 처리(그레이드/틸트시프트/렌더러피처)는 그대로. 이번 세션은 **유저가 COZY를 직접 연결한 뒤** 라이팅이 어떻게 덮였는지 진단하고 화해(reconcile)한 기록.

---

## 1. 무슨 일이 있었나

1. **유저가 COZY 연결** → 예고대로 태양/ambient/fog가 COZY 프로파일 런타임 구동으로 넘어감.
2. **화면이 핏빛 빨강으로 범람.** 원인 = 활성 COZY 대기 프로파일의 상수 색이 핏빛이었음(`sunlightColor (1, 0.35, 0.08)` 등).
3. **de-red 색 변경 적용** → 빨강 제거, 웜골드+쿨섀도우 황혼/밤 톤 복원. 캡처로 검증 완료.
4. **블록 조사** → 유저가 말한 "Cozy block" = `CozyTransitModule`의 시간대 7블록. 라이팅과 무관함을 확인(아래 3절).

---

## 2. 적용한 색 변경 (정확한 수치)

활성 프로파일 `Default Atmosphere.asset`에 `SerializedObject`로 적용 + 즉시 RenderSettings/Directional("Sun Light","Moon Light")에 반영:

| 필드 | 변경 전(핏빛) | 변경 후 |
|---|---|---|
| `sunlightColor` | (1, 0.35, 0.08) | **(1.0, 0.82, 0.60)** 웜골드 |
| `ambientLightZenithColor` | — | **(0.06, 0.08, 0.13)** 쿨(이중온도 섀도우축) |
| `ambientLightHorizonColor` | — | **(0.10, 0.09, 0.10)** |
| `ambientLightMultiplier` | 0.25 | **0.5** |
| `skyHorizonColor` | — | **(0.13, 0.09, 0.07)** 다크웜 |

타깃 베이스라인(전 세션 손튜닝): 태양 #FFD999@3.2 + 쿨/웜 ambient. → 색 방향은 맞췄으나 **노출(밝기)은 아직 어두움**(아래 4절).

---

## 3. "Cozy block" 조사 결론 (유저 질문 해소)

- **블록 = `CozyTransitModule`의 시간대 구간 7종.** 비어있지 않고 각각 `start~end` 시간이 정의돼 있음:
  - dawn 04:00–05:30 / morning 06:00–07:59 / day 07:30–09:00 / afternoon 13:00–14:59 / evening 16:00–18:00 / twilight 20:59:59–21:00 / night 21:00–22:00
- **★이 블록은 "시간대 라벨"일 뿐, 라이팅 연출을 담지 않음.** 안에는 시간값만 있고 색/라이트/프로파일 필드 없음. 라이팅은 `atmosphereProfile`(2절에서 편집한 그것)이 시간곡선으로 구동. → **"블록이 비어서 우리 그래픽이 안 걸린다"는 걱정은 기우.** 경로가 다름.
- **⚠️단 시간 범위가 깨져 있음:** twilight가 **단 1초(20:59:59~21:00)**, **09:00~13:00 구간은 어떤 블록에도 안 속함(구멍)**, day/morning 경계 겹침. COZY 순정 디폴트로 보기 어려움 → "지금이 밤/낮인지" 질의 로직(예: 밤에 좀비 강화)에 쓰면 구멍에서 오작동 가능.

---

## 4. 미해결 / 내일 이어서

- **★어둠의 진짜 원인 2개 (색 문제 아님):**
  1. `CozyTimeModule.currentTime = 0.00` → COZY가 **자정(00:00)** 으로 시간 구동 중. 태양이 지평선 아래. → 시간을 황혼(~0.77)으로 옮기면 의도한 황혼 룩이 나옴.
  2. `StyleLab_Post.asset`에 **post-exposure +1.35가 그레이드에 구워짐**(전 세션 단일 밤씬용 임시값). COZY가 태양강도를 가져가며 밝기 베이스가 무너짐. → 문서 권고대로 **노출을 그레이드에서 빼 COZY 태양 intensity로 이관** 필요.
- **★활성 COZY 프로파일이 패키지 안에 있음:**
  `Packages/com.distantlands.cozy.core/Content/Resources/Profiles/Atmosphere Profiles/Default Atmosphere.asset`
  → 패키지 에셋이라 COZY 업데이트 시 덮어쓰일 위험. **`Assets/_Project/Setting/`로 복사해 프로젝트 소유로 이전 후 그 사본을 모듈에 재연결**할 것. (climate/ambience/time/wind 프로파일도 전부 Packages 내 → 동일 처리 검토)
- **블록 시간 범위 정리** (택1, 유저 결정 대기): ①구멍·겹침 없이 표준 분할로 손정리 ②COZY 순정 기본 프리셋 값 찾아 복원.

---

## 5. 내일 시작 시 빠른 복귀 절차

1. `feat/graphics` 브랜치 확인. `rm -f .git/index.lock` (Unity VC가 재생성).
2. `_capture_cozy_dered.png`로 마지막 룩 상기.
3. 유저 결정 받을 갈림길: **(A)** 시간을 황혼으로 옮겨 룩 확정 → 노출을 그레이드→COZY 태양으로 이관 / **(B)** 프로파일을 패키지→프로젝트로 이전 / **(C)** Transit 블록 시간범위 정리.
4. 캡처 방식은 MCP `Unity_Camera_Capture` 대신 **신선 렌더**(cam.Render→RenderTexture→EncodeToPNG→파일→Read) 사용 — 전 세션 함정 참조.

---

*작성: Claude (Opus) · 세션: COZY 연결 후 라이팅 화해 + 블록 진단*
