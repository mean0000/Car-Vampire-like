---
name: project_upper_lower_body_split_pattern
description: 상하체 분리 QA — v1(폐기) → v2(콤보=UpperBodyCombo 레이어) → v3(콤보→걷기 튐 수정, H-1=위상 디싱크 확정) → v4/Fix B(H-1 해결책=콤보 홀드+웨이트 3분기+Attack트리거 재시작, 단 ★P0=대시캔슬 시 이동 영구프리즈 신규 발견).
metadata:
  type: project
---

## v1 (폐기됨 — HEAD에 커밋된 적 없음, 착공 전 리젝트)

QA 대상: `LowerBodyCombo.mask`(신규) + `KatanaMelee.controller` layer2 "LowerBodyLocomotion"(Override, mask, 단일상태 LowerLoco가 Base Locomotion 1D BlendTree 공유참조) + `PlayerAnimatorDriver.cs`(웨이트 구동 + 스텝인 억제). **골반 커플링 문제로 유저가 A안 자체를 기각** → B안(v2)으로 완전 대체. v1은 `git show HEAD:...controller` 확인 결과 커밋된 적 없음(작업 트리에서만 존재하다 v2로 덮어써짐).

**H-1 (v1 한정, v2에서 해소 확인됨)**: `KatanaWeapon.OnComboEnd()`(애니이벤트)→`ResetCombo()`→`SetCombo(0)`이 `_comboActive`를 프레임 후반에 끄고, `OnAnimatorMove`가 같은 프레임 그 직후 라이브로 읽어 게이트가 "종료되는 바로 그 프레임"에 풀려 클립 꼬리 루트모션이 새는 레이스. ★이 프로젝트에서 최소 2번째 발생한 "Update↔AnimationEvent↔OnAnimatorMove 같은 프레임 순서" 클래스 — 애니메이션/콤보 리뷰 시 항상 우선 스캔.

**M-1 (v1 한정)**: 대시-캔슬 시 `_comboActive` 즉시 OFF지만 레그오버라이드 웨이트 블렌드-다운(0.12s) 동안 Dash 클립 위에 잔존 러닝 포즈가 섞임.

**검증 기법 노트(재사용 가치 높음, v2에서도 재사용)**: `.mask` 파일의 `m_Mask`는 `AvatarMaskBodyPart` 13그룹(Root=0,Body=1,Head=2,LeftLeg=3,RightLeg=4,LeftArm=5,RightArm=6,LeftFingers=7,RightFingers=8,LeftFootIK=9,RightFootIK=10,LeftHandIK=11,RightHandIK=12) × 8비트=104비트, 각 그룹의 offset+1 위치에 실제 불리언. bash `${s:$((i*8)):8}` 슬라이싱으로 그룹별 추출, `${chunk:1:1}`이 실제 값. 기존에 이미 쓰이는 마스크(UpperBody.mask 등)와 상보 교차검증하면 그룹 인덱스 매핑 자체의 정확성도 검증됨.

---

## v2 (07-04 — 실제 채택안, QA 완료·Critical 0)

**아키텍처**: `KatanaMelee.controller`에 3번째 레이어 "UpperBodyCombo"(mask=`UpperBody.mask`, guid `802696c7d6b1df644ac81db43b54eddd` — **UpperUnequip 레이어와 동일 마스크 재사용**) 신설. 이 레이어의 StateMachine(fileID `-4890113998690559061`, 내부 `m_Name`은 v1 잔재로 "LowerBodyLocomotion"인 채 방치 — 순수 코스메틱, Unity UI에 안 보임, 기능 무관)에 `UB_Loco`(Base Locomotion과 같은 BlendTree fileID `-6802273172499591872` 공유참조) + `Combo1/2/3`(Action 태그) 이관. Base Layer는 `Locomotion` 상태의 `m_Transitions: []`(빈 배열)·AnyState 5개(Dash/Counter/DashAttack/SkillCharge/Skill01, ComboStep 조건 0개) — **콤보 전이가 Base에서 완전히 빠짐**(그림자로도 안 남음, child-state 리스트에도 Combo1/2/3 fileID 없음). `PlayerAnimatorDriver.cs`: `_comboLayer = GetLayerIndex("UpperBodyCombo")`, `_comboWeight`(진입 스냅1/종료 이즈아웃 `comboLayerBlendTime`=0.12f), `_suppressStepIn`(Tick 스냅샷, v1 H-1 픽스 패턴 그대로 계승 — 코멘트에 "Stab H-1" 명시 인용), `IsActionPlaying = LayerHasActionTag(0) || LayerHasActionTag(comboLayer)`.

