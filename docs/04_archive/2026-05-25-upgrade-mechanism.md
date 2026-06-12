# ZombieCrush 업그레이드 선택 메커니즘 설계

> 🟥 **폐기** — 피벗 이전(차량 시대) 업그레이드 설계. 카드 권위: [[2026-05-31-levelup-cards-catalog]].

**확정일:** 2026-05-25  
**상태:** 설계 확정, 구현 대기

---

## 핵심 전제

- 피트스톱 진입 시 **쌓인 레벨(PendingLevels)을 전부 소진**하며 선택
- 레벨이 3이면 3번 선택, 0이면 UI 미표시
- 선택지: **해금(신규 무기 획득)** + **강화(기존 무기 레벨업)** 혼합
- 카드 4장 제시

---

## 발동 방식 정의 (희귀도 체계)

| 등급 | 효과 유형 |
|---|---|
| **노말** | 수치 향상 (데미지, 쿨다운, 범위, 개수) |
| **레어** | 부가 효과 추가 (상태이상, 연쇄, 조건 변화) |
| **전설** | 행동 방식 자체 변화 |

---

## 1. 카드 풀 구성 규칙

### 해금 / 강화 비율

| 보유 무기 수 | 해금 슬롯 | 강화 슬롯 |
|---|---|---|
| 0개 | 4 | 0 |
| 1~2개 | 2 | 2 |
| 3~4개 | 1 | 3 |
| 5개 이상 | 0~1 (해금 가능 무기 있을 때만 1) | 3~4 |

- **같은 트리 카드:** 한 화면에 최대 2장까지 허용
- **동일 무기 카드:** 같은 화면에 절대 중복 불가

---

## 2. 희귀도 가중치

### 기본 확률

| 등급 | 기본 확률 |
|---|---|
| Normal | 70% |
| Rare | 25% |
| Legendary | 5% |

### 진행도에 따른 변화

```
float timeFactor  = Mathf.Clamp01(gameTime / 1200f);   // 20분에 최대
float levelFactor = Mathf.Clamp01(playerLevel / 30f);  // 레벨 30에 최대
float progression = Mathf.Max(timeFactor, levelFactor);

Normal:     70% - (30% * progression)  →  70% ~ 40%
Rare:       25% + (15% * progression)  →  25% ~ 40%
Legendary:   5% + (15% * progression)  →   5% ~ 20%
```

### 레벨 축적 보정 (Bank Bonus)

피트스톱 진입 시 PendingLevels가 5 이상이면 Legendary 확률에 추가 보정:

```
float bankBonus = Mathf.Clamp01((pendingLevels - 4) * 0.03f);
// 레벨 5부터 3%씩 증가 → Legendary 가중치 추가, Normal에서 차감
```

---

## 3. 트리 어피니티(Tree Affinity)

### 어피니티 점수

- 해당 트리 무기 **해금**: +2점
- 해당 트리 무기 **강화**: +1점
- 매 피트스톱 선택 완료 후 **전체 0.9배 감쇠** (최근 선택이 더 큰 영향)

### 카드 풀 가중치 반영

```
각 트리 선택 가중치 = 1.0 + affinityScore * 0.5

예) 근접 어피니티 6점 → 가중치 4.0
    원거리 어피니티 0점 → 가중치 1.0 (완전히 사라지지는 않음)
```

- 해금 카드에 1.2배 추가 가중치 (해금 우선 유도)
- 한 트리에서 최대 2장 규칙으로 "우연한 발견" 보장

---

## 4. 무기 해금 vs 강화 우선순위

- 보유 무기 0개: 해금만 제시 (자동)
- 최대 등급(전설) 도달한 무기: 해당 무기 강화 카드 풀에서 영구 제거
- 첫 피트스톱: **4개 트리에서 1장씩, 전부 Normal 해금 고정**
  → 플레이어가 빌드 방향을 결정하는 첫 핵심 경험

---

## 5. 중복 방지 규칙

1. 동일 무기 카드 같은 화면에 2장 이상 불가
2. 이미 보유한 무기의 해금 카드 불가
3. 최대 등급(전설) 무기의 강화 카드 불가
4. 같은 트리에서 최대 2장
5. 같은 피트스톱 내 연속 선택 간: 직전에 선택한 카드와 동일한 카드의 가중치 50% 감소

---

## 6. 리롤 시스템

