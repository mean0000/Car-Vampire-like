# 핸드오프 — RMB 스킬 시스템 (SkillSet SO) (2026-06-21)

> 이어서 작업할 때 먼저 §1 TL;DR + §4 다음 작업. 직전 핸드오프 [[2026-06-20-player-stack-audit-rail-handoff]](레일·감사)의 연속. 액션 추가 규약은 권위문서 [[2026-06-20-player-action-coordination-rail]] §4 필독.

---

## 1. TL;DR

데모 본착수 후 카타나에 **액션을 하나씩** 붙이는 중. 이번 세션:
- 반격 모션 교체(Skill02→**Attack02**), 회피 손맛(빠른 대시+채운 잔상+무적 중 몸 시안 발광) — **커밋됨**.
- ★**RMB 우클릭 스킬(Skill01)** 신규 — 레일 패턴(카운터와 동형), **SO 주도(`SkillSet`)** + VFX/사운드 + **기준 토글(Player/Weapon)**. **미커밋.**

**다음 1순위:** `Katana_Cham_Skill01Set` SO에 VFX 프리팹·사운드 클립 드롭 → RMB 플레이 확인 → 스킬 전체 커밋.

---

## 2. 현재 상태

### 커밋됨 (직전 핸드오프 이후)
| 커밋 | 내용 |
|---|---|
| `c59db639a` | 반격(Counter) 모션 Skill02→Attack02 |
| `007a7a00d` | 회피 손맛 — 빠른 대시(dashDuration 0.10) + 채운 잔상(알파블렌드 필) + 무적 중 몸 시안 발광(PlayerDashTint) |

### ★미커밋 (이번 스킬 작업 — 다음 세션 커밋 대상)
- **M** `KatanaWeapon.cs` — RMB 스킬 로직(`_skilling`/쿨다운/워치독/자가치유) + `SkillSet skillSet` 참조 + `weaponAnchor` + SpawnSkillVfx/PlaySkillSfx(basis 분기)
- **M** `PlayerAnimatorDriver.cs` — `TriggerSkill()` + Skill01Hash
- **M** `PlayerBrain.cs` — 대시 중 RMB(secondaryDown) 억제
- **M** `_PlayerStackTest.unity` — KatanaWeapon.skillSet 연결·weaponAnchor=Katana_Mesh
- **??** `SkillSet.cs` (신규) · `Scripts/Audio/`(PlayerAttackVfx/Sfx 등 — 원래 미추적) · `VFX/`(Katana_Cham_Skill01Set.asset 등 — 미추적)
- 컴파일 errors=0.

⚠️ **미커밋 + 일부 미추적**(Audio/·VFX/ 폴더가 git 미추적 — 기존 상태). 다음 세션 커밋 시 이들 포함 필요.

---

## 3. 스킬 시스템 구조 (어떻게 동작하나)

**입력:** RMB(secondaryDown) → `KatanaWeapon`이 `!IsBusy && cooldown 준비 && skillSet 할당` 시 `BeginSkill` → `Skill01` 애니(Any→Skill01 트리거, "Action" 태그). 카운터와 **완전 동형 레일**(busy=Animator 진실, 진행플래그 `_skilling` 자가치유, OnComboEnd 종료).

**데이터 = SO 주도 (`SkillSet`, ComboAttackSet 규약):**
- 에셋: `Assets/_Project/VFX/Katana_Cham_Skill01Set.asset` → KatanaWeapon.skillSet에 연결됨.
- 중첩 4그룹(Inspector 접이식): **hit**(판정) · **timing**(쿨다운·워치독) · **vfx**(슬래시/VFX) · **sfx**(사운드).
- **새 스킬 = SO 하나 더**(코드 무수정, OCP). [[feedback_skill_data_so_driven]]

**VFX/사운드 = 타격 순간(칼 벨 때, OnAttackHit @frame80):** `DoSkillHit`가 판정 + SpawnSkillVfx + PlaySkillSfx 동시.

**★기준 토글 `SkillSet.vfx.basis`** (불렛 vs 슬래시):
- **`Player`**(기본) — 플레이어 위치+조준 기준. `posOffset` z=앞·x=우·y=위(조준 프레임). **전방 발사/불렛**용. 기본 posOffset (0,1,1.5).
- **`Weapon`** — 무기(칼 `Katana_Mesh`) 앵커 기준. `posOffset`/`eulerOffset`이 칼 로컬. **슬래시(휘두름 따라)**용. 콤보 슬래시(PlayerAttackVfx)와 동일 수학. weaponAnchor=Katana_Mesh 연결됨.
- eulerOffset/posOffset/scale/playbackSpeed/lifetime은 양쪽 공용(해석만 basis 따라).

**사운드:** `SkillSet.sfx.clip`(2D, 거리감쇠 없음) — KatanaWeapon이 자체 AudioSource로 PlayOneShot.

**누수 수정(중요):** 스킬/반격은 OnAttackHit `intParameter=0`(콤보=1/2/3). `PlayerAttackVfx`·`PlayerAttackSfx`가 0에도 콤보 슬래시·스윙음을 잘못 내던 것 → **둘 다 `comboStep < 1 return`으로 차단.** 이제 스킬은 자기 VFX/사운드만.

