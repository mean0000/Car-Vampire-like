# 핸드오프: 2026-06-14 — Dimaxillosaurus "클로월" (3번째 몬스터 · 스윙/회수 split)

> **권위**: 이 문서 + [[2026-06-13-caniathrox-pipeline-handoff]](파이프라인 6단 틀·애니 헌법·기술 함정) + 6대 북극성([[2026-06-13-caniathrox-pipeline-handoff]] §0). 메모리: `.claude/.../memory/project_2026_06_13_monster_impl_pipeline.md`.
> **경위**: Caniathrox(근접 돌진)·Venodonte(원거리 사수)에 이은 **3번째 몬스터**. LV3 근접. 유저와 여러 번 반복 디렉팅하며 **"벽처럼 오는 클로월"**로 수렴, 마지막에 **클로 느낌을 스윙/회수 split**로 다듬음. **미커밋 — 유저 ▶ 플레이 판정 대기**(내일).

## ★ 현재 상태 (한 줄)
멀리서 발견 → **그 자리에서 포효** → **좌·우 단발 클로 무한 교대**(클로질이 곧 전진) → **장판 없음** → 벽처럼 끊임없이. 각 단발 = **Swing(자연 1.0) + Recovery(배속 3.0)** 두 상태. 유저: "오 좋은데, 확실히 좋아 — 내일 그렇게 작업해보자."

---

## 1. 디자인 — "벽처럼 오는 클로월" (유저 디렉팅 동결)
- **멀리서 발견 → 즉시 포효**(오프너 1회, ★거리 게이트 없음 — 인지 즉시).
- **좌우 단발 클로 L-R-L-R 무한 교대 = 이동수단.** 별도 접근(run/walk) 단계 없음 — 클로질로 다가온다.
- **장판(텔레그래프) 없음.** 가독성 = 보이는 클로 윈드업 + 포효. (유저: "장판 보여줄 필요 없어.")
- **"벽처럼"** = 끈질긴 포위 위협(*빠름이 아니라 끈질김*). 디스인게이지 없음 — 항상 플레이어 쪽으로 클로질.
- ★**도약(JumpClawsAttack 9m) 안 넣음** — LV3 중간티어. 고티어(LV4 정예/LV5 거물) 위협 어휘로 보존(유저 룰: 레벨 높으면 도약).

## 2. 클로 느낌 반복 디렉팅 (수렴 과정 — 왜 지금 형태인가)
하루 종일 유저가 클로 느낌을 여러 번 디렉팅하며 수렴:
1. 2힛 콤보(제자리) → **전진 콤보 체인** → **좌우 단발 교대** → **클로월**(접근 제거, 장판 제거, 즉시 포효).
2. ClawSpeed 가속(2.5×) → 유저 **"모션 빨리하지 마"** → 1.0 자연 복귀.
3. 끝 트림(import lastFrame로 회수 컷) → 18프레임 **"너무 많이 잘림"** → 26.
4. 유저 **"자르지 말고 뒷부분(회수)만 빠르게 재생"** → **스윙/회수 split (현재 — 유저 확정 방향)**.

> 핵심 교훈: 유저는 **모션 가속(부자연)·트림(거리/모션 손실) 둘 다 거부**. 정답 = 타격은 자연 속도로 온전히, **죽은 회수 tail만 배속 재생**(모션·거리 보존).

## 3. 현재 상태머신 (split — 6상태/3트리거)
```
Idle ──attack(타깃 인지 즉시)──▶ Roar(speed5, 오프너 1회)
                                   │ ExitTime0.95 CUT
                                   ▼
Idle ◀─ExitTime0.98 CUT─ L_Recovery(22~35f, speed3.0) ◀─ExitTime0.99 CUT 연속─ L_Swing(0~22f, speed1.0)
  │                                                                                  ▲
  │ chainGap 0.18s 재조준 비트(_nextRight 반대손)                                     │ chainL
  └─ chainR ─▶ R_Swing(0~22f) ─▶ R_Recovery(22~35f) ─▶ Idle … 무한 교대(LRLRLR)
```
- **6상태**: Idle / Roar / **LeftClaw_Swing** / **LeftClaw_Recovery** / **RightClaw_Swing** / **RightClaw_Recovery**.
- **3트리거**: attack(오프너 Idle→Roar) / chainL(Idle→L_Swing) / chainR(Idle→R_Swing). 드라이버 `_nextRight`가 손 교대(상태 진입에서 파생 = 자가치유).
- **분할**: 같은 take를 2 sub-clip(`LeftClaw_Swing` 0~22f / `LeftClaw_Recovery` 22~35f, R 동일). **ClawHit 이벤트는 Swing만**(컨택 norm = 12.25/22 ≈ 0.557 L / 0.584 R, 절대 0.408s/0.428s).
- **연속성**: Swing.lastFrame(22) = Recovery.firstFrame(22) → CUT 전이여도 **포즈 점프 0**(한 동작의 분할, crossfade 아님 — 헌법 준수).
- **루트모션**: Swing +1.87m + Recovery +0.32m = **2.19m ≈ 풀클립 2.218m**(손실 0). 회수의 +0.32m도 3× 빠르게 *운반*.
- **지속 전진 ≈ 2.1 m/s**(스윙이 자연 1.0이라 느린 벽 페이스).
- 전이 전부 **CUT(dur0)** — 정체성 동작 진입/완결. **Swing·Recovery 둘 다 회전 0**(궤적 보존). 재조준 회전은 Idle 비트(chainGap)에서만.

