---
name: katana-skill01-rmb-wiring
description: 카타나 RMB Skill01 = 차징-게이더링홀드-베기. 윈드업 0→49(0.340)·홀드=게이더링 크로스페이드 셀프루프 47↔49(동결 아님)·베기 49→. 컨트롤러=Assets/_Project/Animations/KatanaMelee.controller. 06-26 갱신.
metadata:
  type: project
---

# 카타나 RMB 스킬(Skill01) = 차징-게이더링홀드-베기 (2026-06-26)

RMB 홀드=윈드업 재생 후 **게이더링(기 모으는) 미세 루프**, RMB 릴리스=베기. **컨트롤러 = `Assets/_Project/Animations/KatanaMelee.controller`**(★`Animations` 복수). AnimatorController API(MCP RunCommand)로 재구축(YAML 손편집보다 안전·멱등). 전부 Base Layer 풀바디, tag Action, cycleOffset 0, motion=Skill01 FBX.

## 상태 (frame 경계: 윈드업끝/홀드피크/베기진입 전부 **frame49 = norm 0.340**)
- **Skill01Charge**(윈드업): speed1. 0→0.340(프레임0→49) 재생. ★이름 "Skill01Charge" 고정 — 팬텀에미터가 `IsInSkillChargeWindup`=`IsName("Skill01Charge")`로 *윈드업에만* 방출.
- **Skill01Hold**(게이더링): speed**0.3**(동결 아님!). 47↔49 미세 크로스페이드 셀프루프 = "기 계속 모으는" 펄스. 에미터 정지(이름≠Charge).
- **Skill01Strike**(베기): speed1. 진입 offset0.340(프레임49)부터 → 144. hit@0.556·end@0.86 여기서 발화.

## 전이 (에디트모드 스텝핑 전수검증 [[measure-rootmotion-by-stepping]])
- AnyState→Charge: `SkillCharge` 트리거, CUT.
- Charge→Hold(settle): exitTime0.340(윈드업 49 도달), offset0.319(프레임46 게이더 바닥), dur0.08 블렌드, **interruptionSource=Source+ordered**(H-1).
- **Hold→Hold 셀프루프(게이더)**: exitTime0.340(49), offset0.319(46), dur**0.10 크로스페이드**, canTransitionToSelf, **interruptionSource=Source+ordered**(H-1). → 46↔49 젠틀 펄스(NT 0.334↔0.351). ★1프레임 CUT 루프=버즈 지터(유저금지)→크로스페이드가 버즈 제거 정석(같은클립 셀프블렌드=제0원칙 무관).
- ★**소스 전이로 베기/취소(H-1)**: 각 상태에 **명시적 source 전이** Hold→Strike·Charge→Strike(`Skill01`, offset0.340)와 Hold→Loco·Charge→Loco(`SkillCancel`) — 전부 CUT, **셀프루프/settle보다 먼저 등록**(ordered 인터럽트 우선권). AnyState→Strike/Charge도 유지(레거시·비크로스페이드).
- Strike→Loco: exitTime0.88, dur0.1.

## ★H-1 크로스페이드 셀프루프 = 트리거 삼킴 소프트락 (Stab 06-26, 필수수정)
게이더 셀프루프(crossfade 0.10s)에 **interruptionSource=None**이면, 크로스페이드 진행 중(사이클 21-42%) 릴리스(Skill01 트리거)가 **전이 못 시키고 그 프레임 평가서 소멸**(Unity 트리거 동작)→Hold 계속 돎→간헐 소프트락. **수정=① 셀프루프+settle의 interruptionSource를 None→Source(+orderedInterruption) ② 베기/취소를 AnyState 의존 말고 명시적 source 전이(Hold→Strike 등)로 추가, 셀프루프보다 먼저 등록**. ★AnyState 전이는 진행 중 크로스페이드를 못 끊을 수 있다(Stab 경고, 실측상 source 전이라야 100%). 검증=홀드 루프 전 스텝(크로스페이드 포함)서 트리거 발사→릴리스21/21·취소21/21 Strike/Loco 발화(mid-crossfade 11샘플 전부)·settle창도·펄스 무파손. interruptionSource는 *긴* 전이(블렌드/루프)에만 의미(CUT dur0은 진행이 없어 None 무관).

## ★게이더링 홀드 루트모션 드리프트 (이번 핵심 교훈) → [[transition-patterns]]
speed0 동결 홀드는 드리프트0(클립 미전진). 하지만 유저가 "동결 말고 기 모으는 루프"를 원함 → speed>0 셀프루프로 바꾸니 **매 사이클 클립 전진=루트모션 누적 드리프트 ~0.26m/s(홀드3s=0.78m 슬라이드)**. 컨트롤러 단독 수정 불가(상태별 루트모션 토글 없음). **수정=드라이버 `PlayerAnimatorDriver.OnAnimatorMove`서 현재상태 IsName("Skill01Charge")||IsName("Skill01Hold")면 ApplyRootStep 스킵**(제자리). 코드가 위치를 *만드는* 게 아니라 클립 변위를 *억제만* → 헌법 부합. 베기(Skill01Strike) 런지 2.3m는 정상 적용(실측). 이게 옵션 "윈드업 in-place(팬텀 출발점 고정)"도 동시 해결.

## 코드 변경 (이번 라운드)
- `PlayerAnimatorDriver.OnAnimatorMove`: **루트모션 게이트 추가** — Charge/Hold면 ApplyRootStep 스킵(위 드리프트 수정 + 윈드업 in-place). 유일한 .cs 변경.
- KatanaWeapon **무변**(오케스트레이터 소유, 차징 상태머신 불변): BeginCharge→TriggerSkillCharge, ReleaseCharge(fire)→BeginSkill→Skill01트리거(베기)/(불발)→TriggerSkillCancel, Cancel→TriggerSkillCancel(비대시 탈출 H-1) 전부 이미 반영됨.

## 클립 사실 (불변)
`...Root_Motion/Frank_RPG_Katana_S1_Skill01.FBX` guid `e03c0e64a065f3d46a833ec7f6cfbb45`, 144f/2.4s. 이벤트 OnAttackHit@0.556(=1.3344s, 블레이드peak f80)·OnComboEnd@0.86(=2.064s), 둘 다 0.340 뒤=베기서만 발화(데미지 윈드업서 0). meta importWarning 3.20/4.95s=stale.

## 미검증 (유저 빌드 게이트 — 모션 손맛 못 봄)
①게이더 펄스 가독/속도(speed0.3·범위46-49·블렌드0.10 다 튜너블 노브 — 너무 서틀하면 범위↓/speed↑) ②릴리스 지연 0.1s 체감 ③차징중 ROOT(in-place, Action태그 busy) ④윈드업 0→49 빠른지. [[transition-patterns]] [[measure-rootmotion-by-stepping]] [[project_vexa_humanoid_katana_base]]
