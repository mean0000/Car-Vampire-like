# 핸드오프 — 타이밍-클래시("맞받음") 전투 프로토 (2026-06-24)

> **상태: 탐색(EXPLORATION) 프로토 · 미플레이 검증 · 동결 아님 · 권위 인용 금지.**
> 코어 전투 동사를 *코드로 처음 시험*한 프로토. 게이트(Stab+Codex) 통과·컴파일 클린이나 **플레이 손맛은 미검증**(게임감=유저 판정).
> 권위 전문(설계 탐색) = `docs/02_logs/2026-06-24-core-combat-direction-exploration.md`.
> 로컬 메모리 미러 = `docs/02_logs/2026-06-24-core-combat-memory-note.md`.

---

## 0. TL;DR (다음 세션 1분 브리핑)

- **코어 동사 후보:** "원터치 타이밍-듀얼 크리티컬" → 액션 위주로 좁혀 **"맞받음(클래시)"** = *적이 공격을 실행하는 그 순간 베면 클래시*(데미지 배수 + 전파 + 짧은 프리즈 + 시안).
- **3차까지 빌드됨**(아래 §2). 전부 스크립트만(씬/프리팹 무수정). 게이트 통과.
- **★현재 블로커:** 실제 Caniathrox에서 **클래시가 전혀 안 뜸.** 진단 = 애니 상태명은 정상(`Coil/Lunge/Bite` 일치 확인) → 원인은 **타이밍/도달성 창**(Lunge가 빠르고 멀어서 "적이 Lunge 상태 ∧ 칼 사거리 안 ∧ 스윙 히트프레임"의 겹침이 거의 빈 집합).
- **다음 액션:** `ClashTestDummy`로 *격리 실험* → 메커니즘이 작동하는지 + A(맞받음) 손맛이 사는지 판정. 그 결과로 분기(§5).
- **미커밋이었음 → 이 핸드오프와 함께 커밋.** 직원로스터↔단일주인공+엘 = 보류(추후).

---

## 1. 어떻게 여기 왔나 (이 세션 흐름)

1. 탐색 문서 검토 → "타이밍 액션 너무 뻔한가?" 논의. 나+Codex 독립 수렴 = **"뻔한가"는 틀린 프레임**(신선함=실행). 진짜 리스크 = **함정2: 호드 판타지 vs 개체 집중 충돌**.
2. "플레이로만 갈린다" → 유저 GO로 **최소 프로토 착공**(직원로스터 보류, 액션 위주).
3. **1차 플레이:** "체감 안됨 + 적이 둘러싸 동시공격해 안 읽힘." → 진단: 메커닉만 깔고 **가독/피드백 레이어 부재**(눈감고 타이밍).
4. **2차(가독 레이어):** 본체 커밋 글로우(회→주황)+NOW 흰 펄스 + 크리티컬 시안. HTML 목업 3종이 설계도.
5. **2차 플레이:** "패링 개념이 안 보인다." → ★핵심 인정: **내가 만든 건 듀얼이 아니라 "수동 겹침 보너스"**(자유 휘두르다 마침 겹치면 보너스, 의도적 맞받음·클래시 비트·1:1 읽기 다 없음).
6. 유저 판정 = **A "진짜 맞받음(패링/클래시)"** (B 윈드업끊기 / C 방향재고 중).
7. **3차(A 맞받음):** 클래시 창을 *타격 순간*으로 좁힘 + 클래시 프리즈("탁").
8. **3차 플레이:** "전혀 안 뜸." → §4 진단 → 더미 제작(이 핸드오프 시점).

---

## 2. 현재 빌드 (파일·메커니즘)

**신규 인터페이스(OCP — 무기↔적 디커플):**
- `IAttackCommit` — `IsCommittingAttack`(커밋 전체=윈드업 글로우용) + **`IsStriking`**(실제 타격 실행=Lunge/Bite만, 클래시 판정용).
- `ICritReact` — `OnCritHit()`(크리티컬 시안 플래시).

**맞받음/클래시 (`Player/KatanaWeapon.cs`):**
- `DoHit`에서 적중 적이 `IsStriking`이면 클래시: 데미지 ×`critDamageMult`(4) + `Propagate`(주변 전파) + `(dmg as ICritReact).OnCritHit()`(시안) + `ClashFx()?.Clash()`(프리즈) + `critKick`(0.4 카메라 킥).
- ★창을 `IsCommittingAttack`(커밋 전체)→`IsStriking`(타격 순간)으로 좁힌 게 3차의 핵심(수동 겹침→의도적 메).

**적 (`CaniathroxChaser.cs`):** `IAttackCommit` 구현(`IsStriking`=애니 Lunge/Bite). `UpdateCommitSignal`로 매 프레임 receiver에 글로우/NOW 구동. 적→플레이어 타격(`enableStrikeDamage`, Lunge/Bite 중 사거리면 1회 피해 = "빗나가면 맞음" 위험 절반).

**가독 (`EnemyDamageReceiver.cs`):** `ComposeVisual` 본체 색 합성(우선순위 크리티컬시안>피격흰>NOW흰>윈드업 회→주황>base). `DriveCommit(windup01, now)`·`OnCritHit`. NOW=엣지 래치 짧은 펄스(`commitNowFlashTime` 0.1). `_visualDirty`로 idle 호드 매프레임 MPB 쓰기 회피. `_EmissionColor` 구동(블룸).

**프리즈 (`Player/ParrySlowMotion.cs`):** `Clash()` = 짧은 프리즈("탁")만(긴 슬로모는 방어 퍼펙트회피 OnParry 전용). `clashCooldown`(0.13)으로 호드 스터터 방지. timeScale 단일 소유자.

