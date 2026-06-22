# 핸드오프 — 도심 블록아웃 모듈 키트 + 자동배치 제너레이터 (2026-06-16)

## 0. 한 줄
큰 빌딩 도심을 **뱀서식 열린 맵**으로 블록아웃하는 그레이박스 모듈 키트 + 시드 기반 자동배치 제너레이터를 시공·리뷰 통과. **광장(중앙 킬존)은 유저가 직접 손으로 깐다** — 제너레이터는 광장 풋프린트를 *빈 채로 예약*만 하고 그 둘레의 도심 직물(도로·빌딩·바닥)을 자동 생성한다.

## 1. ★핵심 설계 원칙 (이후 모든 맵 작업의 렌즈)
> **바닥(floor)이 플레이 공간이고, 빌딩은 "벽/경계"다.**

- 뱀서 맵이 오브젝트가 적은 이유 = 플레이가 *열린 바닥*에서 일어나기 때문. 빌딩은 도심 수직 룩 + 경계만 주고, 액션(카이팅·둘러쌈·돌파)은 빌딩 *사이* 도로·광장의 네거티브 스페이스가 가져간다.
- 레퍼런스 = **Human Resources** (높은 아이소, 거대 빌딩 사이 도로/교차로 열린 바닥에서 플레이).
- 우리 캐넌과 정합: 45°/15m 카메라 + 광장/집하장(자연스러운 끌림 독트린) + "타운 = 어깨맞댄 두 줄 + 좁은 거리".
- ⚠️ **45° 카메라 차폐 주의:** 카메라 앞쪽 키 큰 빌딩은 시야를 가린다 → `Bldg_L`(히어로 타워)은 그리드 가장자리/먼쪽에 가중 배치(`largeEdgeBias`). 완전 해결은 후속(벽 디더 페이드 캐넌).

## 2. 역할 분담 (중요)
| 영역 | 누가 | 비고 |
|---|---|---|
| 도심 직물 — 도로 격자·빌딩 블록·바닥 | **제너레이터 자동** | 시드로 결정론 생성 |
| **중앙 광장 (열린 킬존)** | **유저 손제작** | 제너레이터는 빈 footprint만 예약 |

→ 광장을 직접 깔 거면 **`plazaPropChance`를 0으로** 두면 자동 차량(prop)도 안 떨어져 완전히 비워진다. (현재 씬 인스턴스 값 = 0.12라 차량 3대 떨어짐. ⚠️ 코드 default를 바꿔도 씬 저장값이 이김 — 씬 인스턴스 Inspector에서 직접 0으로 바꿀 것.)

## 3. 산출물 (★미커밋 — 레이아웃 판정 후 커밋)
- **`Assets/_Project/Scripts/CityBlockGenerator.cs`** — 제너레이터 본체.
- **`Assets/_Project/Prefabs/Blockout/`** — 모듈 프리팹 5종:
  - `Floor_10` (10×10m, h0.2) · `Bldg_S` (10×10m, h12) · `Bldg_M` (20×20m, h24) · `Bldg_L` (20×20m, h45 히어로 타워) · `Prop_Car` (2×4.5m, h1.5)
  - 구조 = `root(피벗=바닥 y0)` → `Mesh(Cube + BoxCollider)`. 회색 단색 머티리얼 3종(`Blockout_Floor/Building/Prop.mat`).
- **`Assets/_Project/Scenes/_CityBlockBlockout.unity`** — 제너레이터 배선 + Generate 결과 포함.
- 검증 렌더: `_vidframe/cityblock_v2_topdown.png` (탑다운 직교, seed 12345 = 바닥 196 / 빌딩 76 / 차량 3).

## 4. 사용법
1. 씬 `_CityBlockBlockout` 열기 → `CityBlock_Generator` GameObject 선택.
2. Inspector 우클릭 컨텍스트 메뉴 **`Generate`** (다시 누르면 기존 것 Clear 후 재생성), **`Clear`** (생성물만 삭제).
3. 생성물은 `CityBlock` 부모 Transform 아래에만 모임 — 씬의 다른 오브젝트 안 건드림.
4. **시드 락 워크플로:** `randomizeSeed`를 켜고 Generate 반복 → 맘에 드는 레이아웃 나오면 Inspector의 **`lastUsedSeed`(읽기전용)** 값을 `seed`에 복사하고 `randomizeSeed` 끄기 → 그 레이아웃 고정.

