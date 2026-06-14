# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

**확인 스킵 (유저 지시):** 되돌릴 수 있고 요청 범위 안의 작업은 "~할까요?/박을까요?/진행할까요?" 묻지 말고 **바로 실행하고 결과만 보고**한다. 멈추고 묻는 건 셋뿐 — ①파괴적·비가역(force push, 안 만든 것 삭제·덮어쓰기, 커밋/푸시), ②진짜 방향 분기/스코프 변경(유저 답이 *만들 결과물 자체*를 바꿀 때), ③외부 공개 액션. 그 외 "Yes 확인"은 순수 오버헤드. (아래 §1 "If uncertain, ask"는 *요구사항 해석이 불확실할 때*에 한함 — 실행 허가를 받으려는 게 아니다.)

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
게임 개발에선 게임감(주스, 물리, 핸들링)이 코드 단계에서 만들어지므로, 설계와 구현이 자주 섞인다. 빌드→느껴보고→수치 조정하는 반복 루프에선 의도를 보존하기 위해 **설계한 모델(=오케스트레이터)이 구현까지 끝까지** 잡는다.

### ⚙️ 활성 구성 (ACTIVE — 이 표 한 곳만 바꾸면 전체가 전환된다)

| 역할 | **현재 모델** | 🔄 Fable 5 복귀 시 |
|---|---|---|
| **오케스트레이터** (판단·직접 시공·막힘 회수 종착점) | **Opus 4.8 (`claude-opus-4-8`)** | Fable 5 (`claude-fable-5`) |
| 중간 위임층 | 오케스트레이터 겸임 (또는 Gameplay `model: opus`) | Opus 4.8 |
| 기계적 위임층 | Sonnet (Gameplay 에이전트) | Sonnet (Gameplay 에이전트) |

> **현재 상태: Fable 5 일시 불가 → Opus 4.8이 오케스트레이터를 겸한다.** 본문의 모든 "오케스트레이터"는 위 표의 *현재 모델*을 가리킨다.
> **🔄 복귀 절차:** 위 표의 오케스트레이터 행을 Fable로 되돌리고 세션 기본 모델(`/model`)을 Fable로 설정 → 본문 수정 없이 원래 의도(Fable 1패스 오케스트레이션)가 복원된다. Fable 전용 규칙은 본문에 `*(Fable 복귀 시: …)*`로 인라인 보존했다. 연동 메모리: [[feedback_delegate_all_fable]].

**오케스트레이터가 반드시 직접 잡는다 (위임 금지) — 우리 게임에서 번복이 가장 비쌌던 존. effort: `xhigh`:**
- **게임의 중심 잡기** — 다른 게임을 레퍼런스로 해부할 때, 우리 게임의 코어 루프·게임성(fun) 자체를 논할 때
- **A. 그래픽/라이팅/포스트 처리 판정** — 무드·그레이드·틸트시프트 등 정답 없는 미적 판단. 최종 미적 콜은 유저 몫.
  - ⚠️ **Opus 오케스트레이터 주의:** Fable의 "1패스 정확"을 전제하지 말 것 → **캡처 루프(렌더→Read→유저 판정)를 더 촘촘히 돌리고 유저를 더 일찍 끌어들인다.** *(Fable 복귀 시: 판단 자체를 1패스로.)*
- **B. 게임감/주스** — 타격감·쉐이크·넉백·경직 등 게임감 튜닝이 포함된 설계+구현 (의도 보존 위해 손까지 오케스트레이터가).
  - ⚠️ **Opus 오케스트레이터 주의:** 한 번에 안 맞을 수 있음 → 빌드→느껴보고→조정 루프를 더 짧게·더 자주.
- **C. 코어 디자인 방향/루프** — **방향 전환·코어 루프 정의·다중 시스템 트레이드오프의 *최종 판단*** 은 오케스트레이터+유저 직접. 단 **무기 체계·성장/이코노미·빌드크래프트·런 구조의 *시스템 구조 설계*(드래프트·레퍼런스 추적·기존 권위 재정합)는 `GameDesign`(gd) 에이전트에 위임** (제안 → 유저 판정 패턴). *(이 위임은 2026-06-13 Fable 부재로 신설 — 원래 "직접만/위임 금지"는 Fable 1패스 전제였다. Fable 복귀 시에도 GameDesign은 드래프트·재정합 양산층으로 유지하고 최종 판단만 오케스트레이터가 회수.)*
- **D. 레벨/인카운터/페이싱** — 스폰 디렉터, 게이트, NavMesh 공간 설계
- Physics/movement 설계, 3+ 스크립트 아키텍처, 다중 시스템 버그 루트코즈

