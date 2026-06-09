# 피격/화면 피드백 핸드오프 (2026-06-09 오후)

같은 날 오전 [총기 전투 타격감 핸드오프](2026-06-09-gun-combat-juice-handoff.md)의 후속. 브랜치 `main`, **작업 씬 = `Greybox_ScanLit.unity`**. 이 세션은 ①스테일 SerializeField 감사·동기화 ②조준선 레이저화 ③피격 화면/캐릭터 피드백(A+B) 구현으로 구성.

> 작업 방식: 게임감이라 Opus 메인세션이 설계+구현, Gameplay(opus 오버라이드) 위임 + Stab/Codex 병렬 리뷰.

---

## 1. 스테일 SerializeField 감사·동기화 (오전 핸드오프 §0 후속)

오전 핸드오프가 경고한 "코드값과 씬 직렬화값 불일치"를 실제 감사. `new SerializedObject(component).FindProperty(...)`로 씬값을 읽어 코드와 대조.

- **유저 방침**: "씬값에 맞추되 **탄 두께(`trailWidth`)만 코드값으로**."
- 적용:
  - `trailWidth` → 코드값 **0.07** 로 씬값(0.16)을 덮어씀(MCP SerializedObject 세팅 + SaveScene).
  - `shotVolume` 0.3 → **0.185**, `reloadVolume` 0.3 → **0.25** (코드 default 동기화).
- 교훈 재확인: **씬 인스턴스에 SerializedObject로 세팅 후 `EditorSceneManager.SaveScene` 필수.** 코드 default만 바꾸면 무효.

## 2. 조준선 = 레이저화 (PlayerCombat.cs)

유저 요청: "빨간 선이 끝까지 안 이어지고 일정 길이부터 점점 사라지게(레이저처럼)."

- 신규 필드: `laserLength`(6), `laserFadeStart`(0.45).
- `LineRenderer.colorGradient`에 **알파 페이드 그라디언트**(0~fadeStart 풀알파 → 1.0에서 알파 0) 1회 빌드.
- `UpdateLaser`: 빔 길이를 `min(히트거리, laserLength)`로 캡, startColor/endColor 균일 세팅 제거. → 일정 길이부터 페이드아웃되는 레이저 룩. **유저 컨펌(매우 만족).**

## 3. 피격 화면/캐릭터 피드백 (A+B) — 이 세션 메인

### 리서치 (codex/art/pm/gameplay 병렬 + 한국 인디 조사)
- 풀스크린 화이트 플래시는 **DOOM(1993) 계보 = 로우인포**, 톱다운에선 정보량 낮고 올드함. 한국 인디(Skul 등) 유저는 풀스크린 화이트를 "버그"로 인식하는 사례 보고.
- 결론 → **국지적 피드백**으로 피벗: (A) 캐릭터 아바타 발광 + (B) 화면 가장자리 비네트 펄스. (C) 풀스크린 플래시는 **강타(heavy hit) 전용으로 강등**.

### (A) 아바타 발광 히트플래시 — PlayerVisualFeedback.cs
- 캐릭터 바디 **SkinnedMeshRenderer 13개**에 `MaterialPropertyBlock`로 `_EmissionColor`를 순간 피크(`hitFlashEmission`=(2.2,2.0,1.7))로 올렸다가 `hitFlashDuration`(0.12s) ease-out 페이드.
- **공유 머티리얼 불변**(MPB라 인스턴싱 안 깨짐). AutodeskInteractive 셰이더는 `_EMISSION` 키워드 OFF여도 MPB `_EmissionColor` 렌더됨(검증).
- 단일 `_flashTimer` = 디바운스(연타 시 피크로 리프레시, 스택 없음).
- **언스케일드 필수**: 히트스탑(timeScale≈0.05) 중에도 진행해야 하므로 `Time.unscaledDeltaTime`.

