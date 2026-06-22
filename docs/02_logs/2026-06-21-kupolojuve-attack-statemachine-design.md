# Kupolojuve 공격 상태머신 설계 (종이 설계 — Unity 미와이어링)

> 2026-06-21 · Animation 에이전트 설계 · **순수 종이 설계**(Unity/unity-mcp 미접촉, 클립 실측은 Unity 세션이 수행).
> 상태: **제안 — 유저 판정 대기 / 런타임 미검증.** "됐다" 주장 없음(아직 런타임 없음).
> 관련: [[project_stage1_roster_anim_read]] · [[project_telegraph_driver_dimax]] · [[feedback_pounce_grammar]] · [[feedback_monster_design_northstar]]

---

## 0. 종 선택 — 왜 Kupolojuve부터

미구현 4종(Lacercharias=롤 / **Kupolojuve=부유 해파리+전격** / Fulgurodonte=저자세 절지 / Carcinoptera=비행 최난) 중 종이 설계 레버리지 1위.

1. **비행/부유 틀의 프로토타입.** 최난종 Carcinoptera가 이 틀(그림자 앵커·평면 다이브·호버 idle)을 재사용한다 → "쉬운 데서 틀 세우고 어려운 데 재활용"(Venosaur가 Dimax 클로월 틀을 직재활용한 그 패턴).
2. **9종 중 유일한 신규 셰이더 후보**(전격) — 유저 TA 트랙과 정렬.
3. 지상 5종(근접 4·원거리 1)엔 비행 선례 0. 부유 바디는 상태머신 문법 자체를 바꾼다.
4. 난이도 = 중간(6종 중 3위), 클립 킷 부족 0 → 틀 검증엔 충분히 크고, 완주엔 충분히 작다.

Lacercharias(롤)는 재지정 대비 골격만 남김(§9).

---

## 1. 신체 판독 — 부유가 문법을 어떻게 바꾸나

지상 틀은 **"Run_RM 자기루프 접근 → 정지 → 공격 → Idle 복귀"**. 부유 바디는 두 지점에서 깨진다:

- **A. 공중이 "집"이다.** 모든 공격이 호버에서 시작·복귀하고, 공격 후 **"다시 떠오르기"** 비트가 추가된다(지상 틀엔 없던 상태). 접근 = 등고도 호버 리포지션(Fly*_RM, 고도 일정), "Run_RM 자기루프"가 아니다.
- **B. 45° 부감서 수직동작이 납작해진다.** 상승/하강/다이브의 Y 성분이 부감 카메라에선 크기변화로만 읽힘 → 텔레그래프·임팩트를 **지면 장판(수평면)**이 전담해야 한다. (Frank 텀블 리타겟이 부감서 납작해진 선례와 같은 렌즈 문제 — 리그 결함 아님.)

---

## 2. 공격 상태 시퀀스

견제형(harasser)이라 **2공격 모드**, 거리 분기로 선택(Caniathrox 거리분기 패턴):

```
                 ┌─────────────── (근거리) ───────────────┐
   HoverIdle ──► Reposition(등고도) ──► [거리분기]          │
   (허브)         (Fly*_RM)              │                  ▼
      ▲                                  │            DiveWindup ──► DiveStrike ──► DiveRecover(재상승)
      │                                  │            (응축/조준)    (내리꽂음)      (호버 복귀)
      │                                  └─ (원거리) ─► ElectroWindup ──► ElectroFire ──► ElectroRecover
      └──────────────────────────────────────────────────────────────────────────────────┘
```

- **HoverIdle = 허브.** 공중이 home. 모든 공격이 여기서 시작·복귀.
- **Reposition.** 등고도 호버 이동(접근 ≠ 지상 달리기). Fly*_RM XZ 이동, 고도 일정.
- **Dive 모드**(근거리, "위에서 온다"): DashSpikeAttack_RM 평면 다이브. 3분할 — Windup(응축·조준) / Strike(내리꽂음) / Recover(재상승). 이즈 램프는 지상 4분할 선례(Dimax v7) 차용, 비행 특성 맞춰 Unity 세션이 실측 후 배율 확정.
- **Electro 모드**(원거리, "맴돌며 지진다"): 정지 방전. 2분할 — Windup / Fire. ElectroShot 클립.
- **전이:** 전부 ExitTime CUT(dur0), 비루프 상태는 자기루프 전이로 지속. **블렌드 후보 = HoverIdle↔Reposition 이음새 한 곳만**(로코모션 이음새 규칙, [[feedback_transition_patterns]]).
- **회전 허용/금지:** 회전 O = HoverIdle·Reposition·DiveWindup(조준 cocking)·ElectroWindup. 회전 0 = DiveStrike·DiveRecover·ElectroFire(커밋 후 정렬 고정 — 헌법: 커밋 동작 중 재조준 ❌).

---

## 3. 텔레그래프 설계

- **Dive = ThreatArc 원(circle) + 예측 착지점.** 45°서 수직 윈드업이 안 읽히니(§1-B) 지면 장판이 텔레그래프 전담. 움직이는 플레이어 요격용 **예측 착지점**(Caniathrox 리드조준, leadTime). fill = DiveWindup 길이에 맞춤.
- **Electro = 레인(lane) 텔레그래프.** 직선 방전 경로 예고.
- **그림자 앵커.** 부유체 아래 약알파 원(ThreatArc 재사용) = "지금 어디 떠 있나"를 부감서 읽히게. **Carcinoptera와 공유 인프라.**

---

## 4. 전격 = 유일 신규 셰이더 (착공 전 비용 선언)

전격은 9종 중 유일한 신규 셰이더 작업이다. **착공 전 비용 선언이 헌법(검증 게이트).**

