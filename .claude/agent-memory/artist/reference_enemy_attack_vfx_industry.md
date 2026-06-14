---
name: reference_enemy_attack_vfx_industry
description: 업계 적 공격 VFX 4축 리서치 — 3층 분해, 호드 관리, 탑다운 처리, 플레이어/적 시각 위계. ZombieCrush 현행 방식 정합 분석 + 9종 작업 권고.
metadata:
  type: reference
---

# 적 공격 VFX 업계 관행 리서치 (2026-06-14)

## 출처 목록
- https://gdkeys.com/keys-to-combat-design-1-anatomy-of-an-attack/
- https://www.gamedeveloper.com/design/enemy-attacks-and-telegraphing
- https://nex-3.com/blog/hades-ii-has-a-legibility-problem/
- https://steamcommunity.com/app/1145350/discussions/2/4358999171577943234/
- https://www.researchgate.net/figure/left-In-Hades-the-enemy-holds-a-pose-and-is-colored-with-a-white-overlay-to-signal_fig5_364535949
- https://forums.crateentertainment.com/t/whats-your-take-on-telegraphs-in-arpgs-good-or-bad/96204
- https://steamcommunity.com/app/2444750 (Shape of Dreams 플레이어 피드백)
- https://www.vfxapprentice.com/blog/creating-vfx-style-guides-for-games
- https://thomassteffen.medium.com/game-design-color-design-and-distractions-part-3-d2e162989f0b

---

## 축 1: 적 공격 3층 분해

업계 공통 프레임워크 (Anticipation → Active → Recovery):

**①텔레그래프(Anticipation / Wind-up):**
- Hades 원작: 적 몸 전체에 흰색 오버레이(overlay)를 씌워 "지금 공격 직전" 신호를 줌. 애니메이션 포즈 + 컬러 오버레이 이중 신호.
- Hades II: 아레나 바닥 특정 구역을 빨간색으로 물들이는 지면 투영(ground projection) 방식. 보스 위치와 독립적으로 지면에 표시되므로 카메라각 무관하게 읽힘.
- Path of Exile 2: 확장하는 nova 이펙트, 지면 슬램 패턴, 보라색 "균류 성장" 모양 지연 AoE 등 형태+색으로 구분.
- 공통 원칙: 포즈가 "실루엣으로 읽혀야" 하며, 과장되고 구분되는 윤곽을 가져야 한다. 최소 340ms의 attack signal+active 시간이 경험자 기준 반응 최소치.

**②스윙/궤적(Active / Swing):**
- 무기 트레일 VFX가 업계 표준: 히트박스 궤적을 따라가며 "어디가 위험한 부분인지" 시각화.
- Hollow Knight: 각 공격마다 고유 VFX를 써서 서로 구별 가능하게 함 (히트박스 인지 + 패턴 암기 보조).
- 트레일 지속시간이 공격 방향을 사후에도 알려주는 부가 기능.

**③컨택/히트(Contact / Impact):**
- Dark Souls 3: "End Pose → Return" 두 단계로 공격 마무리. Recovery 창 = 플레이어 반격 기회창. 경직과 회복이 명확히 분리.
- 히트 임팩트는 충돌 지점에서 방향성 있는 스파크/파편으로 공격이 "붙었다"는 확인 피드백.

---

## 축 2: 호드/대량 적 환경의 VFX 처리

**핵심 문제:** 화면에 적이 수십 마리일 때 개별 텔레그래프가 노이즈가 됨.

**업계 해법:**

**가. Vampire Survivors 방식 — 최소주의(Intentional Minimalism):**
- 적 공격 VFX를 거의 없앰. 적 자체가 위협. 플레이어는 적 위치/밀도로 위험을 판단.
- 핵심: 모든 원소(효과, 사운드, UI)가 같은 낮은 피델리티 수준에 맞춰있어 전체가 일관되게 보임.
- 단점: 특정 적의 공격 시작을 읽기 어려움 → 고밀도에선 개별 반응보다 전체 흐름 회피가 전략.

**나. Shape of Dreams 방식 — 실루엣 우선 + 설정 분리:**
- 화면이 파티클로 포화되더라도 몬스터 실루엣은 읽힌다는 원칙.
- Particle Effect Quality 설정 추가: 유저가 플레이어/적/보스 이펙트를 개별로 끄거나 줄일 수 있게.
- 보스 공격: 원형 슬래시 경고 인디케이터를 별도 레이어로 표시.

**다. Soulstone Survivors 방식 — VFX 불투명도 분리:**
- 플레이어 생성 VFX의 불투명도를 낮춰 그 뒤로 위험한 보스가 보이도록 설정 제공.

