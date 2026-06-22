---
name: katana-skill01-rmb-wiring
description: RMB 스킬(Skill01) 무동작 수정 = 컨트롤러에 Skill01 param/state/전이 + 클립 이벤트 부재. 코드(KatanaWeapon)는 이미 완비. 클립 60fps 144f/2.4s 확정. 06-22.
metadata:
  type: project
---

# 카타나 RMB 스킬(Skill01) 와이어링 (2026-06-22)

## 증상→원인 (디스크 검증)
RMB 무동작. 드라이버 `PlayerAnimatorDriver.TriggerSkill()`=`SetTrigger("Skill01")` 쏘는데 **컨트롤러(KatanaMelee)에 Skill01 파라미터·상태·전이 없음** → Unity가 무음 무시. 추가로 Skill01 FBX 클립에 AnimationEvent 0개.
- ★메모리 정정: [[project_vexa_humanoid_katana_base]] Step6(06-20)가 기록한 Skill01 상태(`1114...` 대역)는 **이후 세션이 덮어씀** — `1114...`는 현재 **DashAttack**, `1112...`는 Counter. Skill01은 소실되어 있었음(Step6 메모리 stale).
- Counter 클립 guid=`a1926410b13a7ee47bb907df03589011`(Skill02 `3a3b771ce...` 아님!), DashAttack도 동일 guid 공유(타세션 작업, 미터치).

## ★코드는 이미 완비 (KatanaWeapon.cs, 오케스트레이터 소유, 미터치)
애니 와이어링만 빠졌었음 — 코드 경로 전부 존재:
- `BeginSkill()`: `_skilling=true`+`TriggerSkill()`+`BeginAction()`(레일 진입유예)+`_skillFallbackTimer=skillSet.timing.maxDuration`(3.5s 워치독).
- `OnComboEnd()`: `if(_skilling){EndSkill();return;}` → 클립 OnComboEnd 이벤트가 busy 해제.
- `OnHitFrame`: `if(_skilling){DoSkillHit();}` → OnAttackHit 이벤트가 데미지+VFX+SFX(SkillSet).
- 자가치유 `!IsBusy && _skilling` → EndSkill(유저가 언급한 line~195 가드, _skilling 이미 포함).
- 발동 조건 `input.secondaryDown && skillSet!=null && !IsBusy && _skillCdTimer<=0`. SkillSet SO=`Katana_Cham_Skill01Set.asset`(timing=cooldown0/maxDuration3.5, hit=range3.5/arc80/dmg20/kb7). SO는 타이밍을 코드로 구동 안 함(이벤트 구동) — 중복 이벤트 추가 안전.

## ★Skill01 클립 = `Frank_RPG_Katana_S1_Skill01.FBX` (모호성 0)
guid `e03c0e64a065f3d46a833ec7f6cfbb45`, internalID `1827226128182048838`, type 3(FBX 서브클립). 이름·SO 짝(Katana_Cham_Skill01Set)으로 명확. **60fps 144f = 2.4s** 확정(Step6 메모리 맞음).
- ★함정: SerializedObject로 `m_ClipAnimations` 만지면 frame bounds 깨져 길이 4.9536s로 팽창(전체 take). **revert=in-place 패턴(override 배열 읽어 `.events`만 set→재대입→SaveAndReimport)으로 길이 2.4s 복구**. SerializedObject로 loopBlend* 만지지 마라.
- 블레이드속도 peak 실측(skin FBX `Frank_Katana_Skin.FBX` Weapon_Blade world속도)=**51.5 m/s @ frame80(norm0.556)** 단일 강타. Step6 값과 일치.
- 이벤트(API time=NORMALIZED, 임포터가 ×길이): `OnAttackHit`@**0.556**(int0)·`OnComboEnd`@**0.86**. 런타임 검증=1.3344s·2.0640s. bake=loopBlendOrientation1/Y1/XZ0(BakeRot/Y ON·XZ OFF 전진보존), len 2.4 불변.

## 컨트롤러 추가 (직접 YAML Edit, [[project_vexa_humanoid_katana_base]] 패턴)
- ①`Skill01` trigger 파라미터(type9, DashAttack 다음) ②`Skill01` 상태(fileID **`1115...`** 신규대역=Counter1112/DashAttack1114 충돌회피, tag **Action**, motion=Skill01 FBX) ③AnyState→Skill01(`1115...004`, `[If Skill01]`, **CUT** hasExit0/dur0/self0) ④Skill01→Locomotion(`1115...003`, hasExit1/exitTime**0.88**/dur0.1).
- AnyState 우선순위=Dash>Counter>DashAttack>**Skill01**>Combo1(전부 별개 트리거, 1프레임 충돌0).
- 검증=param/state/clip(len2.4·human·이벤트2)/전이 전부 PASS·broken-dst0·콘솔 에러0.

## ★소프트락 회피 3중 (Combo2 사고 재발0)
1. OnComboEnd@**0.86** → EndSkill(클립구동 1차). 2. 시각 exit@0.88(>0.86, busy 해제 후 복귀). 3. 워치독3.5s+자가치유 `!IsBusy&&_skilling`(이벤트 실패 백스톱). ★Combo2는 1~3 전부 무력(이벤트0·_step 워치독 없음)이었던 게 차이. **이벤트<exit(0.86<0.88) 원칙 = 스킬류 캐넌**(Step6 Counter 0.92>0.90 아슬함을 고침).

## 미검증 (유저 빌드 게이트)
RMB 발동감·타격(norm0.556) 타이밍·모션 느낌·VFX 스폰. 유저 플레이로만 확정.
[[project_vexa_humanoid_katana_base]] [[project_katana_combo2_event_gap]] [[project_frank_fbx_animevent_gotchas]]
