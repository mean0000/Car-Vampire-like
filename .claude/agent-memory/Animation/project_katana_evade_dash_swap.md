---
name: katana-evade-dash-swap
description: 대시(회피)=Frank Evade 클립 — ★R14c 회수 이동캔슬(DashCancel 트리거·무입력=풀재생)·R14b 변위 소유권 코드→클립 루트모션(이동-시각 분열 수정·window-S/L 분리)·R14 3단 배속·Bake OFF
metadata:
  type: project
---

카타나 대시(회피) 애니를 Frank Step→**Frank_RPG_Katana_Evade** 4방향으로 교체 (2026-07-11 구현). 컨트롤러=KatanaMelee.controller Dash 상태의 DashDir 블렌드트리(SimpleDirectional2D·DashX/DashY).

**Why:** 유저 지시 "Frank_RPG_Katana_Evade로 변경". R10에서 dashFaceMotion=true(기본)라 대시 순간 몸이 대시방향 보고 DashX=0/DashY=1 → **Evade_F가 주력**, B/L/R은 dashFaceMotion=false 폴백.

**How to apply:** 대시 비주얼/리타임 재튜닝 시 이 값과 구조 재사용.

## Evade 클립킷 실측 (전부 0.8s / 60fps / 48f · grounded y=0)
MotionT 커브 실측(루트모션 direct read). guid는 각 FBX(Root_Motion 폴더):
- **F** (net z=+3.28m, x0) peakSpeed f10(n0.208)·travel80%@f18(n0.38) — guid `d55406f904646974698056610fa84d71`
- **B** (net z=-2.79m) peak f14(n0.29)·80%@f18(n0.38) — guid `fbb77d11ed143604a88c47fa0effb008`
- **L** (net x=-2.80m) peak f3(n0.06)·80%@f20(n0.42) — guid `a4535bb3880138e41bb2996e4c85ddc6`
- **R** (net x=+2.80m) peak f7(n0.15)·80%@f19(n0.40) — guid `393869f841fa5594aa5ee5a2eecccb3d`
- 클립 internalID(fileID) 전부 1827226128182048838 (Frank 단일테이크 관례). 블렌드트리 child 매핑: pos(0,1)=F·(0,-1)=B·(-1,0)=L·(1,0)=R (driver 축 우=+X/전진=+Y 정합).

## ★★★★★R14c = 회수 이동 캔슬 (2026-07-13 유저 "회피 이후 딜레이가 있어서 바로 안 움직이는데 정리해줘")
- **R14b 루트모션 전환은 유저 "좋네" 승인.** 단 R14b가 정직 깃발 꽂은 "이동 커밋 ~0.7s"(회수 꼬리 동안 이동 입력 죽음)를 유저 기각. **회수 구간만 이동 캔슬 추가.**
- **설계(오케 확정):** ①무입력=현행 유지(착지 풀재생 exitTime0.95, R13 '착지 읽힘' 살아남음). ②이동키(홀드/신규 무관) + 클립 진행 `dashMoveCancelPoint`(기본**0.5**) 지나면 → Dash→Locomotion 하드컷 + 변위창 즉시 종료 → 다음 프레임 이동 복귀. ③변위 구간(발구름+비행 ~n0.42 이전)은 **못 끊음**(cancelPoint 하한 0.42=dashFlightEnd 넘김, 회피 커밋/거리 일관성).
- **컨트롤러:** `DashCancel` 트리거 신규 + Dash→Locomotion 전이 2번째(cond DashCancel·hasExitTime**false**·dur0 CUT). 기존 exitTime0.95 전이와 공존(둘 다 →Locomotion). exitTime 전이는 nt≥0.95에만 활성이라 nt0.5의 DashCancel이 이김(순서 무해).
- **드라이버:** nt 구동 블록서 `if(dnt>=dashMoveCancelPoint && hasIntent){ SetTrigger(DashCancel); _motor.EndDashRoot(); }`. `hasIntent`=moveIntent(input.move) 매 프레임 판정(홀드=신규 동일). **시작 시 `ResetTrigger(DashCancel)`**(직전 대시 미소비 트리거가 새 대시 프레임0 즉시캔슬하는 위생 함정 차단).
- **모터:** `EndDashRoot()` 신규 = `_dashActive=false·_dashRootGrace=0`(grace 0.05s 안 기다리고 즉시 종료 → "바로 안 움직임" 잔존 제거). window-S(_dashTimer)·i-frame 무관.
- **왜 컷+창종료 둘 다:** 창만 닫고 Dash 상태 안 끊으면 로코모션 이동+Dash 회수포즈 재생=발슬라이드. 컷(포즈=로코모션)+창종료(모터 이동) 동시라야 싱크.
- **실측:** 잔여변위 드랍=무입력 3.27m vs 이동캔슬(n0.5) **2.90m**(드랍 0.37m·11%). 이동복귀=대시시작→이동가능 **~0.4s**(n0.5도달0.378s+모터1프레임), 구 R14b ~0.75s 대비 ~0.35s↑. 재진입가드/글라이드불변식/대시베기버퍼(DashCommitted) 유지.
- **리스크:** 컷프레임 포즈팝(회수포즈→달리기 하드컷, 플레이어 DMC 캐넌이라 dur0=정석·팝 크기는 유저플레이). 노브 dashMoveCancelPoint(0.42~0.9): 낮을수록 즉복귀·거리드랍↑, 높을수록 거리보존·복귀늦음. 컴파일0·콘솔0·씬/컨트롤러 정합(param+전이 디스크 persist 확인).

