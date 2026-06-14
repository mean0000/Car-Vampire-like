---
name: project-telegraph-driver-dimax
description: Dimaxillosaurus 근접 상태머신·AI (★현행 v9=AdvanceGain 1.3× 전진증폭 OnAnimatorMove, v8 끊임없는좌우 위) + 장판 텔레그래프 드라이버(TelegraphPad/Pool — 보존, 타 종용). 세 번째 몬스터 틀.
metadata:
  type: project
---

세 번째 몬스터 틀 = **근접 "정직한 접근-정지-스윙"**(Caniathrox 돌진·Venodonte 원거리에 이은). Dimaxillosaurus LV3 클로 콤보. ★메인 신규 = **장판 텔레그래프 런타임 드라이버**(ThreatArc.shader 첫 게임 활성화 = Venodonte의 ProjectilePool에 대응하는 새 재사용 시스템).

## 만든 파일 (미커밋 — 오케스트레이터 리뷰 대기)
- `Assets/_Project/Scripts/TelegraphPad.cs` — 장판 1개 인스턴스(ThreatArc 쿼드, _Progress 구동). SpawnFan/SpawnCircle/SpawnRing + ForceFull.
- `Assets/_Project/Scripts/TelegraphPool.cs` — 공유 풀(ProjectilePool 동형). PickupInfo 레이어13 쿼드 prewarm, Acquire/Return.
- `Assets/_Project/Scripts/DimaxillosaurusBrawler.cs` — 근접 AI(접근→정지→Roar 앵티시페이션→ClawCombo). AnimationEvent 콜백 TelegraphClaw/ClawHit.
- `Assets/_Project/Scripts/DimaxillosaurusLabSpawner.cs` — 랩 스포너(VenodonteLabSpawner 동형).
- `Assets/_Project/Scripts/Editor/DimaxillosaurusLabSetup.cs` — 클립복제·이벤트주입·머티리얼·컨트롤러 빌드.
- `Assets/_Project/Scripts/Editor/DimaxillosaurusLabCapture.cs` — 씬빌드+디스크 렌더 캡처.
- `Assets/_Project/Animations/DimaxillosaurusBrawler.controller`(★5상태 v3) + `DimaxRM/Dimaxillosaurus@Left/RightClawsAttackForward_RM.fbx`(★단발 이벤트 사본 2개). (구)2HitCombo 사본은 미사용.
- 씬 `Assets/_Project/Scenes/Greybox_DimaxillosaurusLab.unity`. 캡처 `docs/03_reference/assets/dimaxillosaurus_lab/`.

## 장판 드라이버 재사용 인터페이스 (나머지 장판 종 토대)
- `TelegraphPool.Acquire()` → `TelegraphPad`. 드라이버가 `pad.SpawnFan(origin, forward, radius, angleDeg, fillDuration, holdAfterFull, color, masterAlpha, fillAlpha, edgeAlpha, edgeWorld)` 호출. Circle/Ring도 동형.
- `pad.ForceFull()` — 컨택 프레임에 _Progress=1 강제(채움 클립 미세 어긋남 보정).
- ★셰이더 계약(ThreatArc.shader): _Style 3=원/5=부채꼴/6=링, **_SizeWorld.x = 지름(반지름×2)**(쿼드 localScale와 일치 필수), _AngleDeg **1~180 클램프**(초과=SDF 부호반전 통째 소실), _Color/_Alpha/_FillAlpha/_EdgeAlpha/_EdgeWorld/_InnerR01. 부채꼴 apex=쿼드 중심, 전방=쿼드 로컬+y.
- ★렌더 경로(동결, 재발명 금지): 쿼드=PickupInfo 레이어13 → URP_HighFidelity_Renderer의 PickupInfoOverlay RenderObjects(이벤트 600, RenderQueueType=1 투명, m_Bits=8192=bit13). _ZTest LEqual(4)+renderQueue 3000. StandardRequest 렌더가 이 패스 포함 → 정적 캡처로도 검증됨(레드오렌지 부채꼴 확인).

