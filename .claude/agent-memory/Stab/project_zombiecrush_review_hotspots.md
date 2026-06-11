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
- 검증된 안전 전제(2026-06-11 기준): 좀비 프리팹 Animator cullingMode=0(AlwaysAnimate) — renderer.enabled 끄기에도 본/BakeMesh 정상. 단 무가드 암묵 의존이라 프리팹 최적화 시 재검증 필요. 렌더러 숨김 채널 관례: DeathFX=GameObject.SetActive, LKP=renderer.enabled, 플래시/스캔=MPB — 채널 분리로 충돌 회피 중.
- 팀 관례: 리뷰 반영 커밋은 코드 주석에 "(리뷰 H-1)" 식 태그를 남김 — 기존 태그를 보면 과거 리뷰 이력을 추적 가능.
