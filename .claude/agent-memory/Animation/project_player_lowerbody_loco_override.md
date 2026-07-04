---
name: player-lowerbody-loco-override
description: 플레이어 상하체 분리(이동/정지 무관 완전 분리, SoD/RUINER). ★현행 v4=Fix B(위상 디싱크 확정 수정): 웨이트 3갈래(baseOwns→0/combo→1/else→이즈아웃)+Combo→UB_Loco 전이 제거(홀드)+Attack트리거 재시작(canTransitionToSelf)+busy=ComboLayerActivelyPlaying(!_comboActive 게이트). v2=상체 콤보 레이어 이관·v3=웨이트 상수1(디싱크 유발, 폐기). KatanaMelee.controller. 2026-07-04.
metadata:
  type: project
---

# 플레이어 상하체 완전 분리 — ★v2 상체 콤보 레이어 (2026-07-04)

유저 지시(방향 변경, 최우선): "상체와 하체가 아예 따로 놀아야 한다. 상체의 공격이 하체엔 아무런 영향을 끼치지 않게. SoD/RUINER처럼." → **정지=전신 콤보 하이브리드(Day2 손맛 보존)를 명시적 기각.** 이제 목표=이동/정지 무관 **완전 분리**: 콤보 중 다리는 100% 로코모션(정지=idle 스탠스·이동=달림), 공격은 상체에만.

## ★핵심 결정 — 콤보를 *상체 오버라이드 레이어*로 이관 (v1 하체-오버라이드 기각)
- **왜 v1(하체 로코모션 오버라이드)이 실패하나 — 실측:** Base=전신 콤보가 골반(hips)을 크게 회전시킨다(Combo1 클립 실측: 골반 world yaw **~120° 범위**·전진 0.16m·상하 0.05m). 전신 콤보 클립에선 **다리 근육이 능동 카운터로테이션**해 발을 붙여둔다(full pose 발 world yaw 범위 **0.3°**=거의 고정). 하체-오버라이드는 그 다리 근육을 idle로 덮어 카운터로테이션을 잃는다 → **idle 다리가 콤보 골반을 그대로 타 발이 ~120° 스윙 + 0.48m 슬라이드**(A안 정지 콤보 재구성 실측). 완전 분리 요구 위반. 최소변경(`wTarget=_comboActive?1:0`)로는 못 고침.
- **왜 v2(상체 레이어)가 맞나 — Unity 휴머노이드 진실:** 두 마스크(UpperBody·LowerBodyCombo) 모두 **Root(=hips) OFF**라, **hips는 마스크가 아니라 *base 레이어*가 소유**한다. v1은 base=콤보 → hips=콤보(회전). v2는 **base=로코모션 → hips=로코모션(안정)**. 그래서 다리+골반 둘 다 로코모션, 상체만 콤보. 재구성 실측(hips+다리=idle, 척추+팔=combo): **발 world yaw 0.0°·슬라이드 0.000m·Chest yaw 70.7°·오른손 이동 1.0m** = 발 완전 고정 + 베기(척추 트위스트+칼 스윙) 온전. 이게 SoD/RUINER 정석이자 유저가 이름으로 지목한 것.
- 결론: **완전 다리 분리 = 공격을 허리 위로 가둔다(=상체 마스크 레이어). 하체 오버라이드로는 원리상 불가**(hips-vs-spine을 하체 마스크로 못 가름).

## 만든/바꾼 것 (v2)
- `KatanaMelee.controller` **layer 2 재활용**: `LowerBodyLocomotion`(mask LowerBodyCombo) → **`UpperBodyCombo`**(mask **UpperBody**·Override·weight0). API(editor script)로 편집·SaveAssets.
  - 상태: 구 `LowerLoco`→**`UB_Loco`**(default·로코모션 1D 트리 공유참조 유지=회복 시 상체가 로코로 매끄럽게 이즈, bind-pose 팝 없음) + **Combo1/2/3 이관**(Base의 클립·tag Action·speed1·WD false 그대로). 전이: `ANY→Combo1(ComboStep==1 CUT)`·`Combo1→Combo2(==2)`·`Combo2→Combo3(==3)`·각 `ComboN→UB_Loco(==0, blend 0.12)`.
  - **Base(layer0) 수술**: Combo1/2/3 상태 + `Locomotion→Combo1` + `ANY→Combo1` 전이 제거 → **Base는 콤보 중 Locomotion에 머문다**(ComboStep 전이 없음=다리 로코모션). Counter/DashAttack/Skill01*/Dash/Locomotion 무변(유저 지시=콤보 3타만 분리, 나머지 전신).
  - layer1 UpperUnequip 무변.
