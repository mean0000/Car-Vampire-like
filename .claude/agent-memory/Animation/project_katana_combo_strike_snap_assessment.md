---
name: katana-combo-strike-snap-assessment
description: 평타(콤보 3타) "베는 순간 스냅" 요구의 방식 판정 — 상태 분절(브루트식)은 배선 그래프 폭발(9상태·체이닝 웹·이벤트 재저작·busy/Attack진입 재검증)로 안정화 컨트롤러 위협 → 리타임(KatanaComboRetimer 3세그) 권장. 현 콤보 이벤트/속도 ground-truth 표 포함. 2026-07-04, 유저 방식 판정 대기.
metadata:
  type: project
---

# 카타나 평타 "베는 순간 스냅" — 분절 vs 리타임 방식 판정 (2026-07-04)

유저 요구: 평타 콤보 3타 ①전체 속도↑ ②★베는 순간 스냅(비균일 — 윈드업 보통·스트라이크 확 빠르게·회수 보통). 브루트(CrassorridBrawler) 상태-속도-분절 이식을 지시받음. 유저가 '이벤트로?' 물었으나 이벤트=속도제어 부적합(animator.speed 전역·계단 불연속)이라 분절 선택 — 단 **리타임 옵션은 그 선택 시점에 테이블에 없었음**(이벤트 vs 분절만 비교됨).

## ★현 콤보 ground-truth (디스크 YAML/meta 직접 실측 — 컨트롤러 KatanaMelee layer2 UpperBodyCombo)
| Combo | 클립(상태) | 길이 | Hit(norm) | Window(norm) | End(norm) | 현 속도 프로파일 |
|---|---|---|---|---|---|---|
| 1 | `S1_Combo01_01_Retimed.anim` (guid 3291e7ea t2, line613) | 0.878s | 0.245s(0.279) | 0.362s(0.412) | 0.798s(0.909) | 윈드업[0,hit]×**1.5**·[hit,end]×1.0 (+MotionT.z 0.40m 스텝인 baked, 단 Root마스크로 런타임 억제) |
| 2 | FBX subclip `Frank_RPG_Katana_S1_Combo01_02` (guid ebd5d44d t3, line354) | 1.133s(f0~68@60) | 0.227s(0.200) | 0.390s(0.344) | 1.031s(0.910) | **uniform ×1.0 — 리타임 안 됨**(이벤트만 meta로 추가, [[project_katana_combo2_event_gap]]) |
| 3 | `S1_Combo01_03_Retimed.anim` (guid 702d3829 t2, line1317) | 1.130s | 0.216s(0.192) | 0.334s(0.296) | 1.036s(0.917) | [0,window]×1.0·[window,end]×**0.9**(피니셔 무게, 대시로만 캔슬) |
- ★현 프로파일이 요구와 정반대: Combo1은 *윈드업*이 빠르고 스트라이크 보통. Combo2 완전 균일. Combo3 회수 느림. → 재프로파일 필요.

## ★판정 = 리타임 권장(분절이 배선 위협 — 폴백 플래그 발동), 유저 최종 결정
### 분절(유저 최초 선택)이 위협하는 지점 — "깨거나 심하게 복잡" 5/5 적중
- **상태 3→9**(콤보당 Windup/Strike/Recovery). 브루트는 단발 선형(체이닝 없음)이라 분절이 쉬웠지만 콤보는 *분기*(캔슬창+버퍼로 다음 단 Advance 또는 OnComboEnd 종료)라 분할이 분기점을 곱한다.
- **체이닝 웹**: Advance가 ComboStep=N 세팅 → ComboN_Windup 전이는 Combo(N-1)의 *어느 서브상태가 활성이냐*(Strike or Recovery, 창은 둘 중 열림)에서 나와야 함 → 체인 엣지 2배 + AnyState 재진입 재설계.
- **이벤트 재저작**: 서브클립 9개에 이벤트 분배(Hit=Strike·Window=Strike/Recovery·End=Recovery끝). ★현 콤보는 2 retimed .anim + 1 FBX subclip — .anim은 프레임범위 임포터 슬라이스 불가(Crassorrid FBX-take 분할법 안 통함) → 커브 잘라 9클립 신규 저작 필요.
- **busy**(ComboLayerActivelyPlaying)는 Fix B "단일상태 홀드" 가정 — 9상태 전역 재검증.
- **Attack 재진입**(ANY→Combo1 canTransitionToSelf, 소비형): 홀드 대상이 Combo1_Recovery로 바뀌어 self→cross 의미 변경, 재검증.
- 부분분절(Combo1만)=1/3만 스냅 = [[project_katana_combo2_event_gap]] 재발(N단 균일처리 원칙 위반). 기각.
- 단일상태 state.speed = *균일*만 → intra-clip 스냅 불가(리타이머 메모리서 이미 "불합격"). 기각.

### 리타임(권장) = 동일 결과·배선 무손
- `KatanaComboRetimer.cs`(재실행 가능, [[project_katana_combo_retimer]])의 세그 모델만 3세그로 바꿔 메뉴 재실행. 스냅이 *모션에 구워짐*(키프레임 물리 압축) = state.speed 컷 경계 속도 불연속보다 정직·이벤트는 remap돼 모션순간에 핀 고정.
- **컨트롤러 무변**: 전이·체이닝·busy·Attack진입·위상·이벤트 개수(remap, 클립당 3개 그대로) 전부 불변. 유일 예외 = Combo2를 retimed .anim으로 만들면 그 상태 m_Motion 1회 repoint(에디터 API, 하드YAML 금지).
- 원 설계 결정이 이미 분절 기각→리샘플 채택했던 바로 그 근거(루트모션 이중적용·CUT 이음새·"한 동작=한 상태").

