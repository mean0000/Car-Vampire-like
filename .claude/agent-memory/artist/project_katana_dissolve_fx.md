---
name: katana-dissolve-fx
description: 칼 나노봇 디졸브 v2 — MPB 구동 런타임 수정 완료(weaponMesh 직렬화+SRP Batcher _Dissolve CBUFFER 밖 이동)
metadata:
  type: project
---

칼(Katana_Mesh) 나노봇 디졸브 v1 구현 (2026-06-21), 런타임 배선 수정 (2026-06-21).

**Why:** 달리기 시 칼이 SetActive로 즉시 사라지던 것을 나노봇 세계관에 맞게 디졸브 연출로 교체.

## 산출물
- 셰이더: `Assets/_Project/Shaders/KatanaDissolve.shader` (`ZombieCrush/KatanaDissolve`)
- 머티리얼: `Assets/_Project/Materials/MAT_Katana_Dissolve.mat`

## 런타임 버그 수정 (2026-06-21)

두 개의 독립된 버그가 동시에 디졸브를 막았음.

### Bug 1: PlayerAnimatorDriver.weaponMesh = NULL
`KatanaWeapon.weaponAnchor`는 `Katana_Mesh`를 가리키고 있었지만,
`PlayerAnimatorDriver.weaponMesh` Inspector 직렬화 값이 NULL이었음.
드라이버 Awake의 자동 연결 경로(`_motor.GetComponentInChildren<KatanaWeapon>`)가
Awake 순서 경합으로 실패해 `_weaponRenderer = null` 상태로 남아 MPB를 구동하지 않았음.

**수정:** `weaponMesh` 필드에 `Katana_Mesh` Transform을 SerializedObject로 직접 할당 + 씬 저장.

### Bug 2: SRP Batcher vs MPB — _Dissolve가 CBUFFER_START(UnityPerMaterial) 안에 있었음
URP의 SRP Batcher는 `UnityPerMaterial` CBUFFER를 Material 단위로 고정 공급한다.
MPB.SetFloat()은 이 CBUFFER를 우회하지 못해 값이 반영되지 않았음.

**수정:** 3개 Pass(ForwardLit/ShadowCaster/DepthOnly) 전부에서
`_Dissolve`를 CBUFFER 밖으로 꺼내 standalone uniform으로 선언.
Properties에 `[PerRendererData]` 어트리뷰트도 추가.

```hlsl
// 수정 후 패턴 (3개 Pass 동일):
CBUFFER_START(UnityPerMaterial)
    // ... _Dissolve 제거됨 ...
CBUFFER_END
half _Dissolve;  // ← CBUFFER 밖, per-draw MPB 오버라이드 가능
```

★이 패턴은 프로젝트 내 MPB로 구동하는 모든 커스텀 URP 셰이더에 적용 필수.

## 구동 체인 (현재 정상)
```
PlayerAnimatorDriver.weaponMesh = Katana_Mesh (직렬화 할당됨)
  → _weaponRenderer = Katana_Mesh.GetComponentInChildren<Renderer>()
  → MAT_Katana_Dissolve (ZombieCrush/KatanaDissolve)
  → Tick(): MPB.SetFloat("_Dissolve", t) → 셰이더 standalone uniform으로 반영
```

## 칼 메시 정보
- Renderer: `Katana_Mesh` (MeshRenderer)
- sharedMaterials[0]: `MAT_Katana_Dissolve`
- sharedMaterials[1]: `SG_Frank_Katana_Blade` (원본 유지)

## 프로퍼티 노브 (Inspector / MPB)
| 프로퍼티 | 기본값 | 설명 |
|---|---|---|
| `_Dissolve` | 0 | 0=솔리드, 1=완전 소멸. MPB로 구동 |
| `_DissolveEdge` | 0.05 | 경계 발광 폭 |
| `_DissolveColor` | (0,4,4,1) HDR | 경계 발광색 (HDR 시안, 블룸 받음) |
| `_SweepBias` | 0.7 | Z방향 sweep 강도 |
| `_NoiseScale` | 6 | 나노입자 밀도 |
| `_ParticleSharpness` | 8 | 입자 경계 선명도 |

## 검증 결과
- 컴파일 에러: 0
- weaponMesh == weaponAnchor: YES
- 정적 캡처(_Dissolve=0.45/0.85): 시안 경계 발광, 입자 분해 확인
- 플레이 런타임 검증: 유저가 달리기로 확인 필요 (Shift + 이동)
