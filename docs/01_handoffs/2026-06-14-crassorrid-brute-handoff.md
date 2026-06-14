# 핸드오프: 2026-06-14 — Crassorrid 거구 브루트 (4번째 틀 · 텔레그래프 첫 가동 · 거구 알고리즘)

> **권위/경위**: Crassorrid(LV4 7m 브루트) = Caniathrox(근접돌진)·Venodonte(원거리)·Dimaxillosaurus(클로월)에 이은 **4번째 몬스터 틀 = "접근형 브루트: 접근→예고원 차오름→내려찍기 광역"**. 지어두고 안 쓰던 **ThreatArc 텔레그래프 시스템의 첫 소비자** + 근접 몬스터 첫 **임팩트 주스**(VFX·쉐이크·히트스탑). 하루 종일 유저가 플레이하며 거구의 *위협감*과 *행동*을 반복 디렉팅해 수렴.
> **상태**: 전부 **미커밋** — 유저가 플레이로 리듬 판정 후 커밋 예정(이 세션 다음). 전 단계 Stab+Codex 리뷰 통과.
> 연관: [[2026-06-14-large-enemy-ai-research]](거구 AI 웹 리서치 권위), [[2026-06-14-dimaxillosaurus-clawwall-handoff]](Dimax 파킹), `docs/00_authority/2026-06-13-topdown-attack-grammar.md`(공격 문법).

---

## ★0. 오늘 유저가 추구한 방향 (다음 세션 나침반 — 가장 중요)

거구 브루트를 통해 유저가 확정한 설계 가치. **기술보다 이게 먼저다.**

1. **거구 = "위협적이고 묵직"해야 한다.** 안 무서우면 실패("덩치 큰데 전혀 위협이 안 됨"). 위협 = 큰 AoE + 빠른 슬램 + **임팩트 VFX(먼지·충격파·균열) + 카메라 쉐이크 + 히트스탑**. 핵심 = *닿는 순간의 물리적 무게*. → 근접 몬스터 전반에 VFX 없는 게 "위협 안 됨"의 근본(Caniathrox·Dimax도 해당, SmashImpactPool로 재사용 토대 마련).
2. **공정하되 어렵게 — "걸어선 못 빠지고 대시로만"(dash-or-die).** 범위를 *훨씬* 크게(r8) + 빠른 공격 → 걷기로는 못 빠지고 대시로만 클리어. 단 완전 회피불가(운 게임)는 ❌(For Honor 갱 회피).
3. **★자연스러운 행동 — 어색함 박멸.** 거구가 *믿기는* 행동을 해야 한다. 박멸 대상: 뱅뱅 돌기 ❌ · 멀뚱 서기 ❌ · 굳음 ❌ · 어색한 백오프 ❌ · 지나쳐 찍기(미스) ❌. (= 6대 북극성의 *플레이어수용성*·*영리함*.)
4. **근접 = 계속 슬램(최종 방향).** 플레이어가 붙으면 굳거나 물러나지 말고 **계속 내려찍는다**. 옆에 붙은 빠른 플레이어도 *돌아서라도* 찍는다. (지난 턴의 스탠드오프/백오프는 굳음·어색함 유발로 폐기 → 이 방향으로 교체.)
5. **슬램 리듬 = 손드는건 보통속도 · 내려찍는건 빠르게 · 찍은 뒤 천천히 손 올림(느린 회수) · 큰 딜레이.** 느린 회수 + 딜레이가 "멀뚱"이 아니라 *의도적 비트*(노려보는 코일 자세). 이게 "정면에서 멀뚱 서있으면 안 된다"의 해법.
6. **소수 거구 = 동시 공격 OK.** 게이팅으로 막아 *기웃거리는* 게 더 어색. 동시 슬램 허용하되 **각 분산 + 미세 스태거**로 공정(같은 각·같은 순간만 막음). 대기 브루트는 맴돌지 말고 *플랭킹 재배치*.
7. **포효(Roar) 제거.** 인지 즉시 접근(오프너 없음).
8. **★레퍼런스 추적(임의설계 금지).** 거구/다중적 AI를 "게임들이 실제로 어떻게 짜는지" 웹 리서치로 근거 잡고 설계 → [[2026-06-14-large-enemy-ai-research]].

> **메타 프로세스(오늘 내내)**: 게임감(주스+행동) 집중 반복 — tune→play→feedback→tune. 유저가 *매번 직접 플레이로 판정*(정지 캡처로 모션/시간 효과 검증 불가). 핵심 변경마다 Stab+Codex 2중 리뷰.

---

## 1. Crassorrid 현재 상태 (행동 스택 + 노브)

**상태머신(컨트롤러 5상태 / 2파라미터, Roar 제거됨):** `Idle / Approach / SmashWindup / SmashStrike / SmashRecovery`. 파라미터 `isApproaching`·`smash`. 전이 전부 CUT(dur0). 슬램 = SmashAttack_RM(50f) 3분할.

