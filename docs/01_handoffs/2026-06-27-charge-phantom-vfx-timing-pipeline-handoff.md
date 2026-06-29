# 2026-06-27 핸드오프 — 차징 팬텀 VFX(SO화·프레임타이밍) + 공격-VFX 파이프라인 정석 + 카메라/연출 방향 미결

> 06-25 `charge-phantom-skill-handoff` 이어서. 어제 만든 `ChargePhantomEmitter`(Skill01 차징 팬텀)에 VFX를 *제대로* 얹는 작업 → SO화·인스펙터 구분·프레임 정확 타이밍까지. + 파이프라인 정석 리서치 + 방향(카메라/연출) 탐색.

---

## TL;DR (현재 상태)
- **차징 팬텀 슬래시 VFX 시스템 = 구현 완료, 게이트(Stab+Codex) 다회 통과, 미커밋.** 유저 Play 튜닝 대기.
- 슬래시는 이제 **각 팬텀의 *플립북 슬래시 프레임*(`slashFrame`)에 발동**(스폰 즉시 ❌) — ComboAttackSet이 AnimationEvent로 띄우는 것의 플립북판.
- **공격-VFX 파이프라인 "정석"을 리서치해 확정**(Codex+웹 강수렴) — 미래 무기/공격 양산용. 아래 §4.
- **방향 미결:** MOTORSLICE 계기로 "탑다운 → 카메라 틸트(배경+연출)" 탐색 중. 장르 피벗 ❌, *카메라/연출 업그레이드* 쪽. 유저 판정 대기. §5.

---

## 1) ChargePhantomSet SO 시스템 (구현·게이트 통과)

어제 인라인 `flipbooks[]`였던 걸 **SO로 분리**(SkillSet/ComboAttackSet 동형). 유저 요청: 인스펙터 구분·내가 순서 지정·SO화·정합성.

**신규/변경 파일:**
- `Assets/_Project/Scripts/Player/ChargePhantomSet.cs` — SO. `[CreateAssetMenu "ZombieCrush/Charge Phantom Set"]`. 중첩 블록: `phantoms[]`(PhantomAnim) · `emission` · `ghost` · `slashVfx`.
- `Assets/_Project/Scripts/Player/Editor/ChargePhantomAnimDrawer.cs` — PhantomAnim 배열 원소를 `name`으로 헤더 표시(Element N ❌).
- `Assets/_Project/Scripts/Player/ChargePhantomEmitter.cs` — 재작성. 씬참조(weapon/ghostShader) + `phantomSet` SO만. 공유 SO 비변형(런타임 로컬 사본·라이브 읽기).
- `Assets/_Project/VFX/Katana_Cham_Skill01PhantomSet.asset` — 마이그레이션(어제 씬 값 1:1) + 이후 튜닝.
- `Assets/_Project/Scripts/Player/KatanaWeapon.cs` — `_skillHitSeq`+`public int SkillHitSeq`(DoSkillHit 시 증가) 추가. *(onCut 경로용 — 현재 off)*

**PhantomAnim(공격 1개) 노브:** `name` · `enabled`(boolean 선택) · `frames[]`(플립북) · **`slashFrame`(슬래시 발동 프레임)** · `slashEulerOffset`(각도) · `slashPosOffset` · `slashScale`.
**emission:** `phantomCount` · `emissionOrder`(직접 순서, 비우면 순환=랜덤 제거) · windup/lifetime/travel/slashFraction/scatter.
**slashVfx:** `prefab`(=VFX_Slash_Earth) · **`onCharge`**(차징 중 팬텀별, 현재 on) · **`onCut`**(실제 베기 1발, 현재 off) · cut용 delay/euler/pos/scale · 공용 lifetime/speed/parentToWeapon.

