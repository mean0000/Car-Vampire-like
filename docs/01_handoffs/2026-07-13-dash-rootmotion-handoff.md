# 2026-07-13 대시 루트모션 전환 + 회피 가독 핸드오프 (R12~R14c)

> [[2026-07-11-control-sync-handoff]]의 후속 — 그 문서의 §RESUME은 이 문서로 승계.
> 유저 판정 축: "회피가 안 읽힌다" → 배속(R14) → **구조(R14b 루트모션)** → 복귀 지연(R14c). 최종 유저 반응: 루트모션 전환 **"좋네" 승인**, 이동캔슬 후 포즈 잘림은 판정 진행 중.

## R12 (07-12) — 기본값 확정 + 대시 연출 심화 (문서 미기록분 보충)
- **F/G/H 판정 도착 → 기본값 확정: Facing Hybrid + 8방향 스냅 ON + 조향 360°/s.** 씬 5개 YAML 정규화(RunFeel은 facing 0→2·snap 0→1·faceTurnRate 300→600). lean 14°는 미판정 존치. 유저 플레이 중이라 라이브 주입 → 컴파일 후 씬 디스크 리로드 3중 정합 검증.
- 대시 연출 유저 재판정 = "J킷 둘 다 부족" → 두 킷 심화(비주얼 전용 — **사운드는 유저 지시로 추후**, Sound 에이전트 중단): Glitch=킥오프 실루엣 RGB 분리(chromaSplit 0.14)+착지 재조립 포즈 플래시+샤드 몸통 세로분포 24개 / Physical=킥오프 스트레치 1.1+출구 드래그 먼지 30/s+착지 먼지 관성 0.7.

## R13 (07-12 심야) — Evade 리타임 1차 + Glitch 정리 (문서 미기록분 보충)
- 유저 "회전 시작 빠르게, 착지 천천히" → ★근본 원인 = **Dash==false 조기컷 전이**(모터 0.15s 종료 순간 컷 → 클립 41%만 재생, 착지 미재생). 조기컷 삭제(유일 exit=exitTime 0.95→Locomotion) + Dash 상태 m_Speed 2.2→1·**DashRate 파라미터 구동**(2단: eject 2.2/land 1.2, 경계 0.4).
- 유저 "중간/끝 빛나는 효과 제거" → Glitch 샤드 전부(킥오프 팝+착지 재조립)+착지 포즈 플래시 삭제. EmitShards/_shardPS 인프라=미사용 잔존(굳히기 때 삭제).

## R14a (07-13) — 글리치 OFF + 실루엣 테두리만 (오케 직접)
유저: "글리치 효과는 끄고, 캐릭터의 실루엣은 테두리만 남겨" (= R13의 색분리 지목 여부 질문에 대한 답).
- `dashStyle` 기본 **Off**(코드 default + RunFeel 씬 직렬화 둘 다 — 씬이 코드를 이기는 함정 대응). RGB 분리 경로 미도달. J키 하니스 존치.
- `AfterimageGhost.shader`: **_BodyFloor 0.85→0 / _RimBoost 0.5→2 / _FresnelPower 2.5→4** — 바디 필 제거, 프레넬 림(테두리)만. ⚠️스킬 런지 잔상(EmitBurst)도 같은 셰이더 공유 = 함께 림 온리(잔상 언어 통일, 의도).

## R14 (07-13) — 3단 강약 리타임 (Animation 에이전트)
유저: "더 천천히, 뭘 하는지도 모르겠는데? 어디를 강약 조절해야 맛있는지 찾아서 진행해."
- **클립 실측**(Evade 4방향, 0.8s/60fps/48f, grounded 수평 런지): F 기준 f0-8 발구름(가속 1.8→11.4m/s) / f8-19 회피 실루엣(피크 14.3m/s @f10, 80%이동 @f18) / f19-46 착지·회수(저속, 정보량 낮음). net 3.27m(F), 2.80m(B/L/R).
- ★**핵심 발견: R13 2단은 강약이 거꾸로** — "이젝션"(n0~0.4)이 발구름+정체성 실루엣 전부를 2.2×로 0.145s에 뭉갰고(="뭘 하는지 모르겠다"의 직접 원인), 1.2× "착지"는 정보 없는 회수 꼬리에 0.37s 체류.
- **3단으로 교체**: `dashLaunchRate 1.5`(n0~0.15 발구름=강) / `dashFlightRate 0.9`(n0.15~0.42 실루엣=약, ★가독 핵심) / `dashRecoverRate 1.1`(n0.42~0.95 회수=중). 가독 윈도우 0.145→0.32s, 전체 0.51→0.705s. 구 필드(Eject/Land/Fraction)는 씬 미직렬화라 stale 없음.

