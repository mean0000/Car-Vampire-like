---
name: project-venosaur-clip-kit
description: Venosaur (LV3 호위/물량, 묵직 헌치드 수각 브루저) 클립 킷 실측 — 루트모션·★L/R 전진 비대칭(L 2.413m vs R 4.094m)·컨택 프레임. Dimax 클로월 틀 직재활용(무게 노브만 무겁게). 30프레임/1.0s(Dimax 35와 다름).
metadata:
  type: project
---

Venosaur = LV3 호위/물량, **묵직 헌치드 2족/수각 근육질 브루저**. Dimaxillosaurus(슬렌더 리치 클로, 빠른 "휘릭") **클로월 틀의 직접 재활용**이되 무게 노브만 더 무겁게(둔중 브루저). 클립 경로: `Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 12/Venosaur/FBX Files/`. ★프리팹 = **`Venosaur_Tint_Green.prefab`**(66KB 풀 프리팹 = 베이스, 나머지 Brown/Grey/Purple/Pink는 2.7KB Variant라 이걸 공유). Animator는 **루트 GO("Venosaur_Tint_Green", m_Father:0)에 1개** = OnAnimatorMove 발화 보장(자식함정 없음, 검증). ★머티리얼 M_Venosaur = Standard 셰이더(guid 933532a4…) → URP 변환(이미 변환됨, 변형들 공유).

**Why:** 신규 6종 중 첫 타자 = 양산 틀(Caniathrox·Venodonte·Dimax·Crassorrid에 이은). "묵직 브루저 클로월"로 Dimax 직재활용 검증.
**How to apply:** 다른 클로 종(Carcinoptera 지상 ClawsAttack 등)도 이 패턴 차용. ★단 클립별 프레임 수·L/R 대칭성은 *반드시 실측*(Venosaur는 30f·R비대칭 — Dimax 가정 그대로 쓰면 틀림).

## 루트모션 실측 (SampleAnimation 본추적+루트베이크 — 정적커브 거짓, 사고#2 재발방지)
| 클립 | 길이 | 전진(z) | 측면 | 상승 | 컨택(Finger fwd-reach peak) |
|---|---|---|---|---|---|
| **ClawsAttackLeftForward_RM** | 1.000s (30f@30) | **2.413m** (2.413 m/s) | 0 | 0 | frame 12 / norm 0.400 (reach 2.73) |
| **ClawsAttackRightForward_RM** | 1.000s (30f@30) | **★4.094m** (4.094 m/s) | 0 | 0 | frame 12 / norm 0.400 (reach 3.06) |
| 2HitComboClawsAttack_RM | 1.500s (45f) | (미측정 — 단발 교대로 대체) | — | — | (구)콤보. 보존, 미사용 |
| Roar | 4.000s (120f) | 0 | 0 | 0 | 앵티시페이션/위협 텔레그래프. speed4.5로 ~0.89s 압축(Dimax 0.57s보다 길게=무게) |
| Idle | 2.000s (60f) | 0 | 0 | 0 | 대기 허브 |
| BiteAttack | 0.667s (20f) | (미측정) | — | — | 대안 공격(미사용 — 클로월 채택) |

## ★★★L/R 전진 비대칭 = Venosaur 고유 (Dimax는 대칭 2.22m였음 — 가정 금지)
- **L 2.413m / R 4.094m** (R이 ~70% 더 큰 런지). 클립 *저작 차이*(R 클로가 더 크게 내지름). 헤드리스 런타임 시뮬에서 live 확인: enter-to-enter 교대가 ~2.3m(L 후)/~3.9m(R 후).
- **결정(애니가 진실, 제2원칙)**: 기본 = **비대칭 보존**(per-hand gain 둘 다 1.0). 둔중 브루저의 불균등 보폭 = *더 살아있음*(북극성 #1). "절뚝(limp)"으로 읽히면 유저 ▶ 판정에 따라 per-hand gain 균등화 — 균등화 시 LeftAdvanceGain≈1.697(L을 R로 올림) 또는 RightAdvanceGain≈0.589(R을 L로 낮춤). 증폭이지 발명 아님(Dimax AdvanceGain 메커니즘).
- ★구간별 전진 분포(실측): L = Windup 0.525m / Strike 0.797 / FollowOut 0.818 / Recovery 0.273. R = Windup 0.707 / Strike 1.098 / FollowOut 1.167 / Recovery 1.122(R은 회수에도 큰 전진).

## ★무게 이즈 4구간 분할 (30프레임 재유도 — Dimax 35와 경계 frame 다름!)
- reach 스캔: f9(1.55/2.09 cocking)→**f12 peak(2.73/3.06 컨택)**→f15(초기팔로)→f21(후기팔로)→f30(중립).
- 경계: **Windup 0~9 / Strike 9~15 / FollowOut 15~21 / Recovery 21~30**(Dimax는 9/16/22/35). 같은 take 4분할 → 경계 포즈 비트동일 → CUT 연속(헌법 준수).
- ★ClawHit = Strike 구간, 컨택 norm = (12−9)/(15−9) = **0.500**(L/R 동일, 컨택 f12 동일). 검증: Strike 0.200s 클립에 ClawHit@0.100s(=norm0.5). importer 정규화 time 함정 통과.

## 본 이름 (rig)
`Venosaur_ L Finger0`·`R Finger0`(공백 주의: "Venosaur_ L Finger0"). Dimax와 같은 underscore-space 패턴.

연동: [[project_telegraph_driver_venosaur]](상태머신·드라이버)·[[project_dimaxillosaurus_clip_kit]](재활용 원본 틀)·[[feedback_measure_rootmotion_by_stepping]]·[[project_stage1_roster_anim_read]]