**게이트:** 매 변경마다 Stab(Sonnet)+Codex 병렬. 누적 발견·수정(전부 반영): SO 비변형·직렬화 마이그레이션·OnEnable 리싱크·발동 1회 가드·프레임 경계 클램프·만료 캐치올·LateUpdate null 가드·죽은 주석 정리. **Critical/High 0로 마감.**

---

## 2) 슬래시 VFX 타이밍 — 결론 = `slashFrame`(프레임 정확)

★**여러 턴 헤맨 핵심 오해:** 유저의 "벨 때"를 내가 *실제 캐릭 베기(onCut/DoSkillHit)*로 잡았으나, 실제 의도는 **각 팬텀 *모션*의 슬래시 순간**. (메모리 [[feedback_slash_vfx_stamp_not_trail]]에 박음.)

- **발동:** `onCharge` 시, 팬텀이 자기 플립북의 **`slashFrame`(기본 4)에 도달**하면 1회, *팬텀 현재 위치*에서. → 스폰 즉시(어색) ❌. 공격(Attack01/02/03)별 독립.
- **각도:** per-phantom **`slashEulerOffset`**(이미 있음, 라이브). ★주의: `VFX_Slash_Earth`는 일부가 **billboard 파티클(스파클 등)이라 회전 무시·카메라 향함** — 메인 메시(PS_VFX_Slash, Mesh모드)만 각도 먹음.
- **VFX = `VFX_Slash_Earth`**(Vefects, 시안 아님 흙). 절차 SlashArc 셰이더 시도는 **유저 기각("에셋 쓰자")→삭제.**

---

## 3) 다음 = 유저 손 (Play 튜닝)
시스템 단단함. 남은 건 **순수 비주얼 튜닝 = 유저 영역**(나는 못 봄 — play/캡처 막힘):
1. 각 공격 `Slash Frame`(0~9)을 베는 순간에 맞춤.
2. 각 공격 `Slash Euler Offset`으로 호 각도 정합.
3. 과하면 billboard 스파클 정리 or 다른 Vefects 원소(Electric/Ice 등)로 스왑.

---

## 4) ★공격-VFX 파이프라인 "정석" (Codex + 웹 강수렴) — 미래 양산용

유저 "한땀한땀 반복하면 1년에 무기 하나" → 무기/공격 추가가 *에셋만*으로 끝나는 표준 파이프라인 리서치. **우리가 하던 "공격마다 euler 손튜닝"이 업계 안티패턴.** 4기둥:

1. **소켓 기반 스폰** — 무기에 명명 소켓(BladeTip/BladeBase/SlashPlane) + VFX 프리팹 축 규약 고정(+X=궤적). **무기당 소켓 1번**만 맞춤, 공격마다 euler 튜닝 ❌. *(Unreal Particle Notify가 소켓 위치/방향 상속 — 출처 검증.)*
2. **의미 큐 이벤트** — 애니 이벤트가 프리팹 직접 스폰 ❌ → `AttackCue("slash.main")` 의미 ID만 쏘고 데이터가 해석. *(Unreal AnimNotify + GAS GameplayCue의 유니티판. 우리 `OnAttackHit`=메서드명 결합 트랩.)*
3. **데이터주도 SO 층** — `Weapon → AttackSequence → AttackStep + CueSet` 분리. 무기 추가=에셋만, 코드 0. *(Unity 공식 SO 권장 · GDC 오버워치 Statescript "new heroes ship with no new code".)*
4. **★에디터 프리뷰 툴** — 클립 스크럽하며 VFX·소켓·히트박스를 *에디터에서 보는* 창. **Play 안 켜고** 튜닝 → *우리 최대 병목(못 봄) 해결.*

**마이그레이션 = strangler**(전투 안 멈춤): 소켓 → 큐 라우터 → SO 분리 → 2번째 무기 전 프리뷰 툴.
**출처:** Epic AnimNotify · Unity SO architecture · GDC Overwatch Statescript · Data-Driven Game Object System(paper) · Unity Timeline VFX Control.
> ⚠️ 이 리서치는 deep-research 워크플로로 했는데 **102에이전트·4.66M토큰 써서 세션한도 폭발** → [[feedback_no_heavy_automode]]. 앞으론 메인루프+타겟 Codex만.

