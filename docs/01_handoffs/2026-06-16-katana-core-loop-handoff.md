# 핸드오프 — 첫 볼트: 카타나 코어 루프 (애니 합류 후 착수) (2026-06-16)

**용도:** 뱀서 피벗의 첫 볼트("30초 갈기 + 레벨업 픽" 손맛 검증)를 *애니 완성 후* 이어받아 무대 조립 → 플레이게이트까지 가는 문서.
**먼저 읽을 것:** `docs/00_authority/2026-06-16-vampire-survivors-pivot-spec.md`(척추) · `docs/01_handoffs/2026-06-16-vampire-pivot-handoff.md`(피벗 착수점) · 메모리 [[katana_core_loop_bolt]].

---

## 1. 한 줄 현황

이 세션 = **첫 볼트 설계를 유저와 대화로 확정.** 코드 **미착공**(유저 판정 = 애니 대기). 무기·입력 모델·재활용 범위·세션 경계를 못 박았다. 카타나 애니가 *다른 세션*에서 작업 중이고 애니가 손맛 핵심이라, **애니 완성 후 합류해 한 번에 검증**한다.

## 2. 확정된 설계 (동결 후보 — 유저 합의)

- **무기:** 카타나 근접 (총기 본체는 후속 볼트로).
- **입력 모델:** **누를 때만 발동** (자동연사 ❌, FPS/액션게임식). 연속공격 = **한 클릭당 1타**, 클릭 연계 **타이밍 윈도우** 존재, **늦게 누르면 콤보 종료.**
- **손맛 정체성:** 위치(카이팅) + 조준 + **입력 타이밍 숙련** 동시 요구 = 일반 뱀서(위치만 신경쓰는 자동공격)와 갈리는 **차별점**. 유저 표현 = "정석적인 액션성".
- 참고 레퍼: 엠버 앤 블레이드(자동베이스+수동액션레이어). 단 우리는 더 수동(누를 때만) 쪽.

## 3. 재활용 맵 (생존 자산 — 탐색 확인됨, 파일 경로)

| 시스템 | 파일 | 비고 |
|---|---|---|
| 스폰 디렉터 (시간→초당스폰 곡선) | `Assets/_Project/Scripts/ZombieSpawner.cs` + `Data/EscalationProfile.cs` | ⚠️`RunManager` 페이즈게이트(InMission/Extracting=익스트랙션 잔재) 제거 필요 |
| XP→레벨업 루프 | `XPManager.cs`(-100 싱글톤), `XPOrb.cs`, `ZombieController.SpawnXPOrbs()` | 이벤트 구독 OnDestroy 해제 주의 |
| 레벨업 카드 선택 | `LevelUpChoiceUI.cs` | 6종 기본카드 완성, timeScale=0 멈춤·30s 타임아웃 안전망 |
| 적 스워머 (단순화 후보) | `CaniathroxChaser.cs` → 단순화 | 예측조준 leadTime 제거, Separation+Surround만, `AttackTokenPool`로 동시공격 제한 |
| 근접 판정 | `KatanaController.SwingFan` | ⚠️**애니 세션 영역 — 이 볼트에선 안 건드림** |
| 플레이어 | `PlayerController.cs`(이동+대시), `PlayerStats.cs`(강화 레이어) | |
| 투사체 풀 (보류) | `ProjectilePool.cs` | 근접 첫 볼트엔 불요. 총기 볼트에서 |
| 씬 | `Assets/_Project/Scenes/Labs/Greybox_Arena.unity` 복제 | 열린 평면 |

## 4. 세션 경계 (병렬 충돌 방지 — ★중요)

- **애니 세션(다른 쪽):** 카타나 공격 모션·Animator 상태머신·콤보 윈도우 AnimationEvent·`KatanaController` 애니훅.
- **이 볼트(무대):** 새 그레이박스 씬·`ZombieSpawner`(게이트 제거)·적 스워머 단순화·XP/레벨업/카드 배선·플레이어 이동 셋업.
- **교집합 위험점 = 플레이어** (애니 세션이 플레이어 Animator를 만짐) → 무대 작업은 **Animator/KatanaController 미수정**, 씬 인스턴스 배치·비(非)애니 컴포넌트만.

## 5. 착수 트리거 & 다음 작업자 체크리스트

**트리거 = 카타나 애니 완성.**

- [ ] 피벗 스펙 + 이 핸드오프 + 메모리 [[katana_core_loop_bolt]] 읽기
- [ ] `Greybox_Arena` 복제 → 열린 평면 무대 씬
- [ ] `ZombieSpawner` 페이즈게이트 제거 (RunManager 의존 끊기)
- [ ] `CaniathroxChaser` → 스워머 단순화 (leadTime 제거, Separation+Surround)
- [ ] XP→레벨업→카드 배선 (기존 자산 연결, CarController 레거시 PitStopZone 의존 주의)
- [ ] 카타나 애니 합류 → 콤보 입력/판정 레이어 연결 (누를때만+윈도우)
- [ ] **플레이게이트:** "30초 갈기 + 레벨업 픽" 손맛 — 유저 플레이 판정. unity-mcp(2.6.0-pre.1, 작동확정)로 캡처/검증

## 6. 주의점 (탐색에서 확인)

- `ZombieSpawner`는 RunManager 페이즈 없으면 무한 스폰 (테스트 씬에선 괜찮으나 가드 확인).
- `XPManager` 싱글톤 -100 우선순위 **유지**.
- `LevelUpChoiceUI`/`UpgradeMenuUI` timeScale=0 멈춤/복구 안전망 필수 (영구화 방지).
- `PitStopZone`의 `CarController` 의존 = 도보 게임에 안 맞음 → 필요 시 PlayerController로 교체.