- **권장 순서:** ① ThreatArc 레인 + ProjectilePool(자작 발광구, [[project_projectile_pool_pattern]])로 **틀 먼저 닫는다 — 신규 셰이더 0.** ② 전격 정체성(파란 분기 번개)은 유저 판정 후 **2차 레이어**.
- **색 분리(R-색1):** 전격 파랑 ↔ 레드오렌지 위협 캐넌은 레이어 분리. 레드오렌지 레인 = 공정성/where(위협 예고), 파란 빔 = 정체성/what(이 종이 뭐냐). 섞지 않는다.

---

## 5. AnimationEvent 타이밍 계획 (개념 — 정규화 실측은 Unity 세션)

클립 실측은 Unity 세션이 Animator 스텝으로 수행([[feedback_measure_rootmotion_by_stepping]]). 여기선 "어느 모션 정점에 박을지"만 개념 지정:

- **DiveStrike:** 손/스파이크 최저점(지면 컨택) = 다이브 임팩트 이벤트.
- **ElectroFire:** 방전 정점 = 발사(단발 or 3연 — 클립 실측 후 확정).
- 규약: time = **정규화[0..1]**(임포터가 ×길이 — [[project_frank_fbx_animevent_gotchas]] 함정), SendMessage는 **Animator 같은 GO에만** → 드라이버는 루트에 AddComponent(자식 함정 회피, Dimax v9 검증).

---

## 6. 루트모션 계획 — 종 내부 클립별 BakeY 분리 (★핵심 함정)

같은 종 안에서 클립마다 BakeY가 갈린다:

| 클립 | BakeRot | BakeY | BakeXZ | 이유 |
|---|---|---|---|---|
| 호버/Reposition (Fly*_RM) | ON | **ON** | OFF | 고도 일정 — 드리프트 방지 |
| DashSpikeAttack_RM (다이브) | ON | **OFF** | OFF | **하강 Y가 진실** — 구우면 "역재생 개구리"(안 내려가는 다이브) |
| ElectroShot (정지) | ON | ON | OFF | 제자리 |

- 공통: BakeRot ON(facing 코드 소유) + 공격 전진 보존은 BakeXZ OFF.
- **고도 레일 vs 개구리-Y 경계(헌법):** 호버 Y는 정체성(정당)이나 **클립 펄스에서 와야** 한다. 코드는 **등고도 레일만 유지**(clamp/maintain), **sin 진동을 발명하지 않는다.** 고도 유지 = OK / 코드가 위아래로 흔듦 = 위반. ([[feedback_pounce_grammar]]의 "위로 뜨는 Y가 개구리의 정체"의 비행판.)

---

## 7. 클립 킷 가용성

anim_read 인벤토리 기준 부족 0(부유·다이브·방전 클립 보유). 단 실제 클립명·길이·컨택 프레임은 **Unity 세션 실측 선결**(정적 커브·"_RM" 이름·옛 주석 신뢰 금지 — [[feedback_measure_rootmotion_by_stepping]]).

---

## 8. ⚠️ 미검증 · 위험 플래그 (비누설 — 원문)

- **R-비행1 (최대):** 45° 부감서 수직동작이 크기변화로만 읽힘 → 다이브 하강/재상승이 납작. 그림자 앵커 + 평면 장판으로 보정하나, **"다이브가 위에서 오는 느낌"은 유저 플레이 게이트**(Frank 텀블 리타겟과 같은 렌즈 문제, 리그 아님).
- **R-비행2:** 클립별 BakeY 모순(호버 ON / 다이브 OFF) 미분리 시 "안 내려가는 다이브" or "떠다니는 호버".
- **R-전격1:** 전격 = 유일 신규 셰이더 → **착공 전 비용 선언**(§4). 틀 먼저 닫고 2차 레이어 권장.
- **R-색1:** 전격 파랑 vs 레드오렌지 위협 캐넌 = 레이어 분리(§4).
- **R-흐름1:** 속도감·다이브 타이밍·"한 동작만"은 정지 프레임으로 검증 불가 → **구조**(상태 그래프·CUT·회전·BakeY)는 설계로 확정 가능, **느낌**은 유저 플레이 게이트.

---

## 9. Lacercharias (롤) — 재지정 대비 골격

진짜 Roll 상태머신(anim_read). 구르며 접근 → 펼침 → 공격 → 재차 롤. 부유 틀과 무관한 별도 신규 틀. 유저가 Kupolojuve 대신/다음으로 지정 시 별도 설계.

---

## 10. Unity 세션 인계 — 다음 단계

1. **클립 실측 선결**(Animator 스텝, 정적 커브 거짓): FlyStationary(고도 드리프트?), FlyForward_RM(XZ 이동·BakeY 필요?), DashSpikeAttack_RM(하강 Y·전진 거리·임팩트 프레임), ElectroShot(발사 정점 norm·단발/3연).
2. 클립별 BakeY 분리 임포트(호버 ON / 다이브 OFF). 임포터 정규화-time 함정 주의.
3. 컨트롤러 빌드 — HoverIdle 허브, Dive 3분할, Electro 2분할, 전이 CUT dur0, HoverIdle↔Reposition만 블렌드 후보. (BlendTree 파라미터 = Float 필수, [[feedback_blendtree_param_must_be_float]].)
4. 그림자 앵커 인프라(ThreatArc 약알파 원 재사용) — Carcinoptera와 공유.
5. 드라이버 KupolojuveHarasser.cs(거리 분기·고도 레일·예측 조준·토큰 가드).
6. AnimationEvent 주입(정규화 time·루트 co-location).
7. 전격 셰이더 = 2차(유저 판정 후).
8. 유저 플레이 게이트(R-비행1·R-흐름1).