### ★H-1 — Day2 "전진 스텝인"(직전 커밋 606959b36, Combo1 +0.40m) 완전 소멸 — 미고지 트레이드오프
- `UpperBody.mask` 비트디코딩 확인: Root=OFF, LeftLeg/RightLeg=OFF, LeftFootIK/RightFootIK=OFF, Body/Head/Arms/Fingers/HandIK=ON. Root 마스크 OFF인 레이어는 `Animator.deltaPosition`에 기여 못 함(Unity 표준 동작 — UpperUnequip 레이어가 이미 같은 마스크로 실전 검증된 전례).
- Combo1 클립은 바로 직전 커밋에서 "밀어넣고 멈춤" +0.40m 순변위를 헤드라인으로 추가한 클립(`project_2026_06_28_day2_stationary_cut` 메모리 — 유저 플레이판정 대기 중이던 미확정 기능). v2가 Combo를 Root-제외 레이어로 옮기면서 이 순변위가 **어떤 코드 게이트와도 무관하게 구조적으로 0**이 됨.
- `suppressComboStepIn`(Inspector bool) 자체가 **이제 죽은 노브** — 코드 코멘트가 "이 값과 무관하게 스텝인이 없다"고 자인함. 향후 누군가 "스텝인 되살리려고" 이 토글을 껐다 안 먹히는 걸 보고 삽질할 함정.
- 코드 코멘트에 트레이드오프가 이미 적혀 있어 "숨긴" 건 아니지만, 오케스트레이터가 넘긴 브리프에는 이 트레이드오프가 언급되지 않았음 — **애니메이션 에이전트가 코드 코멘트로만 처리하고 유저 판정으로 안 올라간 결정**. CLAUDE.md §1(트레이드오프 드러내라)·§Animation 헌법(애니가 진실이나 방향 결정은 유저) 관점에서 반드시 유저에게 명시적으로 확인받아야 함 — 완전분리 유지(스텝인 포기) vs 코드 드리븐 전진 펄스로 재도입 vs Root 포함 레이어로 되돌림(골반커플링 재발, v1 기각사유 재소환) 3택.

### M-1 — 콤보 자연종료 직후(0.12s 창) 비-대시 액션 전환 시 레이어 잔상(경미)
- `_comboWeight`/레이어 웨이트를 즉시 0 스냅하는 코드는 `_motor.DashStartedThisFrame` 경로 하나뿐. Counter/Skill01/DashAttack 트리거 함수들(`TriggerCounter/TriggerSkill/TriggerDashAttack`)은 스냅 안 함.
- 실제 리스크는 낮음: `IsActionPlaying`이 layer2의 내부 크로스페이드(Combo_X→UB_Loco, ComboStep==0 조건, 0.12s, `m_HasExitTime:0`) 동안 `GetCurrentAnimatorStateInfo`가 여전히 출발 상태(Action 태그)를 보고하는 Unity 표준 동작 덕에 자동으로 true 유지 → `BeginCharge()`의 `!IsBusy` 게이트가 대부분 막아줌. 다만 `BeginDashAttack()`의 조건식에는 IsBusy 체크가 없어 이론상 좁은 창에서 잔상 가능(육안 임팩트는 낮음 — 종국엔 같은 블렌드트리로 수렴).
- 참고용 픽스안: Dash와 동형으로 Counter/Skill/DashAttack 트리거 함수에도 `_comboWeight=0f` 스냅 추가(일관성).

### L-1 — comboLayerBlendTime(0.12f, Inspector) ↔ 컨트롤러 내부 크로스페이드(0.12s, 3곳 하드코딩: Combo1/2/3→UB_Loco) 매직넘버 미결합
- 두 값이 지금 우연히 일치. **UB_Loco가 Base Locomotion과 같은 BlendTree를 공유참조하기 때문에 desync가 실제로는 무해**(레이어 웨이트가 얼마든 layer2 내용물이 Base와 동일 출력으로 수렴). 단 이 안전성은 "같은 블렌드트리 공유"라는 암묵적 불변식에 의존 — 향후 UB_Loco가 독립 컨텐츠를 갖게 되면 desync가 실제로 보이게 됨. 문서화 권장(코멘트만 추가, 코드 변경 불필요).

