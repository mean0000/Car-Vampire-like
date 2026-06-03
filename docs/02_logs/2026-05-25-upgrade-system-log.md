# 2026-05-25 업그레이드 시스템 세션 로그

## 개요

무기/업그레이드 시스템 전체 설계 및 구현, UI 자동화, 폰트 적용까지 완료한 세션.

---

## 1. 무기 시스템 재설계

### 왜
기존 무기 목록이 차량 탑재 장비로 제한되어 있어 VS-like 게임의 다양성이 부족했음.

### 무엇을
4트리 × 5무기 = 20종 설계. VS 장르 레퍼런스(Vampire Survivors, 20 Minutes Till Dawn, Brotato) 참고해 나노봇 SF 세계관에 맞는 자동 발동형 무기로 재구성. 차량 탑재 제약 없음.

| 트리 | 특성 | 무기 예시 |
|------|------|-----------|
| Survival | 지속/회복 | 나노 실드, 회복 오라, 전기 장막 |
| Melee | 근접/충격 | 충격파, 전기장, 회전 톱날 |
| Ranged | 원거리/투사체 | 레이저, 유도 미사일, 산탄 |
| Architect | 배치/구조물 | 포탑, 장벽, 지뢰 |

**참고 문서**: `docs/2026-05-25-weapon-system.md`

---

## 2. 업그레이드 메커니즘 설계

### 왜
"어떤 확률로 어떻게 보여줄 것인가"를 구현 전에 스펙화. 설계 없이 코딩하면 나중에 전면 재작업.

### 어떻게
Opus Plan 에이전트로 설계 확정 → 구현 진입.

### 핵심 설계

**카드 풀 구성 (CardPoolBuilder)**
- 보유 무기 수에 따라 해금/강화 슬롯 비율 결정
  - 보유 < 4개: 해금 3 + 강화 1
  - 보유 < 8개: 해금 2 + 강화 2
  - 보유 ≥ 8개: 해금 1 + 강화 3
- 중복 무기 금지, 같은 트리 최대 2장 제한
- 첫 피트스톱: 4트리 각 1장 Normal 해금 고정

**희귀도 확률 (RarityRoller)**
```
progression = CurrentLevel / 40   (0~1)
bankBonus   = Clamp01((pendingLevels - 4) × 0.03)

Legendary = 5% + 15% × progression + bankBonus × 100%
Rare      = 25% + 15% × progression
Normal    = 나머지
```

**트리 어피니티 (TreeAffinityTracker)**
- 해금 선택 시 해당 트리 +2pt, 강화 선택 시 +1pt
- 피트스톱마다 전체 점수 × 0.9 감쇠
- 가중치 = 1.0 + score × 0.5 → WeightedRandom 선택에 반영

**천장 시스템 (PityTracker)**
- Rare 4라운드 미등장 → 다음 첫 강화 슬롯 Rare 이상 보장
- Legendary 12라운드 미등장 → 다음 첫 강화 슬롯 Legendary 보장
- **제시된 카드가 아닌 실제 선택한 카드 기준으로 카운터 갱신** (Codex가 잡은 버그)

**참고 문서**: `docs/2026-05-25-upgrade-mechanism.md`, `docs/2026-05-25-design-doc.html`

---

## 3. 구현 파일 목록

### 신규 파일

| 파일 | 역할 |
|------|------|
| `Scripts/Upgrade/WeaponData.cs` | ScriptableObject. weaponName, tree(enum), description, icon |
| `Scripts/Upgrade/UpgradeCard.cs` | 카드 데이터. Type(Unlock/Upgrade), Weapon, TargetRarity |
| `Scripts/Upgrade/PlayerWeaponInventory.cs` | 보유 무기+등급 관리 싱글톤. DefaultExecutionOrder(-90) |
| `Scripts/Upgrade/RarityRoller.cs` | 희귀도 확률 계산 정적 클래스 |
| `Scripts/Upgrade/TreeAffinityTracker.cs` | 트리 가중치 추적 |
| `Scripts/Upgrade/PityTracker.cs` | 천장 카운터 |
| `Scripts/Upgrade/CardPoolBuilder.cs` | 카드 4장 조합 로직 전체 |

### 수정 파일

**XPManager.cs**
- `PendingLevels` 프로퍼티 추가 (피트스톱 대기 레벨업 수)
- `ConsumePendingLevel()` 추가
- `AddXP`에 `Mathf.Max(0, amount + bonusXP)` 음수 방지 추가
- XP 임계값 공식: `10 + (lv × 8) + (lv² × 2)`, 40레벨 캡

**UpgradeMenuUI.cs** (전면 재작성)
- 코루틴 기반 선택 루프 (`SelectionLoop`): PendingLevels 소진까지 반복
- `Time.timeScale = 0f / 1f` 패널 열고/닫을 때 처리
- 30초 타임아웃 안전망 (`WaitUntil` 무한 블록 방지)
- 리롤 최대 2회, 남은 횟수 버튼에 표시

**PitStopZone.cs**
- `XPManager.PendingLevels > 0` 체크 후 `UpgradeMenuUI.Show()` 호출

### WeaponData 에셋 (20종)
`Assets/_Project/Data/Weapons/` — Unity MCP RunCommand로 자동 생성  
S1~S5 (Survival), M1~M5 (Melee), R1~R5 (Ranged), A1~A5 (Architect)

---

## 4. Stab + Codex 리뷰 — 발견 및 수정 버그

구현 후 Stab(QA) + Codex 병렬 실행. 총 8개 이슈.

