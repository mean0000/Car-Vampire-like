# 탑다운 맵 레퍼런스 웹 조사 (2026-06-13)

> **목적**: 맵/레벨 디자인을 감이 아니라 *검증된 최신 게임*에 박기 위한 웹 조사. 유저 지시 — "전형적 탑다운 게임 맵을 조사, 최대한 최신, 약체 기준 금지."
> **방법**: 3레인 병렬(오케=액션/인카운터, 에이전트1=익스트랙션, 에이전트2=호드+탑다운 LD 원론). 전부 WebSearch/WebFetch 현재 정보.
> **상태**: 조사 완료, **앵커 확정은 유저 판정 대기**(유저가 자기 픽 가져와 정면 비교 예정).

## 1. 레인별 앵커

| 레인 | 앵커 게임 | 출시 | 훔칠 것 |
|---|---|---|---|
| **맵 구조(백본)** | **Escape from Duckov** | 2025-10 | 비대칭 추출구·난이도 띠·동심원 보상·**시야 콘 긴장** |
| 〃 생성법 | ZERO Sievert | 2023(1.0) | 고정 랜드마크+절차 디테일·경계 5초 추출·줍는 순간 노출 |
| **호드 흐름** | **DRG: Survivor** | 2025-09 1.0 | 지형을 능동 조형·무리를 회랑으로 몰아 한 줄 학살 |
| 〃 | **Megabonk** | 2025-09 | 고저차로 시야선·도주선·압박 그라데이션(솔로 개발 증명) |
| **방·인카운터 craft** | **Hades II** | 2025 정식 | 레벨이 적을 규정·벽치기 방·방 크기 점층 |

## 2. 1등 앵커 = Escape from Duckov (왜)

1. **최신 + 검증**: 2025-10 출시, 3주 300만 장·95% 긍정·메타 77. 그해 최대 인디 히트(약체 아님).
2. **장르 골격 정확 일치**: 싱글 PvE 탑다운 — 멀티 PvPvE 대작(ARC Raiders 등)과 달리 패턴이 *그대로* 이식.
3. **우리 코어 메커닉 검증**: Duckov 긴장의 본진 = "limited sight cone 탐색". **우리가 이미 동결한 시야 콘이 그 긴장의 살아있는 증거** ([[feedback_vision_cone_always_on]]).

⚠️ **중대 단서 — Duckov에서 훔치는 건 맵/추출 *구조*지 *전투*가 아니다.** Duckov 전투는 엄폐 사격(택티컬). 우리 전투는 대시-처단(액션)으로 동결([[2026-06-13-action-processing-anchor]]). Duckov의 공간/추출 골격만 취하고, 호드 전투 흐름은 DRG:S/Megabonk에서 취한다. **Duckov 전투를 통째로 베끼면 유저가 폐기한 좀보이드-택티컬로 회귀** — 금지.

## 3. 층위 차용 (단일 게임이 우리 게임은 아니다)

- **Duckov** = 공간/추출 골격: 추출구 ≥3개 비대칭 비용(무조건 개방·노출 / 키·은밀 / 플레어+대기·고위험 수렴), 존=난이도 띠, 중앙 고밀도·고노출 ↔ 외곽 저밀도·저위험 동심원.
- **Zero Sievert** = 생성법(솔로 친화): 고정 랜드마크 + 절차 디테일(완전 손배치도 완전 절차도 아닌 중간), 경계 추출 5초 점유, **줍는 순간 위치 노출 고보상**.
- **DRG:S + Megabonk** = 호드 전투 흐름: 지형이 *능동 도구* — 무리를 회랑/고저차로 끊고 흘리고 한 줄로 압축해 벤다. 이동·포지셔닝이 빌드만큼 중요. (우리 대시-처단과 메커닉적 정합.)
- **Hades II** = 방/인카운터 craft: 레벨이 적을 규정(공간 먼저, 적이 거기 맞춤), **벽으로 둘러싼 방=벽치기 데미지**(우리 Fulgurodonte 램-벽-그로기 검증), 방 크기 점층.