### L-2 — StateMachine 내부 이름 잔재
- layer2의 루트 StateMachine(fileID `-4890113998690559061`)의 `m_Name`이 v1 시절 "LowerBodyLocomotion" 그대로 방치. Unity UI에 안 보이는 필드라 기능 무관, 순수 커밋 위생 문제.

### 플레이모드 실측 검증(2026-07-04, 완료) — ★재사용 가치 높은 기법
- `_CombatSlice_ReadAndCut` 씬에서 MCP RunCommand로 EditorApplication.isPlaying=true 후 **EditorApplication.isPaused=true**로 고정, 그 상태에서 `Animator.Update(1f/60f)`를 루프로 직접 호출해(180스텝=3초 시뮬) 실제 Update()/OnAnimatorMove 자동루프와 경합 없이 결정론적으로 스크럽.
- `PlayerAnimatorDriver.SetCombo(1)` 직접 호출(KatanaWeapon 우회) + `AttackHit/ComboWindow/ComboEnd` C# 이벤트에 로컬 카운터 람다 구독 → **hitCount=1, windowCount=1, endCount=1 확인**(정확히 1회씩, 순서대로). Combo1(Action 태그, hash 1043462723) → UB_Loco(태그없음, hash 1280901557)로 정확히 전이, ComboStep 파라미터가 KatanaWeapon의 실제 OnComboEnd 핸들러 체인을 통해 0으로 복귀도 확인(드라이버 단독이 아니라 KatanaWeapon까지 엮인 실제 파이프라인 검증).
- ⚠️함정: 별도 RunCommand 호출에 걸쳐 `Debug.Log`를 람다 안에 심어 크로스-콜로 나중에 GetConsoleLogs로 조회하는 방식은 **신뢬성 없음**(로그가 안 잡힘 — 원인 미상, MCP 동적 컴파일 어셈블리 경계 추정). **동일 RunCommand 실행 안에서 로컬 변수 카운터 + `result.Log`로 마무리**하는 패턴이 유일하게 신뢰 가능. 향후 플레이모드 이벤트/콜백 카운트 검증은 이 패턴(일시정지+수동 Animator.Update 스크럽+단일 실행 내 카운팅) 그대로 재사용할 것.
- 종료 후 `EditorApplication.isPlaying=false`, 씬 dirty 확인 결과 `false` — 오염 없음 확인.

### 안전 확인(v2)
- Base Layer 콤보 전이 완전 제거 확인(코드 검색 + child-state 리스트 대조).
- Dash-캔슬 시 웨이트 스냅 레이스 없음 — `PlayerBrain.Update()`가 `Cancel()`을 `Tick()` 호출들보다 먼저 실행하고, `AnimatorDriver.Tick()` 내부에서도 이징 블록보다 대시 스냅 블록이 나중이라 항상 스냅이 이김(같은 프레임 오버라이트로 승리, 레이스 아님).
- Counter/Skill01/DashAttack(Base 레이어 전신) 구조 무변경 확인(fileID까지 HEAD와 동일) — 분리 영향 없음.
- `LowerBodyCombo.mask` 프로젝트 전체에서 무참조 확인(Grep) — 고아 삭제 안전.

---

## v3 (07-04 — 콤보→걷기 튐 수정: 단일 블렌드, QA 완료)

**변경**: 웨이트 게이트를 `_comboActive?1:0`(진입스냅+종료이즈아웃 `comboLayerBlendTime`) → `baseOwnsUpperBody?0:1`(`LayerHasActionTag(0) || _motor.IsDashing`, 상수 1/즉시스냅, 이즈아웃 없음)로 교체. 복귀 블렌드는 컨트롤러 내부 Combo→UB_Loco 크로스페이드(0.12s) 단일 소유. 죽은 노브 `_comboWeight`/`comboLayerBlendTime` 완전 제거(잔존 참조 없음, 파일 전문 재확인). SM 내부 `m_Name`도 "LowerBodyLocomotion"→"UpperBodyCombo" 정정(L-2 해소). **구 L-1(매직넘버 0.12 이중화) 자동 해소**됨 — 디커플링할 대상(driver 노브) 자체가 없어짐.

