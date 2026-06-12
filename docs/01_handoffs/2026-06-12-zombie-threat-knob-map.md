# 좀비 위협 노브 맵 — 세션 3 산출물 (노브 세션용)

> **목적**: [[2026-06-12-threat-pass-session-briefing]] §4의 산출물. 좀비 위협 관련 수치가 *실제로 어디 사는지* + 노브 세션(유저 인더루프)에서 실시간으로 돌릴 수 있는지를 한 장으로.
> **무대**: `Greybox_CombatLab.unity` · **미니 게이트**: "좀비 한 마리 보고 심박이 바뀌는가"
> **작성**: 2026-06-12 세션 3 (좀비 위협 본체)

---

## ★ 노브 세션 사용법 — 가장 중요한 사실 1개

**좀비 수치의 진짜 위치는 SO 에셋 2~3개다** (`Assets/_Project/Data/ZombieConfig_*.asset`). ZombieController는 거의 모든 값을 **사용 시점에 `_config.*`로 참조**하므로:

- **플레이 모드 중 SO 에셋 인스펙터에서 값을 바꾸면 살아있는 좀비 전원에게 즉시 적용**된다 (라이브 ✅ 표시 행).
- **SO 에셋 수정은 플레이를 꺼도 증발하지 않는다** (씬 오브젝트와 달리 영구 저장) — 노브 세션에서 찾은 값이 그대로 박제됨.
- 예외(라이브 ❌/⚠️): Init 1회 캐시 값들 — 표에 명기. 바꾸면 **새로 스폰되는 좀비부터** 적용.

플레이어 쪽 수치만 **씬 직렬화**(코드 default를 이김 — [[2026-06-12-threat-pass-session-briefing]] §5 함정 1번). 정지 상태에서 씬 오브젝트 인스펙터로 수정 후 씬 저장.

---

## 1. 노브 표 — 치명(대미지/HP)

> 열 안내: **패스 전** = 06-12 위협 패스 이전 값. **시작값** = 이번 패스에서 **이미 에셋에 적용 완료**된 값 — 노브 세션은 여기서 출발해 돌린다.

| 노브 | 진짜 위치 | 패스 전 | 시작값(적용됨) | 라이브 | 메모 |
|---|---|---|---|---|---|
| 좀비 대미지 | `ZombieConfig_General.asset` › `attackDamage` | 20 | **35** | ✅ | 적용 3곳 전부 이 한 노브: 런지 잡힘 한입(ZC:766)·런지 스침(ZC:773)·그래플 타임아웃 한입(ZC:799) |
| 플레이어 HP | **씬** `Greybox_CombatLab.unity` PlayerController › `maxHP` (코드 default `PlayerController.cs:9`도 100이라 지금은 우연히 일치) | 100 | **100 유지** | ⚠️ Start 1회 (`_currentHP = maxHP`) — 정지 상태에서 수정 | 35대미지면 HP 변경 없이 3대 사망권 도달 |
| 그래플 탈출 연타 수 | **씬** PlayerController › `grappleEscapePresses` | 5 | 5 유지 | ✅ (잡히는 시점 참조) | 높일수록 그래플=사형선고에 근접 |
| 그래플 한 입 | `ZombieConfig_General` › `grappleDamage` (신규, Phase B) | —(attackDamage 공용) | **50** | ✅ | 잡는 순간 1회 + 타임아웃 1회 = 풀코스 100 = **즉사권**. 런지 스침은 attackDamage(35) 유지 |
| 그래플 홀드 | `ZombieConfig_General` › `grappleHold` | 2.5s | 2.5 | ✅ | 타임아웃=한입 추가 후 놓아줌 |
| 좀비 HP | `ZombieConfig_General` › `maxHP` | **2** (★코드 default 3과 다름 — 에셋이 진짜) | 2 유지 | ❌ Init 캐시 — 새 스폰부터 | 위협은 좀비를 단단하게가 아니라 아프게 |

