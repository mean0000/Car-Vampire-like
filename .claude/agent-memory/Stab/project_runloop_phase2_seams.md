---
name: runloop-phase2-seams
description: 런 루프 시스템(QuotaRunController/ThreatLevel/CombatEvents 등 7파일) Phase 2 통합 시 재검증할 결합 함정
metadata:
  type: project
---

2026-06-13 런 루프 7파일 스테이징(`docs/02_logs/runloop-staging/`) QA 리뷰에서 확인. 7파일 *내부* 정확성은 통과(Critical 0). 위험은 전부 **Phase 2 통합 seam**에 몰려 있고, 어느 단일 파일을 읽어도 안 보이는 교차 결합이라 기록한다.

**Why:** 스테이징 코드는 자기완결적으로 안전하나, `Assets/`로 옮겨 기존 시스템과 배선되는 순간 발화하는 잠재 결함이 3개 있다. 설계 문서(`docs/02_logs/2026-06-13-run-loop-system-architecture.md`)가 이 중 일부를 P2/P3로 "지연"이라 표기했지만 *충돌 위험 자체는 경고하지 않았다*.

**How to apply (Phase 2 착수/머지 리뷰 시 반드시 점검):**

1. **★OperationTimer.OnExpired 이중 구독 = 커밋 중 즉사 (최우선).** `OperationTimer`는 단일 인스턴스 싱글톤(Assets/.../Run/OperationTimer.cs:13, Awake 중복 파괴). 기존 `RunManager.Start()`가 `timer.OnExpired += HandleTimerExpired`(RunManager.cs:65) → `Settle(RunOutcome.Swept, 0.0f)`(RunManager.cs:154-159) = **총손실 사망**. 스테이징 `QuotaRunController.Begin()`도 `quotaTimer.OnExpired += HandleQuotaTimerExpired`(QuotaRunController.cs:91)를 *같은 타이머 인스턴스*에 건다(설계 §1/§3.4 "재사용"). 둘 다 배선되면 만료 시 양쪽 핸들러 발화 → QuotaRun은 IsOvertime=true(의도) + RunManager는 플레이어 sweep 사망(06-13 피벗이 폐기한 바로 그 하드 타임아웃 사망). **마이그레이션은 원자적이어야 함**: (a) RunManager.cs:65 OnExpired 구독 + :58 해제 제거, (b) OperationTimer 단독 소유를 QuotaRunController로, (c) RunOutcome.Swept는 P3대로 후반 폭격 이벤트로 격하(타이머 만료론 안 탐). 통합 후 불변식 = "OperationTimer.OnExpired 구독자 정확히 1개". 가능하면 assert.

2. **CommitNextTier 이벤트 순서 stale-read (M-3).** QuotaRunController.cs:147-155 순서 = IsLocked=true → CurrentTier++ → OnTierCommitted.Invoke → StartTier. OnTierCommitted 발화 시점(:153)엔 CurrentTier만 새 값이고 Processed/QuotaTarget/State/IsOvertime는 *이전 티어 값*(StartTier가 :154에서 아직 안 돎). OnTierCommitted 구독자가 QuotaTarget 읽으면 한 프레임 구버전. ThreatLevel은 이벤트 아닌 폴링(자체 Update에서 CurrentTier/IsOvertime만)이라 무영향. Phase 2 HUD가 OnTierCommitted로 "새 할당량 N" 표시하면 깜빡임. 권장 = StartTier를 OnTierCommitted 앞으로(단 "알림 후 리셋" 의도면 문서화).

3. **Overtime 위협 latch + 커밋 순간 불연속 (M-1, 디자인 판정).** IsOvertime은 HandleQuotaTimerExpired(:139)가 true 세팅 후 다음 StartTier(:116)에서만 리셋. 오버타임 진입 후 할당량 채우면 ExtractionAvailable 창에서도 위협 계속 상승(_overtimeSec 누적, ThreatLevel.cs:74-80). 커밋하면 StartTier가 IsOvertime 클리어 → 같은 프레임 overtime 위협 0 스냅 + depth 위협 +1 → Current에 가시적 불연속(몬스터가 순간 약해졌다 다시 강해짐). 버그 아닐 가능성 높음(피벗 "만료=위협가속" 정합) — 디자인 확인 사항.

4. **데이터 모양 seam (Low, Phase 2 소비자 책임):**
   - `RunUpgradeDef`/`RunUpgradeCatalog`는 `namespace Upgrade` 선언인데 기존 `Assets/_Project/Scripts/Upgrade/` 전 파일은 **글로벌 네임스페이스**(UpgradeCard.cs:4, WeaponData.cs:11 등 namespace 없음). 충돌은 아니나 혼합 → LevelUpChoiceUI 등 소비자가 `using Upgrade;` 필요. `Run/` 스테이징은 기존 Run 네임스페이스와 정합(문제 없음) — Upgrade만 divergent. 통합 시 일관성 결정.
   - RunUpgradeDef.descByLevel/valueByLevel/maxLevel 길이 관계 무가드. Phase 2 적용 코드가 반드시 인덱스 클램프(Mathf.Clamp(lv-1,0,Length-1)), maxLevel 신뢰 금지(IndexOutOfRange 위험). evolvesInto 자기참조/순환도 Phase 2 순회에서 사이클 가드 필요.

**검증된 안전 전제(재확인 불요):**
- CombatEvents.ResetStatics — [RuntimeInitializeOnLoadMethod(SubsystemRegistration)]로 OnAnomalyProcessed=null. 도메인 리로드 off 정적 이벤트 누수 백신, 정확히 적용됨(CaniathroxChaser/GunSfx와 동일 검증 패턴).
- QuotaRunController 구독 대칭: Begin(:90-91)↔Unhook(:106-110), End(:102)+OnDestroy _running 가드(:76-77) 양쪽 호출. _running 플래그가 이중 구독(:83)·End 누락 시 정리 보장. quotaTimer != null 가드로 미배선 무NRE.
- P8(잉여 정지) 정확: HandleAnomalyProcessed가 State==ExtractionAvailable 시 Processed++ *앞에서* 조기 리턴(:128) → 다음 티어로 안 샘.
- P6(SyncRate 0): 7파일 전체 SyncRateManager/SyncRate 미참조. 위협 다이얼 완전 디커플.
- QuotaTarget>0 가드(:134) + config null 폴백(:115) → config 미배선 시 첫 킬 즉시 점등 사고 방지.
- QuotaTierConfig.Sample(:32-36): null/빈배열 0 폴백 + Clamp(tier-1) → tier 0·음수·끝 외삽 전부 안전.
- 단, ★전제: 모든 안전성이 "사무실로 = 씬 리로드"(RunManager.ReturnToOffice) 계약에 기댐. _running 조기 리턴(:83)은 상태 리셋을 스킵하므로 *비-리로드 재출동* 도입 시 stale tier/counter 위험 — 전면 재검증 필요(LKP/스폰 1회성 플래그와 동일 클래스).