---

## 4. ★ 다음 작업 (우선순위)

### 4.1 [P0] 스킬 VFX·사운드 넣고 확정
- [ ] `Assets/_Project/VFX/Katana_Cham_Skill01Set` 선택 → **`Vfx > Prefab`** 에 VFX, **`Sfx > Clip`** 에 사운드 드롭.
- [ ] **basis 선택**: Skill01이 전방발사면 `Player`(기본), 슬래시면 `Weapon`.
- [ ] RMB 플레이 → VFX가 칼 벨 때 의도 위치에 뜨나, 사운드 맞나. `posOffset`(z=앞/거리, y=높이)·`eulerOffset` 조절.
- [ ] 확정되면 **스킬 전체 커밋**(미추적 Audio/·VFX/ 포함).

### 4.2 [P1] 다음 스킬·잔무
- [ ] 다음 스킬 추가 시: `SkillSet` SO 하나 더 + 컨트롤러에 그 상태(Animation 에이전트, 권위문서 §4 절차). 현재는 RMB 1개 — 추가 키 바인딩 설계 필요(스킬이 늘면).
- [ ] 폐기 레거시 정리(원하면): `WeaponSlashSet.cs` + `Katana_Cham_SlashSet.asset`(참조 0) · 디버그 더미(HazardPad/HitTest/HitboxDebug).

### 4.3 [P2] 이전 핸드오프 잔무 (여전히 유효)
- [ ] **전투 흐름 런타임 검증** — 회피→콤보 즉발·패링→반격(Attack02)·1타리셋 부재(레일 P0, 아직 손맛 미검증).
- [ ] ComboAttackSet 폴백 노출·PlayerHealth 세이브 진입점·asmdef.

---

## 5. 알아야 할 것 (gotchas)

- **★스킬·무기 데이터는 SO로** (인라인 필드 금지). [[feedback_skill_data_so_driven]]. 새 스킬=SO 하나 더.
- **VFX 기준(basis)을 먼저 정하라** — 불렛=Player, 슬래시=Weapon. 안 맞으면 위치/회전이 엉뚱.
- **타이밍은 클립 AnimationEvent** — OnAttackHit(타격)·OnComboEnd(종료). 스킬 추가 시 그 클립에 둘 다 심어야(없으면 워치독까지 잠금). OnComboEnd는 전환 ExitTime보다 *앞*(작은 norm).
- **RunCommand는 플레이 모드 중 씬저장/에셋삭제/필드배선 불가** — "User interactions not supported"·"cannot be used during play mode"로 막힘. **편집 모드에서** 실행. (이번에 여러 번 겪음.)
- **SerializeField 필드 제거→재추가** 시 씬 참조가 도메인 리로드에서 끊길 수 있음 → 재배선 확인(이번 weaponAnchor는 유지됐으나 점검 필요).
- **git 미추적 폴더** `_Project/Scripts/Audio/`·`_Project/VFX/` — 커밋 시 `git add` 필요. 대형 에셋팩(Frank/Footsteps)은 미추적 유지(기존 패턴).
- **콘솔 `UnityEditor.Graphs.Edge.WakeUp` NRE** = 에디터 내부(Animator 그래프 창) 노이즈, 내 코드 무관 — 무시.
- 위치 단일소유=PlayerMotor·애니가 진실·busy=Animator 진실(레일) 캐넌 유지.

---

## 6. 검증 상태 (정적 ✅ / 런타임 ⏳)

| 항목 | 상태 |
|---|---|
| 컴파일 errors=0 | ✅ |
| 스킬 코드(레일 미러·입력·쿨다운·대시억제) | ✅ Stab+Codex 통과(H-2 수정 적용) |
| Skill01 컨트롤러 상태("Action"·Any진입·이벤트) | ✅ Stab 통과(Codex는 서브에이전트 Bash막혀 미실행, 미러라 수용) |
| basis 토글·SkillSet SO·앵커 배선 | ✅ 직렬화 검증 |
| **스킬 발동·VFX 정합·사운드·손맛** | ⏳ **VFX/사운드 미투입 + 유저 플레이 미검증 — P0** |

---

## 7. 핵심 파일·문서

- **권위 문서:** `docs/00_authority/2026-06-20-player-action-coordination-rail.md`(레일·§4 신규액션절차).
- **데이터 규약:** 메모리 `feedback_skill_data_so_driven`.
- **스킬 코드:** `KatanaWeapon.cs`(스킬 로직+VFX/사운드 스폰), `SkillSet.cs`(SO·중첩4그룹·basis), `PlayerAnimatorDriver.cs`(TriggerSkill), `PlayerBrain.cs`(RMB 대시억제).
- **누수 차단:** `Scripts/Audio/PlayerAttackVfx.cs`·`PlayerAttackSfx.cs`(comboStep<1 return).
- **SO 에셋:** `Assets/_Project/VFX/Katana_Cham_Skill01Set.asset`(→ KatanaWeapon.skillSet).
- **컨트롤러:** `KatanaMelee.controller`(Skill01 상태, AnyState `Dash>Counter>Skill01>Combo1`).
- **씬:** `_PlayerStackTest.unity`.
