# Lacercharias 공격 상태머신 설계 (종이 설계 — Unity 미와이어링)

> 2026-06-21 · Animation 에이전트 설계 · **순수 종이 설계**(Unity/unity-mcp 미접촉, 클립 실측은 Unity 세션이 수행).
> 상태: **제안 — 유저 판정 대기 / 런타임 미검증.** "됐다" 주장 없음(아직 런타임 없음).
> 관련: [[project_stage1_roster_anim_read]] · [[project_caniathrox_attack_statemachine]] · [[feedback_pounce_grammar]] · [[feedback_transition_patterns]] · [[feedback_monster_design_northstar]] · 자매문서 `2026-06-21-kupolojuve-attack-statemachine-design.md`

---

## 0. 롤 틀이 왜 별도 신규 틀인가 (Kupolojuve 부유 틀과 무관)

Lacercharias = **저자세 이형**(거대 입 + 뭉툭 앞다리, 수직 거의 0). 첫 스테이지 fodder("쓸어담는 손맛")인데 **에셋이 진짜 Roll 상태머신을 보유**: `GoToRoll → Roll(구르는 이동) → RollToBiteAttack(굴러와 물기) → RollToIdle`. anim_read 판독대로 "단순한데 고유 굴림 문법" — fodder지만 상태 전이가 특수하다.

**왜 기존 4틀(돌진 Caniathrox / 클로월 Dimax / 브루트 Crassorrid / 사수 Venodonte / 부유 Kupolojuve) 어디에도 안 들어가나:**

1. **구르는 몸은 회전축과 전진이 한 모션에 묶인다.** 지상 보행 틀(Run_RM 자기루프 접근)은 "발이 땅을 밀어 전진 + facing은 별도 yaw 회전"으로 위치/방향이 **분리**된다. 롤은 몸 전체가 굴림축(pitch/roll 회전) 자체로 전진하므로 — 전진을 멈추려면 구르기를 멈춰야 하고, 방향을 틀려면 굴림축을 틀어야 한다. **이 결합이 상태머신 문법을 바꾼다**(§1).
2. **"공이 펴진다"는 전용 전이 비트가 있다.** Kupolojuve의 "다시 떠오르기", Caniathrox의 "Coil 응축"처럼, 롤은 **GoToRoll(말림)/RollToBiteAttack(펴짐)** 두 전용 전이 클립을 갖는다. 이건 다른 틀에 없는 모션 자산 — 클로월·다이브엔 "말렸다 펴지는" 비트가 없다.
3. **부유 틀(Kupolojuve)과는 정반대 축.** 부유 틀의 위험은 "수직 Y가 부감서 납작"(공중). 롤 틀의 위험은 "수평 굴림 + 코드 facing이 클립 굴림축과 충돌"(지면 밀착). 공유 인프라 0(그림자 앵커 불요 — 항상 지면 접촉). 별도 신규 틀이 맞다.

**난이도 = 낮음**(anim_read 6종 중 5위). Roll 상태머신만 새로 짜면 끝. 단 **롤 특유의 facing 소유권 함정**(§3)이 fodder 치고는 까다로운 유일한 지점.

---

## 1. 신체 판독 — 구르는 몸이 문법을 어떻게 바꾸나

지상 보행 틀의 표준 = **"Run_RM 자기루프 접근 → 정지 → 공격 → Idle 복귀"**. 롤은 두 지점에서 깨진다:

