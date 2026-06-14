---
name: project-dimaxillosaurus-clip-kit
description: Dimaxillosaurus (LV3 포식자, 직립 클로) 클립 킷 실측 — 루트모션·★좌우 단발/콤보 컨택 타이밍. 세 번째 몬스터 틀(근접). ★현행=벽처럼 오는 클로월 + ★스윙/회수 SPLIT(트림 폐기): 각 단발=Swing(0~22f,자연1.0)+Recovery(22~35f,배속3.0).
metadata:
  type: project
---

Dimaxillosaurus = LV3 포식자, **직립 2족 클로 포식자**. Caniathrox(돌진)·Venodonte(원거리)에 이은 **세 번째 틀 = 근접 "정직한 접근-정지-스윙"**(돌진/도약 아님). 클립 경로: `Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 7/Dimaxillosaurus/FBX Files/`. 프리팹: `.../Dimaxillosaurus/Prefab/Dimaxillosaurus.prefab` (단일, Venodonte처럼 Tint 변형 없음). ★머티리얼 = **Standard 셰이더**(guid 933532a4…=빌트인) → URP 변환 필수(마젠타).

## 루트모션 실측 (Animator 스텝/SampleAnimation — 정적커브 거짓, 사고#2 재발방지)
| 클립 | 길이 | 전진(z) | 측면 | 상승 | 용도 |
|---|---|---|---|---|---|
| **LeftClawsAttackForward_RM** (풀) | 1.1667s (35f@30) | 2.2177m | 0 | 0 | 원본 봉투. L발톱 컨택 norm 0.350(절대 프레임 12.25). ★전진 75%가 norm 0.45(z1.66m)에 완료 — 뒤 55%는 +0.56m 슬금(=느린 회수=L↔R 흐름 끊는 죽은구간) |
| **★(폐기)트림 18f/26f** | 0.600/0.867s | 78%~ | 0 | 0 | 🟥(구)끝트림 — 회수를 *버림*(거리손실). 유저 "자르지 말고 회수만 빠르게 재생"으로 ★SPLIT으로 교체(아래). 트림 const 전부 제거됨 |
| **★★(구)L/R Swing (SPLIT 0~22f)** | 0.733s | ~1.874m | 0 | 0 | 🟥(구)2분할 스윙(speed1.0 자연). ★v7에서 이즈 4분할로 교체(아래). 하드 1.0→3.0 점프가 "휙 채서 어색" 우려라 유저가 "이즈 곡선으로 빠르게"로 방향 정정. 보존 |
| **★★★L/R 이즈 4분할 (v7, 2026-06-14)** | Windup0~9f(0.300s)/Strike9~16f(0.233s)/FollowOut16~22f(0.200s)/Recovery22~35f(0.433s) | 합=풀2.218m | 0 | 0 | ★★★현 정체성 동작. 같은 take를 4분할, **구간별 정적 speed의 계단형 이즈 근사**(per-frame 코드 speed 곡선=헌법위반). 램프 **1.9→1.35→2.3→2.5**(앞 빠르게→읽히는 히트→뒤 빠르게→매끄러운 회수). ★ClawHit은 **Strike**만(norm L0.464·R0.549 = (컨택프레임-9)/7, 검증 L@0.108s=절대frame12.25·R@0.128s=12.85 불변). 경계 frame9/16/22 비트동일→4 CUT 연속. 루트모션 손실0 |
| 2HitComboClawsAttackForward_RM | 1.967s | 4.607m (2.343 m/s) | 0 | 0 | (구)전진 2힛 콤보 — 유저 "더 연속적·더 전진" 요청으로 단발 교대로 교체. 보존 |
| 2HitComboClawsAttack (in-place) | 1.833s | 0 | 0 | 0 | (구)제자리 콤보 — "공격 맞을 상황 안 남"으로 교체됨. 보존 |
| LeftClawsAttackForward / RightClawsAttackForward (비RM) | — | **0** (추정) | — | — | Forward인데 제자리 위험(콤보 비RM 전례) — ★_RM만 전진. 단발도 _RM 써라 |
| Run_RM | 0.600s | **8.000m** (13.3 m/s!) | 0 | 0 | 질주 접근(매우 빠름 → animator.speed로 감속) |
| WalkForward_RM | 1.000s | 2.038m (2.04 m/s) | 0 | 0 | 보행 접근(차분) |
| Roar | 2.833s | 0 | 0 | 0 | ★앵티시페이션/위협 텔레그래프(Venodonte Taunt 위치). 오프너 1회 |

