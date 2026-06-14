---
name: projectile-pool-pattern
description: 투사체 공유 풀 시스템 — 자작 레드오렌지 발광구(URP Unlit 가산). 나머지 원거리 종(스핏·부채탄·링탄·유도탄) 재사용 토대
metadata:
  type: project
---

# ProjectilePool — 투사체 공유 시스템 (2026-06-13, Venodonte와 함께 신설)

원거리 종의 **재사용 토대**. 근접(Caniathrox)+원거리(Venodonte) 두 틀 중 원거리 축의 핵심 신규.

## 파일
- `Assets/_Project/Scripts/ProjectilePool.cs` — MonoBehaviour(씬 1개). prewarm 풀, Fire(origin,dir,speed,range,lifetime). 스포너가 만들어 모든 사수에 같은 참조 주입.
- `Assets/_Project/Scripts/AcidGlob.cs` — 풀링 단위 1발. 직선 비행·유도 0·range/lifetime 소멸 후 풀 반납.

## ★자작 발광구 (Vefects 안 씀 — URP 비호환)
Vefects Projectile 3팩은 BIRP surface라 URP 마젠타(ShaderHasError=False도 거짓신호, 톤게이트 실측). 그래서:
- 구 메시(프리미티브에서 메시만 추출) + **URP/Unlit** 머티리얼(전 글롭 공유).
- ★URP/Unlit엔 **_EmissionColor 없음** — 발광은 **HDR _BaseColor(>1)**가 직접 낸다(예: (1.7,0.45,0.12) 레드오렌지). 가산 블렌딩 + 씬 블룸이 글로우.
- 가산 셋업: _Surface=1(Transparent)·_Blend=1(Add)·_SrcBlend/_DstBlend=One·_ZWrite=0·renderQueue=Transparent + 키워드 `_SURFACE_TYPE_TRANSPARENT`·`_BLEND_ADD`. 셰이더 프로퍼티 존재 확인됨(HasProperty True).
- TrailRenderer(꼬리 0.12s) 같은 공유 머티리얼 — "날아오는 선"으로 읽히게.
- 색 캐넌: 적 위협=레드오렌지(공격 문법 §5). 정지 디스크렌더에서 어두운 배경 위 또렷한 주황 발광 확인.

## 재사용 방향 (다음 종)
같은 풀로 스핏(단발 글롭)·부채탄(N-way=dir 여러 각도로 Fire 반복)·링탄(전방향 Fire)·유도탄(AcidGlob에 유도 옵션 추가) 다 커버. 종마다 globColor/globRadius/globSpeed만 바꿔 재사용. 부채/링은 사수가 Fire를 각도 분산해 여러번 호출(풀은 그대로).

## 헌법 경계 (중요)
투사체 비행 위치는 코드가 직선으로 만든다 — **헌법 위반 아님**. "코드가 위치/포즈 안 만듦"은 *캐릭터 본체의 애니메이션 클립*에 대한 것. 글롭엔 클립이 없다(발사된 독립 오브젝트). 본체(Venodonte) 모션은 여전히 클립 루트모션이 진실.

## 검증 한계
편집모드에서 풀 Build·Fire·머티리얼 색은 검증됨. **글롭 실제 비행·가산발광 가독성·꼬리·"날아오는 위협" 체감은 유저 ▶**(AcidGlob.Update는 런타임만).
[[venodonte-attack-statemachine]] [[animevent-fire-timing]]