- `PlayerAnimatorDriver.cs`(유일 코드): `_lowerLocoLayer→_comboLayer=GetLayerIndex("UpperBodyCombo")`. weight=**`_comboActive?1:0`**(이동조건 제거=완전분리). **진입 스냅 1**(공격 크리스프)·**종료 이즈아웃 0**(comboLayerBlendTime 0.12). 대시시작 엣지 weight 0 스냅 유지(M-1). `IsActionPlaying`=layer0 **AND** _comboLayer 둘 다 Action 태그 스캔(`LayerHasActionTag` 헬퍼). 스텝인: `_suppressStepIn=_comboActive && suppressComboStepIn`(신규 bool, default true) — 콤보 전체 억제. `_commandingMove`·`comboMoveVelocityThreshold` **제거**(weight가 이동무관해 무의미). `lowerLocoBlendTime→comboLayerBlendTime` rename.

## 스텝인(Day2 +0.40m) 거취
- **완전 분리와 근본 충돌**: 정지 콤보=idle 다리인데 전진 루트모션=발 슬라이드. 유저가 분리 위해 Day2 포기 → 기본 OFF.
- 현 아키텍처(콤보=UpperBody 마스크, **Root 제외**)에선 콤보가 delta에 전진을 안 실어 **자동으로 스텝인 없음**(suppressComboStepIn 무관하게 delta≈0). 기존 OnAnimatorMove/ApplyRootStep/_suppressStepIn 경로는 **보존**(Counter/Skill/DashAttack의 Base 런지엔 여전히 적용). `suppressComboStepIn` bool은 억제 경로를 명시적으로 남긴 노브지만 **현 구조선 콤보에 대해 inert**(진짜 부활=콤보를 Root 포함 레이어로 되돌리는 컨트롤러 변경 필요=골반 커플링 재발).

## 검증(구조·포즈 PASS)·미검증(유저 플레이)
- 구조 PASS: 컴파일 클린(에러0)·디스크 재로드(L2=UpperBodyCombo/UpperBody/Override·Base 콤보없음·전이 정확)·플레이어 GetLayerIndex=2·씬 미더티(에셋2+스크립트1만, HideAndDontSave 카피로 측정).
- 포즈 PASS(SampleAnimation 재구성, 신뢰): v2 타깃 포즈=발 고정+베기 온전.
- ★**측정 함정(재발방지):** 동기 MCP RunCommand서 **Animator.Play(normalizedTime) seek·AnimationLayerMixerPlayable Evaluate 둘 다 안 통함**(포즈가 t=0에 얼어붙음). 신뢰 도구=**AnimationClip.SampleAnimation(HideAndDontSave 카피)** 로 본 로컬 회전 재구성. 레이어드 실측이 필요하면 이 재구성으로.
- 미검증(유저 플레이=최종 판정): ①정지+공격 다리 idle 유지·상체 스윙 ②이동+공격 다리 run·상체 스윙 ③대시캔슬 다리 즉시 Step ④**콤보 AnimationEvent(Hit/Window/End)가 layer2 weight>0서 발화하나**(표준 Unity=발화·06-21 상체레이어 선례 있음이나 소프트락 위험 영역이라 플레이서 확인) ⑤회복 이즈아웃·손맛.
- 스코프: 콤보 3타만 분리. 대시/반격/스킬/대시베기=Base 전신(_comboActive=false 자동 제외).
- 잔여: `LowerBodyCombo.mask` 고아(무참조·무해·재사용가능, MCP DeleteAsset 대화형 트랩 회피 위해 미삭제).

