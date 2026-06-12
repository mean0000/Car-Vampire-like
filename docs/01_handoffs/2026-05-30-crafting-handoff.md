# 크래프팅(채널링 구급상자) 작업 핸드오프 — 2026-05-30

> 🟥 **폐기 (2026-06-09 / 06-11).** 부품 파밍→크래프팅 루프는 익스트랙션 전환 + 크래프팅 폐기로 무효 — 현행 = 본사 보급 + 일당 공제, [[2026-06-09-postprocessing-core-design]]. 역사 참고용.

> 작성 목적: 운동 후 이어서 바로 작업할 수 있도록 "무엇을 / 왜 / 다음에 뭘" 정리.
> 코드/씬은 이미 컴파일 클린 + 씬 저장 완료. **지금 상태 그대로 플레이테스트 가능.**

---

## 0. 한 줄 요약

좀비를 처치해 **부품**을 모으고 → **C키를 3초 홀드**해 구급상자를 제작(HP+50)하는
"채널링 제작" 수직 슬라이스를 추가했다. 핵심은 **제작 중엔 무방비**(못 움직이고·못 쏘고)
+ **소음이 점점 커져 호드를 끌어온다**는 긴장.

---

## 1. 무엇을 했나 (파일별)

### 신규 `Assets/_Project/Scripts/CraftingSystem.cs`
- Player에 부착된 **싱글톤 MonoBehaviour** (`CraftingSystem.Instance`).
- **C키 홀드** → `craftTime`(3초) 채널링 → 부품 `partsPerCraft`(5) 소비 + `PlayerController.Heal(50)`.
- 취소 조건: **이동(축 입력)** 또는 **C 떼기** → 즉시 취소, **부품은 보존**(소비 안 함).
- 매 제작 프레임: `NoiseManager.SetMovementNoise(Lerp(15, 55, 진행도))` — 완료 직전 임계(50) 초과.
- `NotifyKill(Vector3)` — 좀비 처치 시 호출, `dropChance`(0.5)로 부품 +1, 그중 15%는 +2.
- 공개 게터: `Parts`, `IsCrafting`, `CraftProgress01`.
- 직렬화 필드(인스펙터 튜닝 가능): partsPerCraft=5, craftTime=3, medkitHeal=50,
  craftNoiseStart=15, craftNoisePeak=55, dropChance=0.5, doubleDropChance=0.15.

### 통합 수정
- **`NoiseManager.cs`** — `SetMovementNoise`를 **max 결합**으로 변경
  (`_sustainTarget = Mathf.Max(_sustainTarget, Mathf.Max(0f, level))`).
  → 이동 소음 + 제작 소음이 한 프레임에 공존(둘 중 큰 값 채택). 단순 덮어쓰기였으면
  제작 호출이 이동 호출을 지워버려 한쪽이 무시됨.
- **`PlayerController.cs`** — `Heal(float)` 추가(maxHP 클램프). + Space 스텔스 발동을
  **제작 중엔 차단**(`!crafting` 조건).
- **`ZombieController.cs`** — `Die()`에 `CraftingSystem.Instance?.NotifyKill(transform.position)` 추가.
- **`PlayerCombat.cs`** — `Update()`에서 `bool crafting = ...IsCrafting`로
  **사격(Fire)·암살(F) 모두 잠금**(`_currentTarget = crafting ? null : Find...`).
- **`HUDController.cs`** — `UpdateCrafting()` 추가:
  - `PartsText` "PARTS: N"
  - `CraftBar`(Fill 진행바) — 제작 중에만 활성(`craftBarFill.parent.SetActive`)
  - `CraftLabel` "CRAFTING MEDKIT..." — 제작 중에만 표시

### 씬 와이어링 (MCP로 완료, 저장됨)
- Player에 `CraftingSystem` 컴포넌트 부착.
- HUD Canvas에 신규 UI 3종 생성 + HUDController에 직렬화 연결:
  - `PartsText`(좌상단, anchoredPos 20,-150)
  - `CraftBar`(하단중앙 0,80, 320x26) + `Fill`(Filled-Horizontal, 초록)
  - `CraftLabel`(하단중앙 0,110)

---

## 2. 왜 그렇게 했나 (비자명 결정)

1. **소음 램프 15→55**: 시작은 조용(근처만), 완료 직전 추격임계(50) 초과 → "안전한 곳에서
   빠르게 만들까 vs 위험 감수"라는 선택을 만든다. 제작이 공짜로 안전하면 긴장이 없음.

2. **★제작이 은신을 깬다 (리뷰가 잡은 핵심)**: `ZombieController.CanHearPlayer()`는
   `IsStealth`면 **무조건 false**를 반환(ZombieController.cs:216). 그래서 스텔스(Space) 중
   제작하면 소음 램프가 **통째로 무력화**돼 긴장 메커니즘이 죽는다.
   → ①제작 시작 시 `BreakStealth()` ②제작 중 Space 차단. **총격이 은신을 깨는 규칙과 동일.**
   ⚠️ **되돌릴 수 있는 게임감 결정**: 만약 "스텔스 비용(감염+1)을 내고 안전 제작"을 의도된
   전략으로 두고 싶으면 이 두 군데를 빼면 됨(CraftingSystem 시작부 BreakStealth 호출 +
   PlayerController Space의 `!crafting` 조건).