## 4. 탑다운 맵 원론 (craft 규칙서 — MY.GAMES + Level Design Book)

- **시야선**: 비회전/부감이면 레이아웃 높이를 캐릭터 키 수준 제한, 큰 랜드마크는 가장자리만. 45°라 약간의 수직성 허용되나 *전술 의미 있을 때만*(조망/도주), 장식용 고저차 금지.
- **가독성(미니맵 없이)**: 바닥/벽 색 대비로 이동 가능 공간 즉시 전달. **차폐물은 항상 더 어둡게**. 랜드마크/조명으로 정위치.
- **밀도 리듬**: **40% 개방 → 30% 중밀도 → 30% 밀집** 교대. 차폐 밀도가 페이싱을 직접 결정.
- **아레나 vs 회랑(Valve)**: 회랑=자연 초크. 한 맵에 장/중/근거리 3종 전투 구역 공존. 다중 접근로로 예측성 방지.
- **매복·초크**: 목표물(보상)을 자연 초크 근처에 배치 → 수렴 유도 → 교전 발생. 메모러블 피처 1개가 맵 정체성.
- **페이싱**: 레벨=비트 연속, **고/저강도 교대 필수**(10분+ 고강도=슬로그). 전투 뒤 휴지(우리 "숨·정적→한 발").

## 5. 확인된 함정 (반례)

- **Death Must Die**(91% 긍정인데도) "평면 반복 지형"으로 명시 비판 → 경로 선택형으로 개편 중. **평면 반복 아레나는 호평작에서도 약점.** 우리가 피할 정확한 함정.
- **Halls of Torment** 커뮤니티: "초크포인트를 더 의도적으로 만들었어야" → 맵이 더 적극 개입했어야 한다는 교훈.

## 6. 우리 맵에 주는 의미 (기존 arbitrary 설계 대비)

폐기된 "임의 선형 시설"에서 → **근거 있는 Duckov형 익스트랙션 구조**로 진화 가능: 난이도 띠 존 + 비대칭 추출구 + 동심원 보상 + 시야 콘 긴장(있는 무기). 호드 전투는 DRG:S식 "지형으로 무리 끊기", 방은 Hades식 craft. 평면 반복은 명시적 금지.

## 출처
**게임**: [Duckov(Wiki)](https://en.wikipedia.org/wiki/Escape_from_Duckov) · [Duckov 추출구(PCGamer)](https://www.pcgamer.com/games/action/escape-from-duckov-extraction-points/) · [Duckov 3M(TheSixthAxis)](https://www.thesixthaxis.com/2025/11/08/escape-from-duckov-hits-3-million-sales/) · [ZERO Sievert(TechRaptor)](https://techraptor.net/gaming/guides/zero-sievert-forest-map-and-locations-guide) · [DRG:S(PCGamer)](https://www.pcgamer.com/games/roguelike/deep-rock-galactic-survivor-review/) · [Megabonk(Wiki)](https://en.wikipedia.org/wiki/Megabonk) · [Hades II(Wiki)](https://en.wikipedia.org/wiki/Hades_II) · [Hades LD(Kotaku)](https://kotaku.com/hades-level-design-is-less-random-than-it-seems-1845254545)
**원론**: [MY.GAMES LD1](https://medium.com/my-games-company/top-down-shooter-level-design-how-map-design-supports-game-mechanics-6ae39fdd095d) · [MY.GAMES LD2](https://medium.com/my-games-company/level-design-in-top-down-shooters-creating-diversified-experience-using-maps-ff9e21c8e600) · [Level Design Book—Pacing](https://book.leveldesignbook.com/process/preproduction/pacing) · [Flow](https://book.leveldesignbook.com/process/layout/flow)
