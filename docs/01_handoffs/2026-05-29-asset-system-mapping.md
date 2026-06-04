# 에셋 → MVP 시스템 매핑 (핸드오프 문서)

> **작성일**: 2026-05-29
> **목적**: GDD v2.7 MVP 스펙(§17)의 각 시스템을 **어떤 보유 에셋·API·기존 코드로 구현할지** 개발팀에 넘기기 위한 매핑.
> **선행 문서**: `2026-05-27-new-direction-gdd.md` (§17 MVP 스펙), 보유 에셋 상세는 메모리 `owned-assets`.
> **검증 수준**: 에셋 내용은 2026-05-29 웹 조사 검증. 기존 코드는 실제 읽고 판단(✅) / 미확인(⚠️) 표기.

---

## 0. 아트 디렉션 (확정 — 모든 구현의 전제)

- **스타일**: 스타일라이즈드 로우폴리 통일. **ithappy 좀비(이미 임포트, 화면 최다 노출)가 톤 기준점.**
- **카메라**: Darkwood식 비스듬한 3/4 탑다운 (~60~70°). 순수 90° 탑다운 아님.
- **플레이어**: POLYGON Battle Royale 생존자 캐릭터(Mercenary/Military/Ghillie/Redneck) **플레이스홀더**. 주인공 정체성은 폴리시 단계로 미룸 (Mecanim 리타겟이라 교체 비용 ≈ 0).
- **가독성 원칙**: 어둠은 균일 X → **대비**로 (중요 액터에 림라이트/이미시브). 상태 표시는 HUD 최소화 + **월드 기반 텔레그래프**(바닥 데칼: 소음 링/시야콘).
- **제외**: 리얼리스틱 PBR 에셋(Post Apocalyptic Town, Industrial Props) — 좀비와 톤 충돌. 원거리 배경용으로만 한정.

---

## 1. MVP 임포트 체크리스트

현재 프로젝트에 **임포트된 것**: DOTween Pro, Feel, ithappy Zombies(321 프리팹), ARCADE Car, PROMETEO.

MVP 그레이박스 시작에 **추가 임포트 필요**:

| 우선 | 에셋 | 용도 | 비고 |
|---|---|---|---|
| ★ | POLYGON Battle Royale | 플레이어 캐릭터 + 근접무기(마체테=단검 대체) | 6M 폴리곤, 필요 프리팹만 선별 임포트 권장 |
| ★ | Human Basic Motions FREE | 플레이어 이동 애니메이션(걷기/달리기 8방향) | Mecanim 휴머노이드, 캐릭터에 리타겟 |
| ★ | Casual RPG VFX | 슬래시/히트/탑다운 이펙트, 루트글로우 | "Projectiles top down" 카테고리 있음 |
| ★ | The Complete UI Sound | 크래프팅(시작/성공/실패), 레벨업, UI | 500+ SFX |
| ○ | COZY (Base + 모듈) | 밤낮/안개/분위기 — 밤낮은 MVP 선택 티어 | 무거움, 분위기 단계에서 |
| ○ | Odin Inspector | 디버깅/수치 튜닝 효율 | 개발 편의 |
| △ | Pixel Art GUI | HUD — **톤 검증 필요** | 픽셀아트가 Hades톤과 맞는지 미정. MVP는 커스텀 미니멀 HUD로 대체 가능 |

★=그레이박스 필수, ○=분위기/편의, △=보류·검증 후

---

## 2. 시스템별 매핑 (핵심)

각 행: **보유 도구/에셋** → **구체 API/프리팹** → **커스텀 코드 범위** → **부족분**

### 2.1 카메라 (3/4 비스듬한 탑다운)

| 항목 | 내용 |
|---|---|
| 기존 코드 | `CameraController.cs` ✅ 확인 — **차량 전용(부스트/드리프트/FlatVelocity 결합), 재작성 대상** |
| 살릴 기법 | 지수 감쇠 추적(`Mathf.Lerp(..., 1-Exp(-k·dt))`), `_logicalPosition`/`_shakeOffset` 분리(Feel 쉐이크 호환), `[DefaultExecutionOrder(-50)]` |
| 커스텀 | 플레이어 추종 follow cam 신규 작성 — 고정 피치 ~65°, 부드러운 위치 추적, 약한 lookahead(이동 방향) |
| Feel 연동 | 카메라 쉐이크는 Feel MMFeedbacks로 (히트/암살 시) |

### 2.2 플레이어 컨트롤러 (이동 + 클릭 근접 + 스텔스)

