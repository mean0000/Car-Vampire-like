# 핸드오프 — 공격 루트모션·발소리·카타나 장착·무기 트레일·슬래시 VFX (2026-06-19)

**세션 성격:** 어제 플레이어 스택 재작업 이어서. 카타나 수직슬라이스의 **이동/공격 정합 + 사운드 + VFX**를 한 번에 끌어올림. 데모 손맛 본진.
**권위/연동:** `docs/00_authority/2026-06-16-vampire-survivors-pivot-spec.md` · `docs/03_reference/animation-values-reference.md`(애니 값) · 메모리 `project_2026_06_19_attack_rootmotion_facing_lock` · `project_2026_06_19_vefects_urp_bundles`
**⚠️ 전부 미커밋.**

---

## 0. 한 줄
이동 속도를 클립 루트모션 의도값에 맞추고(4.31), 공격을 루트모션 런지로 바꾸고, facing을 단별 잠금, "움찔" 제거, 발소리를 발 디딤 동기, 카타나 메시를 손에 쥐어주고 무기 트레일 + **무기 방향 정합 슬래시 VFX(데이터 주도)**까지. Vefects 마젠타/GrabPass는 **진짜 URP 번들 임포트**로 해결.

---

## 1. 완성된 시스템

### A. 이동 — moveSpeed 4.31 (발 미끄러짐 해결)
- In_Place 로코모션이라 `PlayerMotor.moveSpeed`를 클립 루트모션 의도속도에 **수동 매칭**해야 함. `AnimationClip.averageSpeed` 측정: 8way 방향런 = **4.31 m/s**. 적용·저장.

### B. 공격 = 루트모션 런지 (S1_Combo01_01/02/03)
- 공격 클립을 **Root_Motion 폴더의 S1_Combo01_01/02/03**으로 교체(3타 ~1.34m 런지). 
- 아키텍처: Animator는 비주얼 자식, PlayerMotor는 루트 부모 → 자동 루트모션(자식만 이동) 안 씀. `PlayerAnimatorDriver.OnAnimatorMove`에서 **공격 중에만** `deltaPosition`을 `PlayerMotor.ApplyRootStep`(기존 벽가드+지면 재사용)에 태움. **위치 소유 단일(Motor).** 대시 종료 프레임 이중쓰기 가드(`_dashAppliedThisFrame`).

### C. facing = 콤보 단 시작 시 잠금 (유저 확정)
- 각 단 시작에 그 순간 조준으로 **몸/런지/히트박스(`_aimDir`) 통일**, 공격 중 마우스 돌려도 안 꺾임. 단 사이 재조준. 캡처=`PlayerAnimatorDriver.SetCombo`(몸)+`KatanaWeapon.BeginCombo/Advance`(타격).

### D. "끝날 때 움찔" 제거
- 원인=Combo→Locomotion 전환이 **CUT(블렌드0)** + OnComboEnd가 n0.92 조기컷 → 회수 포즈가 idle로 순간 팝(제자리에서도, 마우스 무관). 위치 스파이크·회전 스냅 아님(둘 다 실측 배제).
- 해결=Combo1/2/3 → Locomotion 전환에 **블렌드 0.15s**(체이닝 CUT은 유지).

### E. 발소리 — 발 디딤 AnimationEvent 동기
- 처음 거리기반→유저 "약간 늦음"(위상 어긋남)→**AnimationEvent**로 전환. 8방향 클립 발 본 minY 실측해 L/R 발디딤에 `OnFootstep` 심음(Animation 에이전트)→`PlayerAnimatorDriver.Footstep` 릴레이→`PlayerFootsteps` **디바운스 0.12s**(블렌드 동시발화 병합).
- 표면=데이터 주도 `SurfaceFootstepSet`(SO)+`SurfaceTag`, 발밑 레이캐스트(groundLayer). 현재 씬 바닥 미태깅→기본 흙(`Surface_DirtyGround`). 자산=`Footsteps - Essentials`(12표면 풀).
- 변주=직전제외 랜덤+피치0.92~1.08+볼륨지터. 2D 재생(리스너=카메라). **runSpeedThreshold 3.5**(평속=Run발소리). volume **0.6**. `triggerMode` 토글(Distance 폴백 보존).

### F. 공격 스윙 사운드 (임시 A/B)
- `PlayerAttackSfx` — `AttackHit` 구독→스윙음 1발(2D). 비교 중. ⚠️**디스크=Slash_Generic, 라이브 마지막=Slash_Classic 미저장**(음색 미확정). volume 0.6.

