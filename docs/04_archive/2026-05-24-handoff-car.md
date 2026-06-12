# ZombieCrush 핸드오프 — 2026-05-24

> 🟥 **폐기** — 차량/드리프트/SYNC RATE(정신오염) 전제. 2026-06-08 차량 폐기(도보 캐릭터 확정) + 서사 교체로 전부 무효. 현행: 시스템 [[2026-06-09-postprocessing-core-design]], 스토리 [[2026-06-06-worldbuilding-pitch]].

## 세션 요약

P1 기능 전체 구현 완료. 킬 피드백 → XP 오브 → 레벨/업그레이드 시스템 → 비주얼 폴리시 순서로 진행.

---

## 완료된 작업

### 1. 킬 피드백 (ZombieController.cs)
- 좀비 사망 시 파티클(`killParticlePrefab`) + 사운드(`killSound`) 재생
- XP 오브 3~5개 방사형 폭발 스폰 (`orbCountMin/Max`)
- 각 오브에 `XPOrb.Init(burstDir, car.transform)` 호출

### 2. XP 오브 시스템 (XPOrb.cs)
3단계 페이즈 상태머신:
- **Burst**: 폭발 방향으로 감속 이동 (0.3초)
- **Float**: floatHeight만큼 부드럽게 상승, 0.6초마다 랜덤 방향 방랑 (살아있는 느낌)
- **Attract**: Cubic Bezier 포물선으로 차에 흡수
  - P1 = 차 반대 방향으로 `fleeDistance` + 위로 `windUpLift` (뒤로 빠졌다가 날아오는 느낌)
  - P2 = 중간점 + `arcHeight` (포물선 꼭짓점)
  - P3 = 차 위치 (실시간 추적)
  - Cubic ease-in (t³) — 점진적으로 폭발적으로 빨라짐
- 차가 `attractRadius` 안에 들어오거나 `floatDuration` 경과 시 Attract 전환
- XZ 평면 거리로 흡수 판정 (Y축 제외)

**Inspector 기본값:**
```
burstSpeed=10, burstDuration=0.3, floatHeight=0.6, floatDuration=1.5
attractRadius=8, arcHeight=8, fleeDistance=1.5, windUpLift=2
wanderSpeed=0.3, attractDuration=0.7, absorbRadius=0.8, xpValue=1
```

### 3. XP / 레벨 시스템 (XPManager.cs)
- `[DefaultExecutionOrder(-100)]` 싱글톤
- `AddXP()` → while 루프로 다중 레벨업 처리
- `OnXPChanged(int current, int max)`, `OnLevelChanged(int level)` 이벤트
- **레벨업 시 업그레이드 메뉴 열지 않음** — 피트스톱 거점에서만 열림 (GDD 준수)

### 4. 피트스톱 거점 (PitStopZone.cs)
- `[RequireComponent(typeof(Collider))]`, isTrigger 자동 설정
- `OnTriggerEnter` → `UpgradeMenuUI.Instance.Show()` 호출
- 10초 쿨다운, `IsPanelOpen` 중복 방지
- 진입 시 SyncRate `syncReduction(0.2)` 차감
- 씬에 임시 배치: position (10, 0, 0), BoxCollider 5×2×5

### 5. 업그레이드 메뉴 UI (UpgradeMenuUI.cs)
- `Show()` 시 `timeScale = 0` 일시정지
- 4종 옵션 중 Fisher-Yates 셔플로 3개 무작위 제시:
  - 최고속도 +10%
  - 부스트 용량 +30%
  - XP 보너스 +1
  - 히트당 연료 +50%
- 선택 시 `timeScale = 1` 복구
- `OnDestroy`에서도 복구 (예외 안전)
- HitStop 코루틴과 `timeScale` 충돌 방지: `IsPanelOpen` 가드

### 6. XP 바 UI (XPBarUI.cs)
- `XPManager.OnXPChanged`, `OnLevelChanged` 구독
- 레벨 텍스트 + fill 슬라이더
- 씬 Canvas에 top-center 배치 (400×40)

### 7. SYNC RATE 시스템 (SyncRateManager.cs, SyncRateUI.cs)
- 초당 자동 감소, `ReduceSync()` 호출로 추가 감소
- UI 연동

### 8. 나노봇 오브 비주얼
**머테리얼:**
- `NanobotOrb_Blue.mat` — Standard Fade, color(0.1, 0.6, 1, 0.55), emission(0, 0.8, 2.5)
- `NanobotOrb_RedCore.mat` — Standard Opaque, color red, emission(3, 0, 0)

**프리팹 구조 (XpOrb.prefab):**
```
XpOrb (root)
  ├─ Sphere, NanobotOrb_Blue, scale 0.35
  ├─ XPOrb component
  ├─ Point Light: color(0.1,0.6,1), intensity 0.8, range 3.5  ← 오늘 추가
  └─ RedCore (child)
       ├─ Sphere, NanobotOrb_RedCore, scale 0.4
       ├─ NanobotBlink component  ← Emission + Light 동시 제어
       └─ Point Light: color red, intensity 1.5, range 2.0  ← 오늘 추가
```

**NanobotBlink.cs:**
- Sine파 깜빡임 + 랜덤 플래시
- `_EmissionColor` HDR + Point Light intensity/range 동기화
- RedCore의 붉은 라이트가 주변 바닥/좀비에 실제 조명 투영

### 9. CarController.cs 업그레이드 훅
- `UpgradeMaxSpeed(float factor)`
- `UpgradeBoostCapacity(float factor)`
- `UpgradeBoostFuelPerHit(float factor)`

---

## 씬 배치 상태

| 오브젝트 | 위치 | 비고 |
|---|---|---|
| XPManager | (0,0,0) | 싱글톤 |
| Canvas/XPBar | top-center | 400×40 |
| Canvas/UpgradePanel | center | 500×320, 3버튼, 기본 비활성 |
| PitStopZone | (10,0,0) | 임시 테스트 위치 |

---

## 다음 세션 후보 작업

- [ ] PitStopZone 비주얼 (거점 표시 이펙트/지형)
- [ ] 실제 레벨 디자인 맵에 피트스톱 거점 배치
- [ ] 좀비 웨이브 난이도 곡선 조정
- [ ] SYNC RATE 게임오버 연결
- [ ] 파트너 AI UI 폴리시 (PartnerAIUI.cs 존재)
- [ ] 드리프트 보상 시스템 (재미 진단 1순위 — 이전 세션에서 미완)

---

## 알려진 이슈 / 주의사항

- XpOrb 프리팹 Inspector에서 `xpOrbPrefab` 슬롯을 ZombieController에 연결해야 오브 스폰됨
- UpgradePanel 버튼 3개 Inspector에서 `UpgradeMenuUI` 컴포넌트에 연결 필요
- PitStopZone은 테스트용 임시 위치 — 맵 확정 후 재배치 필요
- Built-in Render Pipeline 사용 중 (URP/HDRP 아님)
