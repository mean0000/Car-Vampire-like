---
name: venodonte-attack-statemachine
description: Venodonte 사수 상태머신 v1 — Idle→Reposition→Aim→Fire(이벤트3발)→Idle. 예측 안 함(정지사격 처벌), Caniathrox 추격과 정반대 철학
metadata:
  type: project
---

# Venodonte 사수 공격 v1 (2026-06-13) — 원거리 사격 두번째 틀 (근접 Caniathrox 짝)

## 컨트롤러: `Assets/_Project/Animations/VenodonteAttack.controller` (guid dff87fee8d8b41d459a5b19cf7cfc2ab)
디스크 직접 작성 YAML(MCP AddState 디스크영속 함정 회피) + ForceUpdate + 재로드검증 통과(param2·state4·default Idle).
파라미터: **isMoving(bool), aim(trigger)**. 상태 4개. 전부 applyRootMotion·WriteDefaults·CUT(dur0).

## 상태 다이어그램 v1
```
Idle(default, Venodonte@Idle, in-place)
  --[isMoving==T, CUT dur0]--> Reposition
  --[aim trigger, CUT dur0]--> Aim
Reposition(CrawlForward_RM, 루트모션 전진)
  --[isMoving==T, ExitTime0.96 CUT]--> Reposition   (비루프 crawl 자기루프 — 이동 지속)
  --[isMoving==F, CUT dur0]--> Idle
Aim(Taunt 머리들어올림, state speed=1·드라이버가 anim.speed=3.3로 압축)
  --[ExitTime0.45 CUT, 무조건]--> Fire               (조준 끝→자동 발사, 트리거 불요)
Fire(3AcidShotCombo 클론[이벤트3발], in-place)
  --[ExitTime0.95 CUT, 무조건]--> Idle               (3연 끝→Idle 쿨다운)
```
풀시퀀스 Animator 시뮬 검증: Idle→Aim(f6)→Fire(f34)→Idle(f108). **이벤트 3발 norm 0.23/0.43/0.63**(스러스트 정점). **정체성 동작(Aim/Fire) 중 IsInTransition=0**(두 클립 안 섞임, 제0원칙 ✓). 모든 전이 dur0.

## 드라이버: `Assets/_Project/Scripts/VenodonteShooter.cs`
★Caniathrox 추격과 **정반대 철학**:
1. **예측 안 함(정지사격 처벌)**: 글롭은 발사 *순간*의 플레이어 위치로 직사(pos+vel 리드 ❌). FireAcidGlob(이벤트콜백)이 그 순간 위치 읽음. 멈춰 쏘면 정통·움직이면 빗나감. (Caniathrox는 Coil 중 미래위치 예측 — 반대.)
2. **사거리 유지(약하게, LV1)**: preferredRange 9·band 2.5. 밴드 밖이면 Reposition(가까우면 등져 후퇴=전진 루트모션이 멀어짐, 멀면 정면). 순수 카이터 아님 — 군체에 섞임. 히스테리시스(band×0.6)로 떨림 방지.
3. **회전 경계(헌법)**: Reposition·Aim 중 yaw O(방향=AI 의도). **Fire 중 회전 0**(글롭은 이벤트 순간 방향으로 이미 고정 발사). 경계질문="발사된 글롭 궤적을 코드가 휘나"→Fire 진입 후 No.
4. **속도 단일진실원**(Caniathrox 패턴 복제): Update 맨 위 anim.speed=1f, Reposition에서 moveSpeed/CrawlNative(2.940)·Aim에서 aimSpeed(3.3)만 올림. 이탈 경로 누수 0.
5. 군중 AI: separation Roster(자기종 회피) + AttackTokenPool 공유(동시 사격 ≤maxTokens, 탄막밀도 규율 직사 사수 ≤4).

## ★시작 노브값 (전부 유저 플레이 튜닝 대기)
preferredRange 9·rangeBand 2.5·moveSpeed 3.0·cooldown 1.0·turnSpeed 300·**globSpeed 7**(정지사격 처벌 공정성 핵심)·globRange 16·muzzleHeight 0.9·muzzleForward 0.5·aimSpeed 3.3(윈드업 ~0.47s)·separationRadius 2.0. 스포너 enemyCount 5·maxAttackTokens 2.

## 미검증(유저 ▶): 사격 속도감(굼뜬가)·"군체에 섞이는 사선" 체감·정지사격 처벌 공정성(globSpeed 7이 위빙 가능선인가)·Aim 윈드업이 위협 예고로 읽히나·글롭 가산발광 가독성(블룸).
[[venodonte-clip-kit]] [[projectile-pool-pattern]] [[animevent-fire-timing]] [[caniathrox-attack-statemachine]]
