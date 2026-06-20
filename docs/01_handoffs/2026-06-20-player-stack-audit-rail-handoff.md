# 핸드오프 — 플레이어 스택 감사 · 액션-조정 레일 · 잔재 정리 (2026-06-20)

> 다음 작업자(또는 다음 세션)용. **먼저 §1 TL;DR과 §4 다음 작업을 읽어라.** 아키텍처 계약(레일이 어떻게 동작하고 새 무기를 어떻게 붙이나)은 별도 권위 문서 [[2026-06-20-player-action-coordination-rail]]에 있다 — 코드 만지기 전 그걸 읽는다.

---

## 1. TL;DR

유저가 `_PlayerStackTest` 씬 전투 기능이 "얽혀간다"(회피 후 공격 미발동)고 느낌 → 플레이어 스택 전체를 9차원(파이프라인/SOLID/데이터/경계/규칙/세이브/빌드/버전관리)으로 **3중 감사**(나+Stab+Codex, 점수화). **E(시스템 얽힘) 3.5 FAIL** 확정 → **"액션-조정 레일"**(busy를 Animator 실상태가 소유)로 구조적 해소 → **E 7.5/8.1 PASS** → 비활성 Tumbling 잔재 전면 제거. 전부 커밋됨. **⚠️ 런타임 손맛은 미검증(정적 검증의 천장) — 다음 작업 1순위 = 플레이 테스트.**

---

## 2. 현재 상태 (커밋됨)

| 커밋 | 내용 |
|---|---|
| `fcfe2d8d8` | feat — 카타나 히트박스 SO통합(ComboAttackSet) + 회피/패링/슬로모/반격 카운터 (감사 전 체크포인트) |
| `1f6cd6998` | refactor — **액션-조정 레일**(busy=Animator 진실) + 빌드위생 + 권위문서 |
| `56e850d34` | refactor — 비활성 Tumbling 잔재 전면 제거(−216줄, 상태 7→6) |

- 컴파일 errors=0. `main` 브랜치.
- **미커밋 잔재(무해):** `Assets/_PlayerStackTest.unity:2420-2421`에 orphan 직렬화값(`tumblingSpeed`/`tumblingDistanceScale`) — Unity가 무시. 다음 씬 저장 시 자동 정리(직접 YAML 편집은 위험 대비 무익이라 방치).
- 작업 트리에 무관한 미커밋 노이즈 다수(ACS meta·머티리얼·에이전트 메모리·미추적 에셋팩 Frank/Footsteps) — 이번 작업과 무관, 손대지 않음.

---

## 3. 무엇을 했나 (작업 흐름)

1. **3중 감사:** 기반(이동/조준/입력/데이터 SO) 7~9 양호, **E=3.5~4 FAIL(구조적 High)**, H(빌드) 4~6. ★Stab은 E=8로 과소평가했고 **Codex+내 루트코즈가 수렴**(Codex가 맞음 — 유저 버그가 증거). 근본원인 = 무기가 입력으로 busy를 낙관 커밋 → Animator 못 받으면 두 상태머신 desync.
2. **OCP 논쟁 해소(유저: "무기 더 오는데 틀 갖추면 OCP 아니냐"):** OCP 3층 — (a)무기 확장 seam=이미 있음 (b)공유 액션-조정 레일=지금 제대로(현재 관심사·이미 고장남) ✓ (c)무기별 내용 틀=미루기(상상 추상화 회피). 결론 = (b) 레일을 지금 짓는다.
3. **레일 구현(=E 수정):** busy=Animator 진실. ①Combo1/2/3·Counter "Action" 태그 ②`IsActionPlaying` ③`WeaponBehaviour` 템플릿 Tick+`BeginAction`+레일 IsBusy ④진행플래그 자가치유 ⑤입력 버퍼 ⑥AnyState→Combo1+Dash정리. **재감사 E Stab 7.5/Codex 8.1 PASS.**
4. **빌드위생(Tier-1):** Debug.Log `#if UNITY_EDITOR`·FindObjectOfType 제거·HitboxDebug 빌드 빈셸·DashGhost hideFlags.
5. **Tumbling 잔재 제거:** 코드+컨트롤러 전면. 4중 게이트 CLEAN.

---

## 4. ★ 다음 작업 (우선순위)