| 항목 | 내용 |
|---|---|
| 모델 | Battle Royale 생존자 캐릭터(플레이스홀더) |
| 애니메이션 | Human Basic Motions(걷기/달리기 8방향, idle). 공격 모션은 별도 필요(아래) |
| 이동 | 커스텀 — 이동속도 5(GDD), Rigidbody 또는 CharacterController |
| 근접 공격 | 마우스 클릭, 쿨타임 0.4초. **Animation Composer System의 ActionBlock으로 공격 프레임에 데미지 히트박스 활성화** |
| 공격 모션 | Battle Royale엔 애니메 없음 → Mixamo 등 근접 공격 모션 조달 필요 |
| 스텔스 | 키 입력 → 3초 은신, 쿨 8초, 감염+1 (커스텀). 비주얼은 Stylized VFX/머티리얼 |

### 2.3 전투 (적 + 플레이어 HP)

| 항목 | 내용 |
|---|---|
| 적 모델 | ithappy 좀비 321 프리팹 (일반/신호 2종 선별) |
| 기존 AI | `ZombieController.cs` ✅ 확인 — **차량 타겟·드리프트런치·Hull데미지·Ranged/Charger/Laser 타입. MVP의 일반/신호와 불일치 → 재작성** |
| 살릴 기법 | `CalcSeparation()` 무리 분리, `SampleTerrainHeight()` 지면 스냅, `MMSpringScale.Bump()` 킬 연출, `MMTimeScaleEvent`(Feel 히트스탑), `Animator SpeedHash` |
| 커스텀 AI | **일반 좀비**: 플레이어 추적 + 근접(HP 3타). **신호 좀비**: 반응범위 15m, 소환딜레이 3초, 4마리 소환 |
| 플레이어 HP | **100 HP, 좀비 타격 20(5대 사망)** 확정. 회복=구급상자 전용(GDD v2.4). 커스텀 — 기존 `HullManager`⚠️(차량 내구도)는 개념만 참고, 재작성 |
| 게임감 | Feel — 히트스탑, 타격 플래시, 카메라 쉐이크 (기존 코드의 Feel 패턴 그대로 차용) |

### 2.4 감염도 (0~10, MAX=런종료)

| 항목 | 내용 |
|---|---|
| 기존 코드 | **`SyncRateManager.cs` ✅ 거의 그대로 재활용** — 싱글톤, `AddSync/ReduceSync`, `OnSyncChanged`/`OnSyncMaxed` 이벤트 완비. 옛 "나노봇 동기화율" = 새 "감염도" 동일 개념 |
| 적응 작업 | 0~1 float → 0~10 정수 스케일 매핑(또는 0.1 단위), 스텔스 시 `AddSync`, 집 도착 시 `ReduceSync(전량)` 훅 연결 |
| HUD | 숫자 표시 (커스텀 미니멀). 8~9에서 캐릭터 이미시브 번짐(DOTween + 머티리얼)으로 긴장 연출 |
| MAX 처리 | `OnSyncMaxed` → 런 종료(좀비화) |

### 2.5 소음 시스템 (0~100)

| 항목 | 내용 |
|---|---|
| 기존 코드 | 없음 — **신규 작성** (COZY ReSound는 음악용이라 무관) |
| 수치(GDD) | 근접+10, 암살+0, 크래프팅 초당+20, 자연감소 초당-3, 임계값 50 → 주변 좀비 접근 |
| 가독성 | **다이제틱 3층**(지속 HUD 링 아님): 오디오 + 행동 순간 펄스(셰이더/파티클+DOTween 확산·소멸, 반경=R, 색 <50흰/≥50빨강) + 좀비 반응(?/!). 상세 `infection-noise-design.md` §1.4 |
| 좀비 연동 | 임계값 초과 시 반경 내 좀비 aggro (커스텀 — 2.3 AI와 연결) |

### 2.6 크래프팅 (레벨업 → 설계도 → 제작)

| 항목 | 내용 |
|---|---|
| 기존 코드 | 업그레이드 카드 시스템 ✅ 확인 — `CardPoolBuilder`/`UpgradeCard`/`RarityRoller`/`PityTracker`/`TreeAffinityTracker`/`PlayerWeaponInventory`. **뱀서식 4장 카드 — MVP "2중 1 설계도 선택"엔 과설계. 참고용, 단순화해서 차용** |
| 레벨업 | 킬 기반 5/12/25킬 3레벨 (커스텀, XP곡선 없음). 기존 `XPManager`⚠️는 XP기반이라 단순화 |
| 설계도 선택 | 레벨업 시 2개 중 1개 — 위 카드 UI 골격 축소 재사용 가능 |
| 제작 | 재료 5개 + 3초 대기 → 아이템. 제작 중 소음 초당+20(2.5 연동) |
| 아이템 4종 | 단검(근접↑)·연막탄(스텔스쿨↓)·지뢰(범위킬)·구급상자(HP회복) |
| SFX | The Complete UI Sound의 Crafting Start/Success/Fail ✅ |
| VFX | Casual RPG VFX 재생/버스트, Ultimate Loot VFX 픽업 |

### 2.7 집 / 보금자리

