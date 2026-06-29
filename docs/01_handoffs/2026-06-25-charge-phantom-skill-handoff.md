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

## 4. TODO (진행 갱신 2026-06-26)
- [x] **비트3 — 릴리스 페이로드** (2026-06-26): `ReleaseCharge`→`BeginSkill` 연결 + `minChargeToFire`(0.12s) 임계값 + 쿨다운. **게이트 통과**(Stab+Codex 병렬, Critical/High 0, 핵심3위험[자가치유레이스·가드우회·불발소프트락] 무결). 수렴 수정 1 = `minChargeToFire > chargeMax`면 영구 무음 불발 → 사용처 `Mathf.Min(minChargeToFire, chargeMax)` 클램프(Codex 클램프+Stab 경고 통합). ⚠️SkillSet VFX/SFX 비어 릴리스는 아직 *시각 빈손*(애니 Skill01 스윙+판정만) — 릴리스 클라이맥스 VFX는 별도 비트.
- [x] **2b — 공격(베는) 포즈 팬텀** (2026-06-26): S1_Attack01/02/03 스윙정점(프레임 0.5) → 프리베이크 메시 3장(`Assets/_Project/VFX/ChargePhantomPoses/Phantom_S1_Attack0N.asset`, Visual-로컬, 각 10368v). `ChargePhantomEmitter` 리팩터 = **라이브 대기베이크 제거 → `poseMeshes[3]` 순환** + 플레이어 위치/`ChargeAimDir`/Visual 스케일 배치. 씬(SlashLab_Closeup) 할당+저장, 컴파일·콘솔 클린. ⚠️**유저 플레이 판정 대기**(차징 중 베는 실루엣 앞으로 읽히나). 정적 프리뷰 캡처로 포즈=동적 베기 자세 확인(대기 아님). 프레임 어색하면 베이크 스크립트 `times01` 조정 재실행(에셋 GUID 보존=CopySerialized).
- [ ] **2c — 슬래시 VFX:** 팬텀마다 슬래시 VFX 동반(2b 판정 후).
- [ ] **에미터 코드 Stab 게이트** (룩 확정 후 — 지금 돌리면 비주얼 튜닝으로 재churn).
- [ ] **릴리스 클라이맥스 VFX** — SkillSet.vfx.prefab(현 빈칸, posOffset z:10 전방발사 세팅) 채우기.
- [ ] 준비되면 **몬스터 되살리기** (SlashLab의 CombatSliceSpawner·TelegraphPool SetActive(true)).

**미커밋.** 베이크 스크립트(AnimationMode→BakeMesh→Visual-로컬 결합→.asset)는 1회성 RunCommand(재실행로 프레임/포즈 튜닝). 함정 기록: RunCommand 래퍼 네임스페이스서 `Mesh`가 네임스페이스로 충돌 → `UnityEngine.Mesh` 완전수식 필수(별칭도 섀도잉에 짐).