## 4. 노브 (유저 ▶ 튜닝 — 튜닝 절차 = 드라이버 const 변경 + `ZombieCrush/Dimaxillosaurus Lab/1. Setup Data` 메뉴 재실행)
| 노브 | 현재값 | 의미 |
|---|---|---|
| **RecoverySpeed** | 3.0 | ★회수 배속 = 속도점프 강도. "휙 채서 어색"이면 ↓(2~2.5). 드라이버 `DimaxillosaurusBrawler.RecoverySpeed` public const(단일 진실원). |
| **SplitFrame** | 22 | 스윙/회수 경계(빌드스크립트 const). 팔로스루 더 자연속도로 = ↑(24). |
| **ClawSpeed** | 1.0 | 스윙 속도(자연 — ★빨리감기 ❌, 유저 확정). 드라이버 public const. |
| **chainGap** | 0.18 | 단발 사이 재조준 비트(추적 회전창). 0이면 측면추적 끊김 → 권장 ≥0.13. |

## 5. ★ 유저 ▶ 판정 대기 (정지 캡처로 못 보는 것 — 내일 플레이로)
1. **★1순위: 속도점프** — 스윙(1.0)→회수(3.0) 경계가 **"스냅 있고 공격적"인가, "휙 채서 어색"인가.** 어색하면 RecoverySpeed↓ 또는 SplitFrame↑. (구조상: 점프가 *타격 중*이 아니라 *회수 중*에 일어나고 reach가 단조감소라 역방향 튐은 없음 → 자연스러울 가능성이 높다는 게 에이전트 소견. 단 3×는 큰 점프.)
2. **벽 체감** — 끈질긴 클로월로 읽히나. 멀리서 클로질이 "우습지" 않나(북극성 — 먼 거리 허공 할퀴기 vs 달려드는 런지).
3. **★설계 긴장(미해결)** — 클로월 ~2.1 m/s ≪ 걷기 5.5 → **단일 Dimax는 걸어서 탈출 가능.** 위협 = 포위/호드/핀 상황(빠름 아님). 1:1 랩에선 "또 안 맞네"가 될 수 있음. 해석 (A)현재구현=포위형 벽, (B)솔로도 압박하려면 "성큼 접근 비트" 추가 필요(클로 배속만으론 모션 파탄 없이 못 잡음).

## 6. 내일 할 일 (TODO)
1. **▶ 플레이로 split 속도점프 판정** → RecoverySpeed 튜닝(3.0 스냅 vs 2~2.5 부드럽게). 스윙/회수 흐름·벽 체감 확정.
2. **OK면 Dimaxillosaurus 커밋** — 메시지 예: `feat(enemy): Dimaxillosaurus 클로월 완성 — 즉시포효+좌우 단발 교대(스윙자연/회수배속 split)·장판없음 (29종 3번째 틀=근접 클로월)`. pathspec 커밋(미커밋 파일은 §파일 목록).
3. **(선택) 설계 긴장 해소** — 솔로 위협이 부족하다 판정되면 "멀리서는 성큼 접근(빠름) → 근접하면 클로월" 비트 추가 검토.
4. **다음 몬스터 선정** — 근접 돌진(Caniathrox)·원거리 사수(Venodonte)·근접 클로월(Dimaxillosaurus) **3틀 확립** → 나머지 26종 대부분이 이 틀들의 조합. 곡사/장판 축(TelegraphPad/Pool 첫 소비자)·빔·소환 등 미착수 축이 후보. 권위 = [[2026-06-13-topdown-attack-grammar]].