**산술 메모 (시작값 근거)**: 브리핑의 "대미지 ×3"(=60)과 "일반 타격 3~4대 사망권"(=25~33)은 HP 100 기준 양립 불가. 시작값은 **결과 스펙(3~4대) 우선 → 35** (35·70·105 = 3대 사망). **×3 리터럴 프리셋 = 60** (2대 사망권) — 노브 세션에서 숫자 하나 바꾸면 즉시 체험 가능. 그래플 풀코스(잡힘 50+타임아웃 50) = 100 → 빨리 탈출하면 절반(50), 탈출 실패 = 즉사권. "잡히면 절반~즉사권" 스펙 그대로.

## 2. 노브 표 — 속도/공격 사이클

| 노브 | 진짜 위치 | 패스 전 | 시작값(적용됨) | 라이브 | 메모 |
|---|---|---|---|---|---|
| 이동속도 (워커) | `ZombieConfig_General` › `moveSpeed` | 3.0 | 3.0 유지 | ✅ (매 틱 참조 ZC:699) | Chase=×1.0 · Alert 접근=×`alertApproachSpeedMult`(0.35) |
| 이동속도 (스프린터) | `ZombieConfig_Sprinter.asset` › `moveSpeed` (신규) | — | **5.2** | ✅ | §4 참조 |
| 공격 쿨다운 | `ZombieConfig_General` › `attackCooldown` | 1.5s | 1.5 | ✅ | 런지 사이클(Recover 종료 시) 쿨다운으로 동작 |
| 런지 사거리/윈드업/속도/지속/후딜 | `ZombieConfig_General` › `lungeRange / lungeWindup / lungeSpeed / lungeDuration / lungeRecover` | 2.6 / 0.5 / 9 / 0.35 / 0.6 | 유지 | ✅ | `lungeWindup` = 텔레그래프 그 자체. 줄일수록 위협↑·억울함↑ |
| 그래플 접촉 반경 | `ZombieConfig_General` › `grappleContactRadius` | 1.0 | 1.0 | ✅ | 키우면 회피 난도↑ |
| 인지(발각) 속도 | `ZombieConfig_General` › `detectBaseChance / detectGaugePerTick / alertStareTime` | 0.45 / 0.34 / 1.5 | 유지 | ✅ (`senseVariance`만 개체 Init 캐시) | Alert 응시 = 스프린터 포함 전 좀비의 1차 텔레그래프 |
| 스폰 인구 | **씬** ZombieSpawner › `minZombies / maxZombies / signalSpawnChance` | 35 / 60 / 0.08 | 유지 | ✅ (Update 참조) | 위협 = 밀도가 아니라 개체. 이번 패스에서 안 올림 |
| 스프린터 혼합률 | **씬** ZombieSpawner › `sprinterChance` (신규) | — | **0.2** | ✅ | §4 참조. Signal(0.08) 우선 후 잔여 롤 → 실효 ~18.4% |
| 어그로 애니 가속 | `ZombieConfig_*` › `chaseAnimSpeedMult` (신규, Phase B) | — | 워커 **1.0** / 스프린터 **1.25** | ✅ | Chase~Recover 중 애니 재생속도 배수. 히트스탑·킬 프리즈와 충돌 없음(리뷰 검증) |

## 3. 함정 명기 (이 표를 의심해야 할 때)

- **`ZombieConfig_Signal.asset`은 신규 필드(인지/피격 사다리/런지 헤더 전체 + Phase B의 `grappleDamage`/`chaseAnimSpeedMult`)가 미직렬화** → 코드 default(50/1.0)로 동작 중. 인스펙터에서 한 번 저장하면 그 시점 값으로 박제됨. Signal 좀비의 해당 수치를 바꾸려면 먼저 에셋을 열어 직렬화부터.
- 옛 `ZombieCharger.prefab` / `ZombieLaser.prefab`은 **죽은 프리팹** (05-25 스크립트가 e05d47c94 재작성에서 소실). 재활용 불가 — 스프린터는 신규 제작이 정답이었음.
- 투시버그(obstacleMask=0)는 CombatLab 해당 없음 — 씬 내 좀비 전원 + `Zombie.prefab` 모두 256(Obstacle) 확인.
- 손배치 좀비 Init 누락 → 미커밋 ZombieController의 Start 자가 부트스트랩 폴백이 처리 (세션 2 일괄 커밋에 포함될 분).

