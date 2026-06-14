---
name: project_vfx_director_infra
description: VFX 호드 인프라 v1 — VfxDirector 싱글톤 + VfxLayers 상수 클래스 신설. 풀 소유권 기존 유지, 얇은 조율층만 추가. Venosaur 임팩트 첫 전환 완료.
metadata:
  type: project
---

# VFX 호드 인프라 v1 (2026-06-14)

## 신규 파일
- `Assets/_Project/Scripts/VfxLayers.cs` — 렌더큐/sortingOrder 단일 진실원(정적 상수 클래스)
- `Assets/_Project/Scripts/VfxDirector.cs` — 호드 조율 싱글톤(자가 부트스트랩)

## VfxLayers 상수 맵
- Scorch = 2999 (바닥 그을림)
- TelegraphFloor = 3000 (장판 텔레그래프, 범위 SDF, 먼지)
- ImpactShock = 3000 (충격파 링)
- SlashTrail = 3050 (슬래시 궤적 메시)
- SlashSpark = 3060
- PlayerInfoLine = 3100, PlayerInfoLineCore = 3101
- KillBurst = 3600 (처치 최상위)
- 캔버스: CanvasRunLoop=50, CanvasRearHint=200, CanvasPurge=200

## VfxDirector API
- `RegisterTelegraphPool(pool)` / `RegisterImpactPool(pool)` — 스포너가 풀 생성 직후 호출
- `SetPlayer(transform)` — culling 거리 계산용 플레이어 주입
- `RequestTelegraph(...)` / `RequestTelegraphCircle(...)` / `RequestTelegraphRing(...)` — 호드 culling 통과 시 풀 경유 발동 + wind-up 글로우
- `RequestImpact(...)` — 임팩트 풀 경유 발동(culling 없음)

## culling 노브 (SerializeField)
- `maxConcurrentTelegraphs` = 4 (잠정, 실측 후 조정)
- `telegraphCullRadius` = 0 (기본 = 거리 제한 없음)

## wind-up 글로우 노브 (SerializeField)
- `glowDuration` = 0.25s, `glowHdrIntensity` = 1.8
- MaterialPropertyBlock으로 _EmissionColor DOTween 삼각형 펄스(0→peak→0)
- 신규 셰이더 0 — URP Lit Emission 직접 조작

## 풀 소유권 구조
- 기존 풀(TelegraphPool/SmashImpactPool/SlashVfxPool/ProjectilePool) = 소유권 각 스포너
- VfxDirector = 참조만 보관하는 라우터(소유권 없음)
- 스포너가 풀 생성 후 Register*Pool 1회 호출로 등록

## Venosaur 첫 전환
- `VenosaurBrawler.FireClawImpact()` → VfxDirector.RequestImpact() 경유
- 디렉터 미등록 시 clawImpactPool 직접 폴백(하위호환 보장)
- `VenosaurLabSpawner.BuildClawImpactPool()` 끝에 `VfxDirector.Instance.RegisterImpactPool(_clawImpactPool)` 추가

## VfxLayers 교체 완료 파일
- TelegraphPad.cs: `_mat.renderQueue = 3000` → `VfxLayers.TelegraphFloor`
- SmashImpactFX.cs: shockMat=ImpactShock, scorchMat=Scorch, dustMat=TelegraphFloor
- SlashVfxFX.cs: rangeMat=TelegraphFloor, slashMat=SlashTrail, sparkMat=SlashSpark

## 잔무 (미교체 - 점진 교체 대상)
- CaniathroxAttackDemo.cs: renderQueue 3000, 3100
- KatanaController.cs: renderQueue 3100
- PlayerAfterimage.cs: RenderQueue.Transparent
- PlayerCombat.cs: renderQueue 3100, 3101, RenderQueue.Transparent
- ProjectilePool.cs: RenderQueue.Transparent (투명 일반 — 위계 정책과 무관)
- Run/StrainHarvestFX.cs: RenderQueue.Transparent
- ZombieDeathFX.cs: RenderQueue.Transparent
- PurgeSnapshotFX.cs: CanvasOrder=200 → VfxLayers.CanvasPurge로 교체 가능
- Editor/RunLoopSetup.cs: sortingOrder=50 → VfxLayers.CanvasRunLoop

## VfxLayers 수정 (2026-06-14 Stab+Codex 리뷰 반영)
- `CanvasRearHint` 200 → **10** (RearThreatHint.cs 실측값 back-match)
- `CanvasPurge` = 200 유지 (PurgeSnapshotFX 실측과 일치)

## H-1 버그 수정 (2026-06-14 Stab+Codex 합의)
- **근본 원인**: TelegraphPool.RecycleOldest()가 ForceDeactivate 미호출 → WatchPadReturn 루프 탈출 불가 → _activeTelegraphCount 고착
- **수정 3점 세트**:
  1. `TelegraphPad.ForceDeactivate()` 공개 추가 (_active=false + _mr.enabled=false, Return 미호출)
  2. `TelegraphPool.RecycleOldest()`에 `oldest.ForceDeactivate()` 추가 (SmashImpactPool과 대칭)
  3. `VfxDirector.WatchPadReturn()` — null/destroyed 체크(a) + watchdog 30s 타임아웃(b)

## H-2 버그 수정 (2026-06-14 Stab+Codex 합의)
- `VfxDirector`: `ClearStatic()` `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 추가
- `VfxDirector`: `HasInstance` + `Existing` 자동생성 없는 접근자 추가
- `VenosaurBrawler.FireClawImpact()`: Instance getter 대신 `Existing` 경유 → 폴백 경로에서 디렉터 자동생성 차단
- ★SlashVfxPool은 동일 취약점(자가부트스트랩 싱글톤, ClearStatic 없음) — 범위 밖이라 노트만

## M 버그 수정 (2026-06-14)
- M-2: `PulseGlow` 진입 시 `DOTween.Kill(r)` + `SetTarget(renderers[0])` — 연속 텔레그래프 트윈 누적 방지
- M-3: `PulseGlow` fillDuration <= 0 + actualDuration <= 0 조기 반환 가드

## 시각 검증 상태
★KatanaController.cs 컴파일 에러로 플레이모드 진입 불가 — 에러 해소 후 Venosaur 씬에서 확인

**Why:** 9종 양산 전에 호드 culling + 레이어 위계를 인프라로 확보. Hades II 교훈(플레이어 이펙트가 적 경고를 묻어버림) 선제 대응.
**How to apply:** 새 종 추가 시 스포너에서 Register*Pool + 드라이버에서 Request* 사용. VfxLayers 외부에 renderQueue 숫자 직접 박지 말 것. 폴백 경로에서 VfxDirector.Instance 금지 → HasInstance/Existing 사용.
