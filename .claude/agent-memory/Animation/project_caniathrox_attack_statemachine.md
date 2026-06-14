---
name: caniathrox-attack-statemachine
description: Caniathrox 공격 v7 상태머신 — 거리분기(Bite/Lunge)+Coil 응축("모았다가 팍")+Coil 중 플레이어 예측조준(요격). 헌법 준수
metadata:
  type: project
---

# ★v7 추가 (2026-06-13): Coil 중 플레이어 이동 예측 조준 (target leading)
유저 피드백: 도약이 발사 시점 *현재* 위치로 가서 옆으로 움직이면 빗나감 → "움직임을 예측해 점프(요격)". **점프(Coil→Lunge) 경로에만** 적용, 물기(Bite)는 근접이라 무변경.
- **드라이버 로직만**(`CaniathroxChaser.cs`), 컨트롤러·speed·클립 0변경. 상태 6개·전이 그대로.
- **속도 추적**: target에 `LabPlayerController` 있으면 그 `PlanarVelocity`(이미 SmoothDamp된 공개 getter — ★플레이어 안 건드림, 이미 노출돼 있었음) 사용, 없으면 위치델타/dt 폴백. 어느 쪽이든 한 겹 더 SmoothDamp(`velocitySmoothTime` 0.12s)로 평활 → 예측 방향 안 펄럭임. `TrackTargetVelocity()`를 Update 상단 상시 호출(상태 무관).
- **예측점**: `predicted = target.pos + 평활속도 × leadTime`(선형 lead, 2차 intercept는 과함=게임 표준). `leadTime` SerializeField **시작값 0.5s**(Coil 실시간 0.42s + Lunge 전진 일부).
- **★헌법 경계(오케스트레이터 판단)**: Coil(응축)=발사 전 *조준* 단계라 yaw 회전 허용(Approach steering의 연장). 새 분기 `else if (SCoil)`에서 `predicted` 향해 `turnSpeed`로 RotateTowards. **Lunge(발사) 진입 후 회전 0 절대** — 조준된 방향으로 직선 발사, 궤적 보존(제2원칙). Coil은 제자리(JumpCoil 루트모션 0)라 회전이 위치를 안 만듦. 경계 판정질문="발사 궤적을 코드가 휘나"(Coil=No 조준일뿐, Lunge=절대No).
- 미검증(유저 플레이): 움직이는 플레이어 실제 요격되는지, leadTime 0.5s 적정(과하면 헛다리/작으면 현위치), velocitySmoothTime 0.12s 반응성.

# Caniathrox 공격 v6 — Coil 응축 추가 (2026-06-13)

## ★v6 변경 (v5 → v6): "점프가 개구리처럼 폴짝" 유저 피드백 → "모았다가 팍 달려들기"로 교정
유저 직접 플레이: Jump_RM의 위로뜨는 Y(0.278m 포물선)+윈드업 없음+착지 꼬리 → 개구리. 3박자 pounce로 교정:
1. **모았다가**=Coil 상태 신규(JumpCoil 0.167s in-place, **state speed 0.4** → 실시간 0.42s=느린 충전). ★에셋 YAML 실측값(이전 메모 0.6은 오기).
2. **팍**=Coil(느림) → Lunge(빠름, **state speed 1.8** → 실시간 0.46s) 타이밍 대비. state speed는 정적 상수라 코드가 매프레임 안 긁음(헌법 안전). ★에셋 YAML 실측(이전 메모 1.3은 오기).
3. **낮게 깔린 돌진**=Lunge 클립을 Jump_RM→**JumpLunge_RM**(Y bake, maxY 0)로 교체. 전진 4.67m 유지·위로 안 뜸.
- **Coil→Lunge 전환은 ExitTime1.0 무조건 CUT** — 응축 끝나면 자동 발사. 드라이버가 2번째 트리거 안 쏨 → "모았다가 팍"이 한 호흡. 드라이버 로직 0변경(attack 트리거가 이제 Lunge 대신 Coil로 감, 주석만 갱신).
- 풀시퀀스 Animator 시뮬 검증: Coil/Lunge 전구간 IsInTransition=False(두 클립 안 섞임, 제0원칙 ✓), Lunge maxY 0.000·dZ 4.585.

## v5 토대 (유지): 점프 상수거리 오버슈트 = 거리 분기
도착 시 거리로 물기/도약 가름. Jump_RM 4.67m 고정이라 가까우면 오버슈트 → biteRange 경계.

**컨트롤러:** `Assets/_Project/Animations/CaniathroxAttack.controller` (벤더 Caniathrox_Controller 안 건드림)
**드라이버:** `Assets/_Project/Scripts/CaniathroxChaser.cs` (군중 AI: steering+separation+surround+token. 상태머신 파라미터만 구동)
**플레이어:** `Assets/_Project/Scripts/LabPlayerController.cs` (룩 랩 전용, 게임 PlayerController와 무관)

