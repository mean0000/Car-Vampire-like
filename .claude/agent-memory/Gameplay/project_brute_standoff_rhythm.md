---
name: brute-standoff-rhythm
description: Crassorrid 1:1 교전 리듬 재설계 — 스탠드오프 정렬+정면 커밋(지나침 방지), 느린 회수, 큰 딜레이, 뱅뱅 금지(마주보기) vs 플랭킹(대기) 합성
metadata:
  type: project
---

2026-06-14 Crassorrid(LV4 7m 브루트) 1:1 교전 리듬 재설계. 유저 플레이 진단 = 지나침/뱅뱅/멀뚱 3병폐. 권위 = `docs/02_logs/2026-06-14-large-enemy-ai-research.md §D`(이동 레이어). [[brute-slam-coordinator]]의 *위* AI 정렬층(슬램 모션·텔레그래프·임팩트 불가침).

**유저 설계**: "정면에 있어야 진짜 손으로 찍는다. 정면에서 멀뚱은 안 되니 느린 회수+큰 딜레이로 채운다. 다시 칠 땐 지나치지 않게."

**★스탠드오프 ↔ 실착탄 정합 (핵심 — 어긋나면 지나치거나 안 닿음, 실측 검증):**
- 슬램 = 전방 `telegraphForwardOffset`(2.5m) 착탄(텔레그래프=데미지 약속) + 본체 `SmashAttack_RM` 전진 3.514m(전체 클립).
- `slamStandoff`(3.5m, =슬램 전진)에서 커밋해야: 텔레그래프 중심 = 3.5-2.5 = **1.0m 앞**(반경 8m라 플레이어 깊이 포함=닿음), 본체 착지 = 3.5-3.514 = **-0.01m**(플레이어 막 못 넘음=지나침 0).
- 밴드 [2.5, 4.5]: near에서 본체 -1.01m, far에서 0.99m → **밴드 전 구간 본체가 플레이어 안 넘음**. 정합 견고.

**CrassorridBrawler 신규 노브(SerializeField, default — 런타임 AddComponent라 코드 default 먹음):** `slamStandoff`(3.5), `standoffBand`(1.0, ±폭), `frontConeDeg`(30, ±반각), `backoffAggression`(0.8), `restBeforeApproach`(0.5→**1.2**). `smashRange`(4.0)는 *dead*(옛 상한 게이트 폐기, 폴백 잔존 — SerializeField라 씬값 보존 위해 안 지움).

**Approach 결정 트리(재설계 — `_smashFired` 아닐 때):**
1. `CanSlamNow`(조율자) 막힘 = **대기 브루트** → `RepositionToFlank`+`Steer()`(슬롯 공전=목적지 있는 플랭킹, 마주보기 ❌). 이건 공전 아님(이미 구현, 보존).
2. 조율자 허용 = **슬램 차례 브루트** → `FacePlayerTurn()`(슬롯 무시, 플레이어 직접 마주봄=제자리 회전=뱅뱅 금지) + 반경 정렬:
   - 밴드 안 & 정면콘 안 & 쿨다운 경과 → **커밋**(즉시 RegisterSlam, race 차단).
   - `d < nearEdge`(너무 가까움=지나침 거리) → **백오프**: `modelAnimator.speed = -(approachSpeed/RunNativeSpeed)*backoffAggression`. Run_RM `loopTime:1`이라 음수 speed 역재생 깨끗(경계 stall 없음, 실측). 마주보기 유지.
   - `d > farEdge` → 전진(마주본 채 직선). · 밴드 안 정면 아님/쿨다운 중 → speed 0(코일된 준비 자세, 멀뚱 아님).

**★느린 회수**: `RecoverySpeed` const 1.4→**0.65**. frame30~50(0.667s native)→1.026s 묵직. ★const 변경 → **SetupData 재실행** 필수(빌드스크립트가 const를 SmashRecovery state.speed로 박음). 검증: 컨트롤러 SmashRecovery speed=0.65 확인. Windup 0.6/Strike 3.4 불변(손드는건 보통/내려찍는건 빠름 유지, 회수만 느리게).

**★큰 딜레이 = 멀뚱 아님**: `restBeforeApproach` 1.2 = 슬램 재커밋 쿨다운(`_slamCooldownUntil`, Recovery 진입 시 `_slamRegistered` 일회 가드로 `Time.time+restBeforeApproach` 무장). Idle은 *즉시* 재접근(rest 타이머 대기 ❌) → 딜레이를 Approach의 백오프+마주보기가 채움. 슬램 직후 런지로 붙은 거리(d<nearEdge)부터 능동 후퇴 = square-up.

**제거된 orphan(내 변경이 만든)**: `_restTimer` 필드+2할당(Idle 읽기 제거됨), `FaceSteer()`(슬롯 기반, 전 호출처 `FacePlayerTurn`으로 교체). `ResetCombatState`에 `_slamCooldownUntil=0` 리셋 추가(풀링 stale 차단). Roar/Idle/Windup의 `FaceSteer`/`Steer`도 `FacePlayerTurn`으로(교전 일관성). `Steer()`는 플랭킹 분기서 유지.

**검증**: 컴파일 클린(에러0/경고0). RunCommand 전수: 정면콘(0/29°=O, 30°경계=X 부동소수, 45/90/180°=X), 조율자(solo T·같은각 F·다른각 T·Unregister후 T=stale누수0), 정합 수치 전부. ★런타임 모션(지나침 사라졌나·정면서만 찍나·느린회수 묵직·뱅뱅 사라짐·멀뚱 아님)은 유저 ▶ 판정 — 하니스 paused라 모션 자동검증 불가([[runtime-spawn-wiring]]). ★미커밋.