**호드 관리 핵심 원칙:**
- 개별 텔레그래프는 호드 전체에 켜면 노이즈 → 우선순위 기반 표시 (보스/엘리트만, 또는 플레이어 시야 반경 내 가장 가까운 N마리만).
- 텔레그래프 오버레이가 지면에 있을 경우 지면 색과 구분되는 보조색 필수 (Hades II 실패사례: 특정 바이옴에서 지면색과 AoE 빨강이 안 구분됨).
- Hades II 교훈: 플레이어 스킬 이펙트가 적 텔레그래프를 덮으면 즉사 상황 → 이펙트 레이어 Z-order 규약 필요.

---

## 축 3: 탑다운/부감 특유 처리

**문제:** 45° 부감에서 팔 휘두름·몸 움직임이 안 읽힘.

**업계 해법:**

**지면 투영(Ground Projection) — Hades II, Diablo 4, Path of Exile 2:**
- 공격 범위를 지면에 투영된 반투명 원/扇형으로 표시. 보스 위치와 별개로 "여기가 위험 구역"을 지면에 그림.
- Hades II: 크로노스 전투에서 "하이라이트된 지면 구역"을 위험지대로 표시.
- PoE 2 사례: 원형 AoE 인디케이터가 화살이 닿기 전에 트리거되어 플레이어에게 이동 시간 제공.

**가장 효과적인 탑다운 VFX 기법 (업계 공통):**
1. 지면 데칼/SDF 장판 텔레그래프 (우리 ThreatArc와 동일한 접근)
2. 공격 직전 적 본체 플래시/글로우 (화이트 오버레이 또는 HDR 색 펄스)
3. 무기/사지에 달린 트레일 (스윙 방향과 히트박스 범위 동시 표시)
4. 충돌 지점 지면 임팩트 링 (컨택 레이어)

**PoE 2의 교훈 (실패 사례):**
- 독/산성 지면 이펙트가 지면 텍스처와 구분 안 됨 → 투명도+대비 관리 부족.
- 초목 아래 깔린 지면 이펙트 불가시성 → 지면 이펙트는 항상 상위 레이어 필요.

---

## 축 4: 적 vs 플레이어 시각 위계

**업계 표준 색 규약:**
- **적 위협 = 따뜻한 색(빨강/주황)**, 플레이어/아군 = 차가운 색(파랑/시안/초록). 거의 장르 보편.
- 출처: 복수의 VFX 아트디렉션 아티클에서 "red = enemy, blue = ally" 명시.
- DMC(Devil May Cry): 파란 적 = 특정 무기 취약, 빨간 적 = 다른 무기 취약 — 색이 전투 정보를 직접 전달.

**채도/밝기 위계:**
- 가장 중요한 VFX = 가장 높은 채도+밝기. 부수적 요소 = 낮은 채도.
- 플레이어 공격 이펙트가 높은 채도면 적 텔레그래프보다 눈에 먼저 들어옴 → 맞는 줄도 모름.
- Hades II 실패: 플레이어 Moon 스킬(밝음)이 보스 공격 표시(빨강)를 묻어버림 → 즉사.

**"맞는 게 보상처럼 보이는 문제" 해결책:**
- 적 이펙트를 채도는 높지만 밝기는 낮게 → 화려하지만 플레이어 이펙트보다 어둡게 유지.
- 또는 적 이펙트 형태를 플레이어 이펙트와 명확히 구분 (날카로움/거칠음 vs 부드러움/깔끔함).
- Hades II 교훈: 비슷한 색(노란 보스 + 노란 텔레그래프 + 노란 스테이지 = Chronos 챕터)은 모든 위계를 붕괴시킴.

---

## ZombieCrush 현행 방식 정합 분석

**잘 맞는 점:**
1. "VFX 먼저, 애니 나중" 원칙 = 탑다운에서 팔 읽기 어려운 문제를 선제로 해결. 업계 정합.
2. ThreatArc SDF 장판 텔레그래프 = 지면 투영 방식. Hades II/PoE2와 동일 접근. 탑다운 최적 기법.
3. 색 규약(적=레드오렌지 예고 / 플레이어=시안 즉발) = 업계 표준 warm/cool 분리. 정합.
4. 컨택 임팩트(SmashShock) = Dark Souls 틀의 충돌 피드백. 정합.
5. 킬버스트 처치 연출(시안코어+마젠타엣지) = 처치 확인 피드백 레이어. 업계 권고 사례.