## ★★★★R14b = 대시 변위 소유권 코드→클립 루트모션 이전 (2026-07-13 유저 재판정 "움직이고 나서 회피 모션이 나오잖아, 루트모션이잖아")
- **진단(배속 아닌 구조):** 모터가 위치를 먼저 쐈다(0.15s 버스트 3.2m, 앞쪽 몰빵=첫 0.1s에 2.84m) → 클립은 그 위에 포즈만 재생 → 몸이 코일 포즈인 채 이미 날아가고 뻗는 실루엣은 이동 끝난 뒤. R10 Codex ④(이동-시각 분열)와 수렴. **클립 MotionT(피크 f10·3.27m)가 이미 정답 변위 프로파일인데 버리고 있었다.**
- **수정 = 공격 루트모션 경로(06-19)와 동일 파이프로 통일.** UpdateDash 버스트 폐기 → OnAnimatorMove가 Dash 클립 deltaPosition을 ApplyRootStep으로 위치에 적용(공격과 같은 WallGuardedStep+지면). 위치 단일 소유=Motor.
- **★핵심 구조 = 두 창 분리(옛 IsDashing 하나가 겸했음):** ①**window-S** `DashCommitted`(`_dashTimer` 0.15s)=재대시 금지+입력 버퍼(하드컷 캐넌·PlayerBrain L79). ②**window-L** `IsDashing`(`_dashActive`)=변위/비주얼 창, **애니 구동**(드라이버 OnAnimatorMove가 Dash 상태 재생 중 `KeepDashActive()` ping, 모터 grace 0.05s 워치독 자동만료). 왜 분리=변위는 클립 길이(~0.7s) 필요, 입력락은 짧게(0.15s) 유지 안 하면 착지 내내 공격버퍼돼 하드컷 죽음. Motor.Tick은 `_dashActive` 동안 양보(locked과 동형, `_velocity=0`·`_glideTimer=0`=글라이드불변식 유지).
- **★재진입 버그(잡음):** 드라이버 Dash **bool**은 window-L로 구동하면 안 됨 — IsDashing이 클립 전체 true라 exitTime 0.95로 Locomotion 빠진 뒤에도 bool true→AnyState→Dash가 Locomotion서 재발화→무한 재진입. **bool=`DashCommitted`(window-S)**로: 0.15s에 꺼져 exit(0.7s) 전에 false=1회 진입만.
- **Bake 무변경(실측):** 4방향 Evade **maxRootRot=0.0°**·경로 직선(F maxAbsX 0.000)→회전/Y 걱정 없이 XZ만 적용. Bake-all-OFF가 이미 클린 직선 3.27m 산출. (R11 "Bake OFF=제자리"는 모터가 루트모션 폐기했을 때만 참이던 것.)
- **제거(죽은 코드):** UpdateDash 메서드 + `dashDistance`/`dashEasePower`/`dashExitSpeed` 필드(UpdateDash 전용). ★씬 RunFeel_Whitebox.unity에 이 3키 직렬화돼 있음→고아(무해, Unity 무시). PlayerAfterimage L298 `8f` 리터럴+주석(현행 dashExitSpeed) stale(안 깨짐).
- **★출구 슬라이드 폐기(feel 변경):** "확! 피했다"(8m/s 관성)=클립 자체 감속(3→0 m/s)이 '정착' 대체. 0.15s 핸드오프는 어차피 window-L 양보(_velocity=0)가 소거해 무기능. 유효거리 구 ~3.9m(버스트3.2+슬라이드~0.7, 무입력)→신 고정 3.27m(~0.6m↓). 거리노브=ApplyRootStep에 없음→안 만듦(constraint).
- **★배속=변위 프로파일 동기(디싱크 원천 소멸):** deltaPosition이 DashRate로 스케일→3단(launch1.5/flight0.9/recover1.1)이 이제 포즈+변위를 함께 만든다. 발구름1.5=빨리 출발(첫0.1s 1.05m≈10.5m/s, 구 2.84m보다 느리나 여전히 스프린트 이상=agency), 실루엣0.9=비행 보임, 회수1.1=정착 정리. i-frame0.3s=변위 ~80%(발구름+비행=회피구간) 덮음, 회수는 취약(정상).
- **리스크/트레이드오프:** ①grace-linger ~0.05s 프레임 정지(dash끝 이동 3프레임 지연, DashRootGrace 조절) ②이동커밋 ~0.7s(walk-cancel 없음=constraint, 회수 발슬라이드는 오히려 해소. 커밋 길면 dashRecoverRate↑/exitTime↓) ③벽=dead-stop→슬라이드(공격과 동일 WallGuardedStep). ④"굼떠짐" 리스크면 dashLaunchRate↑. **손맛·속도체감=유저 플레이 게이트.** 컴파일0·콘솔0에러·씬정합 확인.

