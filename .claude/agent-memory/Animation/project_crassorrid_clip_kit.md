---
name: project-crassorrid-clip-kit
description: Crassorrid (LV4 정예, 7m 직립 브루트) 클립 킷 실측 — 스매시 루트모션·임팩트 프레임·접근 보행. 네 번째 몬스터 틀(접근형 브루트 + 텔레그래프 첫 소비자).
metadata:
  type: project
---

Crassorrid = LV4 정예, **직립 거구 브루트**(7m). Caniathrox(돌진)·Venodonte(원거리)·Dimaxillosaurus(클로월)에 이은 **네 번째 틀 = "접근형 브루트: 접근→정지→예고원 차오름→내려찍기 광역"**. 시그니처 = 전방 스매시(●r3 원 장판 · ~1.13s 윈드업 · 양팔 들어올려 내려찍기). 클립 경로: `Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack vol 9/Crassorrid/FBX Files/`. 프리팹: `.../Crassorrid/Prefab/Crassorrid.prefab`. ★머티리얼 M_Crassorrid = **이미 URP/Lit**(변환 불필요, 방어적 ConvertMaterialToURP는 생략됨). ★Animator = **루트 GO에 1개**(child[0]=root, child[1]=SK_Crassorrid) → 드라이버 루트 AddComponent ⇒ SmashHit SendMessage 도달 보장.

## 루트모션 실측 (Animator 스텝 + SampleAnimation, 2026-06-14 — 정적커브 거짓 회피, 사고#2)
| 클립 | 길이 | 전진(z) | 측면 | 상승 | 용도 |
|---|---|---|---|---|---|
| **SmashAttack_RM** (풀) | 1.6667s (50f@30) | **3.514m** | 0 | **0(grounded)** | 시그니처 스매시. ★Y float 0 = 거구가 떠오르지 않고 바닥에 박힌 내려찍기(개구리 위험 없음) |
| **★Smash_Windup (0~15f 분할)** | 0.500s | ~0.99m | 0 | 0 | 양팔 1.9→**5.28m** 들어올림(탑뷰 가독 = 머리 위 팔 = 읽히는 브루트 예고). speed **0.5**(느린 무게 → 1.0s 실시간) |
| **★Smash_Strike (15~30f 분할)** | 0.500s | ~1.33m | 0 | 0 | 내려찍기 crash(팔 5.18→0.33m). ★임팩트 frame20(SmashHit norm **0.3333**=(20-15)/(30-15), 검증 @0.167s). speed **1.25**(폭발적) |
| **★Smash_Recovery (30~50f 분할)** | 0.667s | ~1.19m | 0 | 0 | 중립 복귀(팔 1.9m로). speed **1.4**(브리스크) |
| Run_RM | 0.6000s | **5.744m** (★9.5728 m/s 질주!) | 0 | 0 | 접근(Approach). approachSpeed 5.0으로 감속(배율 0.522) |
| WalkForward_RM | 1.3333s | 2.692m (2.019 m/s) | 0 | 0 | (미사용 — 거구 접근엔 너무 느림. Run_RM 감속이 정답) |
| Roar | 4.6667s | 0 | 0 | 0 | ★앵티시페이션/오프너(speed 5.0 압축 ~0.93s) |
| Idle | 3.0000s | 0 | 0 | 0 | 대기/사이클 호흡 |

## ★★스매시 임팩트 실측 (SampleAnimation hand-Y 추적, 2026-06-14 — 내려찍기 핵심)
- 측정법: `clip.SampleAnimation(go, t)` 후 양손(`Crassorrid_ L/R Hand`) 평균 world-Y. 윈드업=팔 올라감(Y↑), 임팩트=팔 최저(Y 최소).
- **hand-Y 궤적**: f0 Y1.9 → f14 Y**5.28**(윈드업 정점, 머리 위) → f20 Y**0.334**(임팩트, 바닥 닿음) → f30~ 회복. fwdZ는 f18 정점 3.71m(앞으로 내지름).
- **★임팩트 = norm 0.40 = frame 20 = t0.667s**(풀클립). 손 최저점 = 내려찍기 바닥 컨택 = SmashHit 이벤트 위치(Strike 분할 norm 0.3333).
- **윈드업 정점 = frame 14(t0.467s)** = 머리 위 양팔(탑뷰 수평 가독 = 읽히는 예고 포즈).
- ★분할 경계 근거: frame15 = 팔 정점 직후(cocking 끝, 내려찍기 시작 직전) = Windup/Strike 경계. frame30 = 손이 최저서 복귀 개시 = Strike/Recovery 경계.

## ★★스매시 3구간 분할 (브루트 무게 셰이핑 — Dimax 4분할과 같은 메커니즘)
- 한 take(SmashAttack_RM)를 frame 범위만 다르게 3 ModelImporterClipAnimation. 같은 take라 경계 포즈(frame15/30) 비트-동일 → CUT(dur0) 포즈 점프 0(crossfade 아님, 헌법 준수). ★루트모션 손실 0(검증: Windup0.99+Strike1.33+...=3.39≈풀3.514, 리샘플 ~0.12 손실).
- ★구간별 정적 speed = 무게 셰이핑(코드 매프레임 스크럽 ❌): **Windup 0.5**(느린 무거운 들어올림 = LV4 텔레그래프 윈도) → **Strike 1.25**(폭발적 내려찍기) → **Recovery 1.4**(브리스크 회수). Dimax(앞·뒤 빠르게 휘릭)와 다름 — 브루트는 *느린 윈드업 + 빠른 슬램*(무게가 떨어지는 가속).
- ★컨트롤러 통과 검증(Animator.Update 스텝): Windup(t0)→Strike(t1.0)→Recovery(t1.4)→Idle(t1.85), **IsInTransition 전구간 False**(두 클립 안 섞임 = 제0원칙 ✓). 풀 사이클 와이어링: Idle→(attack)Roar→(isApproaching)Approach→(smash)Windup→Strike→Recovery→Idle 정확.

## 본 이름 (rig)
`Crassorrid_ L/R Forearm`·`L/R Hand`·`L/R Finger0/01`. (공백 주의: "Crassorrid_ L Hand")

연동: [[project_telegraph_driver_crassorrid]](상태머신·AI·텔레그래프 통합)·[[project_caniathrox_clip_kit]](접근형 틀 원형)·[[project_dimaxillosaurus_clip_kit]](분할 메커니즘 원형)·[[feedback_measure_rootmotion_by_stepping]]·[[project_telegraph_pad_shader]]
