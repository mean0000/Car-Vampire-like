---
name: project-telegraph-driver-venosaur
description: Venosaur 근접 상태머신·드라이버(VenosaurBrawler) — Dimax 클로월 틀 직재활용 + 묵직 무게 램프 + ★per-hand 전진게인(L/R 비대칭 보존). 신규 6종 첫 타자(양산 틀 검증). 구현 완료·라이브 검증 보류.
metadata:
  type: project
---

신규 6종 중 **첫 타자** = "묵직 브루저 클로월"(Caniathrox 돌진·Venodonte 원거리·Dimax 슬렌더 클로·Crassorrid 스매시에 이은 5번째 활용). **틀 자체는 Dimax 클로월과 동일**(Idle→Roar→L_Windup→…Recovery→반대손 Windup 직행, 끊임없는 좌우). 신규 = 클립 교체 + 무게 노브 + **★L/R 전진 비대칭 처리(per-hand gain)**.

## 만든 파일 (미커밋 — 오케스트레이터 리뷰/Stab+Codex 대기)
- `Assets/_Project/Scripts/VenosaurBrawler.cs` — 근접 AI(DimaxillosaurusBrawler 직클론). ★per-hand `LeftAdvanceGain`/`RightAdvanceGain`(단일 AdvanceGain 아님 — 비대칭 때문). AnimationEvent 콜백 ClawHit.
- `Assets/_Project/Scripts/VenosaurLabSpawner.cs` — 랩 스포너(DimaxLabSpawner 클론, 프리팹/컨트롤러 경로만 교체).
- `Assets/_Project/Scripts/Editor/VenosaurLabSetup.cs` — 클립복제·4분할·ClawHit주입·머티리얼·컨트롤러 빌드(DimaxLabSetup 클론, 30f 경계 재유도).
- `Assets/_Project/Scripts/Editor/VenosaurLabCapture.cs` — 씬빌드+캡처(DimaxLabCapture 클론, 컨택 norm창 0.45~0.55=norm0.5).
- `Assets/_Project/Animations/VenosaurBrawler.controller`(10상태 v1, 디스크 영속화 검증) + `VenosaurRM/Venosaur@ClawsAttackLeft/RightForward_RM.fbx`(4분할 사본 2개).
- 씬 `Assets/_Project/Scenes/Greybox_VenosaurLab.unity`(저장됨, ▶플레이 가능).