### ★486줄 재직렬화 = 진성(genuine) 확인, 은닉 없음
`git show HEAD:...controller`(분리 이전, Base레이어만) vs 현재를 전수 대조:
- Base Layer 7개 상태(Locomotion/Dash/Counter/DashAttack/Skill01Charge/Hold/Strike) fileID·태그·트랜지션 **byte-identical**(예: Skill01Charge→Hold 트랜지션 fileID 6215059085266534145 HEAD/현재 완전 동일 — diff에는 "변경"으로 잡히나 실은 파일 내 위치만 밀림, Unity가 구조 변경 시 무관계 객체에도 새 fileID를 재할당하는 코스메틱 재배치).
- `m_AnimatorParameters:` 블록 HEAD↔현재 **완전 동일**(diff 0줄) — 파라미터 추가/삭제/타입변경 없음.
- 레이어 3개: Base(무변경 wrapper)·UpperUnequip(무변경, 동일 mask guid)·UpperBodyCombo(신규 추가, UpperUnequip과 동일 mask 재사용) — 순수 추가, 기존 레이어 오염 없음.
- 콤보 상태 이관(Combo1/2/3→신규 layer2, ComboStep 조건값 1/2/3 동일하게 포팅, 목적지만 UB_Loco/Combo2/3로 교체)과 구 Locomotion→Combo1 전이(fileID 7138966006479456075)의 정상 삭제(고아 없음) 확인.
- **결론: 486줄 = 진짜 그 세션에 in-memory로 쌓인 v2 구조변경 전체 + 이번 SM이름 수정의 정당한 플러시. 의도 외 변경 0건.**

### 🟠 H-1(신규) — 상하체 위상(phase) 영구 디싱크, 구조적으로 확정(자진신고 확인·격상)
- 메커니즘: `UB_Loco`(layer2)와 Base `Locomotion`(layer0)은 **동일 BlendTree를 참조하는 별개 AnimatorState 인스턴스**라 각자 독립된 normalizedTime을 갖는다. Mecanim은 레이어 weight와 무관하게 모든 레이어의 상태시계를 매 프레임 전진시킨다(weight=0이어도 시간은 흐름 — Sync Layer 기능이 존재하는 이유 자체).
- Combo1/2/3→UB_Loco 3개 전이 전부 `m_TransitionOffset: 0`(위상 0으로 강제 리셋) 확인. 반면 Base `Locomotion`은 `m_Transitions: []`라 **콤보가 진행되는 동안 한 번도 리셋되지 않고 계속 흐른다**(콤보 직전까지 임의 위치까지 드리프트).
- → 이동 중 콤보 1회만 나가도, 콤보 종료 후 상체(UB_Loco, 위상 0 리스타트)와 하체(Base Locomotion, 임의 위상)가 어긋난 채 **영구 고정**(같은 주기로 같이 도니 수렴 안 됨, 다음 콤보가 재랜덤할 뿐).
- **v2(이전)보다 v3(이번 수정)에서 체감 악화**: 이전엔 콤보 종료 후 레이어 웨이트가 0으로 이즈아웃 → 0.12s 지나면 Base 단독 소스(디싱크 원천봉쇄, 부분블렌드 창만 튐). v3는 웨이트 상수 1 고정이라 디싱크가 **매 콤보마다 전체 가중치로 무기한 지속**.
- 판정: **코드/구조 결함**이지 순수 미적 판단 아님(Sync Layer 부재 + 정확히 이 클래스의 알려진 Mecanim 함정). 상태명 불일치(Locomotion≠UB_Loco)라 Unity 내장 "Sync Layer" 체크박스는 그대로 안 먹음(이름 매칭 요구).
- 컨티전시 평가: ①StateMachineBehaviour.OnStateEnter+`Animator.Play`는 하드컷이라 이번 수정이 만든 0.12s 크로스페이드를 도로 깨뜨림(비추천) ②`Animator.CrossFade(hash, 0.12f, layer, normalizedTime: baseNormalizedTime, normalizedTransitionTime:0)` 오버로드로 위상 지정 크로스페이드가 정답 — Combo1/2/3 종료 트리거 시점(코드 or StateMachineBehaviour.OnStateExit) 어느 쪽에서 호출해도 블렌드 유지+위상정합 동시 달성 가능. 최종 "실제로 거슬리나"는 유저 라이브 플레이 판정.