## ★★importer AnimationEvent time = 정규화(0~1) 함정 (신규 실측 2026-06-13 — 재발 방지)
- `ModelImporterClipAnimation.events[].time` 은 **정규화(0~1)** 로 해석돼 import 시 클립 길이로 곱해진다. **seconds로 넣으면 ×길이 밀려** 마지막 이벤트가 클립 밖으로 나가 영영 안 발화. (Venodonte 3AcidShotCombo 메타의 0.225/0.425/0.625도 *정규화*값이었다 — 초가 아님.)
- → importer 경로엔 **정규화 time**을 넣는다. floatParameter(fillDuration 같은 커스텀 페이로드)는 드라이버가 초로 소비하므로 *초* 그대로 둬도 무관(곱셈 대상 아님).
- `AnimationUtility.SetAnimationEvents(clip, events)` 는 *초*로 정확하나 **reimport 때 wipe**돼 비내구 → importer 경로(정규화 time)가 정답.
- ★부수효과: 정규화 time은 **클립 길이 변동에 불변**(복제 사본이 1.833↔1.949s로 import마다 흔들려도 컨택 정점 추적 유지). 길이 불안정한 generic 클립에 오히려 견고.

## ★★★★★전진 증폭 AdvanceGain (v9, 2026-06-14, 루트모션 ×1.3 — 유저 승인 헌법 확장) ★현행 최상위
★문제: 클로월 전진 ≈3.75 m/s(클로당 2.218m/0.59s) < 걷기5.5 → 솔로가 걸어서 빠짐(§5긴장). ★유저 ▶판정(AskUserQuestion) = **"전진거리 ↑(루트모션 증폭)"**: 속도(state.speed=4구간 이즈램프 "휘릭")는 **불가침** — *거리만* ×1.3. 클로당 2.218→~2.88m → 새 전진 **≈4.9 m/s**(걷기 근접, 걸어선 못 빠짐·대시로만).
- **★헌법 미세확장(유저 승인)**: 코드가 루트모션을 *증폭*하는 건 OK — *증폭*이지 *발명*이 아니면. ✅`animator.deltaPosition`(클립 자신 전진델타)×gain. 방향·궤적·타이밍 100% 클립이 진실, 코드는 전진 *크기*만. ❌코드 자체 속도/벡터로 위치 발명(과거 포물선 사고). 과거 제2원칙("코드 위치이동 금지")의 birth=발명, 증폭은 허용.
- **구현(드라이버만, SetupData 재빌드 불필요 — 컨트롤러 불변·컴파일만)**: `public const float AdvanceGain=1.3f`(SSOT). `void OnAnimatorMove(){ model.position += modelAnimator.deltaPosition*AdvanceGain; model.rotation *= modelAnimator.deltaRotation; }`. **회전은 1×**(클로 회전RM=0 실측→≈identity, 조향=FaceTarget). applyRootMotion=true **유지**(델타 채워두되 OnAnimatorMove가 자동적용을 콜백으로 위임=이중적용 0). false면 deltaPosition 죽음.
- **★★프리팹 구조 검증(OnAnimatorMove 발화 핵심·자식함정 회피)**: Dimaxillosaurus.prefab = **Animator가 루트 GameObject("Dimaxillosaurus", m_Father:0)에 1개뿐**. 스포너가 `enemy.AddComponent<Brawler>()`로 드라이버를 *그 루트*에 붙이고 `model=enemy.transform`(=Animator transform), `modelAnimator=GetComponentInChildren<Animator>()`(루트 것 유일하게 잡음). → 드라이버와 Animator 같은 GO ⇒ **OnAnimatorMove 발화 보장**(자식이면 안 불려 전진0이 됐을 함정 — 검증으로 통과). model==Animator transform이라 model 직접 이동 OK.
- ★튜닝 노브 = AdvanceGain(유저 ▶ 4.5 vs 5 m/s, 1.2~1.4). state.speed·turnSpeed·v8 라우팅·4분할 = 전부 불변.
- ★유저 ▶ 판정(정지캡처 불가): ①압박 체감(걸어선 못 빠지나) ②**★발 미끄럼**(루트모션 1.3×라 보폭<이동=슬라이드 — 15m 탑다운서 경미한가, 심하면 gain↓) ③궤적 안 휘나(증폭은 크기만이라 안 휘어야) ④긴장.

