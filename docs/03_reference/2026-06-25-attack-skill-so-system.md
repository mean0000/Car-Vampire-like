# 공격/스킬 데이터 SO 시스템 — 매뉴얼 (2026-06-25)

> 카타나(및 향후 모든 무기)의 콤보·스킬 데이터는 **ScriptableObject로 관리**한다(인라인 금지). 새 무기/스킬 = SO 하나 더, **코드 0**(OCP). 타이밍은 SO에 없다 — **클립 AnimationEvent가 소유**(애니가 진실).

---

## 1. 라이브 SO = 2개 (이게 전부)

### `ComboAttackSet` — 무기 1개의 콤보
- 정의: `Assets/_Project/Scripts/Player/ComboAttackSet.cs`
- 생성: `Create > ZombieCrush/Combo Attack Set`
- 내용: `steps[]` (인덱스 0 = 1타). 각 step이 **판정 + 비주얼을 한 줄에**:
  - 판정: `range` · `arcHalfAngle` · `forwardOffset` · `damage` · `knockback` · `rangeFromSlashScale`(켜면 사거리 = range × slash scale)
  - 비주얼: `slashPrefab` · `eulerOffset` · `posOffset` · `scale` · `lifetime` · `playbackSpeed` (+ `fallbackSlashPrefab`)
- **소비자 둘이 같은 에셋을 읽음 = 단일 진실:**
  - `KatanaWeapon` → 판정(`TryGetStep`)
  - `PlayerAttackVfx` → 비주얼(`AttackHit(comboStep≥1)` 때 slashPrefab 스폰, 무기 앵커 정합)

### `SkillSet` — 스킬 1개
- 정의: `Assets/_Project/Scripts/Player/SkillSet.cs`
- 생성: `Create > ZombieCrush/Skill Set`
- 내용: 4개 접이식 블록
  - `hit` — range/arcHalfAngle/forwardOffset/damage/knockback
  - `timing` — cooldown / maxDuration(안전 워치독)
  - `vfx` — **`basis`(Player=전방 발사 / Weapon=칼 휘두름)** + prefab + eulerOffset/posOffset/scale/playbackSpeed/lifetime
  - `sfx` — clip(2D) / volume
- 소비자: `KatanaWeapon.skillSet` (RMB Skill01). 타격 순간(`DoSkillHit`)에 판정+vfx+sfx.

### 에셋 위치/네이밍
`Assets/_Project/VFX/Katana_Cham_*Set.asset` (예: `Katana_Cham_ComboAttackSet`, `Katana_Cham_Skill01Set`).

---

## 2. 폐기됨 (2026-06-25 제거)

- **`WeaponSlashSet` (클래스) + `Katana_Cham_SlashSet.asset`** — 삭제. 옛날 "슬래시 비주얼 전용" SO였으나 `ComboAttackSet`의 비주얼 필드로 **흡수 완료**. 고아(참조 0·빈 프리팹)라 제거. 슬래시 비주얼은 이제 `ComboAttackSet.steps[].slashPrefab`가 단일 소유.

---

## 3. ★스킬 추가하는 법 (코드 0)

1. **`Create > ZombieCrush/Skill Set`** → `Assets/_Project/VFX/`에 저장(예: `Katana_Cham_Skill02Set`).
2. 4블록 채우기:
   - `hit` = 사거리/부채꼴/데미지/넉백
   - `timing` = cooldown(밸런스), maxDuration(클립 길이+여유)
   - `vfx` = basis 고르고(슬래시면 Weapon) prefab + 정합 오프셋/스케일
   - `sfx` = 사운드 클립
3. 무기의 스킬 슬롯에 이 에셋을 연결.
4. **타격/종료 타이밍 = 그 스킬 애니 클립의 AnimationEvent**(`OnAttackHit`/`OnComboEnd`)로. SO엔 안 넣는다.

---

## 4. ⚠️ "스킬 늘리기"의 현재 한계 (다음 단계)

현재 `KatanaWeapon`은 **스킬 슬롯이 1개**(`skillSet`, RMB Skill01)다. 스킬을 *여러 개* 달려면 추가 작업 필요:
- `List<SkillSet>` (또는 슬롯별 필드) + **입력 바인딩**(키 → 스킬)
- 스킬별 **Animator 트리거/상태** (현재 `TriggerSkill` 하나)
- (선택) 스킬 쿨다운 UI

→ 이건 *데이터 정리*가 아니라 *시스템 확장*이라 별도 작업. 본 매뉴얼의 SO 구조는 그 확장의 토대(스킬 1개당 SO 1개)는 이미 갖춰져 있음.

---

## 5. 코드-레벨 참고 (안 고침, 의도적)

`ComboAttackStep`의 비주얼 필드 ≈ `SkillSet.VfxData`로 *유사 중복*이나, **합치지 않음**: ① `VfxData`엔 `basis`(Player/Weapon)가 있어 스킬은 전방발사도 필요(콤보는 항상 무기 앵커) ② 합치면 기존 튜닝된 .asset 데이터 마이그레이션 리스크. 마이너 중복 < 마이그레이션 비용 → 현행 유지. (정 거슬리면 공유 `[Serializable] SlashVfxData`로 통합 가능 — 단 `[FormerlySerializedAs]` + 에셋 재직렬화 필요, Stab+Codex 게이트.)