## ★상태머신 v1 (Dimax v8 "끊임없는 좌우" 라우팅 그대로 계승 — 디스크 검증 완료)
- **10상태**: Idle/Roar + L/R × {Windup/Strike/FollowOut/Recovery}. **3파라미터**: attack/chainL/chainR(전부 Trigger). BlendTree 없음(로코모션 상태 없음 → "Float여야" 함정 무관).
- **전이(전부 CUT dur0 검증)**: Idle→Roar(attack)/Idle→L|R_Windup(chainL|R)/Roar→L_Windup(exit0.95)/4구간 체인(exit0.99)/**Recovery→반대손 Windup(chainR|L, exit0.98) 먼저 + →Idle(폴백) 나중**. ★전이 *순서* 핵심(트리거 셋이면 직행, 아니면 Idle) — 디스크 검증서 L_Recov→R_Windup이 →Idle보다 앞 확인.
- **드라이버 로직 = Dimax 1:1**: Idle 허브(미교전 attack/교전 디제너릿 재개)·Windup 진입1회 셋업+FaceTarget(추적 이관)·Strike/FollowOut 회전0·Recovery 진입1회 다음손 trigger. 엣지가드 _windupSetup/_recovChained/_firedThisIdle. OnDisable ResetCombatState.

## ★★무게 노브 v2 = 둔중 브루저 + ★강약 대비(위협감) (SSOT public const, 2026-06-14 유저 ▶ 재튜닝)
- **★v1(밋밋 — 위협X)**: Windup 1.1/Strike 1.0/Follow 1.6/Recov 1.9. Windup≈Strike(1.1배)라 스냅 없음 = 위협감 부족(유저 ▶ "공격 강약 모자라").
- **★★v2(강약 대비 = 위협)**: Windup **0.70**(느린 응축=긴 텔레그래프, 플레이어 반응창) → Strike **2.4**(확 박히는 스냅, Windup 3.4배속=핵심) → Follow **1.3**(무거운 carry) → Recov **1.7**. ★헤드리스 실측 벽시계 대비: Windup 텔레그래프 **0.433s** vs Strike 스냅 **0.083s** = **5.2배**(벽시계는 프레임수 차로 속도비 3.4배보다 큼). ClawHit=Strike norm0.5 고정(클립 정규화 → Strike 빨라져도 같은 포즈, 재타이밍 불필요).
- ★강약은 *구간별 정적 state.speed 램프 모양*으로만(per-frame 코드 곡선 ❌ 헌법). 상태 시퀀스/CUT 연속 불변.
- Roar speed 4.5. turnSpeed 300. ★**separation v2: radius 2.6→3.6 / weight 1.0→1.6**(유저 ▶ "몬스터끼리 겹친다" — 덩치 큼). ★separation은 FaceTarget에서 heading 0.4 가중치로만 섞임 → *조향만 휨, 루트모션 전진은 항상 계속* → 강한 separation = 벽이 옆으로 퍼질 뿐 멈추지/백오프하지 않음(멀뚱 방지). 겹침만 풀고 전진 보존.

## ★★per-hand 전진 게인 v2 = 벽 속도 (L/R 비대칭 4.094 vs 2.413m 보존)
- Dimax는 단일 AdvanceGain(L/R 대칭). Venosaur는 **LeftAdvanceGain/RightAdvanceGain 둘로 분리**. OnAnimatorMove에서 `inLeft?Left:inRight?Right:1f` 게인으로 deltaPosition 스케일. 클로 구간만 게인, Idle/Roar는 1×.
- **★v1 1.0 → v2 1.5**(유저 ▶ "너무 느리다 — 벽으로 안 느껴진다, 최소 플레이어 걷기 이상"). 둘 다 *같은 배율* 1.5 → R이 여전히 70% 큰 런지 = **비대칭 보존(균등화 아님, 동등 증폭)**.
- ★★튜닝 공식(헤드리스 실측): **순수전진 지속 = 4.906 m/s × gain**. 랩 효율 ~0.92. → 목표 랩속도 ≈ 4.906 × gain × 0.92 = gain × 4.51. (gain 1.5 → 순수 7.36 / 랩 ~6.8 예상. gain 1.7 → 순수 8.34 / 랩 ~7.7 더 집요.)
- ★유저 ▶ 굼뜨면 ↑(1.7), 질주로도 못 빠지면 ↓(단 걷기 5.5 이상 유지).

## ★★헤드리스 런타임 시뮬 검증 통과 v2 (gain 1.5, Animator.Update 스텝, 인라인 드라이버 로직)
- **★플레이어 속도 코드 실측**: `LabPlayerController.cs` walkSpeed=**5.5** / sprintSpeed=**9.0** m/s(메모리 일치 재확인).
- **LRLRLRLRL 완벽 교대**(9윈드업, 더블/스킵 0). **백슬라이드 −0.024m**(노이즈, 단조전진). enter-to-enter: 후-L 5.999m / 후-R 10.190m@gain1.7(비율 1.70 = 비대칭 정확 보존).
- **지속속도 = 7.722 m/s @gain1.5**(순수전진 시뮬) → **걷기 5.5 BEATS(벽! 걸어선 못 빠짐, +2.2 m/s) · 질주 9.0 아래(질주하면 ~1.3 m/s 떨굼 = 탈출밸브 보존)**. v1 3.97<걷기였던 게 v2 7.72>걷기로 = "벽" 달성.
- ★★stale-assembly 함정: **KatanaController.cs(타 세션 파일)에 선행 컴파일 에러 20건 → Assembly-CSharp 전체 빌드 실패 → 내 VenosaurBrawler.cs const가 에디터에 로드 안 됨**(RequestScriptCompilation 무효, baked speed가 옛값으로 남음). 회피=**컨트롤러 state.speed를 스크립트로 직접 bake + 헤드리스 sim에 v2 값 리터럴 인라인** → 깨진 어셈블리와 무관하게 수치 검증. 타 세션이 Katana 고치면 SetupData 재실행으로 동일 재빌드. (병렬세션 규율: KatanaController는 내 파티션 아님 — 안 건드림.)
- ★MonoBehaviour 사적 Awake/Update를 리플렉션 호출하면 NRE → **드라이버 로직 인라인 복제 + Animator.Update**가 안전한 헤드리스 검증법.

## 유저 ▶ 판정 대기 (정지/스텝 검증 한계 — 흐름/속도감/무게감은 플레이로만)
- ★**무게감** — Dimax(빠른 휘릭)와 자발 구별되나, "둔중 브루저"로 읽히나(굼뜸 아니고). 노브=4구간 speed 전체.
- ★**L/R 비대칭("절뚝")** — R이 L보다 70% 멀리 가는 게 "살아있는 불균등"인가 "절뚝 버그"인가. 거슬리면 per-hand gain 균등화.
- ★**단독 약함(걷기 탈출)** — 3.97<5.5라 솔로는 걸어서 빠짐. 호위/물량이라 의도지만 "단독도 위협적 신체"와 충돌하면 gain↑(per-hand 증폭)로 압박 강화.
- ★**스폰 반경 10m** — R 클로 4.094m 도달이라 진입이 빠를 수 있음. 멀리서 포효→클로월 진입이 보이나.
- Stab+Codex 2중 리뷰는 오케스트레이터 후속(나는 구현까지).

연동: [[project_venosaur_clip_kit]]·[[project_telegraph_driver_dimax]](직재활용 원본 틀·v8 라우팅·AdvanceGain 메커니즘)·[[feedback_measure_rootmotion_by_stepping]]·[[feedback_animevent_fire_timing]]·[[project_stage1_roster_anim_read]]
