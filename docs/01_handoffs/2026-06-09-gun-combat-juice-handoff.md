# 총기 전투 타격감/주스 핸드오프 (2026-06-09)

직전 [2026-06-08 impact-vfx 핸드오프](2026-06-08-impact-vfx-handoff.md)의 발사체/임팩트 위에, **총기 게임감 전반(머즐·사운드·피격 VFX·에너미 리액션·카메라/Feel 피드백·넉백/경직)**을 얹은 세션. 브랜치 `main`(graphics 머지됨). **작업 씬 = `Greybox_ScanLit.unity`** (Greybox_MVP 아님 — 활성 플레이 씬이 ScanLit).

> 작업 방식: 게임감이라 Opus 메인세션이 직접 설계+구현, 값은 MCP 캡처/플레이로 반복 튜닝.

---

## 0. ★★ 이 세션 최대 교훈 — SerializeField 씬 덮어쓰기 함정

**코드의 `[SerializeField]` 기본값을 바꿔도, 씬에 한 번이라도 저장된 값이 그걸 덮어쓴다.** 세션 내내 넉백·발사쉐이크 값을 코드로 바꿨는데 **실제론 안 먹히고** 씬 저장값(fireShake=0.08, weakKnockback=0.8, bulletKnockback=2.5)이 쓰이고 있었음 → "뚫고 오는 느낌" 튜닝이 통째로 헛돌았음.

- **진단**: `new SerializedObject(FindObjectOfType<PlayerCombat>()).FindProperty("x")`로 씬의 실제 직렬화값을 읽어 코드값과 비교.
- **적용**: 반드시 **씬 인스턴스에 SerializedObject로 세팅 + `EditorSceneManager.SaveScene`** (MCP RunCommand). 코드 default만 바꾸면 무효.
- **하드코딩은 안전**: 메서드 내부 `new MinMaxCurve(...)` 같은 건 직렬화 대상이 아니라 코드대로 적용됨(스파크 두께/길이/shape가 그 예).
- ⚠️ **다음 세션 필수**: 아래 §4 "스테일 의심 필드"를 씬값으로 감사(audit)하고 동기화. 명시적으로 씬에 세팅한 건 fireShake/weakKnockback/bulletKnockback/bulletStagger **뿐**.

(메모리에도 `serializefield-scene-override-trap`로 기록함.)

---

## 1. 머즐 + 발사 (PlayerCombat.cs)

- **머즐 화염 VFX**: 기존 impact-flash 풀 재사용, 총신 끝(`muzzleForward` 앞)에서 짧게 팝(`muzzleFlash*`).
- **머즐 라이트(주위 밝기)**: 씬의 `gunFlashLight`가 **unwired였음** → PlayerCombat이 **코드로 Point Light 생성·구동**(`_muzzleLight`, t² 폴오프). unwired 의존 버그 제거.
- **발사 사운드** (신규 `GunSfx.cs`): Feel NiceVibrations wav를 `Resources/SFX/Guns/`로 복사(권총 pistol_fire_1, 라이플 rifle_fire_1=SniperFire 단발, 샷건 shotgun_fire_1 + 재장전 3종). 무기당 1클립 고정(변형 랜덤 제거 — 일관성). `RuntimeInitializeOnLoadMethod`로 캐시 리셋.
  - ★ **오디오 밀림 해결**: wav를 **PCM+Preload**로 재임포트(Vorbis 디코드 지연 제거), 프로젝트 **DSP 버퍼 256**(128은 이 머신서 크래클 → 256 확정), `AudioSource.time`으로 **완만한 어택 앞부분 스킵**(트리거 즉시 타격), 발사/재장전 소스 분리.

## 2. 피격 VFX — 스파크 + 검은 피 (코드/Resources)

