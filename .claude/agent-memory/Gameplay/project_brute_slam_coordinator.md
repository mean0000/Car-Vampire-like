---
name: brute-slam-coordinator
description: 거대 브루트(Crassorrid) 동시 슬램 조율 — 수 게이팅(토큰)→각·박자 분산. BruteSlamCoordinator 정적 패턴, 플랭킹 재배치, stale 방위 누수 차단
metadata:
  type: project
---

2026-06-14 Crassorrid(LV4 7m 브루트) 공격 조율을 **AttackTokenPool 수 게이팅 → 각·박자 분산**으로 전환. 권위 = `docs/02_logs/2026-06-14-large-enemy-ai-research.md §C`(유저 확정 Option 1, 웹리서치 DOOM 토큰빼앗기·Aztez 각규칙).

**문제**: 토큰으로 동시 슬램 *수*를 막으니 토큰 못 잡은 거구가 기웃기웃 맴돌아 어색. 소수 거구는 동시 슬램 OK — 막을 건 "같은 각/같은 순간 슬램"뿐.

**해법 = `BruteSlamCoordinator.cs`(정적 클래스, MonoBehaviour 아님 — AttackTokenPool과 같은 결):**
- `ActiveSlamAzimuths` Dictionary<object,float> = 슬램 중 브루트→플레이어 기준 방위(도). `_lastCommitTime` 공유.
- `CanSlamNow(self, myAz, angleSpread, staggerMin)`: ①angleSpread(90°) 내 슬램 중 피어 있으면 false(각 충돌) ②마지막 커밋부터 staggerMin(0.2s) 미만이면 false(박자 충돌). ★피어 0(혼자)이면 즉시 true = 1기일 때 즉시 침.
- `OpenestAzimuth(self, myAz)`: 24샘플(15°) 스캔으로 슬램 중 피어 방위들에서 최소 각거리 최대인 빈 틈 반환. 실측: 피어 0°·90° → 225°(양쪽 135° 등거리). 동률 시 현재 방위 가까운 쪽(불필요 우회 방지).
- `RegisterSlam`/`Unregister`. `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`로 플레이마다 정적 리셋.

**CrassorridBrawler 배선:**
- 노브 SerializeField: angleSpread(90), staggerMin(0.2), staggerMax(0.4 정보용), repositionAggression(1). 런타임 AddComponent라 코드 default 먹음.
- 커밋 결정(SApproach, 도착): `tokenPool.TryAcquire()` 게이트 제거 → `CanSlamNow` 질의. 통과=PSmash + **즉시** RegisterSlam(Windup 진입 1프레임 지연 ❌ — 그 지연 동안 같은 프레임 평가한 다른 브루트가 빈 등록부 보고 같은 각 동시커밋하는 race 차단). 막힘=`RepositionToFlank`.
- **플랭킹 재배치(맴돌기와 다름)**: 막힌 브루트는 OpenestAzimuth로 빈 방위 찾아 `slotAngleDeg`를 (openAz - 현재방위) 델타로 *설정*(누적 ❌ — 누적하면 자기꼬리 추적, 설정이라야 openAz로 갈수록 0 수렴). Approach 스티어(SlotTargetPoint/Steer)가 거기로 데려감 = 굼뜬 거구의 의도적 측면 미끄러짐.
- **stale 방위 누수 = 제일 위험**: 죽은/풀회수 브루트 방위가 남으면 산 브루트를 영영 막음. 해제 3중: ①SRecovery 진입 UnregisterSlam ②OnDisable UnregisterSlam ③Update 상단 엣지가드(슬램상태 아닌데 등록돼 있으면 해제 — Strike 건너뛴 비정상복귀 자가치유, `_windupSpawned` 가드와 같은 결). `_slamRegistered` bool로 비대칭/중복 해제 차단.

**tokenPool 처리**: 수 게이팅 *기능*은 끔. `tokenPool` 필드·`_holdsToken`·`ReleaseToken`은 inert 잔존(스포너 주입 호환·다른 종 공유 풀 보존). `_holdsToken`은 이제 never-true라 ReleaseToken은 no-op. AttackTokenPool 클래스는 Caniathrox가 쓰므로 보존.

**검증**: RunCommand API 레벨 전수 통과(solo 즉시·같은각 막힘·다른각 OK·스태거 막힘·OpenestAzimuth 225°·Unregister 해제). 컴파일 클린. ★런타임 모션(동시 슬램 겹치되 읽히나·기웃거림 사라졌나·플랭킹 위협적인가)은 유저 플레이 판정 — 하니스 플레이모드 paused라 모션 자동검증 불가([[runtime-spawn-wiring]] 함정). ★미커밋(유저 판정 대기).

**불가침**: 슬램 모션·상태시퀀스·루트모션·회전경계(Strike/Recovery 회전 0)·텔레그래프·임팩트 주스 전부 불변 — 이건 그 *위* AI 조율층.
