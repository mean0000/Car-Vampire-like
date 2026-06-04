# 근접 무기 스크립트 핸드오프 (2026-06-03)

데모 근접 전투 시스템 구현 핸드오프. **스크립트만** 작업(씬은 타 작업자 편집 중이라 무손상).
권위: `docs/00_authority/2026-06-03-demo-weapon-lineup.md`.

## 1. 무기 라인업 / 데이터

`Assets/_Project/Scripts/WeaponLoadout.cs` — 무기 정의 **단일 출처**.

| 무기 | Kind | 데미지 | 사거리 | 부채꼴(반각) | 넉백 | 경직 | 히트스탑 | 죽음연출 | 소음 |
|---|---|---|---|---|---|---|---|---|---|
| 리볼버 | Ranged | 3 | 20 | – | – | – | – | None | 95 |
| 야구방망이 | Melee | 2 | 2.2 | ±50° (넓은 클리브) | 6 | 0.15 | 0.04 | Splat | 25 |
| 쇠지렛대 | Melee | 6 | 2.5 | ±32° (좁은 단타) | 11 | 0.28 | 0.08 | Crunch | 32 |

- 쇠지렛대 = 야구방망이 **진화형**. `WeaponLoadout.BaseBat`(idx1) / `EvolvedCrowbar`(idx2).
- **필드 재사용 주의**: 근접은 `fireCooldown`=스윙쿨, `range`=리치, **`gunshotNoise`=스윙소음**. 별도 `swingNoise` 필드 없음.
- enum: `Kind { Ranged=0, Melee=1 }`(0=Ranged라 struct 기본값 안전), `DeathStyle { None=0, Splat=1, Crunch=2 }`.

## 2. 스크립트 구성

- **`MeleeAttacker.cs`** (신규, **순수 C# — MonoBehaviour 아님**)
  - PlayerCombat이 소유·구동. 런타임 AddComponent 수명주기 함정 회피용.
  - `Tick(attackHeld, aimDir, locked)` 매 프레임 호출 → 쿨마다 `Swing()`.
  - 판정: `OverlapSphere(range+0.5, ...Collide)` → 부채꼴 반각 + **초근접 0.9m는 각도 무시**(후한 판정) + 벽 LOS 레이캐스트. `HashSet`로 스윙당 중복타격 차단.
  - **히트스탑 소유권 = 스윙 1회**(다중킬 스택 방지). 죽이면 `hitstop*1.5`, 비살상이면 `hitstop`.
  - 스윙 호 비주얼 = 런타임 `LineRenderer`(무기별 색). `Cleanup()`을 PlayerCombat.OnDestroy에서 호출.
  - `Evolve(weapon)` = 진화(profile 스왑).
- **`MeleeSfx.cs`** (신규, static) — 절차 생성 캐싱 오디오. `PlayHit(style,…)`(Crunch→clang/Splat→thud), `PlayWhiff(…)`. 에셋/씬 와이어링 0.
- **`ZombieController.cs`** (편집)
  - `TakeMeleeHit(dmg, attackerPos, knockback, stagger, style)` → bool(죽었나). 사망 시 `DieByWeapon`.
  - `DieByWeapon(style, dir, force)` — 무기별 squash/런치 차등. **히트스탑은 안 냄**(스윙이 소유). `DOMove` 런치 후 제거 + `Destroy(0.6f)` 안전망.
  - 키네마틱 RB라 넉백은 코드 변위: `_knockbackVel`(감쇠) + `_staggerTimer`(AI 일시정지). `AddForce` 금지.
  - 스텔스 잔재 제거: `IsAssassinable`/`TryAssassinate`/`TakeDamage(int,bool)` 삭제, `TakeDamage(int)` 단일화.
- **`PlayerCombat.cs`** (재작성)
  - Awake에서 `WeaponLoadout.Selected.kind` 분기. Melee면 `MeleeAttacker` 생성(원거리는 `_melee=null`, 트레이서 미생성).
  - 입력 통일: **좌클릭 홀드 = 쿨마다 반복**(원거리도 GetMouseButton).
  - 스텔스 F-암살/마커 전부 제거.
  - 디버그 **T키** = 방망이→쇠지렛대 라이브 진화(`_melee != null` 가드).

## 3. 검증 / 주의

- Stab+Codex 병렬 리뷰 완료. 두 건 수정: ①다중킬 히트스탑 스택→스윙 1회 통합 ②T키 NRE→null 가드.
- `WeaponSelectUI`는 씬 카드 버튼 수만큼만 노출 → 쇠지렛대는 씬에 3번째 카드 있을 때만 보임(없으면 자동 숨김, 씬 충돌 없음). 미노출 시 T키로 테스트.
- **미적용(스코프 밖)**: 40+ 좀비 스트레스 시 `OverlapSphereNonAlloc` 전환 권장. `ZombieController` CalcSeparation/SignalCoroutine의 trigger 쿼리 누락은 이번 PR 이전 코드라 미수정.

## 4. 남은 작업

- 소음기 부착물(소음-30% 즉시영구) 연결 — 아직 미구현.
- 진화 트리거를 카드 시스템과 연결(현재는 디버그 T키만).
- 실제 AudioClip 에셋으로 절차 사운드 교체(선택).