## ★★단발 클로(Left/RightForward_RM) 컨택 타이밍 (SampleAnimation 본 추적, 2026-06-14 — 현 정체성 동작)
- 측정법: `clip.SampleAnimation(go, t)` 로 포즈 평가 후 손 본 root-local +z reach. **Animator.Update(dt)는 edit모드서 본 포즈 안 씀**(루트모션만 갱신) → SampleAnimation이 정답(본 평가). 단 SampleAnimation은 루트를 transform에 bake하므로 본은 root-local로 읽어야.
- **LEFT = 왼발톱 단발**: Lfinger 전방 reach 정점 **norm 0.350 ≈ 0.408s** (reach 2.506). fwdToContact(스폰norm0.04 z0.026→컨택 z1.237) = **1.211m**.
- **RIGHT = 오른발톱 단발**: Rfinger 전방 reach 정점 **norm 0.367 ≈ 0.428s** (reach 2.453). fwdToContact(스폰 z0.026→컨택 z1.397) = **1.371m**.
- ★단발은 콤보보다 느림(1.901 < 2.343 m/s) → **state.speed 배속 필수**. 배속 S면 실효전진 1.901×S, 단발 1회 1.1667/S초, 거리/회 2.2177m 고정(시간 스케일이라 루트모션 보존). ★윈드업(fill native초)도 S로 나눠야(드라이버가 처리).
- ★컨트롤러 통과 검증 v4(클로월, Animator.Update 스텝, speed=1.45, *풀클립*): 6단발 누적 13.337m = 6×2.223m, LRLRLR 정확. (트림 전 측정 — 보존.)
- 🟥(폐기)v5 트림 검증: speed2.5·트림0.600s에서 지속 3.94 m/s 급등(거리손실+시간단축). 유저 "회수만 빠르게 재생"으로 SPLIT 교체.
- ★★컨트롤러 통과 검증 **v6 (SPLIT, Animator.Update 스텝, Swing speed1.0 + Recovery speed3.0, gap0.18)**: opener Roar→L_Swing(z0.006)→L_Recov(z1.880)→Idle(z2.191)→R_Swing(z2.191)→…→L_Swing#2(z4.389)→… — **enter-to-enter 정확히 +2.198m 등간격, 백슬라이드/틈 0, LRLRLR**. 한 단발 내 Swing carried +1.874m·Recovery carried +0.311m = 2.185m≈풀2.218(리샘플 보정 ~0.03m). ★★루트모션 손실 0(트림과 정반대 — 회수 +0.311m를 *버리지 않고* 3배 빠르게 운반). 지속속도 = 2.198/(0.733+0.144+0.18)≈**2.08 m/s**(=자연 벽 페이스, 걷기5.5 대비 2.6× 마진 복원). ★스윙 자연1.0 = 타격+팔로스루 온전, 회수만 "탁" 압축.
- ★SplitFrame=22 실측근거: L Finger 전방 reach 컨택 peak frame12(2.49), 팔로스루 12~16(2.49→1.95), frame22 reach1.14·rootZ1.90(전진86%)=발톱이 스윙호 지나 중립귀환 시작. R 동일(frame22 reach1.18). f18은 "late 팔로스루"(여전히 앞), f22가 "공격 끝·귀환 시작" 경계.
- ★"더 전진거리"의 진짜 의미 = 거리/회 늘리기 ❌, **빠른 좌우 연타로 회당 2.22m를 끊임없이 누적**(성큼성큼). 거리/회는 루트모션 고정이라 못 늘림 — 페이스(배속)와 체인 연속성이 레버.
- (구)전진 콤보 컨택: Hit1 norm0.217≈0.426s(fwd1.33), Hit2 norm0.633≈1.246s(fwd1.48). (구)제자리 콤보: Hit1 norm0.213, Hit2 norm0.550. 중간 speed 스크럽 금지(헌법).

## 본 이름 (rig)
`Dimaxillosaurus_ L Hand`·`R Hand`·`L/R Finger0/1/2`·`L/R UpperArm`·`L/R Forearm`. (공백 주의: "Dimaxillosaurus_ L Hand")

연동: [[project_caniathrox_clip_kit]](돌진 틀)·[[project_venodonte_clip_kit]](원거리 틀)·[[feedback_measure_rootmotion_by_stepping]]·[[project_telegraph_driver_dimax]]