## ★콤보→걷기 복귀 튐(hitch) 수정 (2026-07-04, v2 후속)
유저 리포트: "공격 끝나 걷기로 돌아올 때 애니 살짝 끊긴다." 오케 MCP 진단=**이중 블렌드**: 콤보 종료 시 ①컨트롤러 내부 전이 Combo3→UB_Loco(0.12s) ②드라이버 웨이트 이즈아웃(comboLayerBlendTime 0.12s)이 동시에 겹쳐 복귀 곡선이 비선형(가속→꺾임)=튐.
- **수정 = 웨이트 게이트를 `_comboActive`에서 "Base가 상체를 소유하는가"로 교체.** `PlayerAnimatorDriver.cs`: `bool baseOwnsUpperBody = LayerHasActionTag(0) || (_motor!=null && _motor.IsDashing); SetLayerWeight(_comboLayer, baseOwnsUpperBody?0:1);` — **즉시(스냅), 이즈아웃 제거**. 콤보→걷기 복귀는 **웨이트를 상수 1로 두고** 컨트롤러 내부 크로스페이드 Combo→UB_Loco **하나로만** 처리 = 단일 블렌드 = 튐 소멸.
- **왜 웨이트 상수 1이 팝 없나:** UB_Loco = Base Locomotion과 **동일 블렌드트리 공유참조**(둘 다 `fileID -6802273172499591872`)라 ComboStep==0이면 상체 레이어가 로코모션을 재생 = Base와 시각 동일(무해). 그래서 콤보 끝나도 웨이트를 안 내려도 됨. (catch-22: 웨이트 즉시0=크로스페이드중 로코 점프 팝 / 내부전이 dur0=로코 스냅 팝 → 팝 없는 유일 단일블렌드 = 웨이트 상수1.)
- **★웨이트0 커버 = "Base가 상체 소유하는 전 상태"를 빠짐없이.** 컨트롤러 실측(디스크 진실): Base(0) Action 태그 = **Counter·DashAttack·Skill01Charge·Skill01Hold·Skill01Strike**(5개 전부 태그됨 — 오케가 걱정한 Skill Charge/Hold도 확인). Locomotion·Dash=무태그. ∴`LayerHasActionTag(0)`이 5개 커버 + `IsDashing`이 Dash 커버 = 완전. **이 술어는 이미 IsActionPlaying(busy/이동잠금)이 쓰던 검증된 것** → 런타임 발화 입증됨(재확인 불요).
- M-1(콤보 잔상) 자동해소 확인: 모든 Base 액션은 `_step==0`(=ComboStep==0, 레이어2 UB_Loco)일 때만 진입(BeginCounter/Skill/DashAttack/Charge 전부 가드) → 액션 종료 후 웨이트1 복귀해도 레이어2는 UB_Loco(로코모션)라 콤보 잔상 없음.
- **죽은 노브 처리:** `comboLayerBlendTime` 필드 + `_comboWeight` 필드 **제거**(이즈아웃 소멸로 무의미). 복귀 부드러움은 이제 컨트롤러 내부 전이 dur(0.12s)이 단일 소유. 대시-시작 블록의 중복 웨이트 스냅도 제거(IsDashing이 메인 게이트서 대시 시작 프레임 커버 — StartDash가 _dashTimer 세팅→IsDashing 즉시 true, 드라이버 Tick은 모터 뒤).
- **컨트롤러 위생:** 레이어2 루트 SM `m_Name` "LowerBodyLocomotion"→"UpperBodyCombo"(에디터 API `sm.name=` + SaveAssets, 하드 YAML ❌).

## ★★재발방지 함정 — 에디터 API SaveAssets = 미저장 에디터 상태 디스크 플러시
- SM 이름 1줄 바꾸려 `AssetDatabase.SaveAssets()` 했더니 KatanaMelee.controller 디스크 diff **486줄**(260+/226−). 원인 2겹: ①Unity 재직렬화(fileID 재정렬=코스메틱) ②**디스크가 stale였음** — v2 튜닝(콤보 블렌드 0.15→**0.12** 등)이 에디터 in-memory에만 있고 디스크 미반영 상태였는데 SaveAssets가 그걸 다 플러시. 세션시작 git엔 controller 무변경이었으므로 전 diff가 내 SaveAssets 산물.
- **판정=양성:** 라이브 전이표 실측(editor API 열거)이 v2 설계와 완전 일치(콤보 CUT/복귀0.12·액션 CUT·Skill 그래프 온전). 유저가 플레이하던 게 in-memory(0.12)였고 오케 진단도 그 위에서 함 → 디스크를 테스트 상태에 맞춘 것(오히려 divergence 해소). exit=0(HEAD)→0.9(WORK) 차이는 hasExitTime=False 전이라 무동작(inert).
- **교훈:** 에디터 API로 컨트롤러 1줄만 바꿔도 SaveAssets는 **모든 dirty 에디터 상태를 플러시**한다. diff가 크면 겁먹지 말고 **라이브 전이표를 열거해 설계와 대조**(값 히스토그램 diff는 sort 아티팩트로 오해 유발 — 전이 그래프 열거가 진실). 하드 YAML 편집이 금지라 이 플러시는 불가피 = 착공 전 예상하고, 결과를 게이트에 정직 노출.

