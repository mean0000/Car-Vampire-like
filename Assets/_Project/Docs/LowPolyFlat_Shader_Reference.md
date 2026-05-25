# LowPolyFlat 셰이더 참고 문서

> 참고용 — 2026-05-25 작성

## 파일 위치

| 파일 | 경로 |
|---|---|
| 셰이더 | `Assets/_Project/Shaders/LowPolyFlat.shader` |
| 기본 머티리얼 | `Assets/_Project/Material/M_LowPolyFlat.mat` |

---

## 개요

URP 17.0.3 (Unity 6)용 로우폴리 플랫 셰이딩 셰이더.  
각 폴리곤 면이 단색으로 보이는 로우폴리 특유의 깔끔한 면 분리감을 구현.  
`ddx`/`ddy`로 face normal을 재계산해서 스무딩 없이 하드 엣지 효과를 냄.

---

## 핵심 원리 (ddx/ddy 플랫 셰이딩)

```hlsl
float3 dx = ddx(IN.positionWS);
float3 dy = ddy(IN.positionWS);
float3 flatNormalWS = normalize(cross(dy, dx));
```

GPU는 2×2 픽셀 쿼드 단위로 Fragment를 실행.  
`ddx`/`ddy`는 이웃 픽셀 간 월드 포지션 차이를 반환하고,  
두 벡터의 외적 = 해당 삼각형의 face normal (삼각형 안에서 상수).  
→ 면 전체가 동일한 조명값을 가져 플랫 셰이딩 완성.

---

## Inspector 파라미터

| 파라미터 | 기본값 | 설명 |
|---|---|---|
| `Color` | (0.5, 0.55, 0.45) | 폴리곤 기본 색상 |
| `Shadow Strength` | 0.6 | 0 = 그림자 무시, 1 = 그림자 완전 반영 |

---

## 라이팅 구조

- **Diffuse**: Lambert (NdotL), 스펙큘러 없음 → 매트한 표면
- **Ambient Floor**: 0.15 고정 → 뒷면이 완전히 검게 되지 않음
- **Shadow**: URP Cascade Shadow Map 연동, `_ShadowStrength`로 강도 조절
- **Fog**: `MixFog()` 자동 적용

---

## Pass 구성

| Pass | 역할 |
|---|---|
| `UniversalForward` | 메인 렌더 (플랫 셰이딩 + 라이팅) |
| `ShadowCaster` | 이 오브젝트가 다른 오브젝트에 그림자를 드리움 |
| `DepthOnly` | URP의 SSAO, DoF 등 뎁스 기반 이펙트 지원 |

---

## 성능

- 텍스처 샘플: **0** (텍스처 없음)
- 추가 연산: `ddx`/`ddy` 각 1회, `cross` + `normalize` 1회 → 매우 가벼움
- **SRP Batcher 호환** (모든 프로퍼티 `CBUFFER_START(UnityPerMaterial)` 안에 선언)
- 요구 Shader Model: **3.5+** (모바일 포함 대부분 지원)

---

## 사용 방법

1. 로우폴리 메시의 Renderer 컴포넌트 선택
2. Material 필드에 `M_LowPolyFlat` 드래그
3. Inspector에서 `Color` 값으로 각 오브젝트 색상 지정

색 팔레트처럼 오브젝트마다 Color만 바꿔서 사용하면 됨.

---

## 주의사항

- 메시의 노멀 설정과 무관하게 face normal을 재계산하므로, 기존 노멀 데이터는 ShadowCaster Pass에서만 사용됨
- Shader Model 3.5 미만 환경에서는 FallBack `Universal Render Pipeline/Lit`으로 자동 전환
- 투명도(Alpha) 지원 안 함 — 투명 오브젝트는 별도 셰이더 필요
