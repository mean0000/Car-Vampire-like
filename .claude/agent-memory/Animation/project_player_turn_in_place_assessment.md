---
name: player-turn-in-place-assessment
description: 탑다운 제자리회전 "동상 회전" — 3접근 견적 + ★2026-07-11 구현 완료(TurnShuffle 중첩 트리 = 기존 8way 스트레이프를 yaw각속도로 부분 셔플. 회전은 계속 코드 소유)
metadata:
  type: project
---

탑다운 45/15m 마우스조준 회전 시 "발 박힌 채 몸만 턴테이블" 어색함 해결.

## ★★2026-07-11 구현 완료 — 접근(b) 정제판(TurnShuffle 중첩 트리)

**유저 판정:** "마우스로 조준을 돌릴 때 발은 가만히 있는데 캐릭터만 도니까 어색" = 정지 중 몸 yaw가 도는 동안 발이 바닥에 박힌 동상 회전.

**채택 = 접근(b) 정제 (스트레이프 재활용), (a)턴클립 기각.** 이유(스탠스 정합이 crux):
- 팩 유일 턴클립 = `Frank_Sword2_17_Turn_45_90_180.FBX`(6서브: L/R×45/90/180, 30f, loop:0, loopBlendOrientation:0=루트회전 미베이크). **★Sword2 = 검+방패 스탠스**(skeleton에 Shield_Bone/Shield_Mesh). 카타나 Stance1과 상체 포즈 상극 → 블렌드 시 팔이 방패자세로 모프 = 카타나 안 읽힘(수용성 위반). 카타나 네이티브 턴클립 **없음**(팩 전수확인).
- 스트레이프(기존 walk 8way S2_Run)는 **현행 로코모션과 동일 스탠스**(무기 OUT)·루프·검증됨 → 새 스탠스 도입 0 = 모프 위험 0. 이게 결정적.

**구조 (컨트롤러 KatanaMelee.controller):**
- Locomotion 톱 Speed 1D 트리(fileID -6802273172499591872)의 **node0(Speed=0) 모션을 idle 직접클립 → 새 중첩 TurnShuffle 트리로 repoint**.
- **TurnShuffle**(신규 embedded BlendTree fileID `-8800000000000000003`, 1D BlendParameter=**TurnRate**), 3자식:
  - thr −1: 좌 스트레이프 guid `6348f4117c4216c4b884c3826df28e26`(walk MoveX−1과 동일 클립)
  - thr 0: idle guid `6623562bfad04384ca221c285a3ffaea`(구 node0 그대로 = 정지 무회전 시 바이트동일, 회귀 0)
  - thr +1: 우 스트레이프 guid `b244cb4aa3b4c1844a9a518843d8d0d2`(walk MoveX+1)
  - (셋 다 fileID 1827226128182048838)
- **신규 파라미터 `TurnRate`(Float, default 0)** 추가. Speed=0서만 유효(Speed↑ 시 톱 트리가 walk로 페이드 = 이동 중 개입 0).

**드라이버(PlayerAnimatorDriver.cs) — 파라미터 공급만(회전 로직 무변경):**
- `_facingRot`(코드 소유 순수 facing) yaw를 프레임 간 **관측만** 해 각속도 산출: `yawRate = DeltaAngle(_prevYaw,curYaw)/dt`. 부호 +우/−좌.
- 게이트 `turnShuffleAllowed = !moving && !_attacking && !IsDashing && !IsActionPlaying`(요구사항 #3: 이동/공격/대시/액션 중 개입 0).
- 매핑: `|yawRate|>turnShuffleThreshold`(데드존=요구사항 '임계')일 때만, `t=clamp01((mag−thr)/(ref−thr))`, `target=sign·t·turnShuffleMax`(+invert 옵션). `SetFloat(TurnRate, target, turnShuffleDamp, dt)`.
- OnEnable서 `_prevFacingYaw` 초기화(재활성 프레임 가짜 스파이크 방지).

**노브(전부 인스펙터):** `turnShuffleThreshold`55(도/초 데드존)·`turnRateRef`200(도/초→최대)·`turnShuffleMax`0.55(강도상한 0~1, ★"달리는 다리처럼" 보이면 낮춤 — S2_Run 클립이라 에너지 있음)·`turnShuffleDamp`0.09·`turnShuffleInvert`(좌우 뒤집기 A/B).

**헌법/요구사항 준수:** 회전 각도·속도는 계속 코드(_facingRot)가 소유(루트모션 턴 아님). 스트레이프 루트변위는 로코모션서 OnAnimatorMove가 폐기 = 발 제자리 셔플(드리프트 0). Base 하체 범위 해결 = UpperBodyCombo 콤보와 무충돌(공격 중 TurnRate=0). 정지 무회전 = idle 바이트동일(회귀 0).

**★유저 플레이로 확정할 것(정지캡처로 못 봄):** ①S2_Run 스트레이프가 turnShuffleMax=0.55서 "은은한 무게이동"인가 "달리는 다리"인가(후자면 max↓) ②turn-right→right-strafe 매핑이 자연스러운가(어색하면 invert) ③turnShuffleThreshold/ref가 15m 부감서 발 셔플이 읽히는 각속도인가. **게이트=Stab+Codex(컨트롤러+드라이버 동작 변경) → 오케 소유.** 커밋 금지. 변경파일=KatanaMelee.controller, PlayerAnimatorDriver.cs.

## (구, 2026-06-18) 접근 견적 — 배경 보존
- Frank `8Way_S2_Run` = facing-relative strafe 세트, In_Place, loop:1. 컨트롤러 Locomotion = 이미 2D Freeform Directional(MoveX/MoveY, 9노드). 드라이버 = 이미 facing 프레임 투영 + faceTurnRate RotateTowards 캡(즉시스냅 아님, 접근1의 '회전캡'은 이미 적용됨).
- 3접근: 1.회전캡+strafe(추천, 인프라완비) 2.AimIK 상체비틀기(근접 칼정렬 깨짐 리스크) 3.턴클립(카타나 네이티브 없음·Sword2는 스탠스상극·이산턴↔연속조준 상극 → 기각). **구현은 1의 strafe재활용을 명시적 TurnShuffle 트리로 정제**(이동 블렌드 오염 없이 별도 TurnRate 파라미터·자체 노브).

연동=[[project_vexa_humanoid_katana_base]] [[project_katana_locomotion_speed_tiers]] [[project_frank_fbx_animevent_gotchas]].
