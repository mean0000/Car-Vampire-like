# 핸드오프 — 간지 방향 + Skill01 차징-팬텀 빌드 (2026-06-25)

> 내일 이어서 작업용. 오늘 = 코어루프/간지 방향 확정 + SO 정리 + Skill01 차징-팬텀 스킬 **비트1·2a** 빌드(플레이 검증 OK).

## 1. 코어 방향 (오늘 확정 — LOCKED)
- **최상위 가치 = 간지(style/cool).** 모든 게 간지로 복무(액션·VFX·분위기·무기). 반응 타깃 = "와 ㅅㅂ 간지오지네." **결정 규칙: 충돌 시 더 간지한 쪽 / 간지 아니면 컷.**
- **화려함(액션 레이어) + 무게감(분위기 레이어) 둘 다** — 트레이드오프 아니라 다른 레이어. 화려=순수 액션(★루트/돈 주스 ❌, 경제는 메타). 무게=압박 분위기(Devil Daggers, "두려움은 숫자가 아니라 연출"). 간지엔 *플레이 간지*(내가 해서 간지=크레딧)도 포함.
- **맵이 공간 분배:** 떼-zone(화려) / 무거운 분위기+엘리트-zone(무게). block-grid 맵과 정합.
- 매체와 안 싸움: 탑다운 VFX 스펙터클로(유저 TA 강점). 프로토타입식 *육체* 호쾌는 포기(탑다운+솔로 불가).
- 상세 = 자동메모리 `project_2026_06_25_spectacle_direction`.

## 2. SO 정리 (완료)
- 레거시 `WeaponSlashSet`(클래스 + `Katana_Cham_SlashSet.asset`) 제거 — 고아·`ComboAttackSet`에 흡수됨.
- 라이브 = **ComboAttackSet**(콤보, 판정+슬래시 단일진실) + **SkillSet**(스킬, hit/timing/vfx/sfx).
- **매뉴얼: `docs/03_reference/2026-06-25-attack-skill-so-system.md`** (스킬 추가법·무기불가지론 규약 포함).

## 3. Skill01 = 차징 팬텀 스킬 (B안: 플레이어 안 침, 팬텀만 전방 슉슉)
- **비트1 — 차징 상태머신** (`Scripts/Player/KatanaWeapon.cs`): RMB 홀드→`_charging`+`_chargeTime` 누적(`chargeMax` 0.83s≈50f), `!secondaryHeld`→release(고착 방지), 차징 중 콤보 차단, 대시 캔슬, `chargeSkill` 토글(끄면 레거시 즉발). 공개 접근자 `IsCharging`/`ChargeTime01`/`ChargeAimDir`. **★Stab+Codex 게이트 통과** — 고착 2(같은프레임 탭·포커스손실) + 이중플래그 1(dashAttack) 수정.
- **비트2a — 팬텀 방출** (`Scripts/Player/ChargePhantomEmitter.cs`, 신규): `SlashLab_Closeup`의 `Player/Visual`에 부착(weapon=Katana). AfterimageGhost 셰이더 + BakeMesh/풀 골격 재사용 + **전방(ChargeAimDir) 드리프트 + 산란 + 페이드**(대시잔상=제자리와 대비). **유저 플레이 검증 OK("잘 나가고 있어").** 현재 *대기 포즈* 베이크.
- **다이얼 씬: `Assets/_Project/Scenes/Labs/SlashLab_Closeup.unity`** — 근접 시네마틱 카메라(pitch30/dist6/yaw30), **몬스터 비활성**(CombatSliceSpawner·TelegraphPool off, "플레이 액션 먼저"). 플레이→RMB 홀드로 테스트.

## 4. 내일 (여기서 이어서) — TODO
- [ ] **2b — 공격 포즈:** `Frank_RPG_Katana_S1_Attack01/02/03`(`Frank_Slash_Pack/.../Frank_SlashPack_Katana/FBX_Animation/Root_Motion`) → **에디터 프리베이크 3 포즈 메시**(AnimationMode로 Humanoid 정확 샘플 → BakeMesh → Mesh.asset 저장) → `ChargePhantomEmitter`가 현재포즈 대신 3장 순환. 클립당 대표 프레임(스윙 정점) 1개 선택(어색하면 프레임만 조정).
- [ ] **2c — 슬래시 VFX:** 팬텀마다 슬래시 VFX 동반.
- [ ] **비트3 — 릴리스 페이로드:** `ReleaseCharge`에 BeginSkill 연결 + ★최소차징 임계값(Stab M-2: 1프레임 탭→풀스킬 방지) + 쿨다운.
- [ ] **에미터 코드 Stab 게이트** (룩 확정 후 — 지금 돌리면 비주얼 튜닝으로 재churn).
- [ ] 준비되면 **몬스터 되살리기** (SlashLab의 CombatSliceSpawner·TelegraphPool SetActive(true)).

## 5. 핵심 파일
- `Assets/_Project/Scripts/Player/KatanaWeapon.cs` — 차징 상태머신
- `Assets/_Project/Scripts/Player/ChargePhantomEmitter.cs` — 팬텀 방출(신규)
- `Assets/_Project/Scenes/Labs/SlashLab_Closeup.unity` — 다이얼 랩
- `Assets/_Project/Scripts/PlayerAfterimage.cs` — 참조한 대시 잔상(재사용 패턴 원본)
- `docs/03_reference/2026-06-25-attack-skill-so-system.md` — SO 매뉴얼
- 자동메모리: `project_2026_06_25_spectacle_direction.md` — 방향 + 구현 진행