**우리가 빠뜨린 패턴:**
1. **②스윙/트레일 레이어** — 텔레그래프(before)와 컨택(after)은 있는데, 공격이 진행되는 동안의 트레일이 대부분 없음. Venosaur 클로는 트레일 기각됨. 카타나 슬래시만 트레일 있음. 근접 러셔 9종 중 스윙 트레일이 있는 종이 적음.
2. **적 본체 플래시/글로우 (wind-up 신호)** — ThreatArc 장판 텔레그래프는 있지만, Hades처럼 적 몸에 "공격 직전" 글로우/오버레이를 주는 케이스가 없음. 장판이 안 보이는 상황(다른 VFX에 덮임)의 보조 채널 부재.
3. **우선순위 기반 호드 텔레그래프** — 현재 전종 텔레그래프가 항상 켜지는 구조. 화면에 수십 마리 동시 교전 시 ThreatArc 10개가 겹치면 노이즈 → 가장 가까운/위험한 적 우선 표시 로직 미비.
4. **이펙트 Z-order 규약** — Hades II의 실패사례처럼, 플레이어 이펙트(시안)가 적 텔레그래프(레드오렌지)를 덮는 상황 대비 레이어 관리 규약 미수립.

**호드 특유로 조심할 점:**
- 화면 동시 적 수가 많을수록 개별 텔레그래프가 노이즈 → 텔레그래프를 "가장 위험한 공격"에 한정하거나 근거리 한정으로 culling.
- 플레이어 자신의 VFX(슬래시 트레일, 킬버스트)가 화려할수록 적 텔레그래프와 겹치는 면적 증가 → 색 채도 엄격 관리 (적 = 채도 높지만 HDR 밝기 억제, 플레이어 = 즉발 HDR 허용).
- 지면 데칼(ThreatArc)이 파티클/이펙트에 덮이지 않도록 렌더 order 관리.

---

## 9종 작업 즉시 적용 권고

**권고 1 (보유 기법으로 즉시): 적 본체 wind-up 글로우 추가**
- ThreatArc 장판이 등장하는 동시에, 해당 적 메시에 레드오렌지 림라이트 펄스를 1회 (0.2~0.3s, HDR 강도 1.5~2.0).
- 구현: MaterialPropertyBlock으로 _RimColor를 레드오렌지로, _RimPower를 0→3→0으로 DOTween. 신규 셰이더 불필요.
- 근거: Hades 화이트 오버레이 패턴. 장판이 다른 이펙트에 묻혔을 때 보조 채널.

**권고 2 (보유 기법으로 즉시): 스윙 트레일 레이어 — 근접 러셔 우선**
- Caniathrox/Venosaur 등 근접 공격 시 사지(클로/팔/꼬리)에 레드오렌지 트레일 추가.
- 카타나 슬래시 트레일(시안)과 반대 색 → 적/플레이어 구분 즉각.
- Vefects Stylized VFX URP 팩의 슬래시 소재를 레드오렌지로 리컬러 → 신규 셰이더 0.
- 트레일 지속시간 0.1~0.15s로 짧게 → 화면 노이즈 최소화.

**권고 3 (설계 변경): ThreatArc 호드 culling 규칙 수립**
- 근거리 X미터 이내 적만 ThreatArc 활성화. 또는 동시 표시 최대 N개 캡.
- Vampire Survivors 교훈: 호드 전체 이펙트는 노이즈, 가장 위험한 자만 강조.
- 구현: ThreatArc 활성화 코드에 "현재 활성 ThreatArc 수 > 3이면 suppressed" 로직 추가.

**권고 4 (렌더 order 규약): 적 이펙트 Z-order 정책**
- ThreatArc 장판 order 600 (현재) 기준 적용 유지 — 플레이어 파티클(~300) 위에 오도록.
- 킬버스트(처치 시)는 장판보다 위(order 700+)로 — 처치 확인이 최우선.
- Hades II 교훈: 플레이어 이펙트가 적 경고를 묻으면 사망 → 경고가 항상 위.

**권고 5 (신규 셰이더 최소화 재확인): 컨택 임팩트 통합 풀링**
- 현재 SmashShock 재활용 패턴(Venosaur 클로)을 9종 모두 표준화.
- 임팩트 반경(r)과 지속시간(t)만 종별로 다르게 — 셰이더는 1개.
- 큰 종(브루트류): r1.5~2.0, t0.25s / 소형(러셔류): r0.8~1.0, t0.12s.

---

**관련 메모리:** [[stage1-vfx-audit]] [[caniathrox-attack-fx]] [[venosaur-claw-impact-fx]] [[smash-impact-fx]] [[slash-trail-fx]] [[telegraph-pad-fx]]
