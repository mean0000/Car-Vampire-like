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