## R14b (07-13) — ★대시 변위 코드→클립 루트모션 이전 (구조 전환, 유저 "좋네" 승인)
유저: "아니, 움직이고 나서 회피 모션이 나오고 있잖아, 루트 모션이잖아" = R10 Codex 진단 ④(이동-시각 분열)와 수렴.
- 옛 구조: 모터 0.15s 버스트(첫 0.1s에 2.84m=순간이동성) 위에 클립이 포즈만 재생 → 몸이 코일 포즈인 채 이동 완료, 뻗는 실루엣은 사후. 클립 자신의 MotionT(자연 가속→감속, 3.27m)가 정답 프로파일이었는데 discard 중이었다.
- **채택**: `OnAnimatorMove`가 Dash 클립 `deltaPosition`을 `ApplyRootStep`으로 적용(공격 루트모션과 동일 벽가드+지면 파이프, 06-19 선례). 모터 `UpdateDash`+`dashDistance/dashEasePower/dashExitSpeed` **삭제**.
- ★**창 2개 분리** (옛 IsDashing 하나가 겸하던 두 역할):
  | 창 | 프로퍼티 | 길이 | 역할 |
  |---|---|---|---|
  | window-S 커밋 | `DashCommitted`(_dashTimer) | 0.15s | 재대시 금지+입력 버퍼 = 하드컷 캐넌 |
  | window-L 변위 | `IsDashing`(_dashActive) | 클립 재생 동안 | 위치 소유·잔상·발소리·facing·상체레이어 |
- window-L은 **애니가 소유**: 드라이버가 Dash 재생 중 매 프레임 `KeepDashActive()` ping + 모터 grace 0.05s 워치독 자동 만료(하드코딩 지속시간 없음).
- ★**함정 2건**: ①Animator `Dash` bool은 반드시 **window-S**로 구동 — window-L이면 상태 exit 후에도 bool true → AnyState→Dash 무한 재진입 ②`PlayerBrain` 입력버퍼도 window-S 결속 — window-L이면 착지 내내 공격 버퍼돼 하드컷 사망.
- Bake 재검토: 4방향 클립 루트 회전 0.0°·직선 → **Bake 전부 OFF 그대로가 정답**(R11 노트는 "모터가 루트모션 폐기할 때만" 참이던 것). 리임포트 0.
- **feel 변경(정직 보고)**: 유효 거리 3.9→**3.27m**(-0.6m, 스케일 노브 안 만듦 — 필요 시 별도 결정) · 출구 슬라이드 8m/s 폐기(클립 감속이 정착 대체) · 벽 dead-stop→벽면 슬라이드 · 첫 0.1s 변위 2.84→1.05m(≈10.5m/s, 굼뜨면 dashLaunchRate 1.7~2.0) · i-frame 0.3s가 변위 80% 커버(회수 꼬리=무적 밖, 닷지 문법 정합).

## R14c (07-13) — 회수 이동 캔슬 ("회피 후 딜레이" 제거)
유저: "좋네. 그런데 회피 이후 딜레이가 있어서 바로 안 움직이는데 정리해줄래?" (R14b의 이동커밋 0.7s 트레이드오프 기각)
- **규칙: 무입력=착지 풀재생(R13 가독 유지) / 이동 입력(홀드·신규 무관)=클립 n≥`dashMoveCancelPoint`(0.5)에서 Dash→Locomotion dur-0 하드컷 + `EndDashRoot()`(grace 없이 window-L 즉시 종료).**
- 컨트롤러: `DashCancel` 트리거 + Dash→Locomotion 전이 신규(hasExitTime false, dur 0). 기존 exitTime 0.95 전이와 공존.
- ★트리거 위생: 대시 시작 시 `ResetTrigger(DashCancel)` 필수 — 미소비 트리거가 새 대시를 프레임0에 즉시 캔슬.
- 하한 0.42(Range)가 변위 구간 침범을 구조적으로 차단(회피 거리 일관성). 캔슬 시 잔여 변위 0.37m(~11%) 드랍(저속 꼬리라 미미).
- **이동 재개: 홀드 기준 0.75→~0.4s.** 무입력은 여전히 ~0.75s(의도).
- **포즈 잘림 논의(유저 질문 "살짝 딜레이 vs 게임적 허용")** — 오케 권고: **게임적 허용**(장르 통례=회수 컷은 손에 익으면 '빠른 반응'으로 재해석, 입력 지연은 영원히 체감. 크리스프 독트린·DMC 하드컷 캐넌 정합). 절충=`dashMoveCancelPoint` 0.5→0.55~0.6(몸이 더 일어선 뒤 컷, 복귀 +0.04~0.07s뿐). ⚠️시각 블렌드 안은 비추천 — 캐넌 위반 + 전이 중 OnAnimatorMove가 Dash 상태를 계속 봐서 window-L 재개방(지연 부활) = 코드 손질 필요. **유저 결론 대기: 0.5 그대로 더 플레이 → 거슬리면 인스펙터 라이브로 0.55~0.6 맛보기 → 취향값을 기본값으로 굳힘.**