## 7. ★ 함정 기록 (오늘 발굴 — 재발 금지)
1. **상태 rename → LabCapture watcher `IsName`도 동반 갱신.** 상태명 바꾸면 캡처 watcher가 옛 이름 매칭 → 5컷 중 4컷 무음 미발화 → `_shotMask` 영원 미완 → **EditorApplication.update 무한폴링 스톨**(컴파일·무장로그는 정상이라 무음). 6회차 리뷰서 발견·수정. → **상태 rename = Capture watcher 동반 갱신 회귀 핫스팟.**
2. **컨트롤러 진실원 = 빌드스크립트(`DimaxillosaurusLabSetup`).** 수동 API 편집(Approach 자기루프·state.speed)은 SetupData 재빌드가 떨군다 → 회귀("한걸음 정지" 재발 사고). **모든 상태/전이/속도는 빌드스크립트에 박아야 durable.**
3. **재컴파일 타이밍** — const 바꾸고 SetupData 재실행 시 에디터가 아직 재컴파일 전이면 *옛 const 값* 사용(MCP 백그라운드는 도메인 리로드 큐잉만). 우회 = 직접 RunCommand(리터럴 값)로 클립/컨트롤러 즉시 편집 or const-derive 패턴.
4. **ClawSpeed/RecoverySpeed SSOT** — 드라이버 `public const` + 빌드스크립트 `= DimaxillosaurusBrawler.Xxx` 참조(에디터 어셈블리가 런타임 어셈블리 참조 → const 크로스 참조 OK). 이중 정의 무음 desync 차단.
5. **클립 트림(firstFrame/lastFrame) vs split(sub-clip 범위).** 트림 = 회수 *컷*(거리/모션 손실). split = 회수 *배속 재생*(거리 보존). 둘 다 import 설정(.meta)에 durable(reimport 생존). ★원본 Protofactor FBX 불가침 — DimaxRM/ 사본만.
6. **헌법 적용** — 애니가 진실(코드 위치이동 금지), 속도는 *정적 state.speed*(코드 매프레임 스크럽 금지), **회수 배속도 per-frame 코드가 아니라 split 상태(static speed)로** 구현.

## 8. 재사용 자산 (이 세션 부산물 — 보존)
- **`TelegraphPad.cs` / `TelegraphPool.cs`** — 장판 텔레그래프 **런타임 드라이버**(지어둔 `ThreatArc.shader` 첫 게임 활성화). gen 세대 가드·`CancelImmediate`·전진 추적·PickupInfo 레이어13 AfterPost 콘면제 경로. ★**Dimax는 클로월로 전환하며 미사용**(장판 없음)이지만 **다른 장판 종용으로 보존**(미커밋). 다음 곡사/장판/잔류 몬스터가 첫 소비자. 권위 = `.claude/.../memory/project_telegraph_pad_shader.md`.
- **`ProjectilePool.cs` / `AcidGlob.cs`** — Venodonte서 신설, 이미 커밋(487e45fc6). 원거리 투사체 재사용 토대.

## 9. 파일 (전부 미커밋 — 내일 판정 후 pathspec 커밋)
**Dimaxillosaurus (커밋 대상):**
- `Assets/_Project/Scripts/DimaxillosaurusBrawler.cs` (+.meta) — 드라이버(클로월 브레인, split).
- `Assets/_Project/Scripts/DimaxillosaurusLabSpawner.cs` (+.meta) — 랩 스포너.
- `Assets/_Project/Scripts/Editor/DimaxillosaurusLabSetup.cs` (+.meta) — ★컨트롤러/클립 진실원(split 빌드).
- `Assets/_Project/Scripts/Editor/DimaxillosaurusLabCapture.cs` (+.meta) — 디스크 캡처(watcher 수정됨).
- `Assets/_Project/Animations/DimaxillosaurusBrawler.controller` (+.meta) — 6상태.
- `Assets/_Project/Animations/DimaxRM/` — split sub-clip 클론(L/R Forward_RM, 이벤트·범위 박힘).
- `Assets/_Project/Scenes/Greybox_DimaxillosaurusLab.unity` (+.meta) — 플레이 랩.
- `docs/03_reference/assets/dimaxillosaurus_lab/` — 검증 캡처.

**재사용 자산 (함께 커밋 권장 — 미커밋):**
- `Assets/_Project/Scripts/TelegraphPad.cs` (+.meta) · `TelegraphPool.cs` (+.meta) — 장판 런타임 드라이버(타 종용).

> ⚠️ 워킹트리에 병렬 세션의 미커밋분도 섞여 있으니(세션 시작 git status 참조) **반드시 위 경로만 pathspec 커밋**(`git commit -- <paths>`) — 전체 add 금지.
