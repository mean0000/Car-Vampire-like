---
name: stage1-vfx-audit
description: 1차 스테이지 9종 VFX 판단 — 시그니처, 오버드로우 리스크, 원소 에셋 커버리지, 잭팟 우선순위
metadata:
  type: project
---

# 1차 스테이지 9종 VFX 판단 (2026-06-14)

## 핵심 전제 관찰 (비주얼 리뷰 기반)

9종 전부 **주황-갈색 단일 팔레트** (Protofactor Vol.2 패밀리). 베이스 컬러 대비가 거의 없다.
VFX 색대비 = 자연색(에셋 내장 Emissive)이 아니라 **레드오렌지 ThreatArc가 99% 담당** 해야 함.
주황 바디 위에 레드오렌지 텔레그래프 = 채도차가 얕음 → 장판 폭 조정(bloom 의존도 높임)이 필요.

특이점:
- Kupolojuve(유생 = Kupolobrach_Juvenile) — 배 아래 청록 구체 내장. **유일한 자체 대비색**. 전격 발광 VFX의 앵커가 될 수 있음.
- Crustaspikan(유생) — 등 청색 결정체. 보스 예고로 다른 종과 시각 분화됨.
- Fulgurodonte — 입 내부 오렌지 글로우 내장. 램 차징 VFX와 색 연속성 있음.
- Carcinoptera — 갈색 날개+게 형태, 탑다운 실루엣이 좌우로 넓음. 날개가 다이브 방향 힌트 역할 가능.

## 1. 9종 VFX 시그니처 + ThreatArc 재활용 판단

| 종 | VFX 시그니처 1줄 | ThreatArc 재활용 | 근거 |
|---|---|---|---|
| **Lacercharias** | 저자세 정면 돌진 → 레드오렌지 원형 SDF(파장 방출) + 죽을 때 킬버스트 시안코어 | 원형(TYPE=0) 그대로 재활용 | 1.1m 저자세, 전방향 물기, 군체라 개별 텔레그래프 무거움 → 원형이 가장 빠름 |
| **Venodonte** | 지상 엄폐 → 레인 SDF(TYPE=1) 산성 궤적 + 투사체 자체가 레드오렌지 HDR 발광(ProjectilePool 재활용) | 레인 재활용 | 엄폐 뒤에서 쏘므로 전방 좁은 레인이 딱 맞음. 투사체 = 기존 AcidGlob/ProjectilePool 그대로 |
| **Caniathrox** | 쾌속 도약 → 부채꼴 SDF(TYPE=2, 120도) 레드오렌지 차오름 + 착지 임팩트링 — 이미 구현됨(caniathrox-attack-fx) | 부채꼴 재활용 | 기존 틀 그대로 |
| **Kupolojuve** | 하강 다이브 → 원형 SDF(착지점 중심 팽창, 배 청록 글로우와 레드오렌지 링 동시) | 원형 재활용 | 착지 지점 예고 = 원형이 명확. 배 청록은 에셋 내장이라 VFX 추가 비용 0 |
| **Dimaxillosaurus** | 콤보 클로 → 레인 SDF(좌/우 번갈아, 폭 넓음) + 클로 스윙 궤적 시안 플래시(공격 가독) | 레인 재활용 × 2 | 직립 양팔 스윙 → 좌레인/우레인 교대. 기존 레인 파라미터만 조정 |
| **Venosaur** | 호위 클로 + 스핏 → 부채꼴 SDF(좁은 110도) + 레드오렌지 스핏 투사체 | 부채꼴 재활용 | 호위 역할이라 단독 VFX 복잡도 낮게 유지. 투사체 = ProjectilePool 재활용 |
| **Fulgurodonte** | 램 돌진 → **레인 SDF(폭 극대, 긴 런웨이 방향)** 레드오렌지 전진 충전 + 벽 임팩트 스매시 VFX(smash-impact-fx 재활용) | 레인 재활용 | 돌진 방향이 직선이라 레인이 가장 선명. 폭만 키우면 됨. 임팩트 = 기존 Crassorrid 슬램 VFX 재활용 가능 |
| **Carcinoptera** | 공중 선회 → 원형 SDF(다이브 착지점 예고, 큰 반경) + 착지 후 발산 클로 임팩트링 | 원형 재활용 | 탑다운에서 날개 실루엣이 착지 방향 힌트. 원형 텔레그래프 + 날개 그림자 겹치면 가독성 최고 |
| **CrustaspikanLarvae → Crustaspikan** | 유생 분출 웨이브 → 링 SDF(TYPE=3, 크레이터 펄스) + 성체 강림 → 대형 링 SDF 펄스 + 지면 스톰프 충격파(smash-impact-fx 스케일 업) | 링 재활용 | 크레이터 분출 = 링 SDF가 가장 자연스러운 표현. 유생이 청색 결정 보유라 링에 청색 틴트 가능(성체 예고 분화) |

**ThreatArc 재활용 9종 모두 가능. 신규 셰이더 0.**

## 2. 호드 오버드로우 리스크

**위험 조합: Lacercharias 떼(6~15) + Venosaur 호위(×4) 동시 화면**

- Lacercharias: 저자세 1.1m, 떼라 개별 텔레그래프 원형 SDF가 화면 전체에 겹침
- 예측 오버드로우: 원형 SDF × 15 + 부채꼴(Caniathrox) × 4~5 = 화면 거의 전부 AfterPost 패스