## 대시 노브 지도 (현행 종합 — RunFeel Visual 오브젝트, 전부 코드 default 지배)
| 노브 | 위치 | 값 | 체감 |
|---|---|---|---|
| `dashLaunchRate` | 드라이버 | 1.5 | 발구름 스냅(굼뜨면 ↑ 1.3~2.0) |
| `dashFlightRate` | 드라이버 | 0.9 | ★실루엣 가독(또렷=↓ 0.75~1.05) |
| `dashRecoverRate` | 드라이버 | 1.1 | 착지 무게(0.95~1.3) |
| `dashLaunchEnd`/`dashFlightEnd` | 드라이버 | 0.15/0.42 | 구간 경계 |
| `dashMoveCancelPoint` | 드라이버 | 0.5 | 이동캔슬 시점(잘림 거슬리면 ↑ 0.55~0.65) |
| `dashRateDamp` | 드라이버 | 0.04 | 배속 전환 스무딩 |
| `dashDuration` | 모터 | 0.15 | 커밋창(재대시금지+버퍼). ★거리 아님 |
| `iframeDuration` | 모터 | 0.3 | 무적(변위와 별개) |
| `DashRootGrace` | 모터 const | 0.05 | 무입력 경로 window-L 만료 여유 |
| 실루엣 테두리 | 셰이더 Properties | RimBoost 2/FresnelPower 4 | 진하게=RimBoost↑, 얇게=Power↑ |

## 변경 파일 (07-13분, 전부 미커밋 — 07-11 목록에 누적)
`Player/PlayerMotor.cs`(창 분리·EndDashRoot·UpdateDash/노브3종 삭제) · `Player/PlayerAnimatorDriver.cs`(3단 DashRate·루트 피드·이동캔슬·트리거 위생) · `Player/PlayerBrain.cs`(버퍼 게이트=DashCommitted) · `PlayerAfterimage.cs`(dashStyle 기본 Off) · `Shaders/AfterimageGhost.shader`(림 온리) · `KatanaMelee.controller`(DashCancel 트리거+전이) · `RunFeel_Whitebox.unity`(dashStyle 0). ⚠️씬에 구 모터 필드 3개(dashDistance/EasePower/ExitSpeed) 고아 키 잔존(무해, 굳히기 때 정리). `PlayerAfterimage.cs:298` "8f=dashExitSpeed" 주석 stale(리터럴이라 무해).

## RESUME (다음 세션 순서)
1. **유저 플레이 재판정**: ①루트모션 회피+테두리 실루엣 종합 ②R14c 포즈 잘림 → `dashMoveCancelPoint` 취향값 확정(0.5 유지 vs 0.55~0.6) ③거리 3.27m·출구슬라이드 폐기 체감(짧으면 거리 스케일 노브 별도 결정).
2. **커밋 전 일괄 게이트(필수 — 재량 없음)**: R2~R14c 누적분 Stab+Codex 병렬. 07-11 선언 유지 — 컨트롤러/씬/모터 구조 변경 = 객관 트리거.
3. 게이트 통과 → **커밋**(유저 확인 후).
4. **굳히기**: 하니스 키 F/G/H/J+OnGUI 제거 · EmitShards/_shardPS 미사용 인프라 삭제 · 씬 고아 필드 정리 · 승인값 타 씬 이식(★튜닝은 RunFeel 단일 규율 — 이식은 굳히기 때 일괄).
5. **애니 어휘 소싱**(나+Codex 완전 수렴 최우선): 출발/정지/피벗/브레이크 전용 클립 — Frank 팩 전수 재조사→부족분 MoCap Online 등(Animation 에이전트).
6. 미판정 소항목: 07-11 핸드오프 ①~⑧ 승계(선딜 스미어·lean 14°·고티어 슬라이드·전투씬 걸음새·comboMoveSpeedMult·셔플 강도·Glitch whoosh·대시 스타일은 R14a에서 Off로 사실상 정리) + 신규: L클립 피크 편차(f3 — 자유방향 회피에서 좌만 이질감 시 보고).
7. 백로그(07-11 승계): SFX 변주 배열 · 2차 모션/모션블러 조사 · 스프린트 전용 클립.