## ★★★리타임 v3 = R14 3단 강약 (2026-07-13 유저 재판정 "천천히·뭘 하는지 모르겠다") — R14b 루트모션 전환으로 배속이 이제 변위도 스케일
- **★진단(v2가 왜 또 실패했나 — 경계가 틀렸다):** Evade_F 프레임 실측(MotionT 커브, 위치=모터소유·클립=포즈만): **발구름 f0-8(n0~0.17) 1.8→11.4m/s가속 · 회피실루엣 f8-19(n0.17~0.40) 피크14.3@f10→3m/s감속=정체성포즈("내가 지금 피한다") · 착지회수 f19-46(n0.40~0.95) 3→0m/s무게회복=정보량낮음.** v2의 이젝션경계 0.4는 발구름+실루엣을 **통째로** 2.2×에 넣어 0.145s에 뭉갰고(정체성실종), 1.2×착지는 정보없는 회수꼬리에 0.37s를 썼다 — **강약이 거꾸로**(가독구간 뭉개고 무정보구간에 체류).
- **★수정 = 2단→3단(경계 2개), 강약 뒤집기.** ①발구름 `dashLaunchRate` **1.5**(빠른 커밋스냅, n0~`dashLaunchEnd`0.15) ②회피실루엣 `dashFlightRate` **0.9**(★핵심 가독 — 정체성포즈를 붙잡음, n0.15~`dashFlightEnd`0.42) ③착지회수 `dashRecoverRate` **1.1**(중간, 캔슬대상이라 안늘어지게, n0.42~0.95). damp 0.04. **가독윈도우(발구름+실루엣) 0.145s→0.32s(2.2배) · 총 0.51s→~0.705s(38%↑).** 4방향 공통(80%-이동 n0.375~0.42·정지 n0.77~0.85)이라 단일 경계 통용, F가 주력(dashFaceMotion).
- **핵심 원리:** 위치는 모터(0.15s버스트+8m/s슬라이드)가 소유하고 **DashRate는 포즈 배속만** 조절 → 실루엣을 0.9×로 늘려도 위치미스매치 0, 그냥 회피포즈를 슬라이드동안 더 오래 붙잡음=가독. 시작반응성=상태진입이 보장(배속과 무관, agency 불변).
- **드라이버 3단 로직:** 시작스냅 `SetFloat(DashRate, dashLaunchRate)` + `nt<dashLaunchEnd?launch : nt<dashFlightEnd?flight : recover`를 damp로. 재대시 `Play("Dash",0,0)`·연속대시 리스타트·하드컷캐넌(move만 예외) 전부 v2 그대로.
- **노브지도(라이브 인스펙터):** launch(0.8~2.4, 커밋스냅감) · flight(0.5~1.5, ★"뭘하는지" 고치는값·낮을수록 또렷) · recover(0.5~1.8, 낮으면 묵직하나 늘어질위험) · launchEnd(0.05~0.35, 피크 n0.208 앞) · flightEnd(0.3~0.7, 80%이동 근처) · damp0.04.
- **검증:** 컴파일0·콘솔0에러·씬 Visual 인스턴스 6필드 코드default 적용·구필드(Eject/Land/Fraction) 씬 미직렬화(정상 rename). **손맛·속도체감=유저 플레이 게이트.**

