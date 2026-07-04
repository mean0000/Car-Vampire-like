---
name: katana-combo-retimer
description: Re-runnable Editor script that NON-UNIFORMLY retimes humanoid muscle clips by physically resampling all curves + remapping events. ★현행=2026-07-04 3세그 스트라이크 스냅(윈드업1.25/스트라이크2.2/회수1.4·피니셔1.0/lead0.07)으로 Combo1/2/3 전부 재프로파일. 이벤트는 소스 미의존 상수 저작. (구=2026-06-21 2세그 windup1.5).
metadata:
  type: project
---

# ★★2026-07-04 3세그 스트라이크 스냅 개편 (현행 — 유저 "베는 순간 확 빠르게")
유저 요구=①전체 속도↑ ②베는 순간 스냅(윈드업 보통·스트라이크 확 빠르게·회수 보통). 브루트 상태-분절 대신 리타임 채택(분절=콤보 분기 구조라 배선 그래프 폭발, 상세 [[project_katana_combo_strike_snap_assessment]]).
- **3세그/콤보**: Windup[0,hit−lead]×1.25 · **Strike[hit−lead,window]×2.2(★스냅)** · Recovery[window,end]×1.4(C1·2)/1.0(C3 피니셔 무게). lead=0.07s(컨택 직전 휘두름을 스냅 창에 브래킷). 노브 전부 const 단일 진실원.
- **★이벤트=소스 미의존 상수 저작**(WriteRetimed가 authoredEvents 받아 map remap): C1/C3 소스 FBX 이벤트 0개라 FindEventTime throw + Combo가 이벤트없이 구워지면 소프트락([[project_katana_combo2_event_gap]]). 원본 FBX 노름 상수(ComboDef)에서 3개 저작→항상 정확히 3개 remap. norm=C1 0.367/0.484/0.920·C2 0.200/0.344/0.910·C3 0.206/0.318/0.920.
- **Combo2도 이제 retimed .anim**(guid fda78cae 신규) — 별도 메뉴 "Repoint Combo2 Motion"으로 상태 m_Motion 1회 물림(에디터 API). 재튜닝(속도 const 변경+Retime 재실행)은 컨트롤러 무변(Combo2 in-place 덮어씀 guid 보존).
- 결과: C1 0.878→0.691·C2 1.133→0.762·C3 1.130→0.918s. guid C1/C3 보존. 배선 100% 보존(라이브 실측). ★재-bake로 C1 +0.40m 스텝인 소실(dormant/masked라 무영향). ★repoint SaveAssets가 pre-Fix-B HEAD에 in-memory Fix B 플러시(diff 488줄, 양성). 손맛=유저 게이트.

---

# (구) 카타나 콤보 비균일 리타이밍 (2026-06-21) — 아래는 2세그 시절 기록(메커니즘 동일, 세그/노브만 위로 대체)

유저 요구: 전체 속도(uniform m_Speed)가 아니라 **구간별** — Combo1 윈드업만 빠르게, Combo3 회수만 빠르게, 타격+캔슬창 가독은 평속 보존. 굼뜬 불쾌함 제거.

## ★메커니즘 = 클립 물리 리샘플 (나·Codex 독립 수렴 #1안)
Unity는 클립에 "시간별 속도 커브"가 없다. 비균일 리타이밍의 유일한 정답 = **모든 커브 키타임을 piecewise time-map T()로 물리 재배치**해 새 .anim을 굽는 것. 대안들 기각:
- 상태 m_Speed = uniform만(불합격).
- 상태 분할(윈드업/스트라이크/회수 각 다른 speed) = ★루트모션 이중적용 위험(콤보는 BakeXZ OFF로 전진 보존, OnAnimatorMove가 `_attacking`중 적용 — 상태 쪼개면 구간별 속도로 런지 느낌 깨지고 CUT 이음새 늚). 헌법 "한 동작=한 상태"에도 어긋남. → 단일 리샘플 클립이 정답.
- firstFrame/lastFrame 트림 = 워프 못함(자름만), 하드 컷 팝 위험.

## 스크립트 = `Assets/_Project/Scripts/Editor/KatanaComboRetimer.cs` (재실행 가능)
메뉴 `ZombieCrush/Animation/Retime Katana Combo1+Combo3`. **상수 2개만 만지고 메뉴 재실행 → .anim 덮어씀(guid 보존, in-place EditorUtility.CopySerialized). FBX·Animator 배선 안 건드림.**
- `Combo1_WindupSpeed` (현재 1.5) = 시작→OnAttackHit 구간 가속.
- `Combo3_RecoverySpeed` (현재 1.5) = OnComboWindow→끝 구간 가속.
- 경계는 클립 이벤트에서 읽음(OnAttackHit/OnComboWindow) — 하드코딩 norm 아님.