## 4b. 2026-06-26 후속 — 플립북 + 플레이어 차징 애니 (★2b/2c 갱신, 최신)
유저 교정으로 2b 정적포즈 → **애니메이션** 전환 + 플레이어 차징 애니 추가:
- **팬텀 = 정적 3포즈 ❌ → 슬래시 동작 플립북** (`Phantom_S1A01_f00..09`, Attack01 t[0.2,0.85] 10프레임). `ChargePhantomEmitter` 재작성: 각 팬텀이 수명 동안 플립북 *재생* + 사거리(`weapon.SkillRange`=3.5m)까지 이즈아웃 전진 + **스냅/슬래시/스르륵** 프로파일("슉! 딱! 스르륵", 연기처럼 X). 노브: interval 0.28(~3개)·lifetime 0.6·slashFraction 0.8·scatter 0. (구 `Phantom_S1_Attack0N.asset` 3장 미사용 잔존 — 플레이모드라 DeleteAsset 막힘, 정리 대기.)
- **플레이어 차징 애니** (Animation 에이전트): `Skill01`(`Frank_RPG_Katana_S1_Skill01`, 144f@60fps, **프레임70≈norm0.486=차징/베기 경계**) → 3상태 `Skill01Charge`(0→0.486 윈드업)/`Skill01Hold`(speed0, 프레임70 동결, RMB홀드 동안)/`Skill01Strike`(0.486→ 베기). 트리거 `SkillCharge`/`SkillCancel`, 기존 `Skill01`=베기 트리거. 전이 전부 CUT. 컨트롤러 `Assets/_Project/Animations/KatanaMelee.controller`(★`Animations` 복수). 데미지=베기 OnAttackHit(프레임80)에서만.
  - 배선: `BeginCharge()`→`TriggerSkillCharge()`, `ReleaseCharge()` fire→`BeginSkill()`(=Skill01 베기 트리거)/미스파이어→`TriggerSkillCancel()`. 차징 상태머신 로직 불변.
- **게이트(Stab+Codex) on 차징애니 — 강수렴(둘 다 Cancel 탈출 지적):** H-1 `Cancel()`에 `if(_charging) TriggerSkillCancel()`(비대시 Cancel[사망·스턴·미래] 시 speed0 홀드 영구 고착 방지) · M-1 self-heal 조건에 `_charging` 추가+정리 · L-1 `BeginCharge` BeginAction 의도적 생략 주석 · +`TriggerSkillCharge`에 `ResetTrigger(SkillCancel)` 잔류가드. **전부 적용·컴파일0·콘솔0.**
- **★플레이 판정 대기(유저):** ①윈드업→홀드→베기 흐름 ②**회피-중-차징 탈출**(게이트 권고=런타임 확인) ③미스파이어(살짝 떼면 불발) ④**차징 중 루팅**(못 움직임=커밋, 카이팅 원하면 상체 레이어=큰 작업) ⑤윈드업 루트모션 드리프트.
- **남은 후속:** 팬텀 방출을 0→70 윈드업 창에 정밀 결속(현 RMB홀드≈근사 ~3) · **팬텀 데미지**("베는 잔상이 대미지", 몬스터 필요+게이트) · 릴리스 **쾅 VFX**(SkillSet.vfx 빈칸) · 구 단일포즈 에셋 정리 · 몬스터 되살리기.
- **변경 파일:** `KatanaWeapon.cs` · `PlayerAnimatorDriver.cs` · `KatanaMelee.controller` · `ChargePhantomEmitter.cs` (+ SlashLab_Closeup 씬 노브/할당). **전부 미커밋.**