### (B) 피격 엣지 비네트 펄스 — HudV2Controller.cs
- 매 피격마다 **어두운 적색 화면 가장자리 비네트**(`hitEdgeColor`=#5A0010, peak alpha 0.35, 0.18s, OutQuad)를 펄스. 런타임 Image + DOTween `.SetUpdate(true)`.
- 방사형 스프라이트 256×256, `SmoothStep((d-inner)/ramp)`. `BuildRadialSprite(innerRadius, out sprite, out tex)`로 리팩터(저체력 비네트 필드 stomping 버그 수정).

### (C) 풀스크린 플래시 = heavy 전용 강등
- `heavyHitDamageFrac`(0.20) 이상 데미지에만 풀스크린 가산 플래시(`hitFlashColor`=#FFE8CC, peak 0.28, 0.22s). 일반 피격은 A+B로만.
- 구 `hitFlashPeakAlpha`/`hitFlashDuration` 필드는 고아라 **제거**.
- **가산 블렌드**가 다크 씬에서 알파보다 펀치 있음 → 신규 `ZombieCrush/UIAdditive` 셰이더(+`.mat`) 생성(`Blend One One`, ZWrite Off, premultiplied).

### 저체력 위험 비네트(기존 유지)
- color #8B0A0A, onset 0.60, pulseThreshold 0.15, 정적 alpha 0.72, 펄스 0.55~0.85, cycle 1.6s(`[Min(0.1f)]`). `Time.unscaledTime` 기반.

### 정리/안전성
- `PlayerController.OnDestroy`에서 `OnPlayerDamaged = null`(static 이벤트 — 씬 리로드 잔존 구독자 제거).
- `HudV2Controller.OnDestroy`: 트윈 Kill+null **후** 스프라이트/텍스처/머티리얼 Destroy.

---

## 4. 리뷰 결과 (Stab + Codex 병렬)

**A+B 구현 자체엔 Critical/Major 신규 버그 없음.** 검증된 긍정: 언스케일드 타임 일관, 런타임 리소스 OnDestroy 정리, MPB null 안전, 디바운스 정확, 0-나눗셈 가드.

### ⚠️ 두 리뷰어 공통 지적 = 기존 코드 버그 2건 (미수정, surgical 원칙상 보류)

| # | 위치 | 문제 |
|---|---|---|
| 1 | `PlayerVisualFeedback.CreateRing` | 소음 링 `new Material(ringShader)`를 OnDestroy에서 미파괴 → **씬 리로드마다 네이티브 머티리얼 누수** |
| 2 | `HudV2Controller.UpdatePartsToast` | `_toastTimer -= Time.deltaTime`(scaled) → 히트스탑 중 토스트 열리면 **1.8s가 36s처럼 지속** |

- 둘 다 한 줄 수정이고 실제 누수/UX 버그. A+B 이전부터 존재 → **다음 세션에서 처리 권장**(또는 유저 지시 시).
- `static OnPlayerDamaged = null` 정리 순서 우려도 언급됐으나, 각 구독자가 `OnDisable`에서 `-=` 자기해제하므로 실질 결함 아님(방어적 코드).

---

## 5. 다음 작업

1. **A+B 게임감 플레이테스트·튜닝** — 아바타 발광 세기/엣지 펄스 색·알파.
2. **기존 버그 2건 수정**(§4) — 링 머티리얼 누수, 토스트 타이머 scaled.
3. (보류) 플로팅 데미지 숫자, 크로마틱 애버레이션, 저체력 채도 감소(Phase 2).

## 6. 변경 파일

- `Assets/_Project/Scripts/`: `PlayerCombat.cs`(레이저+감사), `PlayerVisualFeedback.cs`(아바타 발광 A), `HudV2Controller.cs`(엣지 펄스 B + 플래시 강등 C + 위험 비네트), `PlayerController.cs`(OnDestroy static 정리)
- `Assets/_Project/Shaders/`: `UIAdditive.shader`/`.mat`(신규 가산 UI)
- `Assets/_Project/Scenes/Greybox_ScanLit.unity`(직렬화값 동기화), Baking Set / URP Renderer 잔여 WIP
- 콘솔 에러 0.