## 제안 3세그 리타임 모델(권장 시작값 — 유저 손맛 튜닝)
경계(이벤트 파생, lead만 상수): **Windup [0, hit−lead]** ×WindupSpeed · **Strike [hit−lead, window]** ×StrikeSpeed(HIGH=스냅) · **Recovery [window, end]** ×RecoverySpeed.
- `lead ≈ 0.07s` — 스트라이크가 컨택 직전 휘두름-진입부터 포함(컨택을 fast 창 안에 브래킷).
- `WindupSpeed 1.25` — 앵티시페이션 약간 빠르되 읽힘(전체속도 bump). 너무 빠르면 스냅 팔던 예비동작 소멸.
- `StrikeSpeed 2.2` — 헤드라인 스냅. 윈드업 1.25 대비 1.76× 가속 대비 = "팍". 카타나=경량이라 브루트(0.5→1.25)보다 두 베이스 다 높음.
- `RecoverySpeed 1.4`(Combo1/2) — brisk 회수(Day2 후딜제거 방향). `RecoverySpeed_Finisher 1.0`(Combo3, 현 0.9 무게 상대보존).
- 추정 결과(faster+snap): C1 0.878→~0.59s·C2 1.133→~0.76s·C3 1.130→~1.00s(피니셔 무게 유지). Hit는 remap돼 더 이르게 착지.

## ★구현 완료 (2026-07-04, 유저 리타임 승인 후) — 디스크 실측 검증
- **리타이머 개편**(`KatanaComboRetimer.cs`): 2세그 2콤보 → **3세그 3콤보**. 이벤트를 소스 미의존 *상수 저작*(Combo1/3 소스 FBX 이벤트 0개 블로커 회피 + Combo2 이벤트갭 소프트락 원천 차단). 노브=WindupSpeed1.25/StrikeSpeed2.2/RecoverySpeed1.4/FinisherRecoverySpeed1.0/StrikeLead0.07(전부 const 단일 진실원). 메뉴 2개: "Retime Katana Combos (3-seg strike snap)"(클립) + "Repoint Combo2 Motion ..."(1회).
- **원본 FBX 이벤트 노름(확정, 역-remap 교차검증)**: C1(len1.0) hit0.367/win0.484/end0.920 · C2(len1.133) hit0.200/win0.344/end0.910 · C3(len1.05) hit0.206/win0.318/end0.920. norm×clip.length로 abs화(길이 견고).
- **결과(디스크 실측, 예측과 4자리 일치)**: C1 0.878→**0.691s**(hit n0.390·win n0.467·end n0.917) · C2 1.133→**0.762s**(hit n0.206·win n0.303·end n0.905, ★신규 .anim guid fda78cae) · C3 1.130→**0.918s**(hit n0.162·win n0.220·end n0.908). 3클립 전부 humanMotion=True·loop=False·**이벤트 정확히 3개**(OnAttackHit int1/2/3·Window/End int0·msgOpt1). C1 guid3291e7ea·C3 guid702d3829 **보존**(in-place). 콘솔 에러0.
- **배선 보존 라이브 실측**: ANY→Combo1 Attack트리거 canTransitionToSelf=1 dur0 · Combo1→2[==2]·2→3[==3] CUT dur0 · Combo3 홀드(무출구) · Combo2 motion=fda78cae(repoint✓). busy/대시캔슬/위상 경로=코드·구조 무변(리타임은 클립만 교체)이라 논리 보존, 런타임 발화는 유저 플레이.
- **★★컨트롤러 diff 488줄 = SaveAssets 플러시 함정**: HEAD(커밋본)이 **pre-Fix-B**였다(ANY→Combo1 ComboStep==1·dur0.15 UB_Loco 전이 3개·25전이/12상태). RepointCombo2의 SaveAssets가 **미저장 in-memory Fix B**(Attack트리거·UB_Loco전이 제거·21전이/13상태)를 디스크로 플러시. 내 순수 변경=Combo2 m_Motion 1개(ebd5d44d t3→fda78cae t2)뿐, 나머지=Fix B 플러시(불가피=하드YAML금지→에디터API→SaveAssets가 dirty 전량 플러시, [[project_player_lowerbody_loco_override]] §재발방지 함정). 라이브 전이표=Fix B+repoint 정합(양성). ★커밋 시 diff가 Fix B를 번들 — 오케 인지 필요.
- **★Combo1 +0.40m 스텝인 소실**(재-bake 정의상): 원본 FBX엔 스텝인 없음(그건 post-retime MotionT.z 편집이었음). 현 아키텍처(콤보=UpperBody 마스크 Root제외 + OnAnimatorMove _suppressStepIn)선 이미 **dormant/masked**라 관측 변화 0. 콤보를 Root포함 레이어로 되돌릴 때만 phase2 스텝인 재적용 필요.
- **코드 무변경**: KatanaWeapon/PlayerAnimatorDriver 안 건드림(이벤트=이름 기반, 클립 drop-in).

## 미검증 (유저 플레이 게이트)
스냅 실체감("팍" 읽히나)·전체속도 2.2 적정(과한가/모자란가)·피니셔 무게·런타임 이벤트 발화(히트나나·캔슬창 2단가나·종료/busy해제·대시캔슬 이동 안얼어붙나 — 구조·논리 PASS, MCP 플레이 신뢰낮아 런타임은 유저). **Stab+Codex 게이트 오케가 띄움.**

[[project_katana_combo_retimer]] [[project_player_lowerbody_loco_override]] [[project_katana_combo2_event_gap]] [[project_crassorrid_clip_kit]] [[project_telegraph_driver_crassorrid]]