**행동 루프(최종):**
```
Idle ─(타깃 인지)→ Approach ──(조율자 허락 + d≤slamRange + 쿨다운)→ Smash
  Approach 분기:
    · 조율자 막힘(다중 브루트 각/박자 충돌) → 플랭킹 재배치(빈 방위로 Steer)
    · 허락(1대1=항상) → FacePlayerTurn(마주봄):
        d > slamRange       → 마주본 채 직선 접근
        d ≤ slamRange & 쿨OK → 슬램 커밋(d≈0도 강행 — 굳음 없음)
        d ≤ slamRange & 쿨중 → 정지+노려봄(코일 자세, 멀뚱 아님)
Smash: Windup(장판 스폰·차오름) → Strike(SmashHit: 장판 발동+임팩트 주스) → Recovery(느림) → Idle → 반복
```

**노브 (전부 드라이버 SerializeField/const — 런타임 AddComponent라 코드 default 먹음):**
| 노브 | 값 | 의미 |
|---|---|---|
| WindupSpeed (const) | **0.6** | 손드는 속도(보통, 1.0s 윈드업 = 텔레그래프) |
| StrikeSpeed (const) | **3.4** | 내려찍기(빠름, ~0.05s 임팩트) |
| RecoverySpeed (const) | **0.65** | ★찍은 뒤 천천히 손 올림(회수 1.0s) |
| telegraphRadius | **8.0** | ★범위. 텔레그래프 원+충격파(×2)+균열(×1.6)+먼지(×0.18) 전부 스케일 |
| slamRange | **4.0** | 이 안이면 슬램(d≈0 포함) |
| restBeforeApproach | **1.2** | 슬램 후 큰 딜레이(쿨다운) |
| angleSpread / staggerMin | **90° / 0.2** | 다중 브루트 각 분산·스태거(★staggerMin=0 금지=race 재개) |

> const(WindupSpeed/StrikeSpeed/RecoverySpeed) 변경 시 **`ZombieCrush/Crassorrid Lab/1. Setup Data` 재실행**으로 컨트롤러 state.speed 반영(진실원=빌드스크립트). 나머지는 런타임.

---

## 2. 거구 AI 리서치 + 조율 (레퍼런스 추적)

웹 리서치(에이전트 2기) → 권위 = [[2026-06-14-large-enemy-ai-research]]. 핵심 출처: Reynolds 스티어링(mass/max_force), RoR2/DRG(장판=공간점유 압박), L4D(거구=페이싱 *바꾸는 사건*), DOOM 2016(어택 토큰 + *빼앗기*로 "멍청하게 서있기" 방지), Aztez("같은 각에서 둘 안 옴"), For Honor(무제한 동시=갱 불공정).

**조율 결정(유저 확정): 토큰을 "수 게이트"→"각·박자 분산"으로 의미 변경** → `BruteSlamCoordinator.cs`(정적, ActiveSlamAzimuths + lastCommit). 동시 슬램 허용 + 각 분산(90°) + 스태거(0.2s) + 대기=플랭킹. 1대1은 피어0이라 즉시 슬램.

---

## 3. 임팩트 주스 + 카메라 쉐이크 (위협 메이커)

SmashHit(임팩트 frame20) 훅에:
- **임팩트 VFX**: `SmashShock.shader`(가산 HDR 충격파 링) + 먼지 ParticleSystem + 그을림 쿼드. `SmashImpactFX`/`SmashImpactPool`(풀). 색=레드오렌지(§5). telegraphRadius로 스케일.
- **카메라 쉐이크**: ★`LabCameraShake`(오프셋 합성) — Feel MMCameraShaker는 추종 카메라가 position 덮어써 *무효였음*(Stab H-1/H-2), `LabSimpleCamera`가 추종값 위에 XZ 오프셋 합성하게 고침. **메커니즘이 Feel단위→직접 미터로 바뀌어 amplitude 재튜닝 필요**.
- **히트스탑**: 프로젝트 네이티브 `HitStop.Do()`(단일 시간 소유자, timeScale 복원 안전). ~0.05s. `SmashFeel` 래퍼.

---

## 4. 미커밋 파일 + 커밋 계획 (다음 세션)

⚠️ **워킹트리에 병렬 세션 미커밋분 다수**(PlayerController/WeaponLoadout/CameraPresetLab/MeleeSfx/PlayerCombat/CombatFeel 등 — 내 작업 아님). **반드시 pathspec 커밋**(`git commit -- <paths>`), `git add -A` 금지.