**cap (`AttackTokenPool.cs`·`CombatSliceSpawner.cs`):** `SetMax`/`MaxTokens` 런타임 조정 + `uncapTokens` 토글(게이트질문 "cap 풀어도 떼-듀얼 결합되나"용).

**경합 완화 (`HitStop.cs`):** `ActiveScale` 정적 — ParrySlowMotion이 `Mathf.Max`로 HitStop을 0으로 stomp 안 하게(단방향).

**핵심 노브:** `KatanaWeapon`(critDamageMult·critKick·critPropagation*) · `ParrySlowMotion`(clashFreeze 0.05·clashCooldown 0.13) · `CaniathroxChaser`(strikeDamage·strikeHitRadius) · `CombatSliceSpawner`(maxAttackTokens·uncapTokens·swarmSize) · `EnemyDamageReceiver`(commitColor·commitEmission·commitNowFlashTime·critFlashColor).

---

## 3. 게이트 이력 (Stab+Codex 병렬, 재량 없음 — 비누설 보고됨)

3회 게이트 통과. 주요 수정(전부 적용):
- **1차(메커닉):** H-2 전파버퍼 zombie-ref·전파 포화 경고·SpawnOne Animator fail-fast.
- **2차(가독):** NOW 흰색 발사전구간 고착→엣지 펄스 · 크리티컬 시안 희석→완전 override · 첫프레임 감쇠→ComposeVisual 감쇠전 · target null색잔존 · 치명크리티컬 시안 순서 · Awake RestoreBase.
- **3차(클래시):** Clash 프리즈 끝프레임 비활성화 timeScale 누수→OnDisable timeScale≠1 복원 · `_clashFxResolved` Initialize stale→무효화 · ParrySlowMotion↔HitStop 경합 단방향 완화.

---

## 4. ★현재 블로커 — 클래시 전혀 안 뜸 (진단)

- **애니 상태명 = 정상** (확인: 컨트롤러 상태 `IdleAngry|Approach|Lunge|Spit|Bite|Coil|GetHit`, 코드 해시 일치). → 상태명 버그 아님.
- **원인 1순위 = 타이밍/도달성 창:** Lunge가 5m에서 4.67m를 빠르게(~0.77s) 돌진 → 적이 *Lunge 상태이면서 칼 사거리(1.8m) 안*인 순간은 돌진 막바지 찰나. + 플레이어 베기는 스윙 히트프레임에 늦게 떨어짐. → (히트프레임)∧(IsStriking)∧(사거리)의 겹침이 거의 빈 집합.

---

## 5. 다음 — 더미 실험 → 분기

**`ClashTestDummy`(`Scripts/Debug/`)** — 정지 표적이 텔레그래프→타격 반복(또는 [T]키). 자체 코드 타이머로 `IsStriking` 구동(애니 무관), 넉넉한 `strikeWindow`(0.4)라 항상 도달 가능. 화면 좌상단 라벨에 phase + 클래시 판정. 자동 부트스트랩(콜라이더·리시버·비주얼·레이어7).
- **사용:** `CombatSliceSpawner` 비활성(호드 끔) → 빈 GO에 `ClashTestDummy` 붙여 플레이어 앞 ~1.5m → ▶ → `Strike`(흰) 표시 때 베기 → `★CLASH` 떠야.

**분기:**
- 더미에서 **★CLASH 뜸** → 메커니즘 정상. 진짜 적 = 창/도달성 문제 → **Caniathrox 클래시 창을 *시간 기반*으로 넓힘**(타격 직전~직후 일정 시간, 정확 상태 무관) + Lunge 감속/사거리 보정. (가장 유력한 다음 작업.)
- 더미에서도 **안 뜸** → 메커니즘 버그 → KatanaWeapon DoHit 경로·레이어·GetComponentInParent 추적.
- 0.4s 창에서도 **A 손맛 안 남** → A 실패 → **B(윈드업 끊기 맛) 또는 C(방향 재고, Codex 구조충돌 우려 재점화)**.

---

## 6. 미해결 부채 (deferred)

- **H-2 — 치명 클래시 본체 플래시 누락:** 적이 즉사(SetActive false)라 죽음 순간 본체 시안/흰 플래시 1프레임도 안 보임. **죽음연출 시스템**(디졸브/파쇄 티어, 별도 스펙)과 얽힘. 현재 치명 클래시 구별은 critKick(0.4)+킬로만. → critDamageMult 낮춰 비치사로 먼저 손맛 확인 권장.
- **전역 timeScale 소유권 7곳**(HitStop·ParrySlowMotion·LevelUp/Chest/GameOver UI·RunManager·CarHitFeedback) = 장기 부채. 클래시가 표면 넓힘. 단방향만 완화. Crassorrid(HitStop 사용)+클래시 한 씬 공존 시 **통합 중재** 필요. 현 프로토(Caniathrox만)엔 미발화.
- **미구현(이번 범위 밖):** 실루엣 scale 팽창 · 화면공간 마커 · **오디오 틱**(Sound 도메인·음색 판정 불가) · C 위협 벡터선 · 클래시→반격 연계.

---

## 7. 게이트질문(원래) + 검증 채널

- ★**"cap 풀어도 떼-듀얼 결합되나"** (`uncapTokens` 끄고↔켜고) — 클래시가 일단 뜨고 난 뒤 판정.
- 검증 = 인엔진 플레이(게임감=유저). 코드/컴파일 = Unity MCP RunCommand. 시각 = 유저 눈(에디트모드 캡처 불가).

**씬:** `Assets/_Project/Scenes/Labs/_CombatSlice_ReadAndCut.unity`.
