# 애니메이션 값 레퍼런스 (Animation Values Reference)

> **목적:** 플레이어 무기별 애니메이션 값(루트모션 의도 속도·이동 속도·공격 프레임 데이터·Animator 파라미터·AnimationEvent)을 한 곳에 모아 **언제나 확인·수정**할 수 있게 하는 단일 출처. 무기마다 분류.
>
> **상태:** 살아있는 문서(continuously updated). 값이 바뀌면 여기부터 고친다. 현재 = **카타나만 작성**, 나머지 3클래스는 착수 시 추가.
>
> 연동: 핸드오프 `docs/01_handoffs/2026-06-18-player-stack-katana-combo-handoff.md` · 코드 `Assets/_Project/Scripts/Player/`

---

## 0. 측정법 (값을 어떻게 다시 구하나)

루트모션 클립의 **제작자 의도 속도**는 `AnimationClip.averageSpeed`로 정확히 읽는다. unity-mcp RunCommand로 측정:

```csharp
var objs = AssetDatabase.LoadAllAssetsAtPath(fbxPath);   // FBX의 클립은 서브에셋
AnimationClip clip = /* objs 중 AnimationClip, __preview 제외 */;
Vector3 avg = clip.averageSpeed;                          // m/s, 루트 평균 속도벡터
float horizontal = new Vector2(avg.x, avg.z).magnitude;  // 수평 이동속도 = 정답값
```

- **In_Place 클립**은 루트모션이 없어 `averageSpeed≈0` → 직접 못 잰다. 같은 모션의 `_Velocity`(루트모션) 형제 클립으로 잰다.
- Frank 폴더 규약: `In_Place/` ↔ `Root_Motion/` ↔ `Root_Motion_8Way/`(파일명 `@Velocity_...`).

---

## 1. 공통 규약 (코드 ↔ 컨트롤러, 전 무기 고정)

| 종류 | 값 |
|---|---|
| **Animator 파라미터** | `Speed`/`MoveX`/`MoveY`(float) · `ComboStep`(int) · `Dash`(bool) |
| **AnimationEvent 함수** (PlayerAnimatorDriver 수신) | `OnAttackHit(int)` · `OnComboWindow()` · `OnComboEnd()` |
| **루트모션** | `applyRootMotion = true`. `OnAnimatorMove`에서 **공격 중에만** `animator.deltaPosition`을 PlayerMotor에 수동 적용(deltaRotation은 미적용 — 몸 회전은 코드 조준이 소유). 로코모션은 In_Place라 delta≈0 → moveSpeed 주도 유지 (2026-06-19 루트모션 전환) |
| **이동 속도 정합** | 로코모션 = In_Place라 **moveSpeed를 클립 루트모션 의도속도에 수동 매칭**해야 발 미끄러짐 방지. 공격 = Root_Motion 클립의 전진을 deltaPosition으로 살림 |

---

## 2. 카타나 (Katana) — Stance 2

- **컨트롤러:** `Assets/_Project/Animation/KatanaMelee.controller`
- **애니 소스:** `Frank_Slash_Pack` (Humanoid). 임시 캐릭터 = Vexa(Humanoid 변환됨)
- **스탠스:** S2 (Stance 2)

### 2.1 이동(Locomotion)

- **사용 클립:** `8Way_S2_Run` 세트 (In_Place, loop, 8방향 + idle). 컨트롤러 = 2D Freeform Directional 블렌드.
- **블렌드 입력:** `MoveX`(우측 성분) / `MoveY`(전진 성분) — facing 프레임 투영(facing-relative strafe).

| 항목 | 값 | 출처 |
|---|---|---|
| **PlayerMotor.moveSpeed (적용값)** | **4.31 m/s** | ← 아래 의도속도에 매칭 (2026-06-19) |
| 의도 속도 — 8way 방향런 (`@Velocity_8Way_Run_F`) | **4.31 m/s** | 측정(averageSpeed) |
| 의도 속도 — 단일 전방런 (`Run01/02/Stance2_Velocity`) | 6.72 m/s | 측정. **8way 로코모션엔 부적합**(히어로 전력질주, 미사용) |

> ⚠️ **정밀 주의:** Frank가 `8Way_**S2**_Run`의 velocity(루트모션) 버전을 안 줬다. 적용한 **4.31은 base 8way(S1)** 의 측정값 = S2의 근사 프록시. 케이던스 미세차 가능 → **플레이로 ±α 미세조정**. (어제 눈대중 4.3이 이 측정 4.31과 사실상 일치하여 신뢰도 높음.)

### 2.2 공격 콤보 (좌클릭 3단) — ★루트모션 클립 (2026-06-19 교체)