## (구) 리타임 v2 = 시작 빠름·착지 느림 (2026-07-12, R14가 대체)
- **문제:** 균일 m_Speed 2.2는 0.15s 이동창에 클립 41%(이젝션)만 재생 후 **Dash==false 조기컷**→착지(회전 마무리)가 안 읽혔다("회피 뭉개짐").
- **채택 메커니즘 = 드라이버 구동 DashRate 배속 멀티플라이어**(리타이머 아님). 근거: ①라이브 인스펙터 노브(유저 수시 플레이서 즉시 재판정) ②신규 클립 0(Evade 네이티브·Bake OFF 보존) ③위상=클립 자신의 normalizedTime(자기완결) ④단일 클립 배속만 조절=포즈 블렌드 없음(헌법 부합, LocoRate 선례).
- **컨트롤러 계약(KatanaMelee.controller):** Dash 상태 **m_Speed 2.2→1** + **SpeedParameterActive=1, SpeedParameter=DashRate**(신규 float 파라미터 default 1). **조기컷 전이(fileID 4320341387295703969, Dash==false CUT) 삭제** → Dash 상태의 유일 exit=폴백 exitTime **0.95**→Locomotion(CUT). 착지가 exitTime까지 재생된다. AnyState→Dash는 **CanTransitionToSelf=0 그대로**(Dash bool 0.15s 유지라 self=1이면 프레임0 동결). (이 컨트롤러 계약은 R14도 그대로 상속 — 바뀐 건 드라이버 배속 로직뿐.)
- (구 v2 노브 dashEjectRate/dashLandRate/dashEjectFraction는 R14에서 3단 필드로 대체됨. 균일 2.2 방식 기록: 0.15s컷=norm~0.41·이젝션피크 n0.208@0.076s.)

## ★Bake Into Pose = 변경 불필요 (전부 OFF, Step과 동일)
- 현행 워킹 Step 클립·Evade 클립 **둘 다 loopBlendOrientation/Y/XZ 전부 0 (Bake 전부 OFF)**. 대시 중 루트모션이 **완전 폐기**되므로(코드가 위치 소유) Bake OFF가 정답=제자리 발놀림, 코드가 리그 슬라이드, 워프 없음. Bake XZ ON이면 몸이 클립 전진(3.28m)만큼 리그 앞으로 튀었다 되돌아옴=금지.
- ★★기존 06-20 메모(project_player_dash_rootmotion: "Step lockRot ON/lockY ON/lockXZ OFF")는 **루트모션-구동 대시 시절 값** — 현행 코드소유 대시에선 Step import가 Bake 전부 OFF로 바뀌었고 그게 워킹. 대시 클립 bake 판단은 "루트모션 쓰나(→Bake로 grounded/facing 조절) vs 폐기하나(→전부 OFF)"로 갈린다.

## 이중이동 안전 검증 (구조)
- PlayerAnimatorDriver.OnAnimatorMove: `_attacking||IsDashing`일 때만 ApplyRootStep(deltaPosition) 호출(rotation 미전달). 대시 중 IsDashing=true라 호출은 되지만 **PlayerMotor.ApplyRootStep가 `_dashTimer>0f||_dashAppliedThisFrame`면 즉시 return**(위치=UpdateDash 코드버스트 단일소유). ∴ 클립 루트모션 폐기=비주얼 전용.
- DashLocalX/Y는 PlayerMotor L390-391서 **카디널 스냅**(한쪽만 ±1) → SimpleDirectional2D가 항상 단일 Evade 100% 선택, 두 클립 블렌드 없음(face-motion·폴백 양쪽). 헌법 "한 동작 중 그 애니만" 충족.

## 남긴 것 / 게이트
- Step FBX(guid 5acbe88…/64cf2b…/ddcd63…/df099c…)는 디스크에 남김(미삭제)=이제 DashDir 미참조 고아. 다른 컨트롤러 참조 0(검증).
- 컨트롤러 편집=자동 게이트(Stab+Codex). 커밋 금지. 모션 느낌·속도·발슬라이드 최종판정=유저 플레이(3.28m 클립 footwork vs 코드 dash거리 미스매치=화이트박스 발슬라이드 허용).