| 항목 | 값 |
|---|---|
| 리롤 가능 횟수 | 피트스톱당 최대 2회 |
| 리롤 비용 | 나노봇 코어 50개 또는 레벨 1 소진 |
| 리롤 시 어피니티 | 그대로 유지 (빌드 방향성 보존) |
| 최소 변경 보장 | 이전 카드 중 최소 2장은 반드시 교체 |

---

## 7. 보장(Pity) 시스템

| 등급 | 피티 카운터 | 효과 |
|---|---|---|
| Rare | 4회 연속 Normal만 | 다음 화면에서 1장 이상 Rare 이상 보장 |
| Legendary | 12회 연속 Legendary 없음 | 다음 화면에서 1장 Legendary 보장 |

- 해당 등급이 나오면 카운터 리셋

---

## 카드 선택 알고리즘 (순서도)

```
[STEP 0] 피트스톱 진입
  PendingLevels 확인 → 0이면 UI 없이 힐/Sync 감소만 수행 후 종료

[STEP 1] 선택 라운드 시작 (PendingLevels 횟수 반복)
  PendingLevels--
  rerollCount = 0

[STEP 2] 슬롯 구성 결정
  ownedCount 기반으로 해금/강화 슬롯 수 결정
  첫 피트스톱 첫 선택이면 → 4트리 각 1장 Normal 해금 → STEP 5

[STEP 3] 희귀도 결정
  progression 계산 → RarityRoller.Roll()
  PityTracker.GetGuaranteedRarity() 확인 → 보장 등급 있으면 첫 슬롯에 적용

[STEP 4] 카드 풀 구축
  해금 풀: 미보유 무기 목록
  강화 풀: 보유 무기 중 최대 등급 미달 목록
  트리 어피니티 가중치 적용
  해금 카드 1.2배 추가 가중치
  중복 방지 검증 후 4장 선택

[STEP 5] UI 표시
  Time.timeScale = 0
  카드 4장 + 리롤 버튼 표시

[STEP 6-A] 카드 선택
  해금: Inventory.Unlock() + Affinity +2
  강화: Inventory.Upgrade() + Affinity +1
  PityTracker.OnCardsPicked()
  AffinityTracker.DecayAll() (전체 0.9배)
  PendingLevels > 0 → STEP 1 / 아니면 STEP 7

[STEP 6-B] 리롤
  rerollCount < 2 확인 + 비용 지불
  최소 2장 교체 보장 후 STEP 4

[STEP 7] 종료
  Time.timeScale = 1 / UI 닫기 / 게임 재개
```

---

## 카드 풀 고갈 폴백

20종 무기 전부 전설까지 달성하면 카드 풀이 빌 수 있음.  
→ 이 경우 **패시브 스탯 카드**(속도, 부스트, 드리프트 수치 업그레이드)를 폴백으로 제시.

---

## 구현 파일 구조

```
Assets/_Project/Scripts/
└── Upgrade/
    ├── Data/
    │   ├── WeaponData.cs              ScriptableObject - 무기 정의
    │   └── UpgradeCard.cs             카드 데이터 클래스
    ├── CardPoolBuilder.cs             카드 풀 구축 + 가중치 선택
    ├── RarityRoller.cs                희귀도 확률 계산
    ├── TreeAffinityTracker.cs         트리 어피니티 추적
    ├── PityTracker.cs                 피티 보장 시스템
    └── PlayerWeaponInventory.cs       보유 무기 관리 싱글톤
```

### 기존 파일 수정 필요

| 파일 | 변경 내용 |
|---|---|
| `XPManager.cs` | `PendingLevels` 필드 추가, 레벨업 시 증가, `ConsumeLevels()` 메서드 |
| `PitStopZone.cs` | PendingLevels > 0 확인 후 UI 오픈 |
| `UpgradeMenuUI.cs` | 하드코딩 제거, 4장 카드 + 리롤 버튼, 다회 선택 루프 |

---

## 주의사항

- `Time.timeScale = 0` 은 다회 선택 루프 동안 유지, 모든 선택 완료 후 1로 복구 (매번 토글 X)
- `TreeAffinityTracker`, `PityTracker` 상태는 런 내에서만 유지 (저장 불필요)
- `RarityRoller` 수치는 ScriptableObject + AnimationCurve로 구현해 Inspector 튜닝 가능하게
