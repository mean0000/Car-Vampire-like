# 2026-06-28 Day2 — 정지 베기 "한 방" 손맛 (후딜 제거 + 전진 설계) — Handoff

> 화이트박스 사다리: Day1 질주✅ → **Day2 정지 베기(카타나 한 방=간지 본체)** → Day3 슬라이드베기 → Day4 떼 → Day5 일점돌파 → Day6 리듬(카이팅).
> 씬 = `Assets/_Project/Scenes/Labs/RunFeel_Whitebox.unity`. 무기 = `Assets/_Project/Scripts/Player/KatanaWeapon.cs`.

## 세션 아크
"Day2 뭐 할지 분해부터 같이" → 나·Codex·Animation 3자 독립 분해 강수렴. **★재구성: Day2는 맨땅 빌드가 아니다 — 베기 코드(콤보·킥·VFX·판정)가 이미 성숙.** 실제 작업 = *튜닝 + 빠진 조각*. 유저가 플레이로 "DD처럼 전진↑·후딜↓" 판정 → 후딜 제거(변수1) 구현·검증 통과, 전진(변수2) 설계 완료(미구현).

## ★사실 정정 (이번 세션의 핵심 — 메모리에 박을 것)
- **히트스탑은 *이미 있다*.** `HitStop.Do`(전역 timeScale 0.05, 단일소유·복원가드)를 `EnemyDamageReceiver.TakeHit`이 **매 타격 호출**(0.035s, 킬 0.08). 더미 베면 지금도 발동. (내 초기 "미구현" 단정 = 미검증 오류, 정정.)
- **코드 내 캐넌 충돌**: KatanaWeapon 주석은 "전역 timeScale 금지·피격자만 정지 재도입"이라 하나, HitStop/SmashFeel/Receiver는 전역 HitStop을 *안전하다며 실제 사용 중*. → **전역은 이미 채택·작동**, 주석이 stale or "무기가 자기 timeScale 직접 만지지 마라"만 뜻함.
- **피격자-정지(캐넌 업그레이드)는 Day4(떼)로 이연** — 전역 dip↔피격자-한정 차이는 1:1엔 안 보이고 호드서만 갈림(HitStop.cs가 "연사 매발 정지=스터터" 직접 경고). 화이트박스 더미는 코드타이머라 애니-freeze 테스트베드도 못 됨. "전역 vs 피격자" 결정 = Day4.

## 완료 + 게이트 통과
### 1) 임시 베기 SFX (Sound 에이전트) — 유저 "사운드 충분히 좋아" ✅
- **무음이 아니라 *오배선*이었다**: 기존 `PlayerAttackSfx`가 슬래시를 히트프레임에+헛스윙에도+vol 0.6(캐넌 0.03~0.15 위반)으로 울림.
- → **2층 재배선**(KatanaWeapon): ①`swish`=스윙 시작(BeginCombo/Advance, 헛스윙 포함) ②`impact`=적중 순간만(`FireHitFeedback`=connected 가드 일원화 → 헛스윙 자동 무음). 2D one-shot(PlaySkillSfx 정책). 임시음원=Vefects Slash_Classic+Impact_01.
- `PlayerAttackSfx` 컴포넌트 비활성(씬 `m_Enabled:0`, 롤백=재활성).
- **게이트**: Codex 클린. Stab **M-1=pitch 0 바닥 가드**(`Mathf.Max(0.01f,…)`) 적용. (Lows 이연: BeginCounter/DashAttack swish 없음·DoSkillHit 이중사운드·PlayerAttackSfx 고아소스 — Day2 스코프 밖)