### 핵심 구현 디테일 (재현용)
- 소스 = FBX 서브클립(읽기전용) `AssetDatabase.LoadAllAssetsAtPath` → `humanMotion=True` 클립(137 float bindings = 머슬/IK/루트, objectRef 0).
- 커브: `GetCurveBindings`→`GetEditorCurve`→경계에 앵커 키 삽입(`AddKey`, 보간값)→키타임 `Map()`, **탄젠트 in/out ×구간속도(체인룰: 시간 ×1/s 압축 ⇒ slope ×s)**, weight 불변→`SetEditorCurve`.
- 설정: `Get/SetAnimationClipSettings`로 humanoid 메타(loop 등) 복사 → humanMotion 보존됨(검증 True).
- 이벤트: `GetAnimationEvents`→각 .time에 같은 `Map()` 적용→`SetAnimationEvents`. intParameter/messageOptions 전부 보존.
- `dst.EnsureQuaternionContinuity()` 후 CreateAsset 또는 CopySerialized(기존 덮어쓰기).
- ★type: .anim 메인에셋 참조는 컨트롤러 YAML서 `fileID: 7400000, type: 2`(FBX 서브클립은 1827226128182048838/type 3과 다름).

## 첫 패스 결과 (1.5×, 디스크 검증)
- **Combo1_Retimed** guid `3291e7ea318ce084893c6f7ed7b5fdca`: 1.0s→**0.8777s**. Hit 0.367→**0.245s**(빨라짐), Window 0.362s(★hit→window 갭 0.117s = 원본과 동일=평속 보존), End 0.798s(norm0.909).
- **Combo3_Retimed** guid `702d382967fb89b42a3eb108fabfdebd`: 1.05s→**0.8113s**. Hit **0.216s**(원본과 동일=평속), Window **0.334s**(원본과 동일=평속), End 0.755s(norm0.931, 빨라짐).
- 둘 다 137 bindings·3 events·humanMotion=True·isLooping=False·에러0.

## 코드 안전성 (KatanaWeapon.cs는 오케스트레이터 소유, 미터치)
이벤트 구동(시간 안읽음)이라 리타이밍 안전. 단 2개 소프트상수 확인됨:
- `inputBufferTime=0.5s` — Combo1 윈드업 빨라지면 OnComboWindow가 **앞당겨짐**(버퍼 여유 ↑) = 안전.
- `Time.time-_lastAdvanceTime<0.1f` stale-end 가드 — OnComboEnd가 상태진입 0.1s 후보다 한참 뒤여야. 둘 다 0.75~0.80s = 안전.
- **코드 변경 불필요** — 클립/컨트롤러만으로 완결.

## 미검증(유저 빌드 게이트)
실제 손맛(굼뜸 제거됐나, 1.5×가 과한가/모자란가)은 플레이로만 확정. 재튜닝 = 위 상수 바꿔 메뉴 재실행.

## ★2026-06-28 DD식 전진 스텝인 설계조사 (설계만, 미구현 — 유저가 변수1=후딜제거 코드 먼저)
유저 요구: Combo1(기본 베기)에 Death's Door식 **모던/절제 전진 스텝인**(~0.3~0.6m). 실측·검증 결과:
- ★**리타이머는 순수 time-warp** — translation(전진) 저작 능력 0. BakeXZ는 *기존* XZ 보존일 뿐 *생성* 불가. Combo1 소스는 net 전진 0이라 어떤 bake 플래그도 전진 못 만든다. → 유저옵션 Ⓐ("BakeXZ로 전진저작") 불가.
- ★**측정(avgSpeed×len = net 전진, SampleAnimation 프로파일=궤적):** S1_Combo01_01(현 Combo1)=net **0m** (RootT.z가 0.046→0.123(norm.42)→0.046 = 앞으로 0.16m world 기울었다 *복귀*, foot은 안 옮김). **S1_Attack01(유저 후보Ⓑ)=net 0m=완전 제자리**(가설 반증!). S1_Attack02=**0.41m** 전방프론트로드(strike@n.32서 1.16m peak→리커버리 0.41m로 settle=DD형이나 *내려찍기 chop*·다른 스윙). S2_Attack02=0.53m 동형. S1_Combo01_03(Combo3 런지)=**1.34m**, ★**백로드**(norm.58→1.0서 +0.6m 계속 슬라이드=유저혐오 "둥둥 미끄럼"). Combo2=0.05m.
- ★~~전진 저작 레버 = RootT.z~~ **(06-29 측정으로 반증·정정)**: 권장값은 그대로(norm0→hit +0.4m, 회수 flat hold=DD 플랜트, 스윙 포즈 불변·루트만 이동) — 단 **레버는 RootT.z가 아니라 MotionT.z다**(아래 §06-29 참조). 정적 커브로 "RootT.z가 forward 같다" 본 게 함정이었음([[measure-rootmotion-by-stepping]] 캐넌 적중).