### 노브 맵
| 노브 | 기본 | 효과 |
|---|---|---|
| `gridCells` | 14×14 | 맵 전체 크기(셀 수) |
| `cellSize` | 10 | 셀 한 변(m). 10m 모듈 그리드 |
| `roadStride` | 4 | 도로 회랑 간격(↑클수록 블록·빌딩 슈퍼블록 커짐) |
| `roadWidth` | 1 | 도로 폭(셀) |
| `plazaCenter` | (-1,-1)=자동중앙 | 광장 중심 셀 |
| `plazaSize` | 4×4 | **광장(유저 킬존) 넓이** |
| `plazaPropChance` | 0.12 | 광장 자동 차량 확률 — **유저 직접 깔면 0 권장** |
| `weightS/M/L` | 0.55/0.30/0.15 | 빌딩 크기 분포 |
| `largeBuildingsSpanTwoCells` | ON | M/L이 2×2 셀 차지 시도 |
| `heightJitter` | 0.12 | ±12% 높이 지터(바닥은 박힌 채 top만) |
| `largeEdgeBias` | 0.75 | L 타워 가장자리 쏠림(시야 차폐 방지) |
| `seed` | 12345 | 결정론 시드 |

## 5. 리뷰 게이트 (Stab + Codex 병렬) — 처리 내역
**8 실버그 수정, 1 오진 스킵:**
- **M-2 (둘 다):** `CanSpan2x2`가 2×2 빌딩의 +1 셀 광장 침범 미체크 → 전 4셀 `IsPlazaCell` 검사 추가 (광장 킬존 보호).
- **H-2 (Stab):** `plazaCenter` 수동 설정 시 광장이 그리드 밖으로 삐짐 → `GetPlazaRect`로 그리드 안 완전 clamp.
- **H-3 (둘 다):** 에디트타임 `Instantiate` = 프리팹 링크 끊김 → `InstantiateModule`(`PrefabUtility.InstantiatePrefab`, `#if UNITY_EDITOR`)로 링크 보존. **유저가 ProBuilder로 모듈을 직접 깎을 거라 링크 보존이 중요** — 원본 프리팹 수정이 재Generate 시 반영됨.
- **M-1:** 레이어 하드코딩 → `LayerMask.NameToLayer` 검증 + fallback 경고.
- **M-4:** `cellSize` `[Min(0.1f)]` 가드.
- **M-3:** `lastUsedSeed` 읽기전용 노출 (시드 락 워크플로 지원).
- ★**H-1 오진 (스킵):** Stab "heightJitter가 빌딩을 띄운다" → 프리팹 직접 확인 결과 `root(scale1, y0)` → `Mesh(localPos.y=half)` 구조라 루트 Y스케일은 **바닥 y0를 원점으로 균등 스케일** = base는 박힌 채 top만 늘어남. 안 뜬다. Codex는 안 잡음. (자기인증 금지 — 프리팹 YAML 직접 열어 판별.)

## 6. 정직한 한계
- 그레이박스 회색 박스 단계 — 디테일/라이팅 0.
- **NavMesh 미베이크.**
- 검증 캡처는 **탑다운 직교 렌더**지 실제 45°/15m 게임 카메라가 아님 — 차폐는 게임 카메라로 따로 확인 필요.
- 빌딩 풋프린트가 불규칙(어깨맞댐) → 깔끔한 맨해튼 격자보다 "부서진 도심" 느낌(톤엔 부합).

## 7. 다음 단계
1. **유저: 광장(킬존) 손제작** + 레이아웃 시드/블록·광장 크기 판정.
2. **NavMesh 베이크** → 모듈 이음새 단절 없나 확인(가장 흔한 함정).
3. **플레이어 + 몹 한 마리 떨궈 45°/15m 게임 카메라로 공간감 확인** — 차폐·동선·카이팅 룸.
4. 그레이박스 통과 후 → Synty 프리팹으로 **드레싱**(빌딩 박스를 실제 메시로 교체).

## 8. 연관
- 메모리: `project_2026_06_16_city_blockout_generator`
- 자연스러운 끌림 독트린: `docs/00_authority/2026-06-14-natural-pull-doctrine.md`
- 수직 슬라이스 전환: 메모리 `project_2026_06_14_vertical_slice_pivot`