### 2) ★변수1 = 이동 self-cancel (후딜 제거) — 유저 판정 1타·2타 ✅
- **문제**: Combo1 상태가 "Action" 태그라 클립 전체(0.878s) `busy=true` → `PlayerBrain`이 Motor 이동 양보 → 회수 0.44s 동안 뿌리박힘 = 후딜.
- **구현**(KatanaWeapon.OnTick, Advance 분기 *뒤* else-if): 캔슬창(`_windowOpen`, hit 지남) 이후 이동 입력(`input.move`≥데드존) → 소프트캔슬(`ResetCombo()+base.Cancel()`) → Combo→Loco. **윈드업~스트라이크는 잠금(무게 보존)**, **피니셔(`_step<comboMax`) 제외(Hades 대시-온리 커밋)**, 클릭(Advance)·회피(PlayerBrain Cancel) 우선.
- 노브: `moveCancelEnabled`(토글/롤백) · `moveCancelDeadzone`(0.2).
- **게이트**: Codex 7/7 PASS. Stab **H-1=데드존 0이면 zero벡터 자동통과→매틱 자동캔슬** → `[Range(0.01f,1f)]` 적용. 컴파일 0 에러.
- **유저 플레이 판정**: 1타·2타 = 베고 WASD로 즉시 이동(후딜 제거 됨). 3타 = 의도적 제외(아래 미해결).
- ⚠️ **watch-point(Codex)**: 이동 해제가 Animator의 Combo→Loco **0.15s 복귀 블렌드**를 따라가 *완전 즉발 아닐 수* — 유저 "끈적" 보고 시 IsActionPlaying 타이밍/복귀 블렌드 단축. (현재 1·2타는 "바로 움직임" 판정이라 OK)

### 3) 화이트박스 단발 표적
- **Day2_ClashTestDummy** 씬 배치(Unity MCP): 플레이어 정면 4m, `autoLoop=false`(가만한 표적), HP 99999(무한 베기). 맞으면 흰 플래시+전역 히트스탑+카메라 킥+impact SFX.
- **CombatSliceSpawner는 이미 비활성**(activeSelf=false)이었음 → 호드 0(내 초기 "40마리 스폰" = active 미확인 오류, 정정). Day4 때 재활성.
- ⚠️ 유저 **Ctrl+S 저장 여부 미확정** — 더미가 씬에 영속됐는지 내일 확인(런타임 MarkSceneDirty만 함).

## ★변수2 = 전진 스텝인 (2026-06-29 ✅ 구현완료 — 유저 플레이검증 대기)
> **구현완료(2026-06-29):** 경로2로 `S1_Combo01_01_Retimed.anim` MotionT.z 전진 ramp 저작(**드라이버=MotionT.z, RootT.z 아님** — 아래 정정 참조). net 0m→**0.40m**(hit까지 싣고 회수 flat hold = "밀어넣고 멈춤"), 이벤트/guid/바인딩 보존, 코드변경0(OnAnimatorMove deltaPosition 이미 적용). 게이트(Codex): Q3/Q5 CLEAN · Q4 MUST-FIX는 오독 반증(raw 절대값 vs 측정 net 혼동, 자릿수 정합) · Q1·Q2 WARNING=edit-mode↔런타임 등가성 인헌런트(플레이모드 MCP막힘) → **유저 플레이판정으로 해소: 전진성 OK? + 전진중/후 미세 앞뒤떨림(=RootT.z bump 발현) 체크.** 백업=scratchpad/day2_anim_backup. ⚠️ anim은 워킹트리 폴더 reorg(Animation→Animations) 안에 entangle(커밋스코프 유저확인 대기).

유저 "DD처럼 공격 전진성 추가". **유저 가설 2개가 실측에 반증:**
- Ⓐ "리타이머로 전진 저작" = **불가**(리타이머=순수 time-warp, translation 생성 못 함. net 0 소스는 어떤 bake로도 전진 못 만듦).
- Ⓑ "S1_Attack01 스왑" = **무효**(그 클립도 net **0.00m**, 현 Combo1과 동일 제자리).
- 숨은 사실: 현 Combo1은 발 안 옮겨도 몸이 0.16m 기울었다 *복귀*. 스텝인 = 이 복귀를 막고 net 양수로.

**경로 비교:**
- **경로2 (권고·2026-06-29 구현완료)**: `S1_Combo01_01_Retimed.anim`의 전진 ramp 저작 +0.35~0.45m, **hit(norm0.28)까지 싣고 회수 flat hold = DD 정석("밀어넣고 멈춤")**. 수평베기·이벤트·리타이밍·컨트롤러 전부 보존. 유일하게 "기본 베기 + 깨끗한 스텝인".
  - ★**드라이버 정정(2026-06-29 측정):** 위 "RootT.z" 표기는 **틀림** — 비파괴 드라이버 테스트(SampleAnimation)로 RootT.z +0.4ramp=net **0.000m(inert)** vs **MotionT.z +0.4ramp=0.428m**. 실제 구동 커브는 **`MotionT.z`**다(휴머노이드 루트모션=MotionT/MotionQ). 정적 커브 읽고 RootT.z로 단정한 게 오류 — "스텝 측정이 진실" 헌법 위반. 구현은 MotionT.z 교체(net 0.40m, raw진폭 0.3736×k1.0705)로 들어감. RootT.z는 손 안 댐(inert).