## ★2026-06-29 전진 스텝인 구현 완료 (phase2, 측정검증·미커밋)
- ★★**휴머노이드 루트모션 forward 드라이버 = `MotionT.z`이지 `RootT.z`가 아니다**(SampleAnimation 측정으로 결정). 검증법: clip 클론에 +0.4 ramp을 RootT.z에 ADD→SampleAnimation net=**0.000(무반응)**; 같은 ramp을 MotionT.z에 ADD→net=**0.4282**. RootT.z는 이 클립서 루트모션에 **inert**(reference/IK 추정). 06-28 핸드오프·구메모리의 "RootT.z가 0.046→0.123→0.046로 전진구동" = 정적커브 오독. **편집·측정 항상 MotionT.z**.
- 측정도구 = `clip.SampleAnimation(go, t)` (go=Frank_Stealth_Kill_Skin.fbx 인스턴스, avatar=human). SampleAnimation transform.position.z = 런타임 deltaPosition 적분과 등가(=applyRootMotion이 실제 적용할 값). ★에디트모드 `Animator.Update`는 휴머노이드 루트모션을 transform에 미적용이라 SampleAnimation이 정답. `averageSpeed=(0,0,0)`도 net0 교차확인.
- **저작 = MotionT.z 커브 REPLACE**(ADD 아님!). ADD하면 기존 bump-return이 비쳐 hit후 0.586→0.428로 **뒤로 드리프트=recoil**(유저 금지). REPLACE= v0(0.0099 보존)→ A*smoothstep([0,hitT]) →hitT후 flat hold(v0+A). 62키 time그리드·loop설정 보존, SmoothTangents.
- **calibration**: raw amplitude A와 SampleAnimation net은 ~k=1.0705 배(아바타 projection). 목표 net 0.40 → **A=0.3736**(raw). 1패스 측정→k계산→A=0.40/k 2패스로 정확히 landing.
- **결과(디스크 reload 검증)**: net forward **0.000→0.4000m**. 프로파일 0→0.388@n0.25→**0.400@n0.30(hit norm0.279서 적재완료)**→n1.00까지 flat 0.400(recoil 0). len 0.8777·humanMotion True·loop False·**137바인딩 불변**·MotionT.x/y·RootT.z 전부 미변경(surgical). **이벤트 3개 norm 불변**(OnAttackHit 0.2788·OnComboWindow 0.4121·OnComboEnd 0.9088). **guid 3291e7ea 불변**(in-place SetEditorCurve+SaveAssets, 메인 fileID 7400000 type2). 콘솔 에러 0.
- 노브: A=0.3736(=net0.40). 더/덜 전진은 A 비례조정(net목표×0.934). hitT=0.2447(OnAttackHit 시간)서 적재완료. **잔여리스크=foot-slide**(클립이 발 안 옮김, 0.4m/~0.16s 스텝) — 정지 더미 베기 손맛은 **유저 플레이 게이트**. ★구코드 무변경(애니가 진실, OnAnimatorMove가 deltaPosition 자동적용).
- ★**phase2 차단요인(검증):** Combo1·Combo3 **소스 FBX 이벤트 0개** = 리타이머 현재 재실행 시 `FindEventTime("OnAttackHit")` throw. 이벤트는 retimed .anim에만 생존(C1: hit n0.279/0.245s·window n0.412·end n0.909, len0.878). → 재실행 전 ①소스 FBX 이벤트 복원(meta) 또는 ②경계를 .anim/상수서 읽도록 리타이머 수정, 또는 ③소스 우회=retimed .anim 직접편집(이벤트·리타이밍 보존, guid 3291e7ea 불변 = 추천). 컨트롤러 Combo1 m_Motion = KatanaMelee.controller **line 609** guid 3291e7ea type2 / Combo3 = line 1081 guid 702d3829.
- ★**롤백:** _Project/Animations/ (복수) 의 .anim·.controller = **git-untracked**(git status의 Animation 단수 삭제는 구경로 reorg). git 안전망 없음 → 신기능 regen 전 .anim 수동 백업복사 필수·swap이면 원guid(3291e7ea) 기록. 소스 FBX는 tracked·미편집.
- **Combo1 회수단축 다이얼(Q3, 별개변수):** 리타이머에 `Combo1_RecoverySpeed` 없음(현 Combo1=Seg(0,hit,1.5)+Seg(hit,end,1.0), 회수 평속). Combo3_RecoverySpeed가 정확히 동형 선례 → Combo1도 3세그(…+Seg(window,end,RecoverySpeed>1)) 추가로 회수클립 자체 가속 가능(권장 ~1.3-1.5). 단 코드 self-cancel(오케스트레이터 변수1)과 *스택*되니 그것부터 체감 후 튜닝(이중 단축 주의).

[[project_vexa_humanoid_katana_base]] [[project_frank_fbx_animevent_gotchas]] [[feedback_player_self_cancel_canon]] [[project_katana_combo2_event_gap]]