| 항목 | 내용 |
|---|---|
| 위치 | 맵 한쪽 구석 고정 (그레이박스: 큐브) |
| 감염 리셋 | 도착 시 `SyncRateManager.ReduceSync(전량)` |
| 나노봇 교란 펄스 | 입장 시 주변 좀비 흩어짐 — Stylized VFX 펄스 + 좀비 일시 후퇴(커스텀) |
| 재입장 제한 | 없음 (MVP) |

### 2.8 런 종료 / 게임 루프

| 항목 | 내용 |
|---|---|
| 종료 조건 | HP 0 / 감염 MAX(`OnSyncMaxed`) / 12분 초과 |
| 기존 코드 | `GameOverUI`⚠️ 차량용 — 개념 참고, 재작성 |
| 타이머 | 12분 (커스텀) |

### 2.9 밤낮 / 분위기 (COZY — MVP 선택 티어)

| 항목 | 내용 |
|---|---|
| 주의 | MVP §17 핵심 스펙엔 밤낮 미포함(12분 루프). 분위기 단계에서 추가 |
| 시간 | `CozyWeather.instance.timeModule.currentTime` |
| 밤→좀비 강화 | Events Module `onNight`/`onDawn` UnityEvent → 좀비 스탯 변경 |
| 스텔스 안개 | Height Fog(건물 주변 고임) + Plume + Eclipse(구름→실제 어두움) |
| 조명 가독성 | HTrace SSGI — 이미시브를 실제 광원으로(중요 액터 부각). 데스크톱 GPU 전제 |
| 적응 음악 | COZY ReSound — 밤=긴장, 낮=평온 셋리스트 |

---

## 3. 기존 코드 재활용 맵 (요약)

| 스크립트 | 판정 | 조치 |
|---|---|---|
| `SyncRateManager` | ✅ **직접 재활용** | 감염도로 스케일·훅만 조정 |
| 업그레이드 카드 7종 | 🟡 참고·단순화 | 4장→2장 설계도 선택으로 축소 |
| `CameraController` | 🔧 재작성 | 지수감쇠 추적 기법만 차용 |
| `ZombieController` | 🔧 재작성 | separation/지면스냅/Feel 히트스탑 패턴 차용 |
| Feel 패턴(MMTimeScaleEvent, MMSpringScale) | ✅ 패턴 재활용 | 히트스탑·킬 바운스 그대로 |
| Car*/Hull*/Boost/Fuel/Drift/Biome/Chunk/Terrain/PitStop | ❌ MVP 무관 | 차량 구간(후반) 전까지 보류 |

> ⚠️ 위 차량 스크립트들은 **삭제하지 말 것** — 후반 바이옴 이동(차량 액션)에서 재사용 예정. CLAUDE.md 규칙(사전 데드코드 삭제 금지) 준수.

---

## 4. 조달 필요 (보유 에셋으로 못 채우는 것)

| 항목 | 상태 | MVP 임시 대응 |
|---|---|---|
| 근접 공격 애니메이션 | 없음 | Mixamo 무료 모션 |
| 전투/환경 SFX (좀비울음/발소리/타격/환경음) | 없음 (UI 사운드만 보유) | 무료 SFX 팩 또는 후순위 |
| 아이템 모델 (연막탄/지뢰/구급상자) | 없음 | **프로토는 큐브/기본 메시 대체** |
| 신호 좀비 비주얼 차별화 (발광 안테나/돌기) | 직접 제작 | ithappy 좀비에 이미시브 머티리얼 + 간단 메시 |

---

## 5. 리스크 & 검증 필요

1. **Pixel Art GUI 톤** — 게임 톤(Hades식 따뜻한 어두움)과 픽셀아트 정합성 미검증. MVP는 커스텀 미니멀 HUD 권장.
2. **POLYGON 폴리곤 수** — Battle Royale 6M 폴리곤. 좀비 15~25 + 환경 동시 렌더 시 선별 임포트·LOD 필요.
3. **VFX Graph Magic Pack** — 정확한 에셋 불명, VFX Graph(컴퓨트 셰이더) 의존. MVP는 Casual RPG VFX(파티클)로 충분.
4. **HTrace/Plume** — 데스크톱 GPU 전제. 타겟 사양 확정 필요.
5. **아트 톤 seam** — ithappy 좀비 ↔ Synty 캐릭터 미세 차이 가능. 실제 임포트 후 눈으로 확인 권장.

---

## 6. 그레이박스 진입 순서 (제안)

1. 씬 셋업 (큐브 맵, 집=구석 큐브, 3/4 카메라 리그)
2. 플레이어 컨트롤러 (이동 + 클릭 공격 + 스텔스)
3. `SyncRateManager` 감염도 연결 + 미니멀 HUD
4. 소음 시스템 (바닥 링 데칼)
5. 좀비 AI 재작성 (일반 + 신호 2종)
6. 크래프팅 (축소 카드 + 아이템 4종, 큐브 비주얼)
7. 플레이테스트 → GDD §17 성공/실패 기준 판정
