---
name: signature-aura-pattern
description: MonsterSignatureAura 코루틴+DOTween+MPB 패턴 리뷰 — H-2=OnDestroy Kill 누락·H-1=OnEnable 코루틴 중복 (2026-06-14)
metadata:
  type: project
---

## MonsterSignatureAura 시그니처 오라 — 리뷰 결과 (2026-06-14)

**대상 파일**: `Assets/_Project/Scripts/MonsterSignatureAura.cs`, `VenosaurBrawler.cs`, `VenosaurLabSpawner.cs`

### 실발화 위험 패턴

**H-2 (최우선): OnDestroy DOTween Kill 누락**
- `OnDisable`에서 `DOTween.Kill(_pulseTweenId)` 호출하지만 `OnDestroy` 없음.
- 적 사망/Destroy 처리 시 같은 프레임에 DOTween 업데이트 → OnUpdate/OnComplete 람다가 파괴된 Renderer에 접근 → MissingReferenceException.
- 수정: `void OnDestroy() { StopAllCoroutines(); DOTween.Kill(_pulseTweenId); }` + 람다 안 `if (this == null) return` guard.

**H-1 (재활성화 시): OnEnable 코루틴 중복**
- `OnEnable`에서 `StopAllCoroutines()` 없이 `StartCoroutine(AuraBreathe())`를 호출.
- SetActive false→true 사이클 시 코루틴 2개 동시 진행 → sin 위상 간섭 flicker.
- 현재 스포너에서는 단일 SetActive(true)라 미발화. 풀링/재활성화 시 즉시 발화.
- 수정: OnEnable 진입 시 `StopCoroutine(nameof(AuraBreathe))` 선행.

**M-1: DOTween SetId 문자열 vs SetTarget(this)**
- `GetInstanceID()` 기반 문자열 ID — 파괴-재생성 사이클에서 InstanceID 재사용 시 새 인스턴스 트윈을 잘못 Kill할 수 있음.
- `SetTarget(this)` + `DOTween.Kill(this)` 패턴으로 전환하면 인스턴스 참조가 식별자가 되어 원천 차단.

**M-2: `_mpb` 단일 인스턴스 공유 — 렌더러 간 상태 누적**
- 현재 단일 MPB로 여러 렌더러에 GetPropertyBlock→SetPropertyBlock 루프.
- 동일 프로퍼티를 모든 렌더러에 동일하게 쓰는 구조라 현재는 무해.
- 렌더러별 다른 초기 림 강도 가진 멀티-재질 구성 시 교차 오염 발생.

**M-3: Awake에서 enabled=false 후 _grade 미초기화**
- auraRenderers 미발견 경로: `_grade = default`, `_currentIdleIntensity = 0`.
- PulseAttack은 auraRenderers null 가드로 조기리턴하여 크래시는 없음.
- 이후 외부에서 enabled=true + 렌더러 주입 시 0 기준으로 숨 펄스가 돔.

### 안전 확인 항목
- `_pulseTweenId` 인스턴스 고유성: 동시 존재 인스턴스 간 Kill 교차 없음 (M-1은 파괴-재생성 시나리오).
- AuraGrade 경계: switch-default(LV3 폴백) + SetLevel Clamp(1,5). 안전.
- MPB sharedMaterial 비파괴: GetPropertyBlock/SetPropertyBlock 패턴 올바름.
- ClawHit null-conditional: `signatureAura?.PulseAttack()` 안전. 비활성 가드도 있음.
- 스포너 와이어링 순서: SetActive(false)→AddComponent→필드주입→SetActive(true). 기존 규약 일치.
- `_attackPulsing` 플래그는 데드 스테이트 (L-1 — 제거 또는 활용 권장).

### VfxDirector 연동 패턴 일관성
- 스포너(BuildClawImpactPool): `VfxDirector.Instance` (자동생성 허용) — 생성 단계 의도.
- 브롤러(FireClawImpact): `VfxDirector.Existing` (자동생성 없음) — 실행 단계 의도.
- 이 이중성은 SlashVfxPool 패턴과 동일하며 의도적. 단 VfxDirector RuntimeInitialize 누락([[vfxdirector-horde-infra]] H-2) 해소 전제.