3. **이동=취소 (별도 이동 잠금 안 함)**: "제작 중 못 움직임"을 물리 잠금이 아니라
   "움직이면 취소"로 구현. 더 단순하고, 플레이어가 위험을 느끼면 즉시 빠져나갈 수 있음.

---

## 3. 리뷰 결과 (Stab + Codex 병렬, High 0건)

**보류한 지적(전부 MVP 비이슈, 다녀와서 판단):**
- **M1** `CraftingSystem.IsMoving()`이 `PlayerController`의 축입력 로직과 중복.
  지금은 둘 다 `GetAxisRaw`라 일치하지만, 나중에 대시/넉백/네비메시 이동이 생기면
  CraftingSystem이 그걸 못 봐서 강제 이동 중에도 제작이 진행될 수 있음.
  → 해결책: PlayerController에 `public bool IsMoving` 게터 노출 후 그걸 읽기.
- **M2** 완료 프레임에 `CraftProgress01`이 0으로 떨어져 진행바가 1.0에 도달 전 스냅.
  실제로는 완료 시 CraftBar가 숨겨져 안 보임 → 사실상 무해.
- **M3** `NotifyKill`이 `Instance==null`이면 조용히 드롭 무시. 지금은 와이어링돼 있어 OK.
- **1프레임 소음 지연(~16ms)**: NoiseManager `[DefaultExecutionOrder(-100)]`가 read 후 클리어 →
  호출값이 1프레임 뒤 소비됨. 이동 소음도 원래 그랬고 체감 불가. **고치지 않음.**

---

## 4. 알아둘 함정 (다음 세션 필수)

- **좀비 콜라이더는 트리거**(isTrigger=1). 좀비 대상 OverlapSphere/Cast/Raycast는 항상
  `QueryTriggerInteraction.Collide`. (Ignore면 0개로 잡힘 — 과거에 암살/총이 다 안 걸렸던 버그.)
- **직렬화값이 코드 기본값을 덮음**: 씬에 한 번 저장된 SerializeField는 C# 기본값 바꿔도
  안 먹음. MCP `SerializedObject.FindProperty().floatValue/intValue`로 바꿔야 함.
  (단 `CraftingSystem`은 방금 부착돼 아직 코드 기본값 그대로 → 처음 튜닝은 인스펙터/코드 둘 다 OK,
  씬 저장 후엔 직렬화값 우선.)
- **MCP RunCommand UI 타입**: 하네스가 `namespace Unity.AI.Assistant.Agent...`로 감싸서
  `using UI=UnityEngine.UI` 별칭이 형제 `Unity.AI.Assistant.UI`와 충돌(CS0234).
  → UI 타입은 항상 `UnityEngine.UI.Text` / `UnityEngine.UI.Image`로 완전수식.
- 코드 편집 후 MCP 호출 시 "Unity not detected" 뜨면 도메인 리로드 중 → `sleep 6` 후 재시도.
- **CLAUDE.md 규칙**: 코드 작업 = 구현 → **Stab+Codex 병렬 리뷰 필수**. 게임감 걸린 건 Opus 직접.

---

## 5. 다음에 할 일 (돌아와서 바로)

### A. 먼저 플레이테스트로 체감 (수치 튜닝 후보)
지금 상태로 Play → 좀비 처치해 부품 모으고 → C 홀드 제작.
체감 후 조정할 가능성 높은 값들:
- `craftTime`(3s) — 너무 길면 답답, 짧으면 긴장 없음.
- `dropChance`(0.5) / `partsPerCraft`(5) — 제작 1회까지 좀비 몇 마리 잡아야 하는가의 페이싱.
- `craftNoisePeak`(55) — 호드가 실제로 모이는지. 안 모이면 더 올리거나 `craftNoiseStart`도 상향.
- **2번 결정(제작=은신해제)**을 실제로 플레이하며 "맞는 선택인지" 확인 — 답답하면 되돌리기.

### B. 보류 리뷰 정리 (선택, 빠름)
- M1: PlayerController에 `IsMoving` 게터 노출해 중복 제거 (깔끔함, 5분).

### C. 그다음 MVP 항목 (사용자와 우선순위 합의 필요)
- 크래프팅 **레시피 다양화**: 연막(소음 미끼)/지뢰/단검 등 — 부품 소비처 늘려 루프 깊이.
- 부품 **물리 픽업**: 지금은 추상 카운터. 좀비 죽은 자리에 부품 오브젝트 떨구고 줍기.
- **블루프린트 해금**(레벨업 보상)·**홈베이스**(안전 제작 거점).
- `docs/2026-05-29-infection-noise-design.md` §5에 크래프팅 소음을 ✅확정으로 반영(문서 정합).

---

## 6. 현재 빌드 상태
- 컴파일: **에러 0 / 경고 0** (확인 완료).
- 씬: `Greybox_MVP.unity` 저장됨, CraftingSystem + HUD UI 와이어링 완료.
- 조작 전체: WASD 이동 / Shift 달리기 / Ctrl 앉기 / Space 스텔스(3s, 쿨8s) /
  좌클릭 사격 / F 암살 / **C 홀드 제작(신규)**.
