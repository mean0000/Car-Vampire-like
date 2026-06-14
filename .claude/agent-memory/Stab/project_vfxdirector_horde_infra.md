---
name: vfxdirector-horde-infra
description: VfxDirector 호드 VFX 인프라 v1 리뷰 결과 — 싱글톤 레이스·WatchPadReturn 누수·culling 카운터 비대칭 패턴 기록
metadata:
  type: project
---

## 구현 개요 (2026-06-14)

VfxDirector.cs(신규) + VfxLayers.cs(신규) + VenosaurBrawler.cs / VenosaurLabSpawner.cs 수정.
- VfxDirector: 자가 부트스트랩 MonoBehaviour 싱글톤. TelegraphPool/SmashImpactPool 라우터 + culling(maxConcurrentTelegraphs/telegraphCullRadius) + wind-up 글로우(MPB+DOTween).
- VfxLayers: renderQueue/sortingOrder SSOT 정적 상수 클래스. 기존 파일 7곳이 이미 참조.

## 발화된 버그 패턴

### H-1: WatchPadReturn 카운트 누수 (High)
TelegraphPool.RecycleOldest() 경로로 패드가 강제 회수되면 MeshRenderer.enabled가 false로 가지 않음 — TelegraphPad는 Deactivate()를 부를 주체가 없음(Return도 안 불림). WatchPadReturn이 while(mr.enabled) 루프를 탈출하지 못해 _activeTelegraphCount가 영구 +1 잠김. N번 RecycleOldest 후 카운터가 maxConcurrentTelegraphs에 수렴하면 이후 모든 텔레그래프 culling 생략 → 호드에서 무음 경보 소멸.

패드 정상 수명 시도 → pool.Return() → _mr.enabled=false (Deactivate에서) → WatchPadReturn 루프 탈출 → 카운터 감소 정상.
RecycleOldest 경로: _mr.enabled=true인 채로 _live에서 분리 → WatchPadReturn은 루프 탈출 불가 → 카운터 고착.

**수정**: RecycleOldest 시 TelegraphPad.Deactivate() 호출 필요 (public 노출 또는 RecycleOldest에 mr.enabled=false 인라인). 또는 패드에 OnReturnToPool() 훅을 두어 WatchPadReturn을 StopCoroutine으로 정리.

### H-2: static _instance domain-reload 누수 (High)
VfxDirector._instance에 [RuntimeInitializeOnLoadMethod]가 없다. Domain Reload Off 환경(이 프로젝트 표준)에서 에디터 플레이 종료 후 _instance가 파괴된 MonoBehaviour를 가리킨 채 stale 유지 → 다음 플레이에서 Instance getter가 !null 판정으로 그대로 반환 → 접근 시 MissingReferenceException.
이 프로젝트의 기존 SlashVfxPool도 동일 패턴(Domain Reload Off 미보호) — 선례.

**수정**:
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ClearStatic() => _instance = null;
```

### M-1: DOTween 트윈이 Renderer 파괴 후 살아남을 수 있음 (Medium)
PulseGlow()는 KillOnComplete 없이 DOTween.To 실행. glowRenderers 배열의 null 체크(if (r == null) continue)는 있으나, 트윈 자체의 KillOnComplete는 없어 Renderer가 미리 파괴되면 OnComplete 시 foreach가 한 번 더 실행됨 — null체크가 있어 예외는 없지만 불필요한 MPB 조작이 수행될 수 있음. 
같은 렌더러에 연속 텔레그래프 요청(Windup 중 다음 Windup)이 들어오면 트윈 중복 누적 — 이전 트윈을 Kill하지 않음.

**수정**: PulseGlow() 진입 시 해당 렌더러의 기존 트윈을 `DOTween.Kill(r)` 또는 트윈에 `.SetId(r)`를 달아 Kill하고 재시작.

### L-1: VfxLayers.CanvasRearHint == CanvasPurge == 200 (Low)
RearThreatHint의 실제 hardcode CanvasOrder = 10이고 VfxLayers.CanvasRearHint = 200으로 불일치. CanvasPurge = 200은 PurgeSnapshotFX.CanvasOrder = 200과 일치하나 RearThreatHint는 10.
VfxLayers 상수가 실제 코드와 다름 — "단일 진실원" 역할을 못 함.

### L-2: VfxLayers.CanvasRunLoop vs 실제 50 (Low)
CanvasRunLoop = 50 = RunLoopSetup 실제값 50 일치 — 정상.

## 안전 판정 (이상 없음)

- VfxLayers renderQueue 상수 7곳 교체: TelegraphPad(3000), SmashImpactFX(3000/2999), SlashVfxFX(3000/3050/3060) — 기존 하드코딩과 동일 값. 위계 안 깨짐.
- culling PassesCull: maxConcurrentTelegraphs 초과 시 카운트 안 올림 — 거부 경로에서 카운터 비대칭 없음.
- RequestTelegraph 3종(Fan/Circle/Ring) 모두 동일 WatchPadReturn 패턴 — 누락 없음.
- RequestImpact: culling 없음(설계 의도) — 카운터 없음, 정상.
- MaterialPropertyBlock 재사용: _mpb 공유 인스턴스로 alloc 0 — 정상. sharedMaterial 안 건드림.
- Venosaur 폴백 3경로(디렉터 RequestImpact → null 시 직접 pool → 디렉터 없을 시 직접 pool) 논리 정합.
- VenosaurLabSpawner BuildClawImpactPool → SetActive → Awake → Build 순서 보장(inactive 생성 규약 준수).

## 프로젝트 패턴

- 이 코드베이스의 자가 부트스트랩 싱글톤(SlashVfxPool, VfxDirector)은 모두 [RuntimeInitializeOnLoadMethod] 없이 구현 — Domain Reload Off 시 공통 취약점.
- RecycleOldest 강제 회수 시 Deactivate 호출 여부: TelegraphPool.RecycleOldest()는 TelegraphPad.Deactivate() 미호출 — SmashImpactPool.RecycleOldest()는 ForceDeactivate() 호출(올바름). 비대칭 설계.

**Why:** H-1은 호드에서 N회 RecycleOldest 후 카운터 고착 → 이후 모든 텔레그래프 무음 소멸. 재현 시나리오: enemyCount>poolSize 상황에서 동시 텔레그래프가 몰릴 때.
**How to apply:** 다음 TelegraphPool 리뷰 시 RecycleOldest 경로에 Deactivate 삽입 여부 반드시 점검.