- **탄 스파크**(`CreateSparkPS`, 월드 PS 1개를 명중점으로 옮겨 `Emit`): **얇은 스트레치 줄기**가 사방으로(startSize 0.02~0.045, life 0.06~0.1, Sphere **shell**(radiusThickness=0)→**중앙에 틈**). 가산 HDR(`sparkColor`)→블룸. 둥근 글로우보다 "탄 맞은" 느낌. `sparkBurstCount`(16).
- **검은 피**: Synty `BloodSplat_FX` 복사본 `Resources/FX/blood_hit.prefab` → **startColor 검정(0.03)**(빨강 텍스처×검정=잉크), 루트 스케일 4, `scalingMode=Hierarchy`. `PlayImpact`에서 좀비 명중 시 스폰(`SpawnOverride`, `overrideLifetime` 1.2s). 모든 명중에 스파크.
- **폐기**: Feel `FeelTacticalBulletImpact`, Vefects(아래 §5) = URP 핑크/어두운 파편이라 안 씀. `Resources/FX/spark_hit.prefab`(미사용, 삭제 가능).
- **검증법**: 탑다운 카메라(~20m/58°)에선 작은 이펙트 안 보임 → **MCP 씬뷰 캡처 + 2m 캡슐 기준자**로 크기 맞춤(메모리 `verify-topdown-visually`).

## 3. 에너미 리액션 + 넉백/경직 (ZombieController.cs)

- **HP 증가**: `ZombieConfig_General` 3→**12**, `_Signal` 5→**20** (SO 에셋).
- **본체 emission 히트플래시 = 추가했다가 제거**(유저 요청 "맞을 때 크기/번쩍 빼"). 좀비 머티리얼 4종에 `_EMISSION` 키워드는 켜둔 채 검정(무해) — **미사용이니 정리 가능**.
- **피격 스케일 범프 제거** (유저: 맞을 때 커지는 것 싫음).
- **★넉백 = 누적(`+=`)**, 클램프(×2): 덮어쓰기(`=`)일 땐 전진과 상쇄돼 "제자리"여서, **한 발 한 발 밀리고 연사 시 밀려나게** 누적으로 변경. 코드 로직이라 직렬화 함정 없음.
- **짧은 경직**(`bulletStagger`): 맞는 순간 전진 멈칫(넉백만 작용). **연사 간격(0.125s)보다 짧아야** 영구 프리즈 안 됨.
- `TakeDamage(int)` → `TakeDamage(int, Vector3 hitDir, float knockback, float stagger)` 오버로드. DoT 등은 무인자 버전.

## 4. 카메라/플레이어 피드백

- **카메라 쉐이크** (`PlayerCameraRig.cs`): 추종 위치 위에 **Perlin XZ 위치 쉐이크**(회전X=탑다운 멀미 방지), 정적 `Instance`. 발사(`fireShake`)·피격(`damageShake`)에서 트리거. ★ Feel `MMCameraShaker`는 추종 카메라와 트랜스폼 충돌 → **커스텀이 정답**(풀-Feel 원하면 부모홀더 구조 필요).
- **플레이어 피격** (`PlayerController.TakeDamage`): Feel **`MMTimeScaleEvent` 히트스탑 0.015s** + 카메라 쉐이크(`damageShake` 0.4). 좀비 공격은 빈도 낮아 스팸 없음.

### Feel 사용 원칙(이 세션 확립, 최신 4.0+)
- 구 `MMFeedbacks` 폐기 → **MMF_Player**. **전역 효과(프리즈·플래시·쉐이크)=Feel 이벤트 API**(`MMTimeScaleEvent`/`MMFlashEvent`/`MMCameraShakeEvent`, 코드 트리거), **로컬 콤보=MMF_Player**, **유기적 펀치=스프링**.
- `MMFlash`(화면 플래시)는 `#if MM_UI` + 풀스크린 캔버스 필요 → UI 단계에서.

---

## 5. Vefects 플립북 — URP에서 전부 매젠타 (재확인)

