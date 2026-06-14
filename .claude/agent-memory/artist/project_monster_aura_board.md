---
name: monster-aura-board
description: 몬스터 시그니처 오라 v2 — 레드오렌지 단일 색조 + 밝기 차등 (유저 확정 2026-06-14). MonsterSignatureAura.cs v1 구현됨.
metadata:
  type: project
---

# 몬스터 시그니처 오라 v2 (2026-06-14 유저 최종 확정)

## 확정 방향 (유저 판정 완료 — 청록 폐기)
- 오라 형식 = 특정 부위만 발광(전신 X)
- 색조 = **레드오렌지(#FF5A2C) 고정 단일 색조** — 5색 무지개/청록 폐기
- 등급 = **밝기/강도/크기** 차등(색조 아님)
- LV1 어둑한 눈/관절 → LV5 활활 + 검은 입자(LV5 파티클은 다음 릴리즈)
- 시안(플레이어)·금(보상) 충돌 원천 소멸(청록 제거로)

## 구현 상태: v1 완료 (시각 미검증)
- `Assets/_Project/Scripts/MonsterSignatureAura.cs` — 신규
- `Assets/_Project/Scripts/VenosaurBrawler.cs` — `signatureAura` 필드 추가 + ClawHit→PulseAttack() 훅
- `Assets/_Project/Scripts/VenosaurLabSpawner.cs` — AddComponent<MonsterSignatureAura> + brawler.signatureAura 와이어링

## 구현 방식
- **셰이더**: ActorRimLit 기존 프로퍼티 MPB 직접 조작 (신규 셰이더 0)
  - `_EmissionColor` — 부위 Emissive 발광
  - `_RimIntensity` / `_RimPower` — 프레넬 림 강도/폭
- **평시**: AuraBreathe() 코루틴 — sin파 숨 펄스 (breathPeriod=2.8s, 인스턴스 위상 어긋남)
- **공격**: PulseAttack() — DOTween Triangle envelope (0→peak 40% → 0 60%, 0.35s)
  - ClawHit AnimationEvent → VenosaurBrawler → signatureAura.PulseAttack()
  - M-2 패턴 계승: 연속 ClawHit 시 이전 트윈 Kill 후 재시작

## AuraGrade.Params 표 (SSOT — 9종 공유)
| 등급 | idleEmissive | idleRim | attackPeak | rimPower | breathAmp |
|------|-------------|---------|-----------|----------|-----------|
| LV1  | 0.18        | 0.4     | 1.2       | 5.0      | 0.05      |
| LV2  | 0.35        | 0.7     | 1.8       | 4.5      | 0.08      |
| LV3  | 0.65        | 1.2     | 3.0       | 3.5      | 0.12      |
| LV4  | 1.1         | 2.0     | 4.5       | 3.0      | 0.18      |
| LV5  | 1.8         | 3.2     | 7.0       | 2.5      | 0.25      |

Venosaur = LV3. 기본값 노브으로 뽑혀 있어 Inspector 실시간 조정 가능.

## 부위 Renderer 지정 방법
- Inspector에서 `auraRenderers` 배열에 변이 핵심부 SkinnedMeshRenderer 지정
- 미지정 시 전신 SkinnedMeshRenderer 폴백 + 경고 출력(부위 지정 유도)
- Venosaur 변이 핵심부 후보: 머리(Head 본 아래 SMR), 등, 관절 — 프리팹 열어서 확인 필요
- ★현재 SpawnEnemies에서 auraRenderers 비워둔 채 SetActive(true) → 폴백 경고 뜸
  → 플레이 가능해지면 전신 발광 폴백으로 우선 확인 후, 세부 부위 지정은 다음 단계

## VfxDirector 통합
- 공격 펄스 = 독자 DOTween (VfxDirector PulseGlow와 별개 — 공유 MPB 없음)
- 공격 wind-up 글로우(RequestTelegraph 시)는 VfxDirector.PulseGlow가 계속 담당
- 시그니처 오라 펄스는 "컨택 정점"에서 터짐(텔레그래프 = 예고, 임팩트 = 컨택)

## 시각 검증 잔무
- KatanaController 컴파일 에러 해소 후 플레이모드에서 유저 판정
- 판정 항목: ①평시 밝기가 15m 탑다운에서 인식되나 ②공격 펄스 터짐이 임팩트 VFX와 어울리나 ③숨 펄스 주기가 자연스러운가

**Why:** 청록(LV1~2) 제거로 플레이어 시안과 충돌 원천 소멸. 레드오렌지 단일 색조는 위협 일관성 + 밝기만으로 등급 읽기.
**How to apply:** 새 종 추가 시 AddComponent<MonsterSignatureAura> + monsterLevel 설정 + auraRenderers 부위 지정. AuraGrade 수정은 해당 파일 한 곳만.
