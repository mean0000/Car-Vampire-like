# 화이트박스 Verb 랩 — 착공 완료 핸드오프 (2026-07-02)

> **상태:** Layer 0 동결 ✅ · Stage 1 골조 구현 ✅ · 게이트(Stab+Codex) 통과·반영 ✅ · 씬 배선·저장 ✅ · 컴파일 클린 ✅ · **전부 미커밋** · **다음 = 유저 플레이 판정 (§4가 RESUME)**
> 선행 문서: [[2026-07-02-combat-care-catalog]](배려 카탈로그 — 노브 정의) · [[2026-07-02-whitebox-verb-lab-spec]](랩 스펙 — 게이트 질문) · [[2026-07-02-core-verb-session]](결정 흐름 + Codex 원문 2건)

---

## 0. 이 랩이 검증하는 것 (동결 컨텍스트)

**Layer 0 (2026-07-02 유저 동결):** *"사후처리부 — 처리는 실적이다. 후퇴하면 실적이 마르고, 파고들어 처리할수록 회사가 너를 먹여 살린다. 회사가 '효율'이라 부르는 것이, 네 화면에서는 간지다."*

무기불가지론 3원칙 v1.1(Codex 반증 4조건 반영): ①전진=공간(전진은 거리축소 아닌 **각·선 점유**) ②커밋=기회(읽고-처리 증폭, 타이밍 입력 요구 ❌) ③화려함=실적=생존(**저점 안전망** — 실적은 가속기지 생존권 아님).

유저 확정 4건: 게이지=**상시형** · 특별 조작감=**1·2·3 전부**(2는 Stage 2) · 카타나=**3층 구조** · **납도·발도류=추후 거합 스킬**(현행=베기 트리: 가르기+읽고-베기만).

---

## 1. 구현 인벤토리 (코드 — 신규 4 · 수정 5)

| 파일 | 무엇 | 핵심 |
|---|---|---|
| **[신규]** `Scripts/SwarmChaser.cs` | 밀리는 잡몹 — RB 추격+임펄스 밀림+접촉딜 | `OnKnocked`→`AddForce(VelocityChange)`, `shoveScale`=질량 티어 노브, `shoveRecover` 동안 추격 양보. 가속 수렴(`chaseAccel` — 강제 대입 ❌) |
| **[신규]** `Scripts/PerformanceGauge.cs` | 처리효율 게이지 스텁(상시형) | 킬+5/크리+10, 감쇠 4/s, 피격 −12. `HealMult`/`GenerosityMult`(최저 1.0=저점 안전망) — **Stage 2 훅, 아직 미소비**. OnGUI 디버그 라벨 |
| **[신규]** `Scripts/Player/ReadSlowmoTrigger.cs` | 읽기 슬로모 트리거 | `CommitStarted`(정적) 구독→`HighValueCommit`+거리 6m 필터→`ParrySlowMotion.ReadSlow()` |
| **[신규]** `Scripts/WhiteboxVerbLabSpawner.cs` | 랩 스포너 | 캡슐 프리미티브 절차 스폰(레이어 7, 프리팹 0, Shader.Find ❌). 워밍업 12→배치 보충(4/0.4s)으로 목표 60 유지 |
| **[수정]** `Scripts/Player/KatanaWeapon.cs` | ★가르기+히트 글라이드 | `lineCutEnabled`(콤보만 절단선 — 커서=베고 지나갈 선, 폭 1.6m=커서 갭 관용. 반격/스킬/대시베기=기존 부채꼴) · `hitGlideEnabled`(적중 시 +0.25m/0.12s `Motor().AddGlide`) · 절단선 사거리=`along` 기준(원거리 ❌)+수집 반경 보정+XZ 축 정규화 |
| **[수정]** `Scripts/Player/PlayerMotor.cs` | AddGlide API | 벽가드+지면 파이프라인 공유, locked 중 적용(의도), 대시가 잔여 글라이드 클리어. **루트모션과 같은 프레임 순차 가산=의도**(Codex P1-2 판정) |
| **[수정]** `Scripts/Player/ParrySlowMotion.cs` | ReadSlow 진입점 | timeScale **단일 소유 유지**. 읽기 전용 배율 0.3/유지 0.18s/쿨다운 2.5s/복귀 0.15s(패링 ramp와 독립 — Stab M-1). 패링/클래시 진행 중엔 양보. ★잠복버그 수정: 프리즈 분기 `Max(0, ActiveScale=1)=1`이 패링 프리즈를 무효화하던 것 |
| **[수정]** `Scripts/HitStop.cs` | 상호 stomp 완화 | 종료 시 자기 값(0.05)일 때만 1 복원(진행 중 슬로모 파괴 방지). ⚠️완전 통합 중재는 별도 과제(§5) |
| **[수정]** `Scripts/EnemyDamageReceiver.cs` | 이벤트 허브 확장 | `OnKnocked`(인스턴스 — 셔브)·`AnyDied`/`AnyCritHit`/`CommitStarted`(정적 — 게이지/슬로모)·`highValueCommit` 필드·`_dead` 가드. ⚠️정적 이벤트 구독자는 OnDisable 해제 필수(현 구독자 2종 대칭 확인됨) |

읽고-베기 증폭은 **신규 코드 0** — 06-24 프로토(IAttackCommit `IsStriking` 크리 ×4+전파 3m)가 이미 KatanaWeapon에 있고, 잡몹/게이지가 그 위에 꽂힘. 크리 전파는 넉백을 실어 **물리로도 호드를 가른다**.