### 4.1 [P0] 런타임 플레이 검증 — 가장 먼저
구조는 정적으로 닫혔지만 **손맛·발동은 빌드로만 확인 가능**(정적 검증의 천장). `_PlayerStackTest` 씬에서:
- [ ] **회피(대시)→좌클릭** 시 칼이 *즉시* 나가는가(이번 수정의 핵심 — 이게 안 되면 레일 어딘가 재점검).
- [ ] **콤보 1타 리셋 루프** 없는가(Any→Combo1 `canTransitionToSelf=0` 검증됐지만 플레이 확인).
- [ ] **패링→반격(Skill02)** 발동하는가(퍼펙트 회피창 0.15s 안에 적 공격 맞기 → 슬로모 + 반격창 → 좌클릭). 테스트 적 = `__HazardPad`(텔레그래프 후 타격).
- [ ] 콤보 중 이동 잠금·종료 후 즉시 이동 가능(busy 연속성).
- [ ] 진입 실패 자가치유 에디터 경고가 *정상 플레이엔 안 뜨는지*(뜨면 "Action" 태그/전이 문제 신호).

### 4.2 [P1] 잔재/위생 마무리
- [ ] **디버그 더미 정리** — `Assets/_Project/Scripts/Debug/`(HazardPad/HitTest/HitboxDebug)는 throwaway. 데모 빌드 전 제거 or Editor asmdef 격리. 씬의 `__HazardPad`/`__HitTestDummies`/`__HitboxDebug` 오브젝트도.
- [ ] `_PlayerStackTest.unity` orphan 직렬화값(§2) — 씬 한 번 저장하면 정리.

### 4.3 [P2] 감사가 남긴 Medium/Low (구조 위협 아님)
- [ ] `ComboAttackSet` 미할당 폴백 수치(range 1.8/arc 50/dmg 3)가 코드 은닉 → Inspector 노출 or 미할당 에러.
- [ ] `PlayerHealth` 세이브 복원 진입점 부재(미래 세이브 대비).
- [ ] asmdef 부재(`_Project.Player`/`_Project.Debug` 분리, 중기).

---

## 5. 알아야 할 것 (gotchas)

- **★"Action" 태그는 load-bearing.** 새 공격/반격 Animator 상태에 State Tag "Action" 안 달면 busy가 유예(0.12s) 뒤 풀려 이동 누수. 자가치유 에디터 경고가 잡지만 *안 다는 게 정답*. → 권위문서 §4.
- **위치 단일 소유 = PlayerMotor.** 이동기 추가 시 반드시 `ApplyRootStep` 경유(이중 위치 소유 금지).
- **애니가 진실.** 타격/캔슬창/종료 타이밍은 클립 AnimationEvent(`OnAttackHit`/`OnComboWindow`/`OnComboEnd`). 코드 타이머로 타이밍 만들지 마라.
- **시간 도메인 혼재는 의도적**(권위문서 §7 표) — 사람 반응창=unscaled, Animator 정렬=scaled. 새 타이머는 이 표 보고 정렬.
- **git index.lock 반복 이슈** — 이 환경에서 스테일 락이 자주 생긴다. 커밋 전 `test -f .git/index.lock` 확인, *활성 git 프로세스 없을 때만* `rm -f .git/index.lock`(이번 세션 2회 발생, 둘 다 스테일이었음).
- **RunCommand 리플렉션 NRE** — Unity MCP RunCommand에서 private 멤버 리플렉션은 하니스 NRE 유발. 공개 API 직접 참조로 컴파일 검증.
- **Tumbling은 완전히 죽었다.** 코드·컨트롤러에서 제거됨. 패링 시 구르기 연출이 다시 필요하면 새로 설계(옛 배관 복구 금지 — 레일과 안 맞음).

---

## 6. 검증 상태 (정적 ✅ / 런타임 ⏳)

| 항목 | 상태 |
|---|---|
| 컴파일 errors=0 | ✅ |
| 레일이 E 구조적 High 해소 | ✅ Stab 7.5 / Codex 8.1 독립 수렴 |
| Tumbling 제거 회귀 없음 | ✅ 코드/컨트롤러 4중 게이트 CLEAN |
| "Action" 태그·AnyState 전환 직렬화 | ✅ API 읽기 + 디스크 YAML |
| **회피→공격 즉발 / 반격 발동 / 1타리셋 부재 (손맛)** | ⏳ **유저 플레이 미검증 — P0** |

---

## 7. 핵심 파일·문서

- **권위 문서(필독):** `docs/00_authority/2026-06-20-player-action-coordination-rail.md` — 레일 동작·★§4 새 무기/액션 추가 절차·시간도메인 표·함정.
- **메모리:** `project_2026_06_20_player_action_rail.md`(이 세션 요약·OCP 해소·감사 결과).
- **코드:** `Assets/_Project/Scripts/Player/` (WeaponBehaviour=레일 베이스, KatanaWeapon, PlayerAnimatorDriver=IsActionPlaying, PlayerBrain=오케스트레이션, PlayerMotor=위치 소유).
- **컨트롤러:** `Assets/_Project/Animation/KatanaMelee.controller` (Combo1/2/3·Counter "Action" 태그, AnyState Dash>Counter>Combo1).
- **씬:** `Assets/_PlayerStackTest.unity`.