## ★★★★상태머신 v8 — 끊임없는 좌우 (2026-06-14, Idle "잠시 쉼" 제거 + 추적을 Windup에 접음)
★유저 ▶판정: "포효 후 평타 잠시 쉼, 평타 잠시 쉼… 저 쉼 빼고 빠르게 좌우좌우 끊임없이." → 쉼의 정체 = (v7) Recovery→**Idle**(chainGap 0.18s 재조준 비트, Idle 클립 깔림). ★유저 확정(AskUserQuestion) = **"윈드업에 재조준 접기"**: Idle 비트 제거 + 추적(FaceTarget)을 각 클로 *Windup*(cocking)에 이관.
- **쉼-제거 메커니즘 = (b) Recovery→반대손 Windup 직행(Idle 우회).** (a) 1프레임 Idle 라우터 대신 (b) 택한 이유: 유저 확정이 "Idle 비트 없는 끊임없는 좌우"라 Idle 미방문이 직역. 경계 포즈 = Recovery frame35 중립 ≈ 반대손 Windup frame0 중립이라 CUT 점프 작음(★유저 ▶ 확인).
- **전이 구현(★순서 핵심)**: L_Recov→R_Windup(if chainR, exit0.98) **먼저** + L_Recov→Idle(무조건 exit0.98) **나중**(폴백, 타깃소실 시만). R 미러. Animator는 리스트 순서로 평가 → trigger 셋이면 직행, 아니면 Idle. **둘 다 exit0.98 동일**(폴백이 먼저 안 잡히게). MCP 디스크 검증 완료(states10 params3 그대로).
- **드라이버 변경**: ①chainGap·_gapTimer·_comboEntered 제거(고아 정리). ②엣지가드 _windupSetup/_recovChained(해당상태 아닐 때 false→진입1회). ③**Recovery 진입 1회 = SetTrigger(_nextRight?chainR:chainL)** → ExitTime에 반대 Windup 직행. ④**Windup = 매프레임 FaceTarget**(추적 이관) + 진입1회 셋업(_nextRight 토글·스테일청소). ⑤Strike/FollowOut/Recovery = 회전0 그대로.
- **★헌법 미세개정(유저 승인)**: 회전 O = Roar/Idle/**★Windup(cocking)**. 회전 0 = Strike/FollowOut/Recovery(commit~회수). Windup은 *내지르기 전*이라 조준해도 런지 궤적 안 휨 = 헌법 정신 부합. 드라이버·빌드스크립트 회전경계 주석 갱신 완료.
- **★turnSpeed = 추적 노브**(chainGap 대체). Windup ~0.158s(9f÷1.9배속) × 360°/s ≈ 클로당 최대 ~57° 회전. 너무 잘 따라오면↓·사이드스텝 놓치면↑.
- **캡처 watcher 갱신 = 불필요**(상태명/수 불변, 라우팅·회전만 변경 → §7.1 스톨 함정 미해당. Roar/L Windup/L Strike/R Windup/R Strike 다 방문되어 _shotMask 완성 도달).
- ★★유저 ▶ 판정(정지캡처로 못 봄): ①끊김없는 연타 체감(쉼 진짜 사라졌나) ②추적 잘 따라오나(Windup서 충분히 도나) ③**★너무 spam이라 못 읽히나**(쉼이 readability 비트였을 수 있음 — 과도-연타 위험을 플레이로). ④경계CUT(Recov→반대Windup) 점프 보이나.

### (구)v7 — 클로월 + 스윙 이즈 4분할 (2026-06-14, 10상태 — v8 끊임없는좌우로 라우팅·회전 개정, 보존)
★유저 디렉팅: "휘두르는 거 더 빠르게 + 앞부분 빠르게·뒷부분 빠르게·자연스럽게 연결, 휘릭휘릭휘릭." ★방향 정정: (구)"ClawSpeed 1.0 자연·빨리감기❌"는 *flat 균일 2.5×*에 대한 거부였고, 오늘 유저가 "이즈(가속) 곡선으로 빠르게"로 마음 바꿈 → 균일❌·이즈 셰이핑✓.
★메커니즘(왜 헌법 준수): 한 state.speed는 클립 전체 균일 → "앞 빠르게·중간 자연·뒤 빠르게" 이즈를 *per-frame 코드 speed 곡선*으로 만들면 헌법 위반(코드 매프레임 스크럽). 대신 v6의 split을 **2분할→4분할로 확장**, 구간별 *정적 state.speed*의 **계단형 이즈 근사**. 같은 take 4분할이라 경계 비트동일→포즈점프0·루트모션 손실0(메커니즘 정체성=v6 split과 동일, 구간만 늘림).
- **상태 10**: Idle/Roar + L/R×{**Windup/Strike/FollowOut/Recovery**}. 파라미터 3 그대로(attack/chainL/chainR).
- **구간/speed 램프(=이즈)**: Windup 0~9f speed**1.9**(앞 빠르게/cocking) → Strike 9~16f speed**1.35**(컨택f12+초기팔로스루=읽히는 히트, ★ClawHit 여기) → FollowOut 16~22f speed**2.3**(후기팔로스루 휙) → Recovery 22~35f speed**2.5**(중립복귀). ★(구)1.0→3.0 하드 점프 소멸: Strike만 상대적 느림(히트앵커), 2.3→2.5 이음매 거의 없음.
- **전이**: Idle→Roar(attack)/Idle→L_Windup(chainL)/Idle→R_Windup(chainR)/Roar→L_Windup(exit0.95)/**Windup→Strike→FollowOut→Recovery(각 exit0.99 CUT 연속)**/**Recovery→Idle(exit0.98)**. 전부 dur0(MCP 검증 완료). ChainCut 헬퍼=exit0.99/dur0.
- ★드라이버 Update: **Windup 진입**(첫 구간)에서 콤보셋업(engage·_nextRight 교대·gap예약). Strike/FollowOut/Recovery는 한 분기로 묶어 회전0 재생. **4구간 전부 회전 0**(같은 동작 발동~회수). _comboEntered는 Idle서 리셋.
- ★SSOT: WindupSpeed/StrikeSpeed/FollowSpeed/RecoverySpeed = 드라이버 **public const 4개**, 빌드스크립트가 `DimaxillosaurusBrawler.Xxx`로 참조. 경계 const=WindupFrame9/StrikeFrame16/SplitFrame22/ClipFrames35.
- ★컨택 norm = (컨택절대프레임 − WindupFrame)/(StrikeFrame − WindupFrame): L (12.25-9)/7=**0.464**, R (12.845-9)/7=**0.549**. MCP 검증 ClawHit L@0.108s·R@0.128s(절대 frame12.25/12.85 불변).
- ★캡처 watcher(LabCapture) 동반 갱신 완료: LeftClaw_Swing/RightClaw_Swing→**Windup(채움)/Strike(컨택)** 매칭으로 교체(§7.1 스톨 함정 회피).
- ★★유저 ▶ 판정(정지캡처로 못 봄): ①"휘릭" 스냅 체감(이즈 램프가 진짜 휘릭휘릭인가) ②이음매 매끄러움(특히 가장 짧은 FollowOut 0.087s 실효—5틱이라 ExitTime 전이 드랍 없는지 플레이로) ③Strike 1.35가 히트로 읽히나(너무 느림/빠름).

