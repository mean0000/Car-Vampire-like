---
name: caniathrox-crowd-ai
description: Caniathrox 룩 랩 군중 추격 AI v2 — Approach 중 회전 허용(헌법 재해석)+4기법(steering/separation/surround/token), Lunge/Spit 회전 0 엄수
metadata:
  type: project
---

2026-06-13 Caniathrox 룩 랩(LabCombatSpawner) 군중 추격 AI 재설계. v1은 "Approach 중 회전 금지"라 직진하다 멈춰야만 재조준 → "바보"(유저 질책). v2로 표준 군중 근접 AI 적용.

**★헌법 재해석 (오케스트레이터 판단 — 핵심):**
"코드가 위치/포즈를 안 만든다"(제2원칙)는 유지하되, **로코모션(Approach) 중 플레이어를 향한 회전(steering)은 허용**. 회전=위치/포즈가 아니라 *방향*(AI 의도). 루트모션=전진, 코드 회전=방향 = Unity 표준. 단 **정체성 동작(Lunge=도약, Spit) 중엔 회전 절대 0**(궤적 보존). 회전은 Approach 상태(`info.shortNameHash == SApp`)에서만. Lunge/Spit 분기는 Update에서 아무것도 안 함(상태머신이 완결).

**Why:** v1의 "직진+멈춰야 재조준"이 군중을 바보로 만듦. 회전을 방향(의도)으로 재분류하면 제2원칙 위반 없이 곡선 추적 가능. 이게 유일한 헌법 재해석.
**How to apply:** Caniathrox/룩 랩 추격 AI 손볼 때, Approach 중 model.rotation 조향은 OK(RotateTowards/Slerp). Lunge/Spit 중 회전은 절대 추가 금지. 상태머신(CaniathroxAttack.controller: IdleAngry→Approach→Lunge→Spit, 4상태 정상)·CaniathroxAttackDemo(원본 데모)·PlayerCombat·ZombieController는 수정 금지.

**적용 4기법 (CaniathroxChaser.cs):**
1. Steering(seek): Approach 매 프레임 SteerDirection()으로 turnSpeed(도/초) RotateTowards.
2. Separation: static Roster(활성 Chaser 리스트, OnEnable/OnDisable 등록) 순회, separationRadius 내 동료 회피 벡터 ×separationWeight를 seek에 합성.
3. Surround: 플레이어 직타격 X. SlotTargetPoint()=플레이어 둘레 surroundRadius 링 위 한 점. 스포너가 slotAngleDeg를 ±60° 분산 주입(SetSurroundSlot).
4. Attack Token: 신규 AttackTokenPool.cs(순수 C# 객체, MonoBehaviour 아님). 스포너가 풀 1개 만들어 모든 Chaser에 같은 참조 주입. 도착+TryAcquire 성공한 적만 Lunge, 나머진 Approach 유지. IdleAngry 복귀 시 Release(=ReleaseToken). OnDisable에서도 Release(누수 방지).

**노브 전수(SerializeField, 기본값):** Chaser — lungeRange 5.0, restBeforeApproach 0.6, chaseRange 0(항상추격), turnSpeed 360, separationRadius 2.5, separationWeight 1.0(Range0~3), surroundRadius 1.6, slotAngleDeg 0(스포너 주입). Spawner — maxAttackTokens 2(Range1~6), enemyCount 6, spawnRadius 12, radiusJitter 2.

**컴파일 클린 검증:** AttackTokenPool TryAcquire/Release 로직 T T F T 실측 통과, 신규 멤버(tokenPool 필드/SetSurroundSlot) 직접 참조 컴파일 OK, 콘솔 에러 0. 실제 플레이(포위·분리·동시공격 제한 체감)는 유저 ▶ 확정 — [[runtime-spawn-wiring]] 함정2(하니스 플레이모드 paused)로 모션 자동검증 불가.