### 🟡 M-1(신규) — `_comboLayer` 룩업 실패 시 무음 강등(Awake, PlayerAnimatorDriver.cs:94)
- `_comboLayer = _animator.GetLayerIndex("UpperBodyCombo")`에는 `_motor`/`_aim` 널 체크(L102-106, "배선 실패를 무음으로 두지 않는다" 명시)와 달리 실패 시(-1) 로그가 없다. 레이어 리네임/오탈자 시 `if (_comboLayer >= 0)` 가드가 조용히 웨이트 세팅을 건너뛰어 **콤보 내내 상체가 안 움직이는(Editor 기본값 weight=0 고착) 채로 무경고 배포** 가능 — 같은 파일 안에서 스스로 세운 정책과 비대칭.
- 현재는 이름이 정확히 일치해 발화하지 않음(발견일 뿐, 지금 당장 터진 버그 아님).
- 수정안: `if (_comboLayer < 0) Debug.LogError("[PlayerAnimatorDriver] UpperBodyCombo 레이어를 못 찾음 — 상체 콤보 오버라이드가 비활성.", this);` 한 줄 추가.

### 이전 H-1(Day2 스텝인 소멸) 상태 — 재확인만, 재-플래그 아님
- v3에서도 여전히 열려 있음(유저 미판정, `suppressComboStepIn` 죽은 노브 그대로). 이번 diff의 범위(웨이트 게이트 교체) 밖이라 신규 이슈로 세지 않음 — 기존 H-1 유효 지속.

---

## v4 / "Fix B" (07-04 — v3의 H-1 위상 디싱크 해결 시도, QA 완료)

**변경**: v3의 상수-웨이트1 방식을 폐기하고 웨이트 3분기(`baseOwnsUpperBody→0즉시` / `_comboActive→1즉시` / `else→MoveTowards 이즈아웃 comboLayerBlendTime`)로 교체 + **컨트롤러에서 `Combo1/2/3→UB_Loco` 전이 3개를 전부 제거**(콤보가 끝나면 그 상태의 마지막 프레임을 영구 홀드 — 클럭 없는 정적 포즈 → 웨이트 이즈아웃이 이 정적 포즈를 Base 로코모션으로 단일 블렌드, 그래서 위상 무관). `ANY→Combo1` 조건을 `ComboStep==1`에서 소비형 `Attack` 트리거(+`CanTransitionToSelf=1`, dur=0 CUT)로 교체(홀드된 Combo에서 재시작 가능하려면 self-transition 필요, int조건+selfTransition 조합은 매프레임 재발화 위험이라 트리거가 정답). `IsActionPlaying`의 콤보분기를 `LayerHasActionTag(_comboLayer)`(태그만)에서 `ComboLayerActivelyPlaying()`(태그 && normalizedTime<1, 즉 "실제 재생 중"만)로 교체 — 안 그러면 홀드가 태그를 영구 유지해 busy가 영구 true가 됨.

### 위상 디싱크(v3 H-1) 자체는 해결됨 — 설계 타당성 확인
정적 홀드 포즈를 웨이트로 Base에 단일 블렌드하는 접근은 "두 개의 독립 시계가 동시에 weight>0" 상황을 구조적으로 없앤다. Combo1/2/3→UB_Loco 전이 제거, ANY→Combo1 트리거 재진입(모든 홀드 상태·self 포함해서 발화 확인), normalizedTime 오버슈트를 이용한 busy 게이트, 전부 컨트롤러 YAML 직접 대조로 검증 완료. 이 부분은 Critical/High 0.

