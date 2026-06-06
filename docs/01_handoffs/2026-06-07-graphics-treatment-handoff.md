# 그래픽 처리 레이어 핸드오프 — 2026-06-07

> 선행 핸드오프: `docs/03_reference/2026-06-05-graphics-session-handoff.md`, `docs/03_reference/2026-06-06-cozy-integration-handoff.md`
> 씬: `StyleLab.unity` (그래픽 체크용), 브랜치: `feat/graphics`

---

## 0. 한 줄 요약

스테이지마다 바뀌는 **라이팅은 안 건드리고**, 모든 조건에서 살아남는 **처리(treatment) 레이어**만 작업했다. 그레이드 재작업 + 대기 헤이즈 + **액터 시야방향 프레넬 림(핵심)** 3종. 밤·황혼·밝은낮 3조건 디스크 렌더로 검증 완료 — 액터가 더 이상 바닥에 묻히지 않는다(찰흙 탈출).

---

## 1. 핵심 원칙 (이번 세션의 헌장)

- **태양/앰비언트 위치·색·강도는 스테이지마다 바뀐다 → 절대 하드코딩하지 않는다.**
- 우리가 만드는 건 **조건 무관 처리**: 그레이드, 헤이즈, 액터 림. 어떤 라이팅에서도 동일하게 작동해야 함.
- 검증은 **디스크 렌더만 신뢰**. MCP `Unity_Camera_Capture`는 캐시된 죽은 프레임을 반환(과거 "찰흙" 실패의 근본 원인). 검증 = `RunCommand`로 JudgeCam → `cam.Render()` → RenderTexture → PNG 저장 → Read.
- 텍스처는 유저 판단대로 손대지 않음(텍스처가 문제라는 결론은 유지).

---

## 2. 오늘 적용한 처리 (전부 커밋 전, working tree에 있음)

### 2-1. 포스트 그레이드 — `Assets/_Project/Setting/StyleLab_Post.asset`
| 항목 | 이전 | 변경 |
|---|---|---|
| ColorAdjustments postExposure | 1.35 | **1.0** (과노출 제거) |
| ColorAdjustments contrast | 12 | **20** |
| WhiteBalance temperature | +14 (웜) | **-8** (쿨화) |
| WhiteBalance tint | 2 | **0** |
| SplitToning highlights | (0.30,0.26,0.17) | **(0.22,0.20,0.14)** |
| SplitToning balance | -10 | **-18** |
| Bloom tint | (1,0.95,0.88) 웜 | **(0.92,0.95,1.0)** 쿨 |

> 쿨섀도/웜하이라이트 이중온도는 이미 정확했음 — 과노출만 빼고 대비·쿨밸런스로 다듬음.

### 2-2. 대기 헤이즈 — CristianQiu Volumetric Fog (같은 프로파일 내 `VolumetricFogVolumeComponent`)
| 항목 | 변경 |
|---|---|
| density | **0.10** (0.4 극단 테스트 후 정착) |
| maximumHeight | **10** |
| enableMainLightContribution | **True** |
| scattering | **0.4** |

> StyleLab 테스트 씬은 평평·작아서 헤이즈 깊이감이 약하게 보이는 게 정상. 깊이 있는 실제 레벨에서 살아남는 베이스라인 값.

### 2-3. ★액터 가독성 — 시야방향 프레넬 림 (이번 세션 핵심)
- **새 셰이더**: `Assets/_Project/Shaders/ActorRimLit.shader` (HLSL 직접 작성, Shader Graph 아님)
  - 4패스(ForwardLit/ShadowCaster/DepthOnly/Meta). `UniversalFragmentPBR`로 URP Lit 풀 거동(메인광/그림자/추가광/APV·GI/포그) 유지 + 라이팅 결과 **위에** 프레넬 림을 가산 emissive로 추가.
  - 림 = `pow(1 - saturate(dot(normalWS, viewDirWS)), _RimPower)` × `_RimColor` × `_RimIntensity`.
  - **카메라/시야 방향 기반 → 태양 각도·색·강도와 완전 무관**. 그래서 모든 스테이지에서 동일 작동(= 진짜 처리).
- **적용 머티리얼**: `Assets/ithappy/Zombies_Pack/Materials/Color.mat`
  - 좀비 60종 + 플레이어 스탠딘이 공유하는 단일 머티리얼. FBX(`Base_Mesh.fbx`) 내장 머티리얼(`InPrefab`)을 `ModelImporter.AddRemap`으로 이 외부 `.mat`에 리맵 → 36개 SkinnedMeshRenderer 전부 이 파일을 가리킴.
