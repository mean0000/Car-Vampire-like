---
name: zombiecrush-review-hotspots
description: ZombieCrush 리뷰에서 반복 확인해야 할 엣지케이스 핫스팟과 검증된 안전 전제
metadata:
  type: project
---

ZombieCrush(Unity URP 톱다운) QA 리뷰 시 반복 점검 항목.

**Why:** 2026-06-11 시야 콘 정보 게이트 리뷰에서 확인 — 같은 클래스의 버그(초기 겹침 캐스트, 폴링 지연, URP 패스 부재, 회귀 노브 분산)가 시스템마다 재발하는 구조.

**How to apply:**
- 이동/대시 리뷰: Physics.SphereCast는 시작 시 겹친 콜라이더를 무시 — 매립 복구(디페네트레이션) 경로 유무를 항상 확인. 대시(Raycast)와 보행(SphereCast)의 캐스트 형상 불일치가 매립 진입 벡터.
- 가시성/게이트 시스템 리뷰: 폴링(Rescan) 기반 등록은 스폰~등록 사이 노출 창이 생김 — 스포너 측 즉시 등록 훅 유무 확인. 벽 컷어웨이 때문에 "벽 뒤 비가시"는 전적으로 코드 게이트 책임.
- 커스텀 URP 셰이더 리뷰: ForwardLit/ShadowCaster/DepthOnly만으론 부족 — SSGI/SSAO(DepthNormals 소스) 씬에선 DepthNormalsOnly 패스 부재 시 깊이·노멀 버퍼에서 오브젝트 통째 누락.
- "회귀 노브"(coneBlend=0 등) 계약 검증: 노브 의미가 여러 컴포넌트에 분산돼 반쪽 회귀가 되기 쉬움 — 모든 소비자를 추적해 검증.
- 런타임 `Shader.Find("ZombieCrush/...")` 패턴: 커스텀 셰이더가 어떤 에셋에도 참조 안 되면 플레이어 빌드에서 스트립 → null 폴백으로 조용히 사라짐. 확인된 사례 2건(HudV2Controller의 UIAdditive, PurgeSnapshotFX의 InkBlob) — 빌드 전 Always Included Shaders 등록 필요. 에디터 플레이테스트에선 절대 재현 안 되는 클래스.
- FX 타이밍 리뷰 체크리스트(히트스탑 timeScale 0.05 전제): WaitForSeconds/DOTween 기본 트윈=scaled 함정. 안전 패턴 = unscaledTime 가드 + `yield return null`+unscaledDeltaTime 수동 적분 + ParticleSystem.main.useUnscaledTime. PurgeSnapshotFX(2026-06-11)가 전 구간 통과한 레퍼런스.
- 1회성 풀스크린 오버레이 FX: 코루틴 정상 완료 외에 상태 리셋 경로가 없으면 StopCoroutine/OnDisable 시 화면 덮은 채 잔존 — 코루틴 시작 시 방어적 비활성 or OnDisable 리셋 유무 확인.
- 검증된 안전 전제(2026-06-11 기준): 좀비 프리팹 Animator cullingMode=0(AlwaysAnimate) — renderer.enabled 끄기에도 본/BakeMesh 정상. 단 무가드 암묵 의존이라 프리팹 최적화 시 재검증 필요. 렌더러 숨김 채널 관례: DeathFX=GameObject.SetActive, LKP=renderer.enabled, 플래시/스캔=MPB — 채널 분리로 충돌 회피 중.
- 팀 관례: 리뷰 반영 커밋은 코드 주석에 "(리뷰 H-1)" 식 태그를 남김 — 기존 태그를 보면 과거 리뷰 이력을 추적 가능.
- 외부 스폰 경로(트리거/이벤트성 웨이브)는 ZombieSpawner._activeZombies를 우회 — 인구 상한·backfill·DespawnFar·LKP 즉시등록이 전부 폴링/저자 예산에만 의존하게 됨. E-001 ReturnWaveTrigger에서 확인(2026-06-11). 새 스폰 경로 리뷰 시 ① 스포너 집계 편입 ② LKP 즉시 등록 ③ 상한 가드 3종을 항상 점검.
- 풀스크린 `SampleSceneColor`(_CameraOpaqueTexture) 재구성 FX: 소스에 투명 큐가 없으므로 `Blend One Zero` 전면 덮어쓰기는 화면의 모든 투명 오브젝트(LKP 고스트, 잔상, 파티클, 머즐)를 지운다 — 순서 문제가 아니라 소스 문제라 renderQueue 조정으로 해결 불가. 안전 패턴 = "바뀐 픽셀만" 알파마스크 블렌드 or 런타임 ScriptableRenderPass(AfterRenderingTransparents) 주입. GlitchFX(2026-06-11)에서 발견.
- DDOL 싱글톤의 sceneLoaded 재구독 패턴: 인스턴스 추적 Unsubscribe(_subbedX 필드)는 검증된 안전 패턴. 단 "씬 로드 후 늦게 스폰되는" 씬 싱글톤은 다음 씬 로드까지 미구독 — "사무실로=씬 리로드" 계약에 기대는 또 하나의 코드.
- 파이프라인 에셋(URP_HighFidelity) 실측 기준: m_RequireOpaqueTexture=1(전역 상시), OpaqueDownsampling=None, MSAA=1x, HDR=on — 카메라별 requiresColorOption 오버라이드는 현재 중복(무해). 리뷰 시 "opaque texture 비용 추가" 지적은 성립 안 함.
- 씬 스폰 마커가 디제틱 프롭 지오메트리(벽 슬랩·맨홀 디스크) 트랜스폼을 겸하는 패턴 — 스폰 위치가 벽 콜라이더와 겹치거나 공중(y>0)일 수 있음. 마커 ≠ 빈 오브젝트면 반드시 월드 좌표·주변 콜라이더 확인. 런 재시작 1회성 플래그는 "사무실로 = 씬 리로드" 계약에 안전하게 기대는 중(비-리로드 재출동 도입 시 전부 재검증).
- IsHiddenFromPlayer(z) API: hidden 필드가 SetRenderers() 내부에서만 써진다 — SetRenderers를 통하지 않는 경로(렌더러 직접 조작 등)가 생기면 hidden 캐시가 stale해짐. 외부 채널이 이 API를 진실 소스로 쓰므로 채널 분리 원칙을 반드시 유지할 것.
- DDOL 부트스트랩 싱글톤(RearThreatHint 패턴): s_instance static은 domain reload 없는 Enter Play Mode에서 이전 런 값 잔존 가능. Bootstrap()의 `if (s_instance != null) return;` 가드는 이 경우 새 GO 생성을 막아 정상처럼 보이지만, 이전 인스턴스가 Destroy됐으면 s_instance는 fake-non-null — Awake()의 중복 인스턴스 파괴 경로가 진짜 보호막.
- DDOL 씬 싱글톤 재획득: _partner(PartnerAIUI) 같은 씬 종속 참조는 Rescan 루프(1s) 안에서 null 체크+재탐색. 단 rescanTimer가 0에 도달하는 최대 1초 지연 노출 창이 항상 존재 — 찰나 창에 이벤트 발화가 필요한 시스템엔 부적합(RearThreatHint 토스트는 허용 가능 수준).
- ZombieController.IsAttacking: Windup/Lunge/Grapple만 포함, Recover는 미포함. 공격 후 Recover 구간(자세 복원 중)은 실물이 다시 숨겨질 수 있음 — 시야 복귀 전 재은폐가 의도인지 리뷰 필요(2026-06-12 발견, 낮은 우선순위 게임 느낌 판단).
- ReturnWaveTrigger _minSpawnDistEffective 미초기화 경로: SpawnWave 코루틴 시작 전에 PickFairIndex가 호출되는 경로는 없으나, PickFairIndex의 `_minSpawnDistEffective > 0f` 폴백이 안전망 역할. 단 float 기본값 0f + `> 0f` 조건이 정확히 맞물리는 구조임을 기억.
- RunHarvest.Instance null 검사 후 StrainHarvestFX.OnZombiePurged 호출 순서: ZombieController.HarvestStrain()이 RunHarvest.Instance null이면 조기 리턴하지만, StrainHarvestFX 내부에서 다시 RunHarvest.Instance를 조회한다 — 이중 null 검사라 안전하지만, 첫 가드를 제거하면 StrainHarvestFX가 FX(시각)는 실행하고 입금만 누락하는 분리 버그 가능성이 있음.
- PartnerAIUI는 씬 오브젝트(DDOL 아님) — RearThreatHint 코드 주석에 "DDOL이라"고 잘못 기재됨(2026-06-12 확인). DDOL 컴포넌트가 씬 오브젝트를 _partner 등으로 캐시할 때, 씬 리로드 후 Unity fake-non-null(파괴된 GO, C# null 체크는 통과) 함정이 발동 — 반드시 `if (_partner == null || !_partner)` Unity null 체크(또는 sceneLoaded 이벤트에서 null 리셋) 필요.
- RearThreatHint 레벨 트리거 재동기화(ZombieLKPSilhouette): renderers[0].enabled 단독 감시보다 t.hidden 캐시 기반 재동기화(`t.hidden == vis`)가 더 강인 — hidden은 SetRenderers만이 쓰는 단일 진실 소스라 다중 렌더러/파티클 0번 슬롯 혼동 문제를 완전 회피.
- FirePulse/FireFootprint에서 SetActive(true) 전에 PositionPulse가 카메라 null로 갱신을 skip하면 위치 (0,0,0)에서 1프레임 렌더 가능 — SetActive를 위치 갱신 성공 후로 순서 교체 필요(씬 전환 첫 프레임 함정).
- _stepQueue(시간차 발자국): scaled Time.time 기반이라 씬 전환+DDOL 구조에서 이전 런의 큐가 잔존할 수 있음 — SceneManager.sceneLoaded 구독으로 Clear가 올바른 위치. dt<=0f 조기 리턴은 timeScale=0 구간을 정확히 동결하지만 씬 전환 클리어 경로를 막기도 함.