- **클립:** `S1_Combo01_01` → `S1_Combo01_02` → `S1_Combo01_03` (전부 `Root_Motion/` 폴더). 각 좌클릭=각 단. 전환 전부 CUT(dur 0).
- **루트모션:** 클립에 박힌 전진을 `applyRootMotion=true` + `OnAnimatorMove`의 deltaPosition으로 살림. 순전진: _01≈0(제자리 발놀림)·_02≈0.05m·**_03≈1.34m(런지 마무리)**.
- **임포트(전 3클립 공통):** Loop Time=OFF(`loopTime:0`) · Bake Into Pose: **Rotation=ON**(`loopBlendOrientation:1`, 회전 드리프트 방지) · **Position Y=ON**(`loopBlendPositionY:1`, grounded) · **Position XZ=OFF**(`loopBlendPositionXZ:0`, 전진 보존).
- **전환 파라미터:** `ComboStep`(int) — Combo1`[==2]`Combo2`[==3]`Combo3(CUT, 스냅 체이닝), 각 `[==0]`Locomotion 복귀.
- **전환 블렌드(2026-06-19 움찔 수정):** Combo→Locomotion 3개 전환 = **블렌드 0.15s**(fixedDuration). CUT(dur0)이면 회수 포즈→idle/run 순간 팝("끝날 때 크게 움찔", 제자리에서도 발생)이라 블렌드로 이김. Combo→Combo는 CUT 유지. OnComboEnd가 n0.92 조기컷이라 블렌드가 회수 일부를 덮음(필요시 이벤트 n1.0으로 늦춤=Animation). 노브=0.15s(손맛 조정 가능).
- **facing 잠금(2026-06-19 유저 확정):** 각 콤보 **단 시작 시** 그 순간 조준으로 facing을 잠근다 → 몸/런지/히트박스(`_aimDir`) 방향 통일, 공격 중 마우스를 돌려도 안 꺾임(묵직한 커밋). **단 사이엔 재조준 가능**(Advance 시 재캡처). 잠금 캡처=`PlayerAnimatorDriver.SetCombo`(몸)+`KatanaWeapon.BeginCombo/Advance`(타격). 대안 "공격 중 방향 수정 허용"은 반려.
- **타격 측정:** Weapon_Blade 본 속도 정점(클립당 SampleAnimation per-frame, hips-local). 블레이드 속도 23.0→24.6→27.0 m/s(피니셔가 최고속).
- **OnAttackHit intParameter** = 콤보 단(1/2/3).

| 콤보 단 | 클립 | 길이/프레임 | 타격(OnAttackHit) | 캔슬창(OnComboWindow) | 끝(OnComboEnd) |
|---|---|---|---|---|---|
| Combo1 | S1_Combo01_01 | 1.000s / 60f | f22 = 0.367s (norm 0.367, int=1) | 0.484s (norm 0.484) | 0.920s (norm 0.920) |
| Combo2 | S1_Combo01_02 | 1.133s / 68f | f12 = 0.200s (norm 0.176, int=2) | 0.317s (norm 0.280) | 1.043s (norm 0.920) |
| Combo3 | S1_Combo01_03 | 1.050s / 63f | f13 = 0.217s (norm 0.206, int=3) | 0.334s (norm 0.318) | 0.966s (norm 0.920) |

> 타이밍 모양: Combo1 윈드업 느림(타격 37%) → Combo2/3 모멘텀 타고 빠른 후속타(타격 18~21%). 블레이드 속도 상승=피니셔 강조.
>
> AnimationEvent 함정: Frank FBX의 meta `time`은 정규화[0..1](임포터가 ×길이=절대초). lock 플래그(Bake Into Pose)는 meta 필드명이 `loopBlend*`이므로 **ModelImporter API로 설정**(`lockRootRotation/lockRootHeightY/lockRootPositionXZ` → `loopBlendOrientation/PositionY/PositionXZ`로 직렬화). `clipAnimations` 배열 **재구성 금지**(길이 팽창) — 기존 배열 in-place 변형만. (`agent-memory/Animation/project_frank_fbx_animevent_gotchas`)

### 2.3 공격 VFX — 무기 트레일 + 슬래시(데이터 주도) (2026-06-19)

- **카타나 메시 장착**: Frank `Sword_Mesh` → 정적 `_Project/Meshes/Frank_Katana_Static.asset`, 오른손 본(Humanoid RightHand)에 그립 오프셋 0. 자식 `BladeTip`(칼끝, 트레일·정합 기준).
- **무기 트레일**: `BladeTip`의 TrailRenderer + `_Project/Materials/WeaponTrail.mat`(URP 파티클 언릿 가산 화이트). `WeaponTrailController`가 `IsBusy` 동안만 emit. time 0.25 / width 0.15→0. **현재 OFF**(슬래시 비교 중).
- **슬래시(데이터 주도)**: `WeaponSlashSet`(SO, 무기-스타일 1개) → `PlayerAttackVfx`가 활성 세트를 읽어 **무기(Katana_Mesh) 방향 정합**으로 스폰. 단별 `{프리팹·eulerOffset·posOffset·scale·lifetime}`. 무기 추가=SO 추가(코드0). 현재 = `_Project/VFX/Katana_Cham_SlashSet.asset`(VFX_Slash_Generic, euler 1/3타=120, **2타 미튜닝**).
- ★"스윙 정합"=프레임 추종 아니라 *그 순간 무기 방향으로 오리엔트해 띄우고 페이드*. 단별 스윙 평면이 달라 단별 각도 필수.
- ⚠️ Vefects 슬래시는 **URP 번들 임포트** 필요(빌트인 BIRP면 마젠타/GrabPass). 상세=핸드오프 `2026-06-19-attack-vfx-footstep-katana-handoff` · 메모리 `project_2026_06_19_vefects_urp_bundles`.

### 2.4 미적용/대기

- 발도·참격파·스킬 클립 = 카타나 카드 확정 후 추가
- 상하체 분리(조준≠이동 facing) = 회전 어색함 해법, 미착수 (별도 결정 대기)

---

## 3. 나머지 무기 (미작성)

런당 1택 4클래스 중 카타나 외 3종은 착수 시 §2 양식으로 추가:

- [ ] **(근접 2)** — 클래스 미정
- [ ] **(원거리 1)** — 클래스 미정
- [ ] **(원거리 2)** — 클래스 미정

> 권위 = `docs/00_authority/2026-06-16-vampire-survivors-pivot-spec.md` (VS 4클래스)
