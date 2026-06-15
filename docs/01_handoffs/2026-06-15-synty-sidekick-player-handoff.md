# 핸드오프 — 주인공 Synty Sidekick 전환 (2026-06-15)

## 0. 한 줄 요약
유저 결정 = **플레이어를 Synty Sidekick 캐릭터로**. Sidekick `Starter_02`(여우가면 무사)를 CombatLab 플레이어로 정식 배선 완료. 우리 카타나 애니가 그대로 호환됨(유저 손맛 OK). 내일 = 플레이테스트 → 그립 오프셋·허리 카타나 정리·룩 확정.

---

## 1. 오늘 확정된 것

### 결정
- **주인공 = Synty 캐릭터로 간다** (동결돼 있던 "셀셰이드 NPR 커스텀" 재검토). 미적 판정은 유저 몫 — 더 막지 말 것.
- 소스 = **Synty Sidekick (Free)**. Battle Royale 완제품 아님.

### Sidekick이 뭔지 (다시 헤매지 말 것)
- 완성 캐릭터가 아니라 **모듈러 캐릭터 제작기**. 메뉴 `Synty > Sidekick Character Tool`.
- 옷 = 별도 레이어가 아니라 "옷 입은 몸 파츠(Torso/Leg/Foot/Hips)" 교체 방식. 얼굴조립·체형슬라이더·옷교체 다 됨.
- 출력 = 합쳐진 SkinnedMesh + **Humanoid 리그 + 블렌드셰입(얼굴 표정)**.
- `Animations` 폴더 = **얼굴 표정만**(FaceCycle 3 + FacePose 17). 몸 동작 0 → 우리 Humanoid 클립이 담당.

### Free 번들 실태 (DB 직접 조사: `Assets/Synty/SidekickCharacters/Database/Side_Kick_Data.db`)
- 카탈로그 3033 파츠지만 **실제 FBX 동봉 Human = 157개뿐**(`file_exists=1`). 나머지 1866은 download-gated.
- ★**무료 계정 만들어도 안 풀림** — Apocalypse Outlaws / Modern Civilians 등은 **유료 Synty 콘텐츠**(팩 소유/구독).
- 동봉 의상 테마 = **딱 3벌**(`HUMN_BASE` 맨몸 / `FANT_KNGT` 기사갑옷 / `SCFI_CIVL` SF시민). 헤어11·수염10·코11은 풍부(얼굴은 잘 나옴), 옷이 빈약.
- **완제품 스타터 4구 동봉**: `Assets/Synty/SidekickCharacters/Characters/Starter/Starter_01~04` (prefab + Humanoid avatar + mat 완비). 전부 판타지/SF 컨셉(Starter_02 = 여우가면+기모노+카타나 아니메무사).

### 애니 호환 검증 (★유저 "애니메이션 좋은데?" OK)
- Starter_02 `isHuman=True`. 우리 **RPG Mecanim 2Hand-Sword 카타나 클립**을 리타게팅 → 본 좌표가 스윙 호 그림(RightHand Y 1.22→0.41→1.24), idle→3연베기 루프 Play 검증 → 유저 판정 OK.
- **결론: Synty Humanoid 캐릭터에 우리 애니 자산 그대로 호환·손맛 OK = 커스텀 리깅 병목 소멸.**
- ⚠️ 미세오차: Synty 통통한 손 → 무기 그립 오프셋 조정 필요(정식 배선 후).

---

## 2. 오늘 시공한 것 (코드/씬)

### A. CombatLab 플레이어 비주얼 스왑 (정식 배선)
파일: `Assets/_Project/Scenes/Greybox_CombatLab.unity` (저장됨)
- 기존 플레이어 비주얼(`CharacterVisual` = Synty Casual Male 13메시) → **Starter_02로 교체**.
- 기존은 **비활성 보관**: `CharacterVisual_OLD_CasualMale` (SetActive false). 되돌리기 = 이거 활성화 + 새 거 삭제.
- 설정 복제: 베이스 컨트롤러 `RifleLocomotion`, 스탠스 rifle=`RifleLocomotion`/pistol=`PistolLocomotion`, localPos (0,-1,0), scale 1.
- ★**왜 안전했나**: 모든 비주얼 참조가 런타임 자동 탐색이라 직렬화로 끊길 게 없음 —
  - `PlayerVisualFeedback.CacheBodyRenderers()` = Awake에 `GetComponentsInChildren<SkinnedMeshRenderer>` (히트플래시)
  - `PlayerHandWeapon.TryAttach()` = 런타임에 humanoid Animator의 RightHand 본 찾아 무기 자동 부착
  - `PlayerLocomotionAnimator.Awake()` = moveSource(부모)·aimSource 자동 배선
  - `KatanaController` / `PlayerCombat` = 비주얼/Animator 직렬참조 없음(플레이어 루트에서 동작)

### B. throwaway 애니 테스트 씬
- `Assets/_Project/Scenes/_SidekickAnimTest.unity` — Starter_02 + idle/3연베기 자동루프. 씬 열고 Play만.
- `Assets/_Project/_SidekickTest/SK_AnimTest.controller` — Idle→A1→A2→A3→Idle exit-time 루프.
- `Assets/_Project/_SidekickTest/SidekickAnimEventSink.cs` — RPG 클립의 `Hit` AnimationEvent "no receiver" 경고만 잠재우는 빈 sink. ★`Hit`은 버그 아님 = 실제 타격판정 타이밍(정식 전투에선 플레이어 전투스크립트가 받음).
- 둘 다 throwaway — 지워도 됨.

---

## 3. 내일 할 일 (우선순위)

1. **CombatLab Play 플레이테스트** — 여우로 직접: 이동/대시/카타나(거합·참격 키1/2). 손맛·동작·무기위치 판정.
2. **그립 오프셋 튜닝** — `PlayerHandWeapon`의 클래스별 `*OffsetPos/Euler`(현재 0). Synty 손에 총/검 맞추기. 플레이모드 라이브 튜닝(매 프레임 적용됨).
3. **Starter_02 허리 카타나 정리** — 메시에 박힌 자기 카타나가 우리 무기랑 겹치면 그 SMR 끄기.
4. **룩 확정** — 이 여우는 임시일 수도(유저 판정). 셀셰이드 얹을지 별개 결정(셰이더라 어느 Synty 메시든 얹힘 = "Synty 편함 + 주인공만 NPR 대비" 양립 가능).

---

## 4. 자산 위치 빠른참조
- Sidekick 루트: `Assets/Synty/SidekickCharacters/`
- 스타터 4구: `.../Characters/Starter/Starter_0{1..4}/Starter_0N.prefab`
- 파츠 DB: `.../Database/Side_Kick_Data.db` (sqlite, `py`로 조회 가능)
- 우리 카타나 애니: `Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animations/2Hand-Sword/`
- 플레이어 로직: `Assets/_Project/Scripts/{PlayerController,PlayerCombat,KatanaController,PlayerLocomotionAnimator,PlayerHandWeapon,PlayerVisualFeedback}.cs`

연동 메모리: `project_2026_06_15_synty_sidekick_player_path`, `project_2026_06_14_monster_lowpoly_shader_limit`(괴수=실사 우세, 주인공 대비), `project_combat_anim_sourcing`(RPG Mecanim=Humanoid)
