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

Use the `Agent` tool with `model: opus` automatically when the task involves:
- Physics or movement system design (force curves, handling model, collision response)
- Architecture decisions across 3+ interacting scripts
- Game-feel tradeoffs with no clear right answer (arcade vs. sim, responsiveness vs. stability)
- Designing a new feature end-to-end (data flow, component responsibilities, API shape)
- Non-obvious bug root-cause analysis spanning multiple systems

Handle directly as Sonnet (no agent spawn needed) when:
- Implementing from an already-decided design
- Editing specific files — code changes, value tuning, field additions
- Codebase search and exploration
- Single-system bug fixes

**Protocol:**
1. On receiving a request, silently classify it: design/analysis vs. implementation.
2. If design/analysis → spawn `Agent(subagent_type="Plan", model="opus")` first, get the spec.
3. Then implement the spec directly as Sonnet (or via Gameplay agent per Agent Workflow).
4. If the request is ambiguous, lean toward Opus — the cost of under-thinking a design is higher than the latency.

> Always announce the switch briefly before acting. Example: "설계 판단이 필요해서 Opus로 처리합니다." or "구현 작업이라 Sonnet으로 바로 진행합니다."

## Agent Workflow (코드 작업 시 필수)

Unity C# 코드를 새로 작성하거나 수정할 때마다 반드시 아래 순서를 따른다:

1. **구현**: `Gameplay` 에이전트가 담당
2. **점검**: 구현 완료 후 `Stab` + `Codex` 에이전트를 **병렬**로 실행해 리뷰

예외 없음. Gameplay가 완료되면 자동으로 Stab+Codex 리뷰를 병렬 실행할 것.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.