## 4c. 2026-06-26 후속2 — 유저 튜닝 4건 + 소프트락 수정 (★최신)
- **팬텀 튜닝:** 전진거리 2.0→**0.5m** · **Attack01/02/03 랜덤 믹스**(에미터 `flipbooks[3]`, 팬텀마다 랜덤 선택, 각 10프레임 30메시 `Phantom_S1A0N_f00..09`) · interval 0.28→**0.45**(텀↑ 덜 나옴) · **엉덩이-XZ 센터링 재베이크**(몸에서 출발, Attack 런지 오프셋 제거) · 에미터 **출발점 잠금**(`_chargeOrigin`, 윈드업 시작 1회 = 모든 팬텀 동일 위치 출발/소멸).
- **차징 애니(에이전트):** 경계 frame70→**49**(전이 offset 0.340) · 홀드 동결→**46↔49 게이더 펄스**(Skill01Hold speed0.3 + Hold→Hold crossfade self-loop 0.10). 정직: 문자 48↔49 핑퐁은 forward-only 불가→정점49 도달 펄스가 등가.
- **★루트모션 게이트(필요 코드변경):** 게이더 루프가 움직여 드리프트(0.78m/3s) → `PlayerAnimatorDriver.OnAnimatorMove`서 `Skill01Charge/Hold`면 `ApplyRootStep` 스킵(제자리=윈드업 in-place, 베기 런지 2.306m 보존). 게이트 통과(Stab+Codex, 베기/타공격/deltaPosition 안전).
- **★게이트 H-1 (Stab 발견, Codex는 PASS시킴):** Hold 루프 crossfade(0.10s 창)서 릴리스 시 Skill01 트리거가 전이 미발화로 소멸→Hold 고착=간헐 **소프트락**. 수정 = **명시 source 전이 `Hold/Charge→Skill01Strike`(Skill01)·`→Locomotion`(SkillCancel)를 루프보다 앞 순서로 + 그 루프/settle 전이 intrSrc Source(1)+ordered**. (interruptionSource만 1로는 부족 — AnyState는 source 아니라서. 명시 source 전이가 빠진 조각.) 에이전트 검증 릴리스21/21·취소21/21(crossfade 중간 포함) 발화, 오케 구조 독립확인.
- **비차단 노트(Codex 항목4):** `OnAnimatorMove`가 GetNextAnimatorStateInfo 미참조 → Hold→Strike crossfade 첫 프레임 런지 미세 깎임 가능(sub-frame). 플레이서 "런지 짧다" 시 GetNextAnimatorStateInfo 체크 추가(보류).
- **★플레이 판정 대기:** 4건 + 펄스 가독성·릴리스 스냅(~0.1s 지연)·제자리 차징·런지 길이. **후속:** 팬텀 **데미지**(몬스터 필요+게이트) · 릴리스 **쾅 VFX**(SkillSet.vfx 빈칸) · 구 단일포즈 3장 정리 · 에미터 코드 정식 게이트(룩 락 후). **전부 미커밋.**

## 4d. 2026-06-27 — 슬래시 VFX(검에 따라·공격별) + 노브 정리 + ★현재 상태/내일 TODO (★최신 진입점)

### 추가/변경
- **개수 노브:** interval(텀) → **`phantomCount`(int, 기본 3)** + `windupDuration`(0.82, 윈드업 길이 추정). 방출 간격 = windupDuration/phantomCount, 윈드업 시작마다 산출(라이브). 직관적 "차징당 몇 장".
- **거리 노브:** `travelRange` 2.0→**0.5m** (스킬 사거리와 분리한 독립 값).
- **팬텀 슬래시 VFX (2c 완료):** 평타 콤보처럼 팬텀마다 슬래시 VFX 동반. `slashVfxPrefab`=`VFX_Slash_Earth`(콤보 guid 6c66384d…, Vefects Stylized/Slashes Piercing/Earth). **검 앵커(`weapon.WeaponAnchor`=Weapon_Sword) 위치·방향에 정합**(PlayerAttackVfx와 동일 수학 = "검에 따라"). StripEmbeddedAudio 재사용·강제재생·lifetime 후 destroy. 폴백=출발점+전방.
- **★공격별 슬래시 정합:** Attack01/02/03 스윙 평면이 달라 슬래시 euler를 공격마다 따로 줘야 함 → `SlashFlipbook`에 **per-clip `slashEulerOffset`·`slashPosOffset`·`slashScale`** 추가(콤보 단별 euler와 동형). 전역 slash euler/pos/scale 제거. 공유 잔존=slashVfxPrefab·slashLifetime·slashPlaybackSpeed·slashParentToWeapon. **현재 셋 다 euler/pos 0, scale 0(=1) — 유저가 공격별 튜닝할 차례(미조정).** ⚠️Awake 압축 시 `fb` 객체 유지(새 SlashFlipbook 생성 금지 — per-clip 값 보존).