**미착수.** 착수 시 §4 순서대로(소켓·프리뷰 먼저 = 레버리지 최대).

---

## 5) 방향 미결 — 카메라/연출/배경 (MOTORSLICE 계기)

유저가 **MOTORSLICE**(3D 파쿠르 핵앤슬래시, 거대 brutalist 구조물, 동적 카메라)를 보고 "이쪽으로 틀까(탑다운 아니라)". 파보니 **진짜 끌림 = 파쿠르 장르가 아니라 *배경(메가구조물 월드룩) + 연출(시네마틱 카메라)*.**

**오케스트레이터 판정(P0, 솔직 교차):**
- **핵심 통찰:** 뱀서(호드=카메라 빼야 함) ↔ 간지베기(카메라 당겨야 함)가 *카메라*를 두고 싸움. 탑다운이 **배경 납작 + 연출 0**으로 둘 다 죽임. → *장르*가 아니라 *카메라* 문제.
- **굿뉴스:** 원하는 "웅장+시네마틱"은 *배경+카메라*에서 나옴(오르는 것 X). = **장르 피벗 없이** 얻을 수 있고, 마침 **유저 강점(절차 빌딩·TA/카메라/셰이더).**
- **갈림(비용 천지차):** 메가구조물을 **"보는"**(백드롭+틸트 카메라+연출 비트) = 솔로 가능·싸다. vs **"오르는"**(3D 수직 트래버설/캐릭터액션) = 솔로 비현실(맞춤 애니·캐릭터컨트롤·카메라멀미·수직레벨, 자금 있는 MOTORSLICE도 조작 까임). → **"오르는"은 말림.**
- **추천:** *피벗 전에* **카메라 틸트 하루 실험** — 지금 슬라이스 부감을 기울이고 메가타워 백드롭 세워 베기 캡처. 배경 솟고 연출 살면 = 카메라가 답(피벗 0).

**미결정.** 유저 "보는 vs 오르는" 선택 대기. 연동 메모리 후보: [[project_2026_06_25_spectacle_direction]](간지 최상위) · [[project_2026_06_21_engine_stay_unity_ruiner_ref]](카메라 북극성).

---

## 6) 프로세스 교훈
- **내 "벨 때" 오해가 다수 턴 소모** — 유저 의도(팬텀 모션 순간)를 늦게 잡음. 비주얼 의도는 더 일찍 되물을 것.
- **★진짜 병목 = 내가 결과를 못 봄**(play-mode 막힘·MCP 캡처 실패) → 시각 조정 하나하나가 유저 왕복. → **역할 분리 재확인: 시스템/코드/배선=나, 손맛/각도/타이밍 미세조정=유저(노브 다 깔아줌).** §4 프리뷰 툴이 이걸 구조적으로 해결.
- **무거운 오토모드 금지** [[feedback_no_heavy_automode]].

---

## 파일 / 커밋
- **전부 미커밋.** 신규: ChargePhantomSet.cs(+meta) · ChargePhantomAnimDrawer.cs(+Editor folder) · Katana_Cham_Skill01PhantomSet.asset(+meta). 변경: ChargePhantomEmitter.cs(재작성) · KatanaWeapon.cs(SkillHitSeq). 삭제(절차 시도): SlashArcVfx.cs · M_KatanaCutSlash.mat · VFX_KatanaCutSlash.prefab.
- 씬 `SlashLab_Closeup.unity`: `Visual`의 ChargePhantomEmitter에 phantomSet 배선됨(MCP, 저장 확인).
- 메모리 신규: [[feedback_slash_vfx_stamp_not_trail]] · [[feedback_no_heavy_automode]].