## 4. 스프린터 — 시공 명세 (이번 세션 시공분)

- **`ZombieConfig_Sprinter.asset`** (신규, General 타입): `moveSpeed 5.2` · `acceleration 8` · `alertStareTime 1.0` · `alertApproachSpeedMult 0.4` · `lungeRange 3.0` · `lungeSpeed 11` · `lungeWindup 0.45` · `chaseAnimSpeedMult 1.25` · 나머지 General과 동일(대미지 35, grappleDamage 50, HP 2). **전 필드 명시 직렬화** (§3 Signal 함정 방지).
- **`Zombie_Sprinter.prefab`** (Zombie.prefab 변형): `_config` → Sprinter 에셋 + 몸체 머티리얼 색 차별(불그스름/탈색 — 원거리 사전 식별용. 메시 교체 아님).
- **텔레그래프 (브리핑 §4.3 필수 조건)**: ① Alert 응시 1.0s(멈춰 쳐다봄 — 기존 시스템) ② 런지 윈드업 0.45s(상체 젖힘) ③ 머티리얼 식별색. 추가 절규 사운드 큐 = 세션 1 소유.
- **혼합**: `ZombieSpawner.cs`에 `sprinterZombiePrefab` 슬롯 + `sprinterChance 0.2` (Signal 판정 후 잔여에서 20%). CombatLab 스포너에 와이어링 + 스프린터 2기 추가 손배치(보장 조우 — 실측 손배치는 14기: 서측 CornerLab 군집/동측 Lab A·B·C 군집, 군집당 1기씩 `Zombie_Sprinter_A @ (83,1,43)` / `_B @ (-44.5,0,-38.5)`).

**시공 기록 (2026-06-12, 에디터 API 경유)**: `ZombieConfig_Sprinter.asset`(guid 0d7b16d7…) · `Zombie_Sprinter.prefab`(변형, guid 5252040f…) · `M_ZombieSprinter.mat`(적색 틴트 1.0/0.5/0.42, 원본 Color.mat 불변, guid f2bc0407…) · ZombieSpawner.cs 3지점 · CombatLab 와이어링. 컴파일 에러 0. ⚠️`sprinterChance`는 CombatLab 씬에 0.2로 직렬화됨 — 이후 코드 default 변경은 이 씬에 안 먹음(노브는 씬 인스펙터).

## 5. Phase B — 상태 (세션 2 일괄 커밋 f56bd969f로 동일 세션 내 해금됨)

1. ✅ **`grappleDamage` 노브 분리** — 시공 완료(잡힘 한입 50 + 타임아웃 한입 50, 스침은 attackDamage 유지). 그래플 홀드 중 틱 대미지 옵션은 노브 세션 판정 후 필요 시.
2. ✅ **어그로 애니 가속** — `chaseAnimSpeedMult` 시공 완료(BaseAnimSpeed 헬퍼, 히트스탑 복원·킬 프리즈 0과 충돌 없음 — Stab/Codex 교차 검증. Stab의 "첫 입 즉사 → 영구 Grapple 잠김" HIGH는 오탐 판정: `_state=Grapple` 대입이 TakeDamage보다 선행하므로 사망 경로의 OnGrappleEnded가 정상 통과→Recover 전이).
3. ⏳ **스프린터 전용 어그로 연출 훅** (런지 모션 차별 등) — 보컬은 세션 1과 합류 후.
4. ⏳ **추격 이동 장애물 스윕** (Codex 리뷰 MEDIUM) — 일반 추격 `MovePosition` 경로에 수평 obstacle 레이캐스트 없음(런지에만 있음). 워커 시절부터의 구조적 한계인데 moveSpeed 5.2에서 얇은 콜라이더 끼임 위험이 약간 상승. 발현 확인 시 런지의 sweep 패턴(ZC:739) 이식.

---

*결과 합류점: 노브 세션 (유저 + 세션 1, 이 문서 사용) → 미니 게이트 판정.*
