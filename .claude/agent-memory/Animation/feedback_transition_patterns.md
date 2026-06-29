---
name: transition-patterns
description: 자연스러웠던/위험했던 전이 패턴 — CUT vs 블렌드 경계, 비루프 로코모션 자기루프 함정
metadata:
  type: feedback
---

상태머신 전이 설계 원칙(검증된 것):

**블렌드(crossfade) 허용 = 로코모션 이음새 단 한 곳:** Idle↔Run 같은 속도 이음새만 짧은 블렌드(0.10~0.15s). 그 외 전부 CUT(duration=0).
- **Why:** 제0원칙 — 정체성 동작(공격·도약·물기·스핏) 재생 중 다른 클립이 섞이면 "애니 도중 다른 애니"(유저 명시 금지, 사고#1). 검증질문 "이 프레임에 두 클립 섞이나"가 곧 합격선.
- **How to apply:** 정체성 동작 진입 전이는 dur0 컷 또는 ExitTime 후 컷. 검증은 캡처 프레임에서 `anim.IsInTransition(0)==False` 확인.

**비루프 로코모션 클립 함정:** "_RM" run/walk 클립이 isLooping=False면 한 번 재생 후 얼어붙어 루트모션 정지(Caniathrox Run_RM=2.46m 후 멈춤). 접근/순찰을 지속하려면 **상태 자기루프 전이**(ExitTime≈0.98, dur0, 지속조건)로 클립을 재시작.
- **Why:** 벤더 클립 import의 loopTime을 켜면 원본 에셋 수정(금지). 자기루프 전이가 에셋 안 건드리고 로코모션을 지속시키는 정석.
- **How to apply:** 로코모션 상태에 self-transition 추가, 공격 트리거 전이를 자기루프보다 위 순서에 둬 우선권 보장(트리거 즉시 발동, 자기루프는 ExitTime에만).

**WriteDefaults=true 유지**(상태 간 본 포즈 누수 방지, 단일 레이어 풀바디면 안전). [[caniathrox-attack-statemachine]]

**★cycleOffset != 0 은 그 상태의 exitTime 전이를 깨뜨린다 (2026-06-26 카타나 Skill01 차징, 실측 확정):** 한 클립의 *중간 프레임부터* 재생하고 싶을 때 `state.cycleOffset`를 쓰면, 그 상태의 ExitTime 전이가 **발화하지 않는다**(Strike cycleOffset0.486+exit0.88 → state-local normalizedTime 0.917까지 가도 전이 안 됨 = 스턱-인-포즈 소프트락). 같은 컨트롤러의 cycleOffset0 상태는 exit 정상 발화(Charge exit0.486 OK). 
- **정석 = `transition.offset`(진입 normalizedTime) + cycleOffset 0:** 중간진입은 *그 상태로 들어오는 전이*의 `offset`에 normalizedTime을 박는다(예 AnyState→Strike.offset=0.486). 상태 cycleOffset는 0으로 둬 exitTime이 정상 작동. **freeze(홀드)도 동일** — speed0 상태에 진입전이 offset=0.486 주면 그 프레임에 얼어붙음(Hold@0.486, cycleOffset 불요).
- **Why:** cycleOffset는 *지속 오프셋*이라 exit 비교 normalizedTime 회계를 깨고, transition.offset은 *1회 진입 위치*라 이후 state-local 시간이 0부터 정상 누적 → exitTime 정상.
- **검증:** 에디트모드 `anim.Update(dt)` 스텝으로 "Strike가 Locomotion으로 *빠져나오나*"를 반드시 확인([[measure-rootmotion-by-stepping]]). 추론 금지 — 나도 cycleOffset가 될 줄 알았다가 스텝으로 깨짐 발견.

**★윈드업→홀드→베기 = 한 클립 3분할 + 진입offset (카타나 Skill01 차징 2026-06-26):** "0→70 재생 후 홀드, 릴리스 시 70→144 이어재생"의 정석. 같은 클립 motion으로 3 상태(전부 tag Action·cycleOffset0): Windup(speed1, 0→0.486 exit→Hold) / Hold(speed1→**speed0**, 진입전이 offset0.486 → frame70 freeze) / Strike(speed1, AnyState진입전이 offset0.486 → 70→끝, exit0.88→Loco). 홀드중 RMB 유지=speed0 freeze(전역 Animator.speed=0 안 씀 — 이동/딴레이어 안 얼림). 릴리스=별 트리거로 AnyState→Strike(윈드업 트리거와 분리). 이벤트(hit·end)가 0.486 뒤(0.556/0.86)면 *윈드업서 안 터지고 베기서만* 터짐 = 데미지 구간분리 공짜. 미발동(빈탭) 시 Windup/Hold→Loco 취소전이 필수(안 그러면 Hold 고착). ★06-26 갱신: 경계 frame70→**49(0.340)**, Hold는 freeze→게이더링 루프(아래).

**★"동결 홀드 → 살아있는 게이더링 루프" + 루트모션 드리프트 함정 (카타나 Skill01 06-26):** 유저 "speed0 동결 말고 기 계속 모으는 느낌(48↔49 반복)". (a)Mecanim forward-only라 진짜 핑퐁(49→48 역재생) 불가 — negative speed+exitTime는 즉시발화로 깨짐(실측). (b)1프레임 CUT 루프=버즈 지터(유저 명시 금지). →정석=**좁은범위(예 46→49) 크로스페이드 셀프루프**(Hold→Hold, exitTime=루프top·offset=루프bottom·dur≈0.10·canTransitionToSelf). 크로스페이드가 버즈를 젠틀 펄스로(같은클립 셀프블렌드=제0원칙 무관, 오히려 버즈회피에 *필수*). 릴리스/취소가 루프 인터럽트하는지 스텝핑 검증(지연 max≈loop dur). (c)★**치명 함정=루트모션 드리프트**: speed0 동결은 드리프트0이나 speed>0 셀프루프는 *매 사이클 클립 전진→루트모션 누적*(실측 ~0.26m/s=홀드3s에 0.78m 슬라이드, 캐릭이 걸어나감). 컨트롤러 단독 수정불가(상태별 루트모션 토글 없음)→**드라이버 OnAnimatorMove서 그 상태명일 때 ApplyRootStep 스킵**(변위 억제=헌법부합, 위치창작 아님). 베기 런지는 정상적용. (d)★**크로스페이드 루프=트리거 삼킴 소프트락(H-1, Stab)**: 셀프루프 crossfade가 `interruptionSource=None`이면 *크로스페이드 진행 중* 발사한 트리거(릴리스/취소)가 전이 못시키고 그 프레임에 소멸→루프 계속→간헐 소프트락. 수정=**①셀프루프+settle 블렌드의 interruptionSource를 None→Source(+orderedInterruption) ②탈출(베기/취소)을 AnyState 의존 말고 명시적 *source 전이*(현재상태→대상)로 추가하고 셀프루프보다 먼저 등록**(ordered 우선권). ★AnyState 전이는 진행 중 크로스페이드를 못 끊을 수 있다(source 전이라야 100%, 실측). interruptionSource는 *긴* 전이(블렌드/루프)에만 의미 — CUT(dur0)은 진행이 없어 None이어도 무관. 검증=루프 전 위상(크로스페이드 포함)서 트리거 발사→100% 발화 확인. [[katana-skill01-rmb-wiring]] [[measure-rootmotion-by-stepping]]

**한 클립을 두 상태로 SPLIT = "부분만 배속"의 유일한 정석 (Dimax 클로월 2026-06-14):** 유저가 "잘라내지 말고 *뒷부분만 빠르게 재생*"을 원할 때. 한 state.speed는 클립 전체 균일이라 한 상태로는 불가 → 같은 take에서 두 ModelImporterClipAnimation(frame 범위만 다름, 예 Swing 0~22 / Recovery 22~35)을 만들어 각 상태에 다른 speed(1.0 / 3.0)를 준다.
- **Why:** ①트림(끝 잘라내기)은 동작을 *버려* 루트모션 거리손실+시간단축으로 같은 speed인데 전체가 빨라짐(유저 "회수만"과 어긋남, Dimax v5 3.94m/s 사고). split은 *전부 재생*하되 회수 구간만 압축 → 거리 100% 보존(Swing+Recovery 루트모션 합=풀클립). ②cycleOffset로 한 상태가 뒷부분만 재생은 불가(비루프 클립은 end-frame 서브레인지 개념 없음) — sub-clip import가 유일하게 깔끔.
- **연속성 보장:** 같은 take라 Swing.lastFrame == Recovery.firstFrame = *비트-동일 포즈* → Swing→Recovery 전이가 CUT(dur0, ExitTime~0.99)여도 포즈 점프 0. crossfade가 아니라 한 동작의 분할이라 제0원칙 위반 아님(검증: enter-to-enter 루트Z 연속 + IsInTransition 순간만).
- **How to apply:** 분할점 = 실측(SampleAnimation 손/무기 본 전방 reach가 peak 지나 중립으로 *귀환 시작*하는 frame — 팔로스루 끝 경계). 이벤트(히트)는 *타격이 든 쪽 sub-clip*에 정규화 재계산(컨택절대frame/분할frame수). 빌드스크립트 const(SplitFrame·두 speed)에 박아 durable. 속도점프(1.0→3.0 경계 "탁")는 정지캡처 판정불가 → 유저 ▶(어색하면 RecoverySpeed↓ 또는 분할점↑).