**Crassorrid 커밋 대상:**
```
Assets/_Project/Scripts/CrassorridBrawler.cs(+.meta)
Assets/_Project/Scripts/CrassorridLabSpawner.cs(+.meta)
Assets/_Project/Scripts/Editor/CrassorridLabSetup.cs(+.meta)
Assets/_Project/Scripts/Editor/CrassorridLabCapture.cs(+.meta)
Assets/_Project/Scripts/BruteSlamCoordinator.cs(+.meta)
Assets/_Project/Scripts/SmashImpactFX.cs(+.meta) · SmashImpactPool.cs(+.meta) · SmashFeel.cs(+.meta)
Assets/_Project/Scripts/LabCameraShake.cs(+.meta)
Assets/_Project/Scripts/LabSimpleCamera.cs   ← (M, 쉐이크 합성)
Assets/_Project/Scripts/TelegraphPad.cs(+.meta) · TelegraphPool.cs(+.meta)   ← 첫 게임 활성화(장판 토대)
Assets/_Project/Setting/SmashShock.shader(+.meta)
Assets/_Project/Animations/CrassorridBrawler.controller(+.meta) · CrassorridRM/(+.meta)
Assets/_Project/Scenes/Greybox_CrassorridLab.unity(+.meta)
ProjectSettings/GraphicsSettings.asset   ← (M, SmashShock Always Included)
docs/02_logs/2026-06-14-large-enemy-ai-research.md
docs/01_handoffs/2026-06-14-crassorrid-brute-handoff.md
docs/03_reference/assets/crassorrid_lab/ · smash_impact/
```
**Dimax 파킹분**(별도 커밋 권장): `DimaxillosaurusBrawler.cs`·`DimaxillosaurusLabSpawner.cs`·`Editor/Dimaxillosaurus*`·`DimaxillosaurusBrawler.controller`·`DimaxRM/`·`Greybox_DimaxillosaurusLab.unity`·`docs/01_handoffs/2026-06-14-dimaxillosaurus-clawwall-handoff.md`·`docs/03_reference/assets/dimaxillosaurus_lab/`.

---

## 5. ▶ 플레이 판정 게이트 (다음 세션 유저)

정지캡처로 못 봄 — ▶ `Greybox_CrassorridLab`(다중은 스포너 enemyCount↑):
1. **위협/무게** — 쾅(쉐이크+히트스탑+VFX)이 거구답게 묵직·위협적인가. 쉐이크 강도(메커니즘 바뀜 재튜닝)·히트스탑 거슬림.
2. **dash-or-die** — 걸어선 못 빠지고 대시로만 빠지나(불가판정 아니게).
3. **근접 행동** — 포효 사라졌나 · 딱 붙어도 안 굳고 계속 찍나 · 옆에 붙으면 돌아서 찍나 · 연타 페이스(slamRange 4.0·rest 1.2·Recovery 0.65) 적당한가.
4. **다중 브루트** — 동시 슬램이 겹치되 읽히나 · 대기 브루트 플랭킹(맴돌기 아님)인가.

---

## 6. 다음 레이어 (미착수 · 권고)

- **★이동 = Arrive 스티어링**(높은 mass·낮은 max_force·회전캡) — 현재 비라인 추격을 *호를 그리며 미끄러지는 무게*로. "거구다움"의 코드적 본진([[2026-06-14-large-enemy-ai-research]] §D, 미착수).
- **다중 브루트 근접 시 플랭킹 vs 슬램 우선순위**(Codex RISK) — 막힌 근접 브루트가 플랭킹으로 새는 게 어색하면 1대1 우선 슬램으로 조정(플레이 판정 후).
- **임팩트 VFX/쉐이크 음색·색·강도** — 유저 튜닝 대기.
- **근접 종 재사용** — SmashImpactPool 패턴을 Caniathrox(착지)·Dimax(클로 컨택)에 얹어 위협 보강.
- **Dimax 파킹** — "압박 vs 공정" 한 점 미결([[2026-06-14-dimaxillosaurus-clawwall-handoff]]).

---

## 7. ★ 함정 기록 (재발 금지 — 오늘 발굴)

1. **음수 animator speed 백오프** = 매프레임 `speed=1f` 리셋 + 배타 if/else + 커밋 직전 명시 리셋 + CUT=다음프레임 *4중 방어*로 슬램 상태 누수 차단(이번엔 폐기했으나 패턴 기록).
2. **★importer 이벤트 time = 정규화(0~1)**(`ModelImporterClipAnimation.events[].time`, importer가 길이로 곱함). **Codex가 이걸 "초 단위"로 3번 연속 오진** — 빌드스크립트에 명시돼 있으니 오진 주의.
3. **추종 카메라 + Feel 쉐이크 충돌** — 추종이 매프레임 transform.position 덮어써 MMCameraShaker(localPosition) 무효 + MMWiggle.PositionActive 기본 false. → 쉐이크를 *오프셋 합성*으로 분리(LabCameraShake).
4. **★ProjectSettings(GraphicsSettings) SerializedObject 수정은 `EditorUtility.SetDirty` 없이 `SaveAssets`만으론 디스크 미반영.** SmashShock Always Included가 1차 등록 안 박혔던 원인 → SetDirty 추가로 해결(확인필).
5. **히트스탑 = 단일 시간 소유자**(프로젝트 `HitStop.Do`로 위임, 자작 MMFreezeFrame 금지 — timeScale 복원 경합).
6. **정적 조율자 stale 방위 누수**(죽은 브루트 방위가 산 브루트 영구차단) = SRecovery/OnDisable/엣지가드 + ★null 조기리턴에도 UnregisterSlam(H-1, 양 리뷰 수렴).
7. **MCP 플레이모드 강제 paused** — 모션/시간/리듬 자동검증 불가, 유저 플레이 판정 필수(정지캡처는 구조/수치/색까지만).