**중간 복잡도 — 오케스트레이터가 직접 처리하거나 Gameplay(`model: opus`)로 내린다. effort: medium~high:**
- 단일 시스템 설계, 복잡한 컴포넌트 와이어링, 명확한 버그 수정
- *(Fable 복귀 시: 이 층은 Fable이 Opus 에이전트에게 명령·위임하는 별도 티어로 분리된다.)*

**Sonnet으로 위임 (Gameplay 에이전트, 빠르게). effort: low~medium:**
- 스펙 동결된 기계적 구현 — 명시된 수치로 값 조정, 필드 추가
- 코드베이스 검색·탐색, 단일 시스템 버그 수정

**Protocol:**
1. 요청을 받으면 조용히 분류한다: 게임 중심·불확실·레버리지(=오케스트레이터 직접) vs. 중간(=오케스트레이터 직접 또는 Gameplay `model: opus`) vs. 기계적(=Sonnet 위임).
2. **막힘 서킷브레이커: 하위층(Sonnet/Gameplay)에 위임한 작업이 실패하거나 헛돌면 즉시 오케스트레이터가 회수해 직접 잡는다. 같은 티어에서 재시도하며 헛돌지 말 것.**
   - **현재(Opus 오케스트레이터): 위로 승격할 GA 티어가 없다** → 회수해 직접 처리 + 캡처/검증 루프를 조이고 유저를 일찍 끌어들인다. *(Fable 복귀 시: 종착점 = Fable로 승격해 직접.)*
3. 스펙이 완전히 동결된 기계적 구현이면 → Gameplay 에이전트(Sonnet)로 위임.
   - **단, 복잡한 설정 작업(다중 컴포넌트 와이어링, 물리·게임감 튜닝이 섞인 구현, 3+ 스크립트 상호작용 셋업 등)일 때는 Gameplay를 `model: opus` 오버라이드로 띄운다.**
4. 모호하면 한 티어 위로 — under-thinking이 latency보다 비싸다.
5. **작은 편집·자잘한 값 조정엔 3단 에이전트 춤(Plan→구현→리뷰)을 생략한다. 순수 오버헤드다.**

**토큰/effort 규율:**
- **★Max5 비용 규율 (2026-06-14, Max20→Max5 다운그레이드)** — 사용량 상한이 병목. ①**기계적 타이핑은 오케스트레이터(Opus)가 직접 치지 말고 Sonnet-Gameplay로 내린다** — Opus 손은 게임감/미적/코어 *판단·구현*에만(B/C존 의도 보존). ②**리뷰 게이트는 유지** — Stab=Sonnet(쌈)·Codex=별도 provider(Max5 예산 0원)라 완화할 이유 없음. ③작업 단위로 `/clear`해 누적 대화 재전송을 끊는다.
- **effort를 작업별로 조절** — 어려운 판단만 `xhigh`, 루틴은 low/medium. effort가 토큰·성능 최대 레버다.
- **캐싱 친화** — CLAUDE.md/메모리를 안정시켜 반복 컨텍스트 input 90% 할인을 받는다.
- **컨텍스트 최소 주입** — 전체 코드베이스 ❌, 관련 파일만 주입.
- *(Fable 복귀 시: Fable은 토큰당 Opus 2배지만 1패스로 루프가 덜 돌아 어려운 작업의 누적 비용이 더 쌀 수 있음 — "항상 켜두되 기계적 손작업만 아래로" 기조 복원.)*

> Always announce the switch briefly before acting. 예: "게임 중심·게임감이 걸려서 오케스트레이터가 직접 끝까지 잡습니다." / "스펙 동결된 기계적 구현이라 Sonnet으로 위임합니다." / "Gameplay(opus)가 헛돌아서 회수해 직접 잡습니다."

## 병렬 세션 규율 (3~4개 동시 작업 시)

여러 Claude 세션이 같은 작업 폴더를 공유한다 — 가상 격리 없음, 실제 디스크/git를 직접 친다. **락·스케줄러 시스템은 짓지 않는다**(에디터는 어차피 직렬이라 이득 < 관리비용). 격리는 다음 분배 규율로 사람(라우터)이 잡는다:

1. **파일 파티션** — 세션마다 *서로 안 겹치는 파일*을 준다. 같은 파일을 두 세션에 절대 주지 않는다. 한 폴더에선 브랜치가 아니라 *파일*이 격리 단위다 — 브랜치 스위치는 공유 working tree를 갈아끼워 다른 세션 파일을 덮어쓴다. 브랜치/worktree는 진짜 갈라진 작업이 필요할 때만(유니티 worktree=전체 재임포트라 무거움).
2. **에디터(unity-mcp) 1세션 전담** — Unity 에디터를 만지는 작업(플레이모드·씬 와이어링·MCP 캡처·에셋 임포트)은 *한 세션만* 한다. 나머지 세션은 unity-mcp를 호출하지 않고 순수 코드/문서만, 에디터 필요한 일은 전담 세션에 패스한다. (전용 I/O 스레드 패턴 — 에디터는 싱글톤이라 직렬화 손실 0.)
3. **결정 즉시 기록** — 파일 파티션은 *판단 충돌*(네이밍·설계 방향)을 못 막는다. 전파돼야 할 결정은 내리는 즉시 메모리/권위문서에 적는다(세션 끝이 아니라). 다른 세션은 그 다음 읽기에서 받는다.

스윗스팟 2~3개. 더 늘리면 사람(라우터)의 추적 비용이 병목. 전담 세션 큐가 진짜 막힐 때만 동적 락(mkdir 락+리스)을 그때 얹는다.

## Agent Workflow (코드 작업 시 필수)

Unity C# 코드를 새로 작성하거나 수정할 때마다 반드시 아래 순서를 따른다:

1. **구현**: `Gameplay` 에이전트가 담당. **단, 캐릭터/몬스터 애니메이션(공격 모션 연결·상태 시퀀스·루트모션·전환)은 `Animation` 에이전트 전담** (2026-06-13 신설 — ★유저 헌법: ①한 동작 진행 중엔 그 애니만 돈다, crossfade로 동작 정체성 뭉개기 금지 ②공격=상태 시퀀스(접근 이동→정지→공격) ③애니가 진실, 코드는 위치/포즈 안 만들고 상태 전환·이벤트만).
2. **점검**: 구현 완료 후 `Stab` + `Codex` 에이전트를 **병렬**로 실행해 리뷰

## Agent Aliases (호출 별칭)

사용자가 아래 약칭으로 부르면 해당 에이전트로 매핑한다:
- **`lv`** → `LevelDesign` 에이전트 (맵 설계·몹 배치·레벨 디자인)
- **`st`** → `Story` 에이전트 (세계관·캐릭터 시트·대사/카피·명명·캐넌 정합 감사)
- **`sd`** → `Sound` 에이전트 (오디오 아키텍처·사운드 디자인·에셋 큐레이션 — 2026-06-12 신설. ⚠️못 듣는 한계가 정의에 내장: 음색 판정=유저 귀, 시스템 검증만 자체 수행)
- **`gd`** → `GameDesign` 에이전트 (무기 체계·성장/이코노미·빌드크래프트·런 구조 시스템 설계 — 2026-06-13 신설, Fable 부재 대응. 제안→유저 판정, 동결 권한 없음. ★레퍼런스 추적 의무(임의설계 금지)·기존 권위 재정합 의무)

**Story 에이전트 역할 경계**: 코어 스토리 방향 판단(결말·세계관 대전환·신규 캐넌 조항)은 기존 정책대로 오케스트레이터가 직접. Story 에이전트는 그 아래 양산·감사 작업에 위임 — 대사/카피 드래프트, 어휘 사전 경유 네이밍, 캐릭터 시트 초안, 기존 캐넌과의 충돌 검사.

**GameDesign 에이전트 역할 경계**: 코어 방향 전환·장르 피벗·코어 루프의 *최종 판단*은 오케스트레이터+유저 직접. GameDesign은 그 아래 — 무기/성장/이코노미/빌드 시스템 구조의 **레퍼런스 추적 드래프트**(Shape of Dreams·Hades II·Duckov 등 잠긴 레퍼 추적, 임의설계 금지), **기존 권위 문서 재정합·충돌 감사**(progression-system 등은 익스트랙션 대전환 이전 산물), **노브 맵·스펙 동결 후보** 제안. 경계: LevelDesign(공간·인카운터)·Story(서사·네이밍)와 달리 GameDesign은 *메카닉/시스템/수치 구조*만. 동결은 유저, 구현은 Gameplay.

예외 없음. Gameplay가 완료되면 자동으로 Stab+Codex 리뷰를 병렬 실행할 것.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.