### G. 카타나 메시 장착 (빈손 해결)
- Frank `Sword_Mesh` 추출→정적 `Assets/_Project/Meshes/Frank_Katana_Static.asset`, **오른손 본(Humanoid RightHand=`Base HumanRPalm`)에 그립 오프셋 0으로 부착**(캡처 검증). 재질 Sword/Blade. **`BladeTip` 마커**(칼끝, 트레일/정합 기준).

### H. 무기 트레일 (스윙 100% 추종)
- `BladeTip`에 TrailRenderer + `WeaponTrail.mat`(URP 파티클 언릿, 강제 가산 화이트). `WeaponTrailController`가 `IsBusy` 동안만 emit(time0.25·width0.15→0). **현재 OFF**(슬래시 비교 위해 비활성).

### I. 슬래시 VFX = 무기 방향 정합 + 데이터 주도 ★
- ★핵심 통찰: "스윙에 맞춘다"=프레임 추종 아님. **그 순간 무기 방향으로 오리엔트해 띄우고 페이드.** 단별 스윙 평면이 달라(1타 하향·2타·3타 상향) **콤보 단별 각도** 필요.
- **데이터 주도 구조**:
  - `WeaponSlashSet`(SO) = 무기-스타일 1개. `steps[]` 단별 `{슬래시 프리팹·eulerOffset·posOffset·scale·lifetime}`.
  - `PlayerAttackVfx` = 활성 세트 읽어 `weaponAnchor`(Katana_Mesh) 방향으로 스폰. `SetSlashSet()`/`SetWeaponAnchor()`로 무기 스왑 교체.
  - `Assets/_Project/VFX/Katana_Cham_SlashSet.asset`(VFX_Slash_Generic, 단별 euler 120).
- **무기 추가 = SO 추가**(Create→ZombieCrush→Weapon Slash Set), 코드/씬 무수정. 카타나(발도/참격)·대검(미정1/2)… 확장 대비.

---

## 2. ★Vefects 마젠타/GrabPass 해결 (유저 진단)
- 임포트된 `Vefects/...`("URP" 폴더 포함)는 **빌트인 BIRP**였다 → 마젠타 + 화려한 슬래시의 왜곡이 **GrabPass(BIRP 전용)** 라 URP서 에러 폭탄.
- ★진짜 URP는 **미임포트 `.unitypackage` 번들**: `Stylized_VFX_Bundle_URP_...`(226M)·`Flipbook_VFX_Bundle_URP_...`(316M). `ImportPackage`로 임포트→BIRP를 URP 네이티브로 덮어씀. 검증: `_Distortion_01_URP_New`(GrabPass→`_CameraOpaqueTexture`), BIRP0·GrabPass0·에러0.
- **교훈**: GrabPass=BIRP 전용(guid 교체 무용). VFX팩은 파이프라인별 .unitypackage 따로 줌 → 재질 셰이더가 실제 `_URP`인지 확인. 상세=메모리 `project_2026_06_19_vefects_urp_bundles`.

---

## 3. 파일

**신규 스크립트** (`Assets/_Project/Scripts/`)
- `Player/` 수정: PlayerMotor(`ApplyRootStep`)·PlayerAnimatorDriver(facing잠금·`OnFootstep`/`Footstep`·`OnAnimatorMove`·`SetAttacking`)·PlayerBrain(footsteps Tick·SetAttacking)·KatanaWeapon(`_aimDir` 단시작 잠금)
- `Audio/` 신규: PlayerFootsteps·SurfaceFootstepSet·SurfaceTag·PlayerAttackSfx·**PlayerAttackVfx**·**WeaponSlashSet**·WeaponTrailController
  - ⚠️ VFX 스크립트(PlayerAttackVfx/WeaponSlashSet/WeaponTrailController)가 `Audio/` 폴더에 있음 → 추후 `Scripts/VFX/`로 정리 권장(임시물에서 출발).

**에셋**: `Animation/KatanaMelee.controller`(루트모션 공격·발디딤 이벤트·Combo→Loco 블렌드) · `_Project/Meshes/Frank_Katana_Static.asset` · `_Project/Audio/Surface_DirtyGround.asset` · `_Project/Materials/WeaponTrail.mat` · `_Project/VFX/Katana_Cham_SlashSet.asset` · 8개 `8Way_S2_Run_*.FBX.meta`(OnFootstep) · `_PlayerStackTest.unity`(Player에 카타나메시·BladeTip·트레일·발소리·공격SFX/VFX) · Vefects URP 번들 임포트.