- 프로젝트의 `Vefects/{Flipbook VFX, Combat Flipbook VFX, Pixel Craft VFX}` 3팩 **전부 핑크**. 이 프로젝트는 **Unity 6000.3.16 / URP 17.3** — Vefects 셰이더는 옛 URP용이라 URP17에서 호환 패스 못 찾아 매젠타 폴백(셰이더 컴파일은 "성공"이라 isSupported=True 오탐).
- 유저 보유 = **Stylized VFX Bundle + Flipbook VFX Bundle**. **info@vefects.com 으로 URP/Unity6 빌드 요청 메일 작성 완료**(유저 발송 예정). 답 오면 받아서 피/스파크에 교체.
- 별개: 유저가 산 **VFX Graph - Mega Pack Vol.4 (Gabriel Aguiar)**는 아직 미임포트 + Visual Effect Graph 패키지 미설치. 내용이 마법/슬래시/소환수라 **총기 피격엔 부적합**(레벨업·근접·버프 능력엔 좋음).

---

## 6. 확정된 타격감 값 (현재 씬 적용 상태)

| 항목 | 값 | 위치 |
|---|---|---|
| 발사 카메라 쉐이크 `fireShake` | **0** (끔) | 씬 적용✓ |
| 피격 카메라 쉐이크 `damageShake` | 0.4 | PlayerController |
| 피격 히트스탑 | 0.015s | PlayerController(MMTimeScaleEvent) |
| 넉백 `weakKnockback`(권총·라이플) | **3.4** (누적) | 씬 적용✓ |
| 넉백 `bulletKnockback`(샷건) | 5 | 씬 적용✓ |
| 경직 `bulletStagger` | **0.01s** | 씬 적용✓ |
| 좀비 HP | General 12 / Signal 20 | ZombieConfig SO |
| DSP 버퍼 | 256 | ProjectSettings |
| 사운드 볼륨 | shot/reload 0.3 | ⚠️스테일 의심 |
| 탄 두께 `trailWidth` | 코드 0.07 | ⚠️스테일 의심 |
| 스파크 `sparkColor`/`sparkBurstCount` | (9,7.5,3.5)/16 | ⚠️스테일 의심 |

**⚠️ 스테일 의심 필드**(코드값과 씬값 불일치 가능, §0): `trailWidth`, `sparkColor`, `sparkBurstCount`, `muzzleFlash*`, `muzzleLight*`, `shotVolume`, `reloadVolume`, impact flash 색/크기. → **다음 세션에서 씬값 감사 후 동기화** 권장(이 값들 튜닝이 안 먹혔을 수 있음).

---

## 7. 미해결 / 다음 작업

1. **★"으그극 헤쳐올려는" strain은 보류** — 좀비에 **피격 리액션 애니메이션이 없어서** 코드/값으론 한계(유저·Claude 합의). 제대로 된 좀비 모델+히트리액트/스태거 애니가 들어오는 단계에 Animator 트리거 + 현 넉백/경직 얹어 완성. (캐릭터=플레이스홀더 단계 과제)
2. **스테일 SerializeField 감사·동기화** (§4/§6) — 1순위 점검.
3. **남은 주스 항목**: ③ **UI/화면 피드백**(저체력 비네트·피격 적색 플래시 MMFlash·데미지 숫자) — MMF_Player/MMFlash가 빛나는 자리(MM_UI 디파인+캔버스 셋업 필요). ④ 어택 피드백 보강(거의 됨).
4. **Vefects URP 빌드** 답 오면 피/스파크 플립북 교체. (미사용 `spark_hit.prefab` 정리)
5. 좀비 머티리얼 emission 키워드(미사용) 정리 가능.

## 8. 변경 파일

- `Assets/_Project/Scripts/`: `PlayerCombat.cs`(대폭), `GunSfx.cs`(신규), `ZombieController.cs`, `PlayerCameraRig.cs`, `PlayerController.cs`
- `Assets/_Project/Data/`: `ZombieConfig_General.asset`(HP12), `ZombieConfig_Signal.asset`(HP20)
- `Assets/Resources/SFX/Guns/`(wav 6 + meta), `Assets/Resources/FX/blood_hit.prefab`(검정), `spark_hit.prefab`(미사용)
- `ProjectSettings/AudioManager.asset`(DSP 256), 좀비 머티리얼 4종(emission 키워드), `Greybox_ScanLit.unity`(PlayerCombat 직렬화값)
- 콘솔 에러 0. Stab+Codex 리뷰는 주요 코드 변경마다 수행(트리아지 후 실버그만 반영).