## 2. 씬 상태 (저장 완료)

`Assets/_Project/Scenes/Labs/_CombatSlice_ReadAndCut.unity`:
- **WhiteboxVerbLab** GO 신설 = WhiteboxVerbLabSpawner + PerformanceGauge
- **Player** GO에 ReadSlowmoTrigger 부착(ParrySlowMotion과 동거 — 자동 탐색 보장)
- 엘리트(Caniathrox, IAttackCommit)=기존 **CombatSliceSpawner**가 플레이 시작 시 런타임 스폰(에디트 타임 0마리는 정상)
- ⚠️Stab L-3: 이 씬의 KatanaWeapon은 새 필드 default로 **가르기 모드 자동 켜짐**(랩 의도). 부채꼴 유지가 필요한 다른 씬이 있으면 그쪽 Inspector에서 꺼서 저장할 것.

## 3. 게이트 기록 (Stab+Codex 병렬 — 원문은 세션 로그·메모리)

- **독립 수렴 1건**: 절단선 사거리를 원거리(dist)로 잘라 선분 구석 누락 — Stab H-1 ≡ Codex P2-1. 수정 ✅
- Codex 수용 3: timeScale 상호 stomp(P1-1) · SwarmChaser 속도 강제대입(P2-2) · 스폰 링 (0,0) 엣지(P3). Stab 수용 6: 읽기 ramp 분리(M-1) · 무음 실패 경고 2건(M-2/L-4) · 스폰 스파이크(M-3) · 사망 커밋 가드(L-1) · 축 정규화(L-2). 전부 반영 ✅
- **거절 1건**: Codex P1-2(글라이드+루트모션 같은 프레임 이중 위치 쓰기) — "적중 시 가산"이 설계 의도, 두 쓰기 모두 벽가드 경유라 안전 판정. **랩 관찰 항목**으로 강등.
- **보너스 발견**: 패링 프리즈 무효화 잠복버그(위 §1 ParrySlowMotion) — 이번 플레이에서 패링 "탁"이 처음 제대로 걸림.

## 4. ★RESUME — 유저 플레이 판정 가이드

**씬**: `_CombatSlice_ReadAndCut` 플레이. 잡몹 12→수 초 내 60, 좌상단 `EFFICIENCY/RANK` 디버그 라벨.

**게이트 질문(랩 스펙 §1 — 사전 정의, 사후 합리화 금지):**
- **G1** 가르기(LMB=커서 방향 절단선)만으로 2분간 *자발적으로* 후퇴 없이 전진하게 되는가
- **G2** "커밋한 놈 베면 터진다"를 설명 없이 스스로 발견하는가 (엘리트 상대)
- **G3** 베면 호드가 갈라지고, 열린 길로 실제 들어가게 되는가 (밀어버리기)
- **G4** 토글 A/B — 끄면 서운한가: KatanaWeapon `lineCutEnabled`(가르기↔부채꼴)·`hitGlideEnabled` / Player `readSlowmoEnabled` / ParrySlowMotion `readSlowDuration=0`(슬로모 무력화)
- **관찰(Codex 반증)**: 커밋 낚시(거리 두고 대기-수확)했는가 · 슬로모가 소음인가 · 60마리에서 프레임 괜찮은가

**주요 노브(Inspector):** 가르기 폭 `lineCutWidth` 1.6 · 글라이드 `hitGlideDistance` 0.25 · 읽기 슬로모 0.3×/0.18s/쿨 2.5s · 잡몹 `shoveScale` 1(밀림 크기)/`chaseAccel` 25 · 스포너 `targetCount` 60 · 게이지 감쇠 4/s.

## 5. 알려진 리스크 / 이후 과제

1. **timeScale 통합 중재 미완** — HitStop↔ParrySlowMotion은 상호 완화만(최악 1프레임 블립). 시간 연출이 하나 더 생기면 그때 단일 중재 레이어로 통합(코드 주석에 표식).
2. **Stage 2 미배선** — `HealMult`/`GenerosityMult` 소비자(회복 시스템·버퍼/마그네티즘 스케일) 연결은 Stage 1 기준값 동결 후.
3. **퇴행 가드 2종**(커밋 낚시·보험 사육)은 규칙 미구현 — 랩에서 실제 발생 확인 후에만 막는다(over-systematization 경계).
4. 몬스터 재설계 재개 시 [[2026-06-28-monster-attack-redesign-grounding-handoff]] **§0.5 Layer 0 정합 조항**(커밋 창 명시·질량 티어) 적용.
5. **커밋 대기** — 코드 9+씬 1+문서 다수 미커밋(유저 확인 후). 별건 혼입 주의: 오늘 diff에는 Fable 복귀 문서 정리(CLAUDE.md·운영정책·README·배너 7건·visualcritic.md)도 섞여 있음 → **커밋 분리 권장**: ①docs(정책/README/배너) ②feat(랩 골조+씬).

## 6. 판정 후 분기

- G1~G4 PASS → 살아남은 배려 기준값 동결 → 커밋 → Stage 2(실적 결합) 착공.
- 가르기 FAIL → `lineCutEnabled=false`로 부채꼴 복귀 + 폭/글라이드 재튜닝 루프 (하드컷 아님 — 토글 보존).
- 판정 결과는 [[2026-07-02-whitebox-verb-lab-spec]]에 기록 후 카탈로그 시작값 갱신.