---

## 4. 노브 (현재값)
| 시스템 | 노브 | 값 |
|---|---|---|
| 이동 | moveSpeed | 4.31 |
| 공격전환 | Combo→Loco 블렌드 | 0.15s |
| 발소리 | volume / minStepInterval / runSpeedThreshold | 0.6 / 0.12 / 3.5 |
| 발소리 | walkStride / runStride / firstStepPrime | 1.6 / 2.0 / 0.9 |
| 트레일 | time / width / 색 | 0.25 / 0.15→0 / 가산화이트 (현재 OFF) |
| 슬래시 | SO Katana_참격 steps[].eulerOffset | 1·3타=120 / **2타 미튜닝(땅에 박힘)** |

---

## 5. 게이트 (통과)
- 코드: Stab 다회(루트모션 이중소유·facing잠금·발소리 폭발/구독대칭·이벤트모드) — 수렴 필수수정 반영, 마감 Low. Codex 크로스(루트모션) 1회.
- 비누설 잔여(낮음): 발소리 M-1(OnComboEnd 0.1s창=클립≤0.1s 소프트락, 현재 1.0s+ 안전) · 좌측스트레이프 단독 발 위상 0.375 어긋남(엣지).

---

## 6. 미결 / 다음
1. **2타 슬래시 각도 튜닝** — `Katana_Cham_SlashSet` → Steps Element 1 → Euler Offset(★SO라 Play 중 편집해도 안 리셋). 1/3타(120)는 유지.
2. **공격 스윙 사운드 음색 확정** — Classic vs Generic vs 기타. 정하면 영구 저장.
3. **콤보 단별 슬래시 변형** — 단마다 다른 슬래시 프리팹(SO step.slashPrefab) 가능.
4. **임팩트 계층** — 적 타격 시 히트 VFX/사운드. ⚠️현재 씬 적 없음(IDamageable 미연결) → 적 시스템 붙으면.
5. **트레일 ↔ 슬래시 레이어 결정** — 둘 다 쓸지(트레일=streak·슬래시=flair), 슬래시만 쓸지. 트레일 현재 OFF.
6. **무기/스타일 확장** — 카타나 발도, 대검 등 SO 추가 + 스왑 배선(SetSlashSet/SetWeaponAnchor).
7. **보유 미임포트**: `GabrielAguiar VFXGraph_MegaPackVol4 URP`(VFX Graph 고퀄 슬래시 후보).
8. **선택**: 왼손 IK(양손 그립 정밀도)·VFX 스크립트 폴더 정리·발소리 표면 태그(현재 전부 흙).
9. **캐릭터 결정 보류**: Vexa(리타겟 갭 ~95%) vs Frank 캐릭터(네이티브 100%, placeholder 룩). 정식 주인공 때 재정합 — Humanoid는 무기-본 못 가져옴(트레일/슬래시는 BladeTip 기준이라 캐릭터 무관).
10. **커밋** — 전부 미커밋. 손맛 확정 후 묶음.

---

## 7. ★함정 (다시 안 밟게)
- **Humanoid 리타겟은 무기 본을 버린다** → 카타나 강체부착(Frank처럼 칼-본 발도는 Generic/Frank캐릭만). 단 트레일/슬래시는 BladeTip 기준이라 무관.
- **GrabPass=BIRP 전용** → URP 마젠타/에러면 빌트인 깔린 것, URP `.unitypackage` 임포트.
- **거리기반 발소리=위상 어긋남** → AnimationEvent 발디딤(블렌드는 디바운스).
- **In_Place 로코모션=moveSpeed를 클립 의도속도에 매칭** 안 하면 발 미끄러짐.
- **Combo→Locomotion CUT=포즈 팝** → 블렌드.
- **SO 편집은 Play 중에도 영속**(씬 컴포넌트와 반대) — 튜닝 편함, 단 의도치 않은 변경 주의.
- unity-mcp: Play 모드면 씬/SO 저장·임포트 막힘 · artist/Animation 에이전트가 캡처용 딴 씬 열어둘 수 있음(작업 전 _PlayerStackTest 재오픈).