- 경로1 (빠른 테스트, 비권고): 컨트롤러 line 609 Combo1 모션 → `S1_Attack02` 스왑(한 줄, +0.41m). 단 **수평베기→내려찍기 chop**(정체성 변화)+회수 recoil+이벤트 0개 재배치 필요.
- 경로3 (Combo3 축소): 비권고(백로드 슬라이드=유저가 싫어한 둥둥 미끄럼).

**⚠️ phase2 차단요인 (먼저 해결):**
- `_Project/Animations/`의 `.anim`·`.controller` = **git-untracked = 안전망 없음** → 편집 전 **수동 백업 필수**.
- **소스 FBX(Combo1/Combo3) 이벤트 0개** → 리타이머 재실행하면 `FindEventTime` throw. 우회 = **retimed `.anim` 직접 편집**(이벤트·guid 보존, 경로2에 가장 깨끗).

## 미해결 결정 (내일)
1. **★3타(피니셔) 거취** — 현재 의도적 이동캔슬 제외(Hades 대시-온리 커밋). 유저 "수정 필요" 느낌. 택1:
   - (A) 커밋 유지(현행) / (B) 3타도 이동캔슬 허용(`_step<comboMax` 가드 제거, 경쾌하나 커밋 캐넌 재litigate) / **(C) 커밋 유지+`Combo3_RecoverySpeed`(현 0.9) 더 내려 끈적함만 제거 — 중간 권고(Animation)**.
2. ~~**변수2 전진 스텝인 구현**~~ ✅ **2026-06-29 완료**(MotionT.z, net 0.40m) — 위 §변수2 참조. **남은 것 = 유저 플레이판정**(전진성 OK + Q2 떨림 체크).
3. **회수 클립 단축 다이얼**(별개) — `Combo1_RecoverySpeed` 신규(~1.3-1.5). 단 변수1(코드)과 스택 — 이중 단축 과하면 묵직 손실.
4. **클래시(맞받음) = Day2 제외**(유저 "너무 어려운 부분"). `timingCritEnabled`은 켜진 채 → 우연 클래시 가능. 동결 아님, 후순위.
5. **커밋 여부** — 변경 미커밋. 유저 판정 후.

## 변경 파일 (전부 미커밋)
- `Assets/_Project/Scripts/Player/KatanaWeapon.cs` — SFX 2층(노브9+메서드3+호출3) + 이동 self-cancel(노브2+OnTick else-if). 컴파일 0 에러.
- `Assets/_Project/Scenes/Labs/RunFeel_Whitebox.unity` — SFX 직렬화 9 + PlayerAttackSfx 비활성 + Day2_ClashTestDummy(저장 미확정).

## Day2 노브 지도 (Inspector 라이브 — SerializeField 씬 덮어쓰기 주의)
| 노브 | 위치 | 현재값 |
|---|---|---|
| 히트스탑 길이 | EnemyDamageReceiver.hitStopDuration | 0.035 (킬 ×2.2, 상한 0.08) |
| 카메라 킥 | KatanaWeapon comboKick/finisherKick | 0.12 / 0.28 |
| 윈드업 무게 | KatanaComboRetimer Combo1_WindupSpeed | 1.5 (윈드업 0.245s) |
| SFX | KatanaWeapon swishVolume/impactVolume/impactPitch | 0.10 / 0.14 / 0.95 |
| 이동캔슬 | KatanaWeapon moveCancelEnabled/moveCancelDeadzone | true / 0.2 |

## 핵심 실측 (Animation, Unity 스텝 측정)
- Combo1(S1_Combo01_01_Retimed): 길이 0.878s · hit@0.245(norm0.279) · window@0.362 · end@0.798 · net 전진 0m.
- Combo3: 1.13s · net 1.34m(백로드 슬라이드).
- 윈드업 28% / 회수 50% = 이미 묵직 프로파일.