- **최종 림 값**: `_RimColor (0.82, 0.90, 1.0)` 쿨화이트, `_RimPower 2.0`, `_RimIntensity 2.6`
  - 1.2는 게임 거리에서 너무 미묘 → 2.6으로 상향(밤에 또렷, 낮에 미묘하지만 존재).
  - 강도/색 조정 원하면 이 머티리얼의 `_RimIntensity`/`_RimColor`만 만지면 됨.

---

## 3. 검증 결과 (디스크 렌더, 3조건)

JudgeCam(pos 0,34,-20 / pitch59.5 / FOV38)으로 같은 처리를 태양만 바꿔 렌더(테스트 후 태양 원복):

- **밤**(태양 0.5/쿨) — `_cond_night.png`: 좀비가 쿨 림으로 또렷이 빛나며 어둠에서 극적 분리. 상자/건물은 림 없이 묻힘. 중앙 웜 풀 = "분위기는 월드, 밝기는 액터" 정확 구현. **림이 진가 발휘하는 조건.**
- **밝은 낮**(태양 4.0/중립) — `_cond_day.png`: 액터 여전히 분리, 림은 미묘하지만 존재.
- **기본 황혼** — `_treat_rim_crop.png`: 액터 클린 분리.

→ 시야방향 림은 라이팅 전 범위에서 하드코딩 없이 살아남음. **figure/ground 회복 = 찰흙 탈출 확인.**

진단 PNG(리포 루트, 정리 가능): `_treat_*.png`, `_cond_night.png`, `_cond_day.png`, `_rim_*.png`.

---

## 4. ★언제든 되돌려 볼 수 있는 복원점 (유저 요청)

### COZY 들어가기 전 커밋 = **`7d485b1f`**
> `audit(style) STEP12: 틸트시프트 3에이전트 감사 + 2D모드/비교캡처`
> COZY는 바로 다음 커밋 `fb7d5f1a`("COZY 연결 후 라이팅 화해")에서 처음 들어옴. 즉 `7d485b1f` = **COZY 직전 상태**.

**⚠️ 오늘 처리 작업은 아직 커밋 안 됨(working tree).** 그냥 checkout하면 충돌/손실 위험. 안전 절차:

**방법 A — 오늘 작업 먼저 커밋하고 자유롭게 왕복(권장):**
```bash
git add Assets/_Project/Shaders/ActorRimLit.shader Assets/_Project/Setting/StyleLab_Post.asset Assets/ithappy/Zombies_Pack/Materials/Color.mat Assets/ithappy/Zombies_Pack/Meshes/Base_Mesh.fbx.meta
git commit -m "feat(style): 처리 레이어 — 그레이드 재작업 + 헤이즈 + 액터 프레넬 림(조건무관)"
# pre-COZY 보러 가기:
git checkout 7d485b1f      # detached HEAD로 그 시점 씬 확인
# 돌아오기:
git checkout feat/graphics
```

**방법 B — 커밋 없이 잠깐만 확인(stash):**
```bash
git stash push -u -m "treatment-WIP"   # 오늘 작업 임시 보관
git checkout 7d485b1f                   # pre-COZY 확인
git checkout feat/graphics
git stash pop                           # 오늘 작업 복구
```

> Unity 열린 상태에서 checkout 시 씬/머티리얼이 디스크에서 바뀌므로, Unity가 리임포트/리로드하게 두고 확인할 것.

### 기존 안전망 stash (이미 존재)
- `stash@{0}` = `pre-COZY-inspect: feat/graphics WIP` — 지난 세션 pre-COZY 확인용으로 만든 백업. 처리 작업 안정화되면 `git stash drop stash@{0}`로 정리 가능(급할 것 없음).
- `stash@{1}` = `main` 위 Unity 재임포트 byproduct.

---

## 5. 다음에 할 수 있는 것

- 처리 값 미세조정(림 강도/색, 헤이즈 밀도) — 실제 깊이 있는 레벨에서 재검증.
- 멀티조건 검증을 영구 프리셋(낮/황혼/밤/실내 라이팅 세트)으로 박아두면 매번 재현 가능(이번엔 스크립트 즉석 시뮬로 처리).
- 진단 PNG 리포 루트에서 정리.
- 텍스처는 별도 트랙(유저 판단). 처리 레이어는 텍스처와 독립이라 무손실.