### (구)v6 — 스윙/회수 2분할 (2026-06-14, 6상태 — v7 이즈4분할로 교체, 보존)
★v4 클로월 골격(아래) 위에 각 단발을 Swing+Recovery 2분할 → 상태 6. Swing speed1.0 자연 + Recovery speed3.0 배속. 🟥하드 1.0→3.0 점프가 "휙 채서 어색" 우려 → v7에서 이즈 4분할로 교체. Swing.lastFrame22==Recovery.firstFrame22 비트동일 통찰은 v7 4분할이 계승.

### (구)v4 골격 — "벽처럼 오는 클로월" (2026-06-14, Approach·장판 제거 — split의 토대)
유저 디렉팅(v3 위, "내가 말한대로"): "멀리서 발견→**그 자리에서 포효**→공격 좌·우·좌·우 반복하며 와→**장판 필요 없어**→**벽처럼**." → **클로 단발 L→R→L→R 무한 교대가 곧 이동수단**(별도 Approach 없음). 디스인게이지 없음.
- **상태(4)·파라미터(3)**: attack(Trig 오프너)·**chainL(Trig Idle→ClawLeft)**·**chainR(Trig Idle→ClawRight)**. ★isMoving/isRunning 제거(Approach 없음 → BlendTree 없음 → "Float여야" 함정 자체 소멸). 상태=Idle/Roar/ClawLeft/ClawRight.
- **상태**: ClawLeft·ClawRight(speed 1.45=ClawSpeed 배속 런지)·Roar(speed 5.0 압축 ~0.57s). ClawSpeed = **드라이버 public const 단일 진실원**, 빌드스크립트가 참조.
- **전이(전부 CUT dur0 — 정체성 동작)**: Idle→Roar(If attack) 오프너1회 / **Idle→ClawLeft(If chainL)** / **Idle→ClawRight(If chainR)** / **Roar→ClawLeft(ExitTime0.95)** 오프너 첫 손 항상 L / ClawLeft→Idle·ClawRight→Idle(ExitTime0.98). ★Approach·자기루프·블렌드 전부 없음(클로질이 전진이라 로코모션 상태 불요).
- **드라이버 로직(Idle 허브, 거리 게이트 전무)**: 미교전이면 즉시 attack(포효 — **멀어도, 거리 무관**) / 교전 중이면 chainGap(0.18s) 재조준 비트(회전만) 후 _nextRight에 따라 chainL/chainR 직행(무한). **_nextRight = 방금 진입한 손의 반대**(ClawLeft 진입 시 true). 오프너 Roar→ClawLeft 후 다음 R. ★attackRange/breakRange/runDistance/moveSpeed 노브 **전부 제거**(벽=끊임없음, 정지 조건 없음).
- **★전진 = 오직 클로 루트모션**(각 2.22m). "더 전진" = ClawSpeed 배속↑ + 체인 연속성(코드 위치이동 금지). 지속전진 ≈ 2.2177/(1.1667/S + chainGap). S=1.45,gap0.18 → **~2.25 m/s**(느림 — 벽은 끈질김이지 빠름 아님).
- **★공정성/탈출 = 걸어도 앞섬**: 클로월 ~2.25 m/s ≪ 걷기 5.5 → **단일 Dimax는 걸어서도 탈출 가능**(질주 불필요). 위협 = 4기 사방 포위 + 무시불가(항상 네 위치로). 단일을 걷는 플레이어도 압박하려면 ClawSpeed↑ 필요(단 고배속=클로 블러 — 유저 ▶ 트레이드오프).
- **★토큰 = 비게이팅**(클로월 변경): 토큰 못 잡아도 전진 멈추지 않음(모두가 벽). 오프너에 best-effort 획득·OnDisable 반납(플러밍 보존 — "토큰 수명=교전 수명"). 동시제한 의미 약화 → 유저 ▶(제거/재용도). ResetCombatState가 세 트리거+플래그 일괄 리셋.
- **★trigger 오발 가드**: `_firedThisIdle`(Idle당 1발)·Roar/단발 진입 시 ResetTrigger(attack/chainL/chainR) 청소.
- **★(구)v3 보존**: Approach(run/walk)·attackRange/breakRange·장판(부채꼴 fan/TelegraphClaw)·콤보 — 클로월 이전 산물. 재추격 접근 페이즈가 다시 필요하면 v3 패턴 참조.