**완화 방향 (신규 셰이더 없이):**
1. Lacercharias 텔레그래프 = **개별 OFF, 집단 텔레그래프만** — 3마리 이상 클러스터 감지 시 클러스터 중심 원형 1개만 표시. 스크립트 로직, 셰이더 수정 0.
2. Venosaur는 호위 역할 = 텔레그래프 생략 가능. 주체(Fulgurodonte)의 램 텔레그래프만 남기면 시각 노이즈 절반 제거.
3. ThreatArc Fill Alpha = 0, Outline만 살리는 파라미터 모드 추가 제안 — 오버드로우 = 채움 부분이 주범. 아웃라인만이면 패스당 픽셀 수 급감.
4. 동심 구심 전진 전제라 실제로 Lacercharias(외곽) + Venosaur(코어)가 동시 최대밀도인 시점은 15분 후반뿐 — 초반~중반은 리스크 낮음.

## 3. 원소 이펙트 커버리지 (보유 에셋)

| 원소 | 종 | 보유 에셋 커버 여부 |
|---|---|---|
| 산성(Venodonte) | AcidGlob.cs + ProjectilePool 레드오렌지 HDR 발광 | **완전 커버** — 이미 구현+검증(Venodonte 커밋 487e45fc6) |
| 전격(Kupolojuve) | 없음 — Vefects BIRP surface라 URP 불가 | **미커버.** 텍스처 추출 + Feel 머티리얼 복제 패턴 필요. OR: 청록 Emissive를 에셋 내장 청록 구체에서 추출 + UnlitAdditive로 번개 스파크 재현. 복잡도 낮음 |
| 램 충격(Fulgurodonte) | SmashImpactFX.cs + SmashImpactPool.cs | **완전 커버** — Crassorrid 슬램 임팩트 틀 재활용, 스케일만 조정 |
| 공중 다이브 충격(Carcinoptera) | SmashImpactFX 재활용 가능 + 착지 파티클 | **부분 커버** — 슬램 VFX 재활용 가능, 공중에서 내려오는 빌드업 파티클은 미보유. 투명 트레일 셰이더(Unlit Additive)로 단순화 가능 |
| 스포너/분출(CrustaspikanLarvae) | TelegraphPad 링 SDF | **부분 커버** — 텔레그래프는 있음. 분출 파티클(지면 균열 등)은 Vefects 의존 → 텍스처 추출 패턴 적용 |

**Kupolojuve 전격이 유일한 신규 VFX 작업 대상.** 다른 원소는 기존 에셋으로 해결 가능.

## 4. 잭팟 처치 연출 우선순위 ("뽕=돈")

| 우선순위 | 종 | 이유 | 연출 방향 |
|---|---|---|---|
| **1순위** | **Fulgurodonte (벽 그로기)** | 코어 잭팟 메인 타겟. "이게 뽕"을 가르치는 첫 클라이맥스. 뽕 밀도 최고. | 벽 충돌 그로기 → 카메라쉐이크 + 히트스탑(HitStop.Do) + 킬버스트 대형(시안코어+마젠타엣지 스케일 업) + 그을림 장판 잔류. 기존 smash-impact-fx 풀 재활용 |
| **2순위** | **Crustaspikan 성체 (escalation 보스)** | "지연의 대가이자 잭팟" — 데모 최종 후보. | 성체 처치 = 초대형 킬버스트 + 링 SDF 붕괴 파동 2~3회. 스케일 × 3~5 파라미터 조정만 |
| **3순위** | **Carcinoptera (분수광장 공중 정예)** | 개활 공중 다이브라 카메라 앵글 극적. 처치 순간 공중에서 떨어지는 시각. | 처치 → 공중 킬버스트(착지 지점 기준) + 낙하 파티클. 현재 킬버스트는 지면 기준이라 Y오프셋 조정 필요 |
| **4순위** | **CrustaspikanLarvae 분출 웨이브** | 성체 예고의 시각 클라이맥스. | 유생 처치보다 분출 연출이 주 — 크레이터 링 SDF 펄스 + 유생 킬버스트 소형 × N. 개별 처치 연출보다 웨이브 연출이 뽕 |

**Color 이슈 경고**: 유생(청색 결정)의 킬버스트를 시안코어+마젠타엣지로 그대로 쓰면 에셋 내장 청색과 색 충돌. 유생 킬버스트는 시안 코어만(마젠타 빼기)으로 차별화 권장.

## 연관 메모리
- [[telegraph-pad-fx]] — ThreatArc 재활용 기반
- [[killburst-fx]] — 처치 연출 기반
- [[smash-impact-fx]] — Fulgurodonte 임팩트 재활용
- [[caniathrox-attack-fx]] — 29종 공격 VFX 틀
- [[killburst-fx]] — URP 파티클 머티리얼 함정 주의

**Why:** 9종 VFX 판단 기록 — 구현 전 판단 레이어로 향후 구현 세션에서 이 판단을 출발점으로 삼는다.
**How to apply:** 각 종 VFX 구현 시 이 파일의 시그니처 + 재활용 판단을 먼저 읽고 시작. 신규 셰이더 제안이 나오면 이 판단과 충돌 여부 확인.