| 심각도 | 이슈 | 수정 내용 |
|--------|------|-----------|
| Critical | `OnDestroy`에서 `upgradePanel`이 이미 파괴된 경우 `IsPanelOpen` NullRef | `if (IsPanelOpen)` → `if (Time.timeScale == 0f)` |
| Critical | `WaitUntil(() => _cardChosen)` — cardViews 미연결 시 무한 블록 | `while (!_cardChosen) { elapsed += unscaledDeltaTime; if (elapsed > 30f) break; }` |
| High | 카드 구성 실패해도 `ConsumePendingLevel()` 선 소비 | BuildCards() 성공 후에만 ConsumePendingLevel() 호출 |
| High | `bonusXP`가 음수면 XP 마이너스 가능 | `Mathf.Max(0, amount + bonusXP)` |
| Critical | PityTracker가 제시된 카드 기준으로 갱신 (선택한 카드여야 함) | `OnRoundCompleted(_currentCards)` → `OnCardPicked(_selectedCard)` |
| Medium | `WeightedRandom` totalWeight=0일 때 0 나눗셈 | `if (totalWeight <= 0f) return pool[Random.Range(0, pool.Count)]` |
| Medium | 폴백 시 중복 무기까지 허용 | 트리 제한만 완화, `!usedWeapons.Contains(w)` 조건 유지 |
| Medium | `GetWeight` 음수 반환 가능 | `Mathf.Max(0f, _affinity.GetWeight(w.tree))` |

---

## 5. 업그레이드 패널 UI 자동화

### 왜
Inspector 수동 연결은 필드 20개 이상 + 실수 가능성. RunCommand 한 번이 확실.

### 어떻게
Unity MCP `RunCommand` (C# Editor 스크립트 즉시 실행):
1. Canvas 하위 UpgradePanel 생성 (반투명 배경, 전체화면)
2. 카드 4개 생성 — 각 카드: RarityBorder(Image) + TitleText(TMP) + DescText(TMP) + Button
3. RerollButton + RerollCostText(TMP) 생성
4. `SerializedObject` / `SerializedProperty`로 UpgradeMenuUI Inspector 필드 자동 연결
5. `allWeapons` 배열에 20종 WeaponData 에셋 자동 할당

**주의**: MCP 스크립트는 Unity 내부 namespace에 래핑되므로 `Image` 단독 사용 시 namespace 충돌 발생 → `UnityEngine.UI.Image` 풀 네임으로 사용해야 함.

---

## 6. 버튼 클릭 안 되는 문제 수정

### 증상
플레이 모드에서 업그레이드 카드 버튼이 클릭에 반응 없음.

### 원인
Canvas 계층 상단의 Image 컴포넌트들이 `Raycast Target = true` 상태로 Button 위를 덮어 클릭 이벤트 차단.

### 진단
RunCommand로 UpgradePanel 하위 전체 UI 컴포넌트 순회 → `raycastTarget` 값 출력.

결과:
- UpgradePanel 배경 Image: `raycastTarget = true` (차단 원인)
- 4개 카드 RarityBorder Image: `raycastTarget = true` (차단 원인)
- TitleText, DescText TMP: `raycastTarget = true` (차단 원인)
- FlashImage: 이미 false (무관)

### 수정
RunCommand로 일괄 `raycastTarget = false` 적용:
- UpgradePanel 배경 Image
- 카드 4개의 RarityBorder Image
- 모든 TitleText, DescText TMP

Button 컴포넌트 자체는 변경 안 함 (Raycast 수신 필요).

---

## 7. 한글 폰트 적용

### 왜
기본 LiberationSans SDF에 한글 글리프 없어서 모든 한글 텍스트 깨짐.

### 폰트 선택: Pretendard
- 모던/클린 산세리프, SF 게임 UI에 적합
- OFL 라이선스 (무료 상업용)
- 출처: github.com/orioncactus/pretendard

### 적용 과정
1. `Pretendard-Black/Bold/Medium/Regular/Thin.otf` 임포트
2. Font Asset Creator로 각각 SDF 에셋 생성 → `Assets/_Project/Font/`
3. RunCommand로 씬 내 TMP 20개 전부 **Pretendard-Medium SDF** 일괄 적용

### 한글 깨짐 진단
RunCommand로 `font.HasCharacter('가')` 등 체크 → 글리프 수 98개(ASCII만), 한글 0개 확인.  
→ Font Asset Creator에서 한글 범위 포함해 재생성 필요.

### Font Asset Creator 설정값
- **Character Set**: Custom Range
- **Custom Character Range 입력값**: `32-126,44032-55203,12593-12686`
  - `32-126` = ASCII
  - `44032-55203` = 한글 완성형 (U+AC00~U+D7A3)
  - `12593-12686` = 자음/모음 낱자 (U+3131~U+318E)
- **Atlas Resolution**: 4096 × 4096 (한글 11,172자 수용)
- 생성 시간: 1~2분

---

## 8. 현재 상태 (세션 종료 시점)

| 항목 | 상태 |
|------|------|
| 업그레이드 시스템 코드 | ✅ 완료 + 리뷰 수정 반영 |
| WeaponData 에셋 20종 | ✅ 생성 완료 |
| 업그레이드 패널 UI | ✅ 생성 + 필드 연결 완료 |
| 버튼 클릭 수정 | ✅ Raycast Target 수정 완료 |
| 엔드투엔드 플레이 테스트 | ⏳ 미완 (플레이 모드 직접 확인 필요) |
| Pretendard 폰트 적용 | ✅ TMP 20개 연결 완료 |
| Pretendard 한글 글리프 | ⏳ Font Asset Creator 재생성 필요 |
| 무기 실제 동작 구현 | ❌ 미구현 (WeaponData만 있고 MonoBehaviour 없음) |