## ★★Fix B — 위상 디싱크 확정 수정 (2026-07-04, v3 폐기·현행)
v3(웨이트 상수1)가 걷기 중 상체 UB_Loco를 Base Locomotion과 **독립 클럭**으로 돌려 콤보 직후 팔-다리 위상이 **영구** 어긋남(Stab+Codex 두 게이트가 구조 결함으로 독립 확정). ★"평타=이동하며 공격"(comboMoveEnabled=true)이 확정 플레이라 거의 매 콤보 발현.
- **원리:** 두 로코모션 클럭이 동시에 weight>0이 되는 상황을 **아예 제거**. 걷기 정상상태서 상체를 Base가 구동(weight 0)=다리와 같은 클럭=위상 자동 정합.
- **드라이버 웨이트 3갈래**(`Tick`): `baseOwns(=LayerHasActionTag(0)‖IsDashing)→0 즉시` / `_comboActive→1 즉시` / `else→MoveTowards(_comboWeight,0,dt/comboLayerBlendTime[0.12])`. `_comboWeight` 상태필드 재도입(프레임 간 이어가기). ★P2b=액션 중 weight0이었고 종료 후 else가 MoveTowards(0→0)=0 유지→0→1 스냅 없음(UB_Loco 드리프트 팝 소멸).
- **컨트롤러(KatanaMelee)**: `Combo1/2/3→UB_Loco`(ComboStep==0) 전이 **3개 제거**→콤보 종료 시 Combo가 마지막 프레임 **홀드**. 웨이트 이즈아웃이 그 *정적* 포즈→Base 로코모션 블렌드(정적=클럭없음=위상무관, **단일 블렌드**라 v3 이전의 이중블렌드 튐도 동시 해결). UB_Loco=vestigial 기본상태(전이 0).
- **★재시작 필수 플러밍(브리프 미명시, 내가 추가):** 홀드된 Combo에서 다음 콤보 재진입 필요(단발-후-단발 흔함). `ANY→Combo1` 조건 `ComboStep==1`(int)→**`Attack` 트리거**(미사용 param 재활용, grep 확인)+`canTransitionToSelf=true`. 드라이버 `SetCombo(step)`서 `if(step==1) SetTrigger(Attack)`. ★int==1+self=true는 재생 내내 참이라 매프레임 재발화(프레임0 동결)→**소비형 트리거 필수**(1회 발화 자동소진, 재생 중 재발화 0).
- **★busy 회귀 2건 수정(게이트 발견):** Combo가 이제 종료 후 Action태그 상태를 홀드→`IsActionPlaying`이 영구 true 위험. 콤보 레이어 분기를 `ComboLayerActivelyPlaying()`로: **①`if(!_comboActive) return false`**(Stab P0=대시캔슬 busy-freeze 방지: Cancel()→SetCombo(0)이 _comboActive를 끄는데, 중단 클립이 강제종료 없어 normalizedTime<1로 계속 재생→busy 고착→대시 직후 이동 얼어붙음. _comboActive가 논리종료[정상·캔슬 공통] 반영) **②normalizedTime<1 안전망**(_comboActive 고착 시 Animator 끝나면 해제→자가치유). LayerHasActionTag(0)[Base]은 무변(Counter/Skill/DashAttack은 Locomotion으로 전이해 태그 정확).
- **게이트(Stab+Codex 병렬)**: P0=대시캔슬 freeze(Stab, 수정 반영). P1=콤보가 Base-액션 exit-tail 중 시작하면 invisible 진입 후 늦게 노출(Codex)—**pre-existing**(v3 동일 웨이트게이트, BeginCombo가 !IsBusy 안 봄, KatanaWeapon:331 입력게이트 이슈, 스코프 밖—미수정 보고). P2=KatanaWeapon:395 "Combo→Loco" 주석 stale(dormant, comboMoveEnabled=true라 self-cancel 분기 OFF).
- **행동 변화 노트(유저 판정):** busy 해제가 자연종료 시 OnComboEnd(~0.9)로 앞당겨짐(v3=크로스페이드 tail ~1.02). ~0.12s 이른 이동복귀=Day2 "후딜 제거" 방향 부합(회귀 아님, 개선 성격).
- **검증:** 구조 PASS(전이표 디스크 실측·컴파일0·씬 미더티). ★미검증(유저 플레이=최종): 팔-다리 위상 실체감·복귀 부드러움·콤보 크리스프·대시캔슬 즉응. ★측정한계=MCP 플레이 신뢰낮음(Animator.Play seek·LayerMixer Evaluate 동기 RunCommand 불통)→재시작/위상은 **상태전이 논리+구조 실측**으로 보강, 런타임 손맛은 유저.