- **A. 접근이 "Run_RM 자기루프"가 아니라 "말림→굴림 루프→펴짐" 3비트다.** 굴러서 가려면 먼저 **말려야**(GoToRoll, 정지 상태에서 공 형태로 응축 = 일종의 anticipation) 굴림이 시작된다. 굴림이 멈추는 것도 **펴짐**(RollToBiteAttack/RollToIdle)으로만 가능 — 보행처럼 아무 프레임에서 정지/공격 진입을 못 한다. 따라서 접근 = **단순 자기루프 Run이 아니라, GoToRoll(가속/말림) → Roll_RM 자기루프(등속 굴림) → RollToX(감속/펴짐)** 의 가·감속 비트가 클립에 내장돼 있다. 이건 [[feedback_pounce_grammar]]의 "응축→발사" 3박자와 같은 형식이되, **발사가 곧 이동**이라는 점이 다르다(Caniathrox는 응축 후 한 번 발사하고 끝, 롤은 굴림이 지속 이동).
- **B. 굴림축과 전진이 한 모션이라 facing을 코드가 함부로 못 돌린다.** 보행 틀은 Run 중 코드가 yaw를 매프레임 RotateTowards로 돌려도 발 모션이 그럴듯하다(제자리걸음 yaw). **롤은 굴림 방향 = 전진 방향이 클립 루트모션에 박혀 있어**, 코드가 yaw를 따로 돌리면 "옆으로 게걸음 구르기"(굴림축은 X로 도는데 몸은 다른 방향 전진)가 되어 즉시 거짓이 된다. → **방향전환은 굴림 루프 자체를 코드가 yaw로 천천히 휘게 하되(heading 조향), 굴림 속도/궤적은 클립이 소유**(§3). 45° 부감서 저자세 롤은 수직성분이 거의 0이라 — Kupolojuve의 "수직 납작" 문제와 정반대로 — **부감서 오히려 잘 읽힌다**(공이 화면 평면에서 굴러옴). 이건 롤 틀의 *장점*.

---

## 2. 공격 상태 시퀀스

fodder = 단일 공격 모드(거리 분기 없음, "굴러와서 문다"). 에셋 Roll 시퀀스를 그대로 상태로 승격:

```
                  ┌──────────── (굴림 지속) ────────────┐
   Idle ──► GoToRoll ──► Roll(자기루프) ──[도착판정]──► RollToBite ──► BiteStrike ──► RollToIdle ──► Idle
   (허브)   (말림/가속)   (구르는 이동·RM)               (펴짐/감속)    (무는 임팩트)   (재차 롤? §2-주)  (복귀)
     ▲        anticipation  └── 자기루프 전이로 지속 ───┘   ★텔레그래프      ★BiteHit       └──────────────┘
     │                                                       핵심 비트
     └────────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Idle = 허브.** 미교전 정지. 교전 신호(isApproaching)에 GoToRoll로.
- **GoToRoll = 말림 + 가속(anticipation).** 정지→굴림 응축. in-place에 가까움(전진 거의 0 — 실측 선결). [[feedback_pounce_grammar]]의 "모았다가"에 대응하되, 여기선 응축 자체가 **굴림 시작 자세**. state speed로 무게 조절(느리게 말리면 텔레그래프가 읽힘).
- **Roll = 구르는 이동(루트모션 자기루프).** Roll_RM 등속 굴림. **비루프 클립이면 자기루프 전이(ExitTime≈0.98, dur0)로 지속**([[feedback_transition_patterns]] — Run_RM 함정과 동일, 구르기도 한 사이클 후 얼면 멈춤). 도착 판정(bite 트리거)까지 굴림 유지.
- **RollToBite = 펴짐(★텔레그래프 핵심 비트).** 굴러오던 공이 멈추며 입을 벌리는 자세로 펴짐. **이게 "공이 펴진다 = 곧 문다"를 부감서 읽히게 하는 예고**(§3). 회전 0(펴지는 순간 facing 고정 — 굴림 멈춤 지점에서 방향 확정).
- **BiteStrike = 무는 임팩트.** RollToBiteAttack 클립의 무는 정점. ★BiteHit AnimationEvent(§5). 전진 약간(파고들며 무는 프로파일 — Caniathrox Bite=BiteForward_RM 1.328m 선례, 단 롤은 펴짐에 전진이 흡수될 수 있어 실측 선결).
- **RollToIdle = 복귀.** 무른 뒤 중립 복귀. RollToIdle 클립.

**★주(재차 롤 vs Idle 복귀):** anim_read 배역 "구르며 접근 → 펼침 → 공격 → **재차 롤**". 두 라우팅 안:
- **(a) BiteStrike → RollToIdle → Idle → (재교전 시) GoToRoll** — 매 공격 후 Idle 경유. 안전·단순·fodder다움. **fodder 기본 권장.**
- **(b) BiteStrike → GoToRoll 직행**(Idle 우회, Dimax v8 "쉼 제거" 패턴) — 무자마자 다시 말려 굴러나감. "끊임없이 굴러붙는 벽"이 필요하면. 단 **RollToIdle 클립을 건너뛰므로 펴진 상태→말림 경계 포즈 점프**가 있을 수 있어 실측 선결(BiteStrike 종료 포즈 ≈ GoToRoll 시작 포즈인지). → **(a)로 시작, 유저가 "더 끈질기게" 요구 시 (b)**.

**전이:** 전부 ExitTime CUT(dur0), 비루프 Roll만 자기루프 전이로 지속. **블렌드 후보 = Idle↔GoToRoll 이음새 한 곳뿐**(로코모션 이음새 규칙). GoToRoll→Roll→RollToBite→BiteStrike→RollToIdle 사슬은 **같은 굴림 동작의 분할이므로 전부 CUT**(crossfade 금지, 제0원칙). 단 ★이 사슬은 Dimax SPLIT처럼 "같은 take 분할"이 아니라 **별개 클립들의 연속**이라 — 경계 포즈가 비트동일이라는 보장이 없다 → **GoToRoll 종료 포즈 ≈ Roll 시작 포즈, Roll 종료 ≈ RollToBite 시작** 등의 경계 연속성을 Unity 세션이 실측해야 함(§8 R-롤2). 에셋이 한 시퀀스로 설계됐다면 경계가 맞을 가능성 높으나 **미검증**.

**회전 허용/금지(헌법 — 커밋 동작 중 재조준 ❌):**
- 회전 O = Idle · GoToRoll(말리며 조준) · **Roll(굴림 heading 조향만, §3)** · ~~RollToBite~~(펴짐 시작까지만 허용, 펴진 뒤 고정)
- 회전 0 = **BiteStrike**(무는 궤적 고정) · RollToIdle(회수)

---

## 3. ★회전/facing 소유권 — 롤 틀 최대 함정 (헌법 경계)

롤은 **전진=굴림축이 클립 루트에 박혀** 있어 위치/방향 소유 경계가 다른 틀보다 미묘하다. 헌법(애니가 진실·코드는 연결): **위치·궤적은 클립이 소유, 코드는 heading(굴러갈 방향)만 천천히 조향.**

| 상태 | 위치(전진) 소유 | facing/heading 소유 | 경계 판정 |
|---|---|---|---|
| GoToRoll | 클립(거의 0) | 코드 yaw 허용(말리며 조준) | 발사 전 cocking = 궤적 안 휨 → 허용 |
| **Roll** | **클립 루트모션(굴림 전진)** | **코드 yaw 조향 — 단 turnSpeed 제한** | ★굴림 *속도/거리*는 클립, *어디로 굴러갈지 heading*만 코드가 천천히 휨. RotateTowards로 model.rotation을 돌리면 Roll_RM의 전방 델타가 그 방향으로 따라감(Caniathrox Approach Steer와 동형). **굴림축 자체를 코드가 안 만든다.** |
| RollToBite | 클립 | 펴짐 시작까지만, 이후 고정 | 펴지는 순간 방향 확정 = 무는 방향. 펴진 뒤 회전 0(궤적 보존) |
| BiteStrike | 클립(약간 전진) | **고정(회전 0)** | 무는 궤적을 코드가 휘면 위반(제2원칙) |

- **★핵심 경계 질문 = "굴림 궤적을 코드가 휘나, 아니면 굴러갈 방향만 가리키나?"** Roll 상태에서 코드는 `RotateTowards(model.rotation, 타깃방향, turnSpeed)`로 **heading만** 조향한다. 이건 Caniathrox Approach의 Steer, Dimax Windup의 FaceTarget과 같은 부류 — **위치를 만드는 게 아니라 클립이 만들 전진의 방향을 가리키는 것.** turnSpeed가 너무 크면 "공이 제자리서 핑 도는" 부자연 → 작게(굴림은 관성이 커서 급선회 못 함, 무게감과도 정합). turnSpeed = 추적 노브.
- **applyRootMotion = true 유지.** Roll_RM의 전진 델타를 Animator가 적용(코드는 deltaPosition을 건드리지 않음 — Dimax v9의 AdvanceGain 증폭은 **롤엔 적용하지 않음**: 굴림 전진을 ×배율로 부풀리면 "보폭 없이 미끄러지는 공"이 됨. 굴림 속도는 클립 native 또는 state.speed로만 조절). **단 굴림이 너무 느려 걷는 플레이어가 못 빠지면**(§8 R-롤3) — AdvanceGain이 아니라 **state.speed 배속**(굴림 사이클을 빠르게 = 더 빨리 구름)으로 해결. 굴림은 빨리 돌수록 빨리 가는 게 자연.
- **굴림 중 yaw 회전이 굴림축과 시각 충돌하나?** 저자세 공이 X축으로 구르며 yaw(Y축)로 천천히 휘면 — 실제 공/바퀴가 곡선 주행할 때와 같음(자연). 단 **turnSpeed가 크면** 충돌 가시화(게걸음 구르기) → ★유저 플레이 게이트(정지 캡처 불가).

---

## 4. 루트모션 계획 — 클립별 BakeY/Rot/XZ

롤 특유: **굴림 동안 Y 바운스가 클립에 있으면** 어떻게 다루나. 저자세 공은 구를 때 무게중심이 약간 오르내릴 수 있음(완벽 구 아님 = 뭉툭 입/앞다리).

| 클립 | BakeRot | BakeY | BakeXZ | 이유 |
|---|---|---|---|---|
| GoToRoll (말림) | ON | **ON** | ON(거의 0) | 제자리 응축 — XZ 드리프트 방지. Y도 grounded |
| **Roll_RM (굴림)** | ON | **판정 필요(§아래)** | **OFF** | **굴림 전진 XZ가 진실 → 반드시 OFF**. Y는 실측 후 결정 |
| RollToBite (펴짐) | ON | ON | OFF(약간 전진?) | 펴지며 grounded. 전진 흡수 여부 실측 |
| BiteStrike (무는 임팩트) | ON | ON | OFF | grounded, 무는 약간 전진 보존 |
| RollToIdle (복귀) | ON | ON | ON(거의 0) | 제자리 복귀 |

- **공통:** BakeRot ON(facing/heading 코드 소유 — §3) + 전진 보존은 BakeXZ OFF.
- **★Roll_RM의 BakeY 판정(핵심 — Kupolojuve 다이브 BakeY 함정의 롤판):**
  - 만약 Roll_RM에 **의미 있는 Y 바운스**(구르는 몸의 무게중심 오르내림)가 있고 그게 **자연스러우면 → BakeY OFF로 보존**(굴림의 무게감 = 정체성, [[feedback_pounce_grammar]]의 "위로 뜨는 Y가 개구리"와 반대 — 여기선 *작은* 지면밀착 바운스라 개구리가 아니라 무게).
  - 만약 Y 바운스가 **루프 시작/끝에서 안 맞아 자기루프 전이서 튀면**(높이 드리프트) → **BakeY ON**(평탄화)으로 드리프트 박멸. 단 평탄화하면 굴림 무게감 일부 손실.
  - **저자세 롤이라 Y 진폭이 애초에 작을 가능성 높음**(수직 거의 0 신체) → 둘 중 무엇이든 부감서 차이 미미할 수 있음. **Unity 세션 실측 후 결정**(정적 커브 신뢰 금지, Animator 스텝 — [[feedback_measure_rootmotion_by_stepping]]). 미검증.
  - **헌법 경계:** 코드는 굴림 Y를 **발명하지 않는다**. BakeY ON/OFF는 *클립이 준 Y를 쓸지 평탄화할지*의 import 결정일 뿐. 코드가 sin으로 위아래 흔드는 건 위반(부유 틀의 "등고도 레일 vs 개구리-Y"와 같은 원칙, 지면판).

---

## 5. AnimationEvent 타이밍 계획 (개념 — 정규화 실측은 Unity 세션)

클립 실측은 Unity 세션이 Animator 스텝으로 수행([[feedback_measure_rootmotion_by_stepping]]). 여기선 "어느 모션 정점에 박을지"만 개념 지정:

- **BiteStrike → BiteHit:** 턱/입이 가장 닫히는 정점(무는 컨택 모먼트) = 데미지 훅. **ClawHit=Strike만** 원칙 계승(Dimax) — BiteHit은 BiteStrike 상태에만, 굴림/펴짐엔 없음.
- **(옵션) RollToBite 진입 = 텔레그래프 발동 동기점.** 펴짐이 시작되는 프레임을 텔레그래프 ForceFull/스폰 동기에 쓸 수 있음(§6). 단 fodder는 텔레그래프 자체가 가벼우므로 — 드라이버 상태 진입(RollToBite enter)으로도 충분(AnimationEvent 불요할 수 있음).
- **규약:** time = **정규화[0..1]**(임포터가 ×길이 — [[project_frank_fbx_animevent_gotchas]]·[[project_telegraph_driver_dimax]] 정규화-time 함정). SendMessage는 **Animator 같은 GO에만** → 드라이버는 **프리팹 루트에 AddComponent**(자식 함정 회피, Dimax v9 검증·OnAnimatorMove 발화 보장과 동일 이유).

---

## 6. 텔레그래프 설계

롤 텔레그래프는 **두 비트가 자연 분리**된다 — 굴러오는 경로(이동 위협)와 멈춰 무는 지점(공격 위협):

- **굴림 경로 = ThreatArc 레인(lane).** 굴러올 직선 경로 예고(부감서 "이 라인으로 공이 온다"). Roll 상태에서 코드가 heading을 조향하므로(§3) — 레인은 **현재 heading 방향**을 따라 갱신될 수 있으나, ★공정성 위해 **펴짐(RollToBite) 직전엔 고정**(차오르던 약속이 따라 돌면 불공정). Dimax/Crassorrid의 "스폰 시점 전방 고정" 원칙 — 단 롤은 이동 위협이라 레인이 갱신되는 게 자연스러울 수도(유저 판정).
- **멈춤/무는 지점 = ThreatArc 원(circle).** RollToBite 진입 시 펴지는 지점 전방에 작은 원(무는 사거리). 이게 **"공이 펴진다 = 여기 문다"의 시각 확정.** fill = RollToBite + BiteStrike 윈드업 길이에 맞춤(실측 후).
- **fill 타이밍:** fodder라 윈도가 짧아도 됨(엘리트/브루트처럼 1.0~1.4s 길 필요 없음). RollToBite~BiteStrike 컨택까지 실시간(state speed 반영) = fill. **단 굴림 자체가 이미 예고**(공이 굴러오는 걸 보면 온다는 걸 안다)라 — 멈춤 지점 원은 *마지막 확정*용. fodder는 ★텔레그래프 최소화 가능(굴림 경로 가시성이 주 예고, 원은 보조).
- **그림자 앵커 불요.** 항상 지면 접촉(부유 틀과 결정적 차이) → Kupolojuve/Carcinoptera 그림자 인프라 공유 안 함.

**착공 전 비용 선언(검증 게이트):** 신규 셰이더 **0**. ThreatArc 레인/원 + 기존 TelegraphPad/Pool 재사용([[project_telegraph_driver_crassorrid]]가 첫 소비, [[project_telegraph_driver_dimax]]가 본체). Kupolojuve의 전격 같은 신규 셰이더 후보 없음 — fodder는 인프라 전부 기성.

---

## 7. 클립 킷 가용성

anim_read 인벤토리 기준 부족 0: GoToRoll · Roll(_RM?) · RollToBiteAttack · RollToIdle · BiteAttack · Idle/Walk/Run/GetHit(방향4)/Death/Turn 풀세트 보유. 단 **실제 클립명·길이·컨택 프레임·루트모션 Y/XZ·경계 포즈 연속성은 Unity 세션 실측 선결**(정적 커브·"_RM" 이름·옛 주석 신뢰 금지 — [[feedback_measure_rootmotion_by_stepping]]). 특히:
- **Roll_RM 굴림 이동거리/사이클**(루프 이동량 — anim_read도 "점검 필요"로 명시).
- **GoToRoll/RollToBite/RollToIdle 전이 클립의 경계 포즈 연속성**(§2 — 별개 클립 사슬이라 비트동일 미보장).
- **굴림 Y 바운스 진폭**(BakeY 판정 — §4).

---

## 8. ⚠️ 미검증 · 위험 플래그 (비누설 — 원문)

- **R-롤1 (최대) — facing/굴림축 충돌:** 코드 yaw 조향(heading)이 클립 굴림축과 시각 충돌하면 "옆으로 게걸음 구르기"가 됨. turnSpeed 작게 + Roll만 heading 조향(BiteStrike 고정)으로 설계했으나 — **"공이 자연스럽게 곡선 주행하나, 게걸음으로 미끄러지나"는 유저 플레이 게이트**(정지 프레임 검증 불가). turnSpeed가 진짜 노브.
- **R-롤2 — 전이 클립 경계 포즈 불연속:** GoToRoll→Roll→RollToBite→BiteStrike→RollToIdle은 **별개 클립 사슬**(Dimax SPLIT의 "같은 take 비트동일"이 아님). 경계 포즈가 안 맞으면 CUT에서 포즈 점프. 에셋이 한 시퀀스로 설계됐으면 맞을 가능성 높으나 **미검증** — Unity 세션이 경계 enter-to-enter 포즈 실측 선결. 점프 있으면 (a)전이에 ★최소 블렌드는 헌법 위반(정체성 동작) → 클립 트림/오프셋으로 경계 맞추거나, (b)에셋 시퀀스를 신뢰.
- **R-롤3 — 굴림 속도 vs 탈출 공정성:** Roll_RM 굴림 속도 실측 전. 걷기(5.5)보다 느리면 fodder가 걸어서 탈출됨(단일은 OK — fodder는 물량 위협, Dimax 클로월과 동일 철학). 너무 빠르면 못 피함(fodder 부당). 조절 = **state.speed 배속**(AdvanceGain ❌ — §3, 미끄러짐). 시작값·노브 유저 플레이 튜닝.
- **R-롤4 — BakeY 모순:** 굴림 Y 바운스를 BakeY OFF(무게 보존)로 두면 자기루프 전이서 높이 드리프트 가능 / ON(평탄)이면 무게 손실. 저자세라 진폭 작아 차이 미미할 수도 — 실측 후 결정(§4).
- **R-롤5 — 재차 롤 라우팅:** BiteStrike→Idle(a, fodder 권장) vs →GoToRoll 직행(b, 끈질긴 벽). (b)는 펴짐→말림 경계 포즈 점프 위험(R-롤2 연장). (a)로 시작 권장.
- **R-흐름1:** 속도감·굴림 타이밍·"한 동작만"은 정지 프레임으로 검증 불가 → **구조**(상태 그래프·CUT·회전·BakeY·heading 소유)는 설계로 확정 가능, **느낌**은 유저 플레이 게이트.

---

## 9. Unity 세션 인계 — 다음 단계

1. **클립 실측 선결**(Animator 스텝, 정적 커브 거짓 — [[feedback_measure_rootmotion_by_stepping]]): GoToRoll(전진 거의 0? 말림 길이), Roll_RM(굴림 XZ 이동·사이클·Y 바운스 진폭·자기루프 필요?), RollToBite(펴짐 전진 흡수·길이·무는 컨택 프레임), BiteStrike/RollToIdle(길이). **경계 포즈 연속성**(R-롤2) 동시 실측.
2. 클립별 BakeY/Rot/XZ 분리 임포트(§4 표) — **Roll_RM은 BakeXZ OFF 필수, BakeY는 실측 후**. 임포터 정규화-time 함정 주의.
3. 컨트롤러 빌드 — Idle 허브, GoToRoll(anticipation)→Roll(자기루프)→RollToBite(텔레그래프 비트)→BiteStrike→RollToIdle. 전이 CUT dur0, Roll만 자기루프 전이, **Idle↔GoToRoll만 블렌드 후보.** (BlendTree 쓰면 파라미터 = Float 필수, [[feedback_blendtree_param_must_be_float]] — 단 롤은 단일 공격이라 BlendTree 불요 가능.)
4. 드라이버 LacerchariasRoller.cs(프리팹 루트 AddComponent — OnAnimatorMove/SendMessage 발화 보장): isApproaching/bite 파라미터 구동, **Roll 상태에서만 heading RotateTowards(turnSpeed 제한)**, BiteStrike 회전 0, 도착 판정으로 bite 트리거, 토큰 가드(fodder는 동시 다수라 maxAttackTokens로 동시 무는 수 제한), OnDisable ResetCombatState(stale 트리거 차단 — Dimax/Crassorrid 패턴 계승).
5. 텔레그래프(§6) — ThreatArc 레인(굴림 경로) + 원(무는 지점), TelegraphPad/Pool 재사용. **신규 셰이더 0.** fodder라 최소화 가능.
6. AnimationEvent 주입(정규화 time·루트 co-location): BiteStrike BiteHit.
7. 유저 플레이 게이트(R-롤1 게걸음 여부·R-롤2 경계 점프·R-롤3 굴림 속도·R-흐름1).

---

## 핵심 설계 결정 요약

1. **롤 = 별도 신규 틀**(부유 Kupolojuve와 무관, 공유 인프라 0). 이유 = 굴림축과 전진이 한 모션에 결합 + "말림/펴짐" 전용 전이 클립.
2. **접근 = GoToRoll(말림/anticipation) → Roll 자기루프 → RollToBite(펴짐)** 3비트. 단순 Run_RM 자기루프가 아님 — 응축/발사 문법([[feedback_pounce_grammar]])이되 발사가 곧 지속 이동.
3. **facing 소유 = 클립이 굴림 궤적/속도, 코드는 Roll에서만 heading만 천천히 조향(turnSpeed 제한)**, BiteStrike 회전 0. AdvanceGain 증폭은 롤에 ❌(미끄러짐) — 속도 조절은 state.speed 배속.
4. **"펴짐"이 텔레그래프 핵심** — RollToBite가 부감서 "공이 펴진다=문다" 예고. ThreatArc 레인(경로)+원(무는 지점), 신규 셰이더 0.
5. **저자세 롤은 부감서 잘 읽힌다**(Kupolojuve 수직-납작 문제와 정반대 — 수평 굴림이 화면 평면). 롤 틀의 장점.
