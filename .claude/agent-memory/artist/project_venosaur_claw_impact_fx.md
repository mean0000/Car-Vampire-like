---
name: venosaur-claw-impact-fx
description: Venosaur 클로 컨택 임팩트 VFX v1 — ClawHit AnimationEvent 훅, SmashImpactPool 절제 재활용, 시각 미검증(플레이판정 대기)
metadata:
  type: project
---

# Venosaur 클로 컨택 임팩트 VFX v1 (2026-06-14)

유저 결정: 「컨택 임팩트만」 (텔레그래프 장판 ❌ · 스윙 트레일 ❌)
이유: 무한 클로질 → 깜빡임 노이즈+오버드로우. 닿는 순간 한 방만.

## 변경 파일

- `Assets/_Project/Scripts/VenosaurBrawler.cs` — `ClawHit` 스텁 → `FireClawImpact(ev)` 호출 + 임팩트 필드군 + `FindBoneRecursive` 헬퍼 추가
- `Assets/_Project/Scripts/VenosaurLabSpawner.cs` — `BuildClawImpactPool()` + 각 Venosaur에 `brawler.clawImpactPool` 주입

## 설계 핵심

- **신규 셰이더 0** — `SmashShock.shader` 완전 재활용. 색·크기·수명만 절제 버전.
- **SmashImpactPool 재활용** — poolSize=4 (슬램 8보다 작음, 단발 클로 동시 활성 ≤ 2~3).
- **절제 원칙 (뽕 경제 보호)**: 적 클로 임팩트 < 슬램 임팩트 < 킬버스트(플레이어 처치).

## 핵심 파라미터 (Inspector 노브, 미검증 — 유저 플레이 후 조정 예정)

| 노브 | 값 | 슬램 대비 |
|---|---|---|
| `clawShockColor` | (1, 0.28, 0.08, 1) 레드오렌지 | 슬램(1, 0.32, 0.10) — 채도 약간 아래 |
| `clawIntensity` | 0.9 | 슬램 1.8의 절반 |
| `clawCoreFlash` | 0.55 | 슬램 0.7보다 낮음 |
| `clawRingWidth` | 0.28 | 슬램 0.35보다 좁음(날카로운 선) |
| `clawRadius` | 1.2m | 슬램 r3의 40% |
| `clawShockDuration` | 0.14s | 슬램 0.35의 40% — 빠르게 명멸 |
| `clawScorchDuration` | 0.10s | 슬램 1.2s 대비 극히 짧음 |
| `clawDustScale` | 0.45 | 슬램 1.0 절반 미만 |

## 손 본 이름 전략

- 기본값: `leftHandBoneName = "Hand_L"`, `rightHandBoneName = "Hand_R"`
- `FindBoneRecursive` = 정확 일치 우선 → 부분 일치(Contains) 폴백
- 본 미발견 시: `model.forward × clawReach(2.0m)` 위치 폴백 + LogWarning
- ★플레이 시 Console에 "[VenosaurBrawler] 'Hand_L' 본 미발견" 뜨면 → Inspector에서 실제 본 이름으로 교정 (Unity 계층창에서 SK_Venosaur 클릭 → 자식 확인)

## ClawHit L/R 판별

- `AnimationEvent.stringParameter "L"/"R"` 우선 (컨트롤러에 세팅되어 있으면 정확)
- 미세팅 시: `_nextRight` 반전으로 현재 손 추론 (교대 상태에서 nextRight=다음 손이라 현재는 반대)

## 빌드 주의사항

- `SmashShock.shader` → 이미 [[smash-impact-fx]] 메모에서 Always Included 등록 권고됨. 미등록 시 빌드 스트립.
- `Resources/VFX/Materials/M_KillBurst_Body.mat` — SmashImpactPool 의존. 미발견 시 풀 비활성(Console 에러).

## ★시각 검증 잔무 (VFX는 눈으로 보기 전 완료 ❌)

KatanaController.cs 컴파일 에러(타 세션 파티션)로 플레이모드 불가.
Katana 에러 해소 후:
1. ToneGateLab 씬 ▶ 플레이 → Console "[VenosaurBrawler] 본 미발견" 여부 확인
2. 클로 컨택 순간 임팩트 VFX 발화 확인
3. 판단 포인트:
   - 임팩트가 "읽히되 절제"인가 (슬램보다 분명히 작아 보이는가)
   - 위치가 손 끝 근처인가 (본 폴백이라면 대략적 위치)
   - 명멸 속도 0.14s가 충분히 빠른가 (너무 오래 남으면 clawShockDuration ↓)
   - 핵 섬광(coreFlash 0.55)이 지나치게 밝으면 → 0.3으로 낮춤

## 관련 메모리

- [[smash-impact-fx]] — SmashShock 셰이더 + SmashImpactPool 틀
- [[killburst-fx]] — 뽕 경제 기준선 (클로 임팩트는 이보다 약해야 함)
- [[stage1-vfx-audit]] — Venosaur 항목: "컨택 임팩트만" 결정 배경

**Why:** 컨택 임팩트 VFX 구현 기록 — 파라미터 튜닝 시 이 노브 맵을 출발점으로 삼는다.
**How to apply:** 플레이 판정 후 노브 조정 시 "절제 원칙(적 < 슬램 < 킬버스트)" 기준을 유지.
