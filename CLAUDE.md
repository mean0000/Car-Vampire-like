# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

## Project: ZombieCrush (Unity)

- Engine: Unity (C#)
- Genre: 좀비 테마 게임
- Solution file: ZombieCrush.sln

**Installed Assets (반드시 참고할 것):**
- **DOTween** — 트윈 애니메이션. `transform.DOMove`, `DOFade` 등 직접 코루틴/Lerp 대신 이걸 우선 사용.
- **COZY Pro: Stylized Weather Bundle** — 날씨/시간대 시스템. 날씨·하늘·조명 관련 작업 시 반드시 이 에셋 API 활용.
- **Feel** — 게임감(주스) 피드백 라이브러리. 카메라 쉐이크, 히트스탑, 이펙트 피드백은 Feel의 MMFeedbacks 활용.

**Unity-specific rules:**
- MonoBehaviour 수명주기(Awake, Start, Update 등)를 임의로 추가/제거하지 말 것.
- SerializeField, public 필드 변경 시 Inspector 연결이 끊어질 수 있으므로 반드시 확인 후 수정.
- 요청하지 않은 성능 최적화(object pooling, coroutine 전환 등) 추가 금지.
- 기존 씬/프리팹 구조는 건드리지 말 것 — 코드만 수정.

## Model Selection — Autonomous Switching

**Do not ask the user which model to use. Decide and switch proactively.**

**진짜 기준선은 "설계 vs 구현"이 아니라 "불확실성·레버리지가 높은가 vs 기계적인가"다.**
게임 개발에선 게임감(주스, 물리, 핸들링, 드리프트 보상 등)이 코드 단계에서 만들어지므로, 설계와 구현이 자주 섞인다. 빌드→느껴보고→수치 조정하는 반복 루프에선 의도를 보존하기 위해 **설계한 모델이 구현까지 끝까지** 잡는다.

**기본 세션 모델 = Fable(`claude-fable-5`). Fable이 오케스트레이터다 — 판단하고 명령을 내려 하위 모델에 위임한다.** 이유: Fable은 1패스로 정확히 맞혀 **루프가 덜 돌기 때문에 토큰 출혈이 더 적다**(토큰당 Opus의 2배여도, 같은 작업을 평균 더 적은 토큰으로 끝내 어려운 작업은 누적 달러 비용이 오히려 더 쌀 수 있음 — Boris Cherny). 따라서 "항상 켜두되 기계적 손작업만 아래로 내려보낸다."

**Fable이 반드시 직접 잡는다 (위임 금지) — 우리 게임에서 번복이 가장 비쌌던 존. effort: `xhigh`:**
- **게임의 중심 잡기** — 다른 게임을 레퍼런스로 가져와 그 디자인을 판단·해부할 때, 우리 게임의 코어 루프·게임성(fun) 자체를 논할 때
- **A. 그래픽/라이팅/포스트 처리 판정** — 무드·그레이드·틸트시프트 등 정답 없는 미적 판단 (최종 미적 콜은 유저 몫 → 캡처루프로 보여주되, 판단 자체는 Fable이 1패스로)
- **B. 게임감/주스** — 타격감·쉐이크·넉백·경직 등 게임감 튜닝이 포함된 설계+구현 (의도 보존 위해 손까지 Fable이)
- **C. 코어 디자인 방향/루프** — 매크로 설계, 다중 시스템 트레이드오프, 방향 전환
- **D. 레벨/인카운터/페이싱** — 스폰 디렉터, 게이트, NavMesh 공간 설계
- Physics/movement 설계, 3+ 스크립트 아키텍처, 다중 시스템 버그 루트코즈

**Opus(`claude-opus-4-8`)로 위임 (Fable이 명령). effort: medium~high:**
- 중간 복잡도 — 단일 시스템 설계, 복잡한 컴포넌트 와이어링, 명확한 버그 수정

**Sonnet으로 위임 (Gameplay 에이전트, 빠르게). effort: low~medium:**
- 스펙 동결된 기계적 구현 — 명시된 수치로 값 조정, 필드 추가
- 코드베이스 검색·탐색, 단일 시스템 버그 수정

**Protocol:**
1. 요청을 받으면 조용히 분류한다: 게임 중심·불확실·레버리지가 걸렸나(=Fable 직접) vs. 중간(=Opus 위임) vs. 기계적(=Sonnet 위임).
2. **승격 서킷브레이커: Opus(또는 하위)에 위임한 작업이 실패하거나 헛돌면 즉시 Fable로 승격해 직접 잡는다. 같은 티어에서 재시도하며 헛돌지 말 것.**
3. 스펙이 완전히 동결된 기계적 구현이면 → Gameplay 에이전트(Sonnet)로 위임.
   - **단, 복잡한 설정 작업(다중 컴포넌트 와이어링, 물리·게임감 튜닝이 섞인 구현, 3+ 스크립트 상호작용 셋업 등)일 때는 Gameplay를 `model: opus` 오버라이드로 띄운다.**
4. 모호하면 한 티어 위로 — under-thinking이 latency보다 비싸다.
5. **작은 편집·자잘한 값 조정엔 3단 에이전트 춤(Plan→구현→리뷰)을 생략한다. 순수 오버헤드다.**

**토큰 규율 (Fable 기본의 대가를 방어):**
- **effort를 작업별로 조절** — 어려운 판단만 `xhigh`, 루틴은 low/medium. effort가 Fable의 토큰·성능 최대 레버다.
- **캐싱 친화** — CLAUDE.md/메모리를 안정시켜 반복 컨텍스트 input 90% 할인을 받는다.
- **컨텍스트 최소 주입** — 전체 코드베이스 ❌, 관련 파일만 주입.

> Always announce the switch briefly before acting. Example: "게임 중심·게임감이 걸려서 Fable이 직접 끝까지 잡습니다." / "스펙 동결된 기계적 구현이라 Sonnet으로 위임합니다." / "Opus 위임이 헛돌아서 Fable로 승격합니다."

## Agent Workflow (코드 작업 시 필수)

Unity C# 코드를 새로 작성하거나 수정할 때마다 반드시 아래 순서를 따른다:

1. **구현**: `Gameplay` 에이전트가 담당
2. **점검**: 구현 완료 후 `Stab` + `Codex` 에이전트를 **병렬**로 실행해 리뷰

## Agent Aliases (호출 별칭)

사용자가 아래 약칭으로 부르면 해당 에이전트로 매핑한다:
- **`lv`** → `LevelDesign` 에이전트 (맵 설계·몹 배치·레벨 디자인)
- **`st`** → `Story` 에이전트 (세계관·캐릭터 시트·대사/카피·명명·캐넌 정합 감사)
- **`sd`** → `Sound` 에이전트 (오디오 아키텍처·사운드 디자인·에셋 큐레이션 — 2026-06-12 신설. ⚠️못 듣는 한계가 정의에 내장: 음색 판정=유저 귀, 시스템 검증만 자체 수행)

**Story 에이전트 역할 경계**: 코어 스토리 방향 판단(결말·세계관 대전환·신규 캐넌 조항)은 기존 정책대로 Fable이 직접. Story 에이전트는 그 아래 양산·감사 작업에 위임 — 대사/카피 드래프트, 어휘 사전 경유 네이밍, 캐릭터 시트 초안, 기존 캐넌과의 충돌 검사.

예외 없음. Gameplay가 완료되면 자동으로 Stab+Codex 리뷰를 병렬 실행할 것.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.