## ★★스윙/회수 SPLIT (v6, 2026-06-14 유저 디렉팅) — "자르지 말고 회수만 빠르게 재생해서 바로 다음으로"
🟥(폐기)끝트림(v5 firstFrame0/lastFrame26): 회수를 *버려* 거리손실+시간단축으로 speed2.5서 3.94m/s 급등. 유저 거부 → SPLIT으로 교체.
- ★핵심 통찰: **한 state.speed는 클립 전체 균일** → "회수만 배속" 불가 → 각 단발을 *두 상태*로 쪼갠다. 같은 take에서 두 ModelImporterClipAnimation(frame 범위만 다름):
  - **ClawX_Swing** firstFrame0/lastFrame**22** = 0.733s, state.speed **1.0 자연**. ★ClawHit 여기(컨택 frame12).
  - **ClawX_Recovery** firstFrame**22**/lastFrame35 = 0.433s, state.speed **3.0 배속**(RecoverySpeed). 회수 *재생*(0.144s 실효), 이벤트 0.
- ★★연속성 = **같은 take라 Swing.lastFrame22 == Recovery.firstFrame22 = 비트-동일 포즈**(SampleAnimation 보장). Swing→Recovery 전이 CUT(dur0, ExitTime0.99)여도 포즈 점프 0 → crossfade 아님, 한 동작의 분할이라 매끄러움(헌법 준수).
- ★★루트모션 **손실 0**(트림과 정반대): Swing이 frame0~22(+1.874m 실측), Recovery가 22~35(+0.311m 실측)을 각자 운반 → 합 풀클립 2.218m. Recovery의 +0.311m가 3배 빠르게 지나갈 뿐 *버려지지 않음*.
- ★구현(빌드스크립트 const 단일 진실원): SplitFrame=22·ClipFrames=35·SrcFps=30. ClawSpeed(드라이버 const)=Swing speed, **RecoverySpeed(드라이버 public const)=Recovery speed**(빌드스크립트가 둘 다 참조). 컨트롤러 LoadClipByName으로 sub-clip 이름 로드(LeftClaw_Swing/Recovery 등).
- ★컨택 정규화 = **컨택절대프레임/SplitFrame**(스윙 frame 수): L 12.25/22=**0.5568**, R 12.845/22=**0.5839**. 스윙은 speed1.0 자연이라 절대 컨택초 불변(L0.408s/R0.428s) — 이벤트가 진짜 타격 모먼트에.