### 🔴 P0(신규) — 대시-캔슬 시 이동 영구(수백ms) 프리즈, 회귀
- **메커니즘**: `Combo1/2/3→UB_Loco` 제거로 콤보 레이어의 유일한 탈출구가 `AnyState(Attack)`뿐이 됨(재시작 전용, 캔슬용 아님). `KatanaWeapon.Cancel()`(대시 최우선 캔슬 경로, `PlayerBrain.cs` dashDown 분기에서 호출)은 `SetCombo(0)`으로 `ComboStepHash=0`·`_comboActive=false`만 세팅 — 이제 이 파라미터를 듣는 전이가 없어서 **애니메이터의 콤보 레이어가 캔슬된 클립을 자연 종료까지 무음으로 계속 재생**한다(웨이트는 `baseOwnsUpperBody`로 즉시 0이라 안 보임 — 순수 게임로직 버그, 시각 단서 없음).
- `PlayerAnimatorDriver.ComboLayerActivelyPlaying()`이 `_comboActive`(C# 진실, Cancel 즉시 false)를 안 보고 순수 `normalizedTime<1`만 보므로, **캔슬 시점의 normalizedTime이 낮을수록(스윙 초반 캔슬일수록) `IsActionPlaying`이 그만큼 오래 true로 남는다** → `IsBusy` true 지속 → `PlayerMotor.Tick()`의 `locked` 분기가 `_velocity=Vector3.zero`를 매 프레임 실행 → 대시가 끝난 직후부터 고아 클립이 자연 종료될 때까지(최악 거의 클립 전체 길이) **플레이어가 완전히 멈춰서 움직일 수 없다**.
- 비대칭 확인: Base 레이어(Counter/Skill01Strike/DashAttack)는 `AnyState→Dash`(Dash bool, 무조건 최우선)로 대시 시 즉시 강제 이탈 — 이 메커니즘은 그대로 살아있고 정상 작동. **콤보 레이어만 이 대응 전이가 없다** — v3까지 있던 `ComboStep==0`이 이 역할을 겸했는데 이번에 삭제되면서 대체 없이 구멍남.
- 자가치유(self-heal, KatanaWeapon.OnTick) 무력: 그 트리거 조건이 `!IsBusy && flags>0`(under-lock만 감지)라 이 시나리오(IsBusy가 stuck true인데 flags는 이미 전부 정상 0)를 구조적으로 못 잡음.
- **재현**: 카타나 장착 → 좌클릭(Combo1 시작) → 스윙 초반(윈도우 열리기 훨씬 전)에 Space(대시) → 대시는 정상 실행 → 대시 종료 직후 WASD 시도 → 캔슬 시점이 이를수록 길게(체감 수백ms) 조작 불능.
- **심각도가 높은 이유**: 대시-캔슬은 이 무기의 정식 캐넌(`feedback_player_self_cancel_canon`, "회피 최우선") — 희귀 엣지케이스가 아니라 매 전투마다 반복되는 핵심 경로. 회피 직후 조작 불능은 회피의 존재 이유(위험 회피)를 정면으로 배신.
- **픽스(적용 권장, 최소 1줄)**: `PlayerAnimatorDriver.ComboLayerActivelyPlaying()` 맨 앞에 `if (!_comboActive) return false;` 추가. `_comboActive`는 `BeginCombo/Advance`에서 애니 트리거보다 먼저 동기 true가 되므로(진입갭은 `_actionGrace`가 별도 커버) 새 문제 유발 없음, `OnComboEnd`(자연종료)·`Cancel()`(캔슬) 양쪽 다 `SetCombo(0)`으로 즉시 false가 되므로 두 경로 다 즉시 반영. **부수효과로 `moveCancelEnabled`(현재 `comboMoveEnabled=true`라 비활성인 이동-셀프캔슬 A/B 경로)의 동일 잠재버그도 같이 닫힌다** — 그 경로 주석("SetCombo(0)→Combo→Loco 전이=기존 복귀 경로")은 이제 stale(그 전이가 없음), 같이 정정 권장.
- 대안(비권장, 참고용): 컨트롤러에 Base 레이어와 대칭으로 `UpperBodyCombo`에도 `AnyState→UB_Loco`(Dash bool 조건) 전이를 추가하는 방법도 가능하나, Cancel()이 대시 외 사유(향후 스턴/사망 등)로도 호출될 수 있어 C#쪽 단일 게이트가 더 일반적이고 우선.
- 상태: **오케스트레이터에 보고, 미수정**(리뷰만 수행, Stab은 코드 수정 권한 없음 — 이 리뷰가 끝난 시점 기준 회귀 상태로 존재).
