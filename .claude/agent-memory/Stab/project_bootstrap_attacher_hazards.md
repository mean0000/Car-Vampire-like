---
name: bootstrap-attacher-hazards
description: 자가 부트스트랩 DDOL/런타임 부착자 패턴(RearThreatHint·AmbientBedDrone·ZombieVocalDirector·CameraPresetLab) 리뷰 체크리스트 — 2026-06-12 세션1 리뷰에서 실제 발화한 항목들
metadata:
  type: project
---

이 프로젝트는 "씬/프리팹 무수정" 정책 때문에 자가 부트스트랩 DDOL + 런타임 AddComponent 부착자 패턴을 반복 사용한다(RearThreatHint, AmbientBedDrone, ZombieVocalDirector, CameraPresetLab/Restorer). 2026-06-12 리뷰에서 실제로 발화한 하자 패턴:

**Why:** 같은 패턴이 계속 복제되므로 같은 구멍도 복제된다. 아래는 코드만 봐서는 한 번에 안 보이는, 이 패턴 고유의 함정.

**How to apply:** 신규 부트스트랩/부착자 컴포넌트 리뷰 시 아래를 항목별로 적용.

1. **오버라이드형 도구의 OnDisable 미처리**: 다른 컴포넌트(Restorer 등)가 `_overriding` 같은 플래그를 참조해 매 프레임 상태를 되돌리는 구조에서, 본체를 인스펙터에서 끄면 플래그가 영원히 true로 남아 카메라/상태가 스테일 값에 고정됨. "롤백 = 컴포넌트 off"를 표방하는 도구는 OnDisable 정리가 필수.
2. **sceneLoaded 클리어 vs Additive**: `_voices.Clear()`류를 모든 sceneLoaded에서 하면 Additive 로드 시 생존 객체에 AudioSource 등이 중복 부착됨. Update의 null/IsDead 프루닝이 이미 있으면 클리어 자체가 불필요한 경우가 많다. (단일 모드만 쓰는 현재는 잠복.)
3. **정적 AudioClip 캐시 vs 도메인 리로드 off**: `static AudioClip[] + if(_cache != null) return` 가드는 Enter Play Mode Options(도메인 리로드 off)에서 파괴된 클립을 영구 보존 → 무음+에러. 현재 EditorSettings는 도메인 리로드 ON(m_EnterPlayModeOptionsEnabled: 0)이지만 m_EnterPlayModeOptions: 3으로 스테이징돼 있어 언제든 켜질 수 있음. 가드에 `_cache[0] == null` 추가가 1줄 백신.
4. **Awake 중복-파괴 경로의 같은 프레임 Update NRE**: `Destroy(gameObject)`는 프레임 말 지연이라, Awake에서 초기화를 건너뛴 중복 인스턴스의 Update가 그 프레임에 한 번 돈다 → 미초기화 필드 NRE. `if (_src == null) return` 1줄 가드.
5. **풀링 도입 시 AddComponent 중복**: ZombieController.Init은 풀링 대비로 작성됨(현재는 Instantiate/Destroy). 풀링이 들어오면 사망 시 Voice 목록에서 빠진 좀비가 부활할 때 부착자가 AudioSource를 또 붙임. 풀링 PR 리뷰 때 부착자 전수 점검 필요.
6. **Init 없는 룩데브/조각상 좀비**: FindObjectsOfType 리스캔은 룩데브 씬의 Init 안 된 좀비도 줍는다(IsDead false, Dormant) → 조각상이 으르렁댄다. 부착자에 게임플레이 씬 가정이 암묵적으로 박혀 있음.

관련: 카메라 샌드위치(복원 Update −9 → 리그 LateUpdate −50 → 랩 −40) 구조 자체는 건전 판정. 단 Feel MMWiggle이 카메라 트랜스폼을 LateUpdate(0)에서 쓰면 오버라이드 중 그 오프셋이 다음 프레임 복원에 지워짐 — 씬 확인 필요 항목.
