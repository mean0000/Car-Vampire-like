# 핸드오프 — 플레이어 스택 백지 재작업 + 카타나 콤보 (2026-06-18)

**세션 성격:** 데모 본착수. 플레이어 이동/공격 컨트롤러 + 무기별 애니 파이프라인을 **완전 백지**로 재작성. 카타나 평타 콤보 + 액션 상태 우선순위 설계.
**권위:** `docs/00_authority/2026-06-16-vampire-survivors-pivot-spec.md` (VS 4클래스, 카타나 첫 슬라이스)
**연동 메모리:** `project_2026_06_18_player_stack_rebuild`

## 0. 한 줄
완전 백지 플레이어 스택(이동/조준/공격/애니) 9파일 + Vexa(Humanoid 변환) + Frank 카타나 애니 + KatanaMelee 컨트롤러로 첫 수직 통합. 좌클릭 콤보 3단(Attack01/02/03) + **공격 중 이동 잠금**.

## 1. 완성된 것

### 코드 스택 (`Assets/_Project/Scripts/Player/`, 9파일 신규)
- **PlayerInputState** — 입력 격리(레거시 Input, New Input System 전환 대비)
- **PlayerBrain** — 단일 orchestrator. 순서 **Aim → Weapon → Motor → Animator** (Weapon 먼저 = 공격 상태 `IsBusy` 확정 → Motor 이동 잠금 판정)
- **PlayerMotor** — 이동(가속/감속 무게감)·대시(스택)·벽가드(SphereCast)·지면 추종. 위치만, 회전X. **`locked`(공격 중) = 즉시 정지**
- **PlayerAim** — 마우스 지면 투영 조준
- **PlayerAnimatorDriver** — facing≠movement 8way + AnimationEvent 릴레이(`OnAttackHit`/`OnComboWindow`/`OnComboEnd`) + `SetCombo(int)`. **이동 방향 8방향 스냅**(대각선 블렌드 애매함 제거)
- **WeaponBehaviour**(abstract) — `IsBusy`(이동 잠금 판정)
- **KatanaWeapon** — 콤보 3단(각 좌클릭=각 단), 입력 버퍼, 캔슬창. `IsBusy=_step>=1`
- **IDamageable** — 적 디커플
- **PlayerCameraFollow** — 45도/15m 추적

### 애니 (`Assets/_Project/Animation/KatanaMelee.controller`)
- Locomotion: idle + 8방향 **S2_Run**(In_Place, loop ON). 이동=항상 달리기
- Combo1/2/3 = **Attack01/02/03**(하향→내려치기→상향). `ComboStep`(int) 각 클릭 전환, 전부 CUT(dur0)
- AnimationEvent: `OnAttackHit`(타격)/`OnComboWindow`(캔슬창)/`OnComboEnd`(끝) per 클립
- 프레임: Attack01 타격0.428/창0.500/끝1.150 · 02 0.433/0.517/1.227 · 03 0.533/0.617/1.227

### Vexa (임시 캐릭터)
- Generic→Humanoid 변환 영속화(디스크 animationType=3). Hand→Palm 재매핑 보정(트위스트 골격).

### 액션 파이프라인 (유저 디렉팅 — "공격 중 안 움직이고 끝나야 움직인다")
- 우선순위: **대시 진행 > 공격 > 이동**
- **공격 중 이동(WASD)+대시 잠금 = 즉시 정지**(제자리 공격 → 발 미끄러짐 해결). 공격 끝나야 이동.
- 프레임 데이터 = 공격 활성 구간 = 이동 잠금 구간.

## 2. 미결 / 다음

### 손맛 플레이 검증 대기 (유저 — 마지막 상태)
- 콤보 3단 타격/캔슬창 타이밍, 3종 베기 구별(하향→내려치기→상향)
- 이동 잠금 묵직함
- 입력 버퍼 0.5 + 캔슬창 0.5 빠른 연타

### ★영구 저장 필요 (런타임값 — 씬 미저장 상태)
- `PlayerMotor.moveSpeed` = **4.3** (씬값 5 / 런타임 4.3 — S2_Run 다리속도 매칭)
- `KatanaWeapon.inputBufferTime` = **0.5** (씬값 0.35 / 런타임 0.5)
- → 내일 **플레이 종료 상태**에서 `_PlayerStackTest` 씬 인스펙터로 조정+저장(런타임값은 플레이 끄면 리셋)

### 미결 결정
- **facing 8방향 스냅** 보류 — (a)조준도 8방향 / (b)몸만 8방향 결정 후 구현
- 디폴트 확인 대기: 공격 중 대시 잠금(완전 커밋) / 조준 허용
- 대시 self-cancel(공격 캔슬) — 헌법상 허용, 미구현
- 적 `IDamageable` 연결 (적 시스템 미정)
- 발도/참격파/카드 (카타나 카드 확정 후)

## 3. ★자산 주의 (커밋 제외 — 미추적 대용량)
- **`Assets/Frank_Slash_Pack` = 1.7GB, git 미추적.** 카타나 애니 핵심. 로컬 존재로 같은 머신은 작동, 클론 시 깨짐. LFS/별도 백업 필요.
- **`Assets/Vefects/.../Vexa` = git 미추적.** Humanoid 변환은 디스크 영속화됨.
- `KatanaMelee.controller` + `_PlayerStackTest.unity`는 위 GUID 참조 — 같은 머신 OK.
- ⚠️ Frank FBX AnimationEvent 함정: meta `time`은 정규화[0..1](임포터가 ×길이). `importer.clipAnimations` 재구성+SaveAndReimport는 길이 팽창 → meta events 블록 직접 편집만. (`agent-memory/Animation/project_frank_fbx_animevent_gotchas`)

## 4. 리뷰 게이트
- Stab 코드 리뷰 통과: 콤보 H-2(구독 재진입 가드)·M-1(OnComboEnd 경합 시간가드 0.1s)·M-2(버퍼 0.5) 반영. H-1(파괴 좀비리스너)=virtual Cleanup 디스패치로 안전(Stab 오진, 검증함).
- Codex 크로스 리뷰는 **Bash 권한 거부**로 못 돌림 → Stab + 직접 검토로 커버. (Bash 허용 시 Codex 재가동 가능)

## 5. 파라미터/이벤트 규약 (코드 ↔ 컨트롤러 고정)
- Animator 파라미터: `Speed`/`MoveX`/`MoveY`(float) · `ComboStep`(int) · `Dash`(bool)
- AnimationEvent 함수(PlayerAnimatorDriver 수신): `OnAttackHit(int)` · `OnComboWindow()` · `OnComboEnd()`