### ★현재 상태 (차징 스킬 풀스택, 게이트 통과, 미커밋, 플레이 가능)
- 씬 `SlashLab_Closeup`(몬스터 off), `Player/Visual`(ChargePhantomEmitter + KatanaWeapon + PlayerAttackVfx).
- **흐름:** RMB 홀드 → 플레이어 Skill01 윈드업(0→49) 후 **46↔49 게이더 펄스 홀드**(제자리), 그동안 **베는 실루엣 팬텀**(Attack01/02/03 랜덤 플립북·몸에서·같은 위치·0.5m 전진·스냅/슬래시/스르륵·~3장) + 각 팬텀 **슬래시 VFX(검에 따라)** → RMB 놓으면 **Skill01 베기(49→) 쾅** + 판정(3.5m/80°/dmg20) + 쿨다운. 미스파이어(살짝 떼면)=불발.
- **노브 위치** = `Player/Visual` → **ChargePhantomEmitter**: phantomCount·travelRange·lifetime·slashFraction·fadeStart·scatter·windupDuration·ghostColor·startAlpha / 슬래시 공유: slashVfxPrefab·slashLifetime·slashPlaybackSpeed·slashParentToWeapon / **flipbooks[0=A01·1=A02·2=A03] 각각 slashEulerOffset·slashPosOffset·slashScale**.

### ★내일 이어서 (TODO 우선순위)
1. **공격별 슬래시 euler 튜닝** (유저 주도·미조정) — flipbooks[0/1/2] slashEulerOffset로 각 어택 슬래시 평면 맞추기. 값 부르면 오케가 에디트모드에서 박음.
2. **팬텀 데미지** — "베는 잔상이 대미지". 몬스터 되살려 테스트(SlashLab CombatSliceSpawner·TelegraphPool SetActive(true)). 에미터 `// DAMAGE HOOK` 위치에 전방 짧은 판정 1회/팬텀. **게이트(전투 동작) 필수**.
3. **릴리스 쾅 VFX** — SkillSet.vfx.prefab 빈칸(posOffset z:10 전방발사 세팅됨) 채우기.
4. **에미터 코드 정식 게이트**(Stab+Codex) — 룩 락 후(지금까진 룩-이터레이션 튜닝이라 유예).
5. 구 단일포즈 3장(`Phantom_S1_Attack0N.asset`) 정리 · **커밋**(전부 미커밋).

### 함정/교훈 (이 세션)
- ⚠️ **유저가 플레이 모드 자주 켬 → 코드/씬 변경 막힘**(컴파일 밀림·MarkSceneDirty/SaveScene "cannot be used during play mode"). 변경 전 `EditorApplication.isPlaying=false`로 정지(가드 후 재시도). 멀티스텝 변경 땐 유저에 "에디트 모드 유지" 요청.
- ⚠️ RunCommand 래퍼 네임스페이스서 `Mesh`=네임스페이스 충돌 → `UnityEngine.Mesh` 완전수식.
- ★Animator 인터럽트: crossfade self-loop서 트리거가 미발화 소멸(소프트락) → 명시 source 전이를 루프보다 앞 순서 + interruptionSource Source(AnyState 전이는 source로 안 잡힘).
- ★두 게이트 가치 입증: Codex가 PASS한 소프트락(H-1)을 Stab이 잡음. ★per-clip 구조 변경 시 Awake 압축이 객체 재생성하면 새 필드 유실 — 기존 객체 유지.

## 5. 핵심 파일
- `Assets/_Project/Scripts/Player/KatanaWeapon.cs` — 차징 상태머신
- `Assets/_Project/Scripts/Player/ChargePhantomEmitter.cs` — 팬텀 방출(신규)
- `Assets/_Project/Scenes/Labs/SlashLab_Closeup.unity` — 다이얼 랩
- `Assets/_Project/Scripts/PlayerAfterimage.cs` — 참조한 대시 잔상(재사용 패턴 원본)
- `docs/03_reference/2026-06-25-attack-skill-so-system.md` — SO 매뉴얼
- 자동메모리: `project_2026_06_25_spectacle_direction.md` — 방향 + 구현 진행
