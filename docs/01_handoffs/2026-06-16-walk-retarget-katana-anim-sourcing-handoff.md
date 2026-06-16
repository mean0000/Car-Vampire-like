# 핸드오프 — Synty 워크 리타게팅 + 카타나 애니 조달 + 검증 규율 (2026-06-16)

> 재시작 후 이 문서부터 읽고 이어간다. 이번 세션은 NewKatana 무료 워크 클립의 리타게팅 진단과 카타나 애니 팩 조사, 그리고 "사실 검증 규율" 신설이 골자.

## 0. 한 줄 요약
유저가 드롭한 `NewKatana/Walk_Loop_F_0` · `Walk_Combat_Loop_F_0` 워크 클립을 Synty(Humanoid)에 얹으려다 **한쪽 발 비대칭** 발견 → 원인은 **무료 클립의 깨진 언리얼 바인드**(소스 결함). 미봉책(Humanoid 변환·toe 언매핑·다리 미러)으로 완전 해결 불가 → **제대로 저작된 카타나 애니 팩 조달이 정답**으로 수렴. 별건으로 **사실 단정 전 검증 규율(CLAUDE.md §1.5)** 신설.

---

## 1. 워크 클립 리타게팅 — 진단과 최종 상태

**대상:** `Assets/_Project/Animations/NewKatana/Walk_Loop_F_0.fbx`(클립 "Take 001"), `Walk_Combat_Loop_F_0.fbx`(클립 "AS_Walk_Combat_Loop_F_0_Seq"). 본 이름이 **언리얼 마네킹 컨벤션**(`foot_l/ball_l/calf_l/thigh_l`).

**한 일:**
1. 둘 다 원래 **Generic** → **Humanoid(Create From This Model)** 변환, 아바타 valid·isHuman=True, loopTime ON.
2. 발끝 좌우 비대칭 발견 → 진단: Walk 클립의 `Right Toes Up-Down` 머슬이 정규화 ±1 초과(**-1.279**, 좌는 -0.694). toe 언매핑(human 52→50).
3. toe 제거 후에도 **발목 자체 비대칭 잔존**(`Left Foot Up-Down` 주로 음수, `Right`는 주로 양수 = 한쪽 발목 반대 꺾임).

**근본 원인(Animation 에이전트 기하 증명):** 타깃(Synty) 무죄 — 같은 캐릭터에 기존 Jorjouto/카타나 워크는 양발 완벽 대칭. 클립 커브도 무죄 — 자기 원본 릭에선 대칭. **오직 소스 FBX의 T-pose 바인드 결함**: 왼발이 바인드에서 ~50° 꺾여 있어 `CreateFromThisModel`이 그걸 muscle-zero로 구움 → 리타게팅 전 프레임이 상수 오프셋 운반.

**시도→철회:** 에이전트가 왼다리 체인(thigh_l→calf_l→foot_l) 바인드를 우측 모델공간 미러로 교체 → 옆구름 0.32→0.17로 감소했으나 **유저 Play 판정 "다리가 돌아간다"(비틀림)** → 철회.

**최종 상태(현재 디스크):** 두 FBX를 `humanDescription` 폐기 후 **깨끗한 자동 아바타로 재생성** + toe 재언매핑. = 다리 비틀림 없음, 발은 원래의 **약한 좌우 비대칭이 잔존**(소스 결함이라 이 클립으론 완전 대칭 불가).

---

## 2. 테스트 베드 (유저 Play 확인용)
- `Assets/_Project/_SidekickTest/SK_WalkTest.controller` (신규) — 기본 Walk / bool `Combat`→CombatWalk.
- `Assets/_Project/Scenes/_SidekickAnimTest.unity` — `Starter_02`(Synty Humanoid)에 위 컨트롤러 물려 저장. ▶Play로 워크 확인, Animator에서 `Combat` 토글.
- 프로덕션 컨트롤러(PlayerLocomotion/Rifle/Pistol)는 **건드리지 않음** — 급습 첫일격 총 vs 근접 미결이라 프로덕션 와이어링 보류.

---

## 3. 카타나 애니 팩 조사 — Studio9CG "Katana Sword Animation Pack" (id 257235)
- **검증한 사실(2026-06-16 라이브 조회):** $59.99, **할인 없음**(여름세일 진행 중이나 이 에셋은 미적용). Humanoid 리그(손가락+twist 본) → Synty 리타겟 깨끗(= §1 발 결함이 안 생김). 548클립: 8방향 walk 60·run 41·dodge·roll·turn·jump·공격 66(콤보/패링/처형)·피격 32. 루트모션 236+인플레이스 38.
- **⚠️ 미확인:** **발도(iai/draw/sheath) 전용 클립이 클립 분류에 없음.** 칼집 본(`Katana_sheath_01/02`)은 있으나 발도 모션은 문서상 확인 불가. **거합(iaido) 트리가 카타나 코어 기둥**이라 중요 → 구매 전 프리뷰 영상/데모로 발도 확인 필수.
- **권고:** 기술 적합도 높음. 단 ①발도 미확인 ②급습=근접 코어 확정 선행 ③정가 기준 판단(반값 대기 근거 없음). 현 NewKatana 무료 클립은 소스 결함이라 장기 사용 비추 — 근접 확정 시 제대로 된 팩으로 교체.

---

## 4. 검증 규율 신설 — CLAUDE.md §1.5 "Verify Before Asserting"
- 계기: 내가 "Unity 여름세일 6/25 50%"를 **미검증 단정**(내부 메모를 현재 사실로 승격) → 유저 지적.
- 규칙: 외부·시간민감 주장(가격/세일/날짜/버전/API·기능 존재)은 (a) 라이브 출처 대거나 (b) "미검증" 명시. 메모리=현재사실 승격 금지.
- 유저 판정: **상설 검증 에이전트는 안 만듦**(검증자도 환각·Max5 비용·무한 회귀). 도메인 산출물=기존 게이트(Stab/Codex/vc), 사실 주장=이 규율. 메모리 `[[feedback_verify_before_assert]]`.

---

## 5. ⚠️ 병렬 세션 주의 (이번 세션 중 발견)
- 같은 워킹트리에서 **다른 세션이 도심 블록아웃(ProBuilder) 작업 중** — `CityBlockGenerator.cs`, `_CityBlockBlockout.unity`, `Blockout_*.mat`, `Prefabs/`, ProBuilder 패키지, `2026-06-16-city-blockout-handoff.md` 등이 미커밋 상태로 트리에 섞여 있음.
- 이번 커밋은 **내 파일만 pathspec으로 골라** 담았다(NewKatana·_SidekickTest·_SidekickAnimTest·CLAUDE.md·이 핸드오프·Animation 에이전트 메모리). 다른 세션 파일·기존 미커밋(폰트·SlashArc·_vidframe)은 **건드리지 않음**.
- `.git/index.lock`이 68분 스테일로 남아 있었음(중단된 세션 잔재) → 활성 git 프로세스 0 확인 후 제거.

---

## 6. 다음 할 일
1. **급습 첫일격 = 총 vs 근접 결정** (06-15 핸드오프의 첫 도미노) — 근접이면 카타나 팩 조달.
2. 근접 확정 시: 카타나 팩 발도 클립 프리뷰 확인 → 구매 → Synty 리타겟 → 프로덕션 로코모션(8방향 MoveX/MoveY 블렌드) 배선 (= Animation 에이전트 전담).
3. NewKatana 무료 클립은 임시/폐기 후보(소스 바인드 결함).