## ★v5 변경 (v4 → v5): 점프 상수거리 오버슈트 해결 = 거리 분기
유저 직접 플레이 피드백: Jump_RM 전진 4.67m 고정이라 가까운 플레이어를 지나쳐 뒤로 착지(오버슈트), 너무 가까우면 점프 안 함. → **도착 시 거리로 물기/도약을 가른다.**

## 상태 다이어그램 v6 (전부 applyRootMotion=true, WriteDefaults=true, 정체성 전이 CUT dur0)
```
IdleAngry(default) --[isApproaching, BLEND 0.12]--> Approach(Run_RM, state speed=1.0)
Approach --[isApproaching, ExitTime0.98 CUT]--> Approach        (Run_RM 비루프 → 자기루프로 접근 지속)
Approach --[attack trigger, CUT dur0]--> Coil(JumpCoil, speed 0.4)   (멀리: dist >= biteRange) ★v7: Coil 중 predicted 향해 yaw
Coil --[ExitTime1.0 CUT, 무조건]--> Lunge(JumpLunge_RM, speed 1.8)    ★응축 끝→조준방향 직선발사(회전0)
Approach --[bite   trigger, CUT dur0]--> Bite(BiteForward_RM)        (가까이: dist < biteRange)
Lunge --[ExitTime0.92 CUT]--> IdleAngry
Bite  --[ExitTime0.92 CUT]--> IdleAngry
Spit  : 고아(전이 0개) — 보존만. 나중 원거리 공격용.
```
파라미터: isApproaching(bool), attack(trigger), bite(trigger). **상태 6개**(v6: Coil 추가). 컨트롤러 YAML 실측: Lunge guid=JumpLunge_RM(11a41d8...) **speed1.8**, Coil guid=JumpCoil(b86d67d...) **speed0.4**.

## ★핵심 교훈
1. **거리 분기는 드라이버가 도착 시 1회 판정** (PlanarDistanceToTarget() < biteRange ? bite : attack). 상태머신엔 두 트리거 전이를 Approach에 병렬로 둔다. 둘 다 토큰 필요·CUT·완결.
2. **접근 속도는 코드 라이브 노브**: Approach 상태 speed는 **1.0으로 두고**(배율 누수 방지), 드라이버가 `modelAnimator.speed = approachSpeed / RunNativeSpeed(4.0942)`를 **Approach 상태에서만** 적용. ★속도 단일 진실원 = Update 맨 위에서 매 프레임 `modelAnimator.speed=1f` 리셋 후 Approach 브랜치에서만 올림 → 이탈 경로(target null/Idle/Lunge/Bite) 전부에서 배율 안 샘(Codex 지적 수정). modelAnimator.speed는 Animator 전역이지만 Chaser마다 자기 Animator라 안전.
3. **Bite=BiteForward_RM**(전진 1.328m, 제자리 아님). 가까울 때 살짝 파고들며 무는 프로파일. Lunge=Jump_RM(전진 4.67m·상승 0.278m).
4. **시작 노브값**: biteRange=2.5m, lungeRange(도착판정)=5.0m, approachSpeed=7.0m/s. 플레이어 walkSpeed=5.5 / sprintSpeed=9.0(Shift 홀드). 적이 걷기<적<질주 사이. 전부 유저 플레이 튜닝 대기.

## ★디스크 영속화 (v5도 통과) — SaveAssets+ImportAsset(ForceUpdate)+재로드 검증
RunCommand로 AnimatorController 로드→AddState/AddParameter/AddTransition→SetDirty→SaveAssets+Refresh+ImportAsset(ForceUpdate). 검증: 재로드 LoadAssetAtPath로 param3개·state5개·default=IdleAngry·전이 확인 + **.controller YAML 직접 Read**로 `m_Name: Bite`·`guid: 2db73d511566ae048b694977ec339c2d`·`m_ConditionEvent: bite`·`m_Speed: 1.71`(이후 1.0으로 리셋) 바이트 확인. v5 재로드 검증 통과(빈 껍데기 아님).
**함정 재확인**: RunCommand에 `using System.Reflection;` 절대 금지(하니스 즉사). / `CreateAnimatorControllerAtPathWithClip`·`AnimatorOverrideController` 등 디스크 에셋 생성 API는 "User interactions not supported" MCP 에러 → 측정/임시 컨트롤러는 **in-memory `new AnimatorController()` + DestroyImmediate**로.

## ★MCP 플레이모드 진입 불가 (블로커 유지)
RunCommand에서 isPlaying/EnterPlaymode 안 먹음. 라이브 검증(VFX·모션 느낌·실제 거리분기 작동)은 **유저 Play**. 구조·루트모션·블렌드 비혼합은 에디터 Animator.Update 스텝 시뮬로 증명.

## 미검증(유저 플레이): 거리분기 체감(2.5m 경계가 자연스러운가), approachSpeed 7.0 체감, 오버슈트 실제 해소, Bite 1.33m가 "닿는" 느낌인지.