## ★단발 클로 이벤트 = ClawHit 1개/클립 (장판 제거 후 — v4 클로월, 트림 후 norm 갱신)
- LEFT: ClawHit norm**0.6806**(트림 0.600s 기준, 절대 0.408s, 실측 검증 `[ClawHit @0.408s] clip.length=0.600s`). RIGHT: norm**0.7136**(절대 0.428s, 검증 `[ClawHit @0.428s] clip.length=0.600s`). 컨택 정점 = 히트 모먼트(향후 데미지 훅, 전투스탯 범위 밖).
- ★장판 텔레그래프 **제거**(Dimax 미사용): TelegraphClaw 이벤트 미주입, fan SerializeField·_activePad·ParseFloat·telegraphPool 필드 전부 드라이버에서 제거. **TelegraphPad/Pool/Lab.cs 클래스는 보존**(타 종용). 스포너도 TelegraphPool 미생성/미주입.
- ★(구)v3 장판: LEFT TelegraphClaw norm0.04 fill0.362native fwd1.211m / RIGHT norm0.04 fill0.382 fwd1.371m / fan r2.5·100°. 장판 종 부활 시 fwdToContact(stringParameter 미터)·fill÷ClawSpeed 패턴 참조.
- ★SendMessage 수신: Animator·Brawler 둘 다 프리팹 *루트* 동거(도달 보장). `(AnimationEvent ev)` = int 소비.

## 유저 ▶ 판정 대기 (정적/스텝 검증 한계 — 흐름/속도감/압박감은 플레이로만)
- ★**벽처럼 끈질긴가** — 클로 단발로 끊임없이 다가오는 게 "벽"으로 읽히나.
- ★**멀리서 클로질이 우습지 않나**(북극성#6) — 먼 거리 허공 할퀴기가 "초등학생"이 아니라 "먹잇감 런지"로 읽히나(Forward_RM이 앞으로 내지르는 커밋 모션이라 OK여야). 아주 먼 거리는 성큼 다가오는 비트가 필요한지(현재 Roar=제자리라 시작점은 안 움직임 → 첫 클로부터 전진).
- ★**속도감** — 클로월 ~2.25 m/s가 굼떠 보이나(노브=ClawSpeed↑, 단 고배속=클로 블러).
- ★**질주(걷기) 탈출 공정성** — 단일 Dimax는 걸어서도 탈출 가능(~2.25≪5.5). 단일도 걷는 플레이어를 압박해야 하면 ClawSpeed↑ 필요. 4기 포위가 진짜 벽 위협.
- ★**토큰 거취** — 비게이팅으로 의미 약화. 제거할지/동시 데미지 제한으로 재용도할지.
- chainGap 0.18s 유지 권장(★트림 후 단발이 0.24s로 짧아져 gap이 사이클 큰 비중 → 0.18로도 흐름 매끈, 더 낮추면 속도>5m/s 급등+추적약화 → 권장 ≥0.13). 좌우 연타 리듬에 맞나.
- ★★**트림 후 속도 급등** — speed2.5에서 지속 3.94 m/s(옛 ~2.25). 걷기5.5 탈출 마진 1.4×로 좁아짐. "벽이 너무 빠른가/적당한가" + ClawSpeed 재판정(트림이 거리아닌 시간을 줄여 같은 speed로 빨라짐).

연동: [[project_dimaxillosaurus_clip_kit]]·[[project_telegraph_pad_shader]](셰이더 본체 — 보존)·[[feedback_animevent_fire_timing]]·[[project_caniathrox_attack_statemachine]](회전경계 패턴)
