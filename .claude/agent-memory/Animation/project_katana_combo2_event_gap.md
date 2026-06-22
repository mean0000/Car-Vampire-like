---
name: katana-combo2-event-gap
description: "콤보가 하나만 나가고 멈춤"의 원인 = Combo2 클립(raw FBX subclip)에 AnimationEvent 0개 → OnComboEnd 미발화 → ComboStep 고착 → 소프트락. 리타이머가 Combo1/3만 손대 Combo2가 사각지대.
metadata:
  type: project
---

# 카타나 콤보 "하나만 나가고 멈춤" — Combo2 이벤트 누락 (2026-06-22)

## 증상→원인 (디스크 검증)
유저 "공격이 하나만 나가고 만다". 원인 = **Combo2 클립에 AnimationEvent가 0개**.
- 컨트롤러(KatanaMelee, layer0)는 건강: Locomotion→Combo1[ComboStep==1]→Combo2[==2]→Combo3[==3], 각 Combo→Locomotion[==0]. 전이 완비. Action 태그 layer0에 정상(드라이버 `GetCurrentAnimatorStateInfo(0)` 정합).
- Combo1=`S1_Combo01_01_Retimed.anim`(3이벤트), Combo3=`S1_Combo01_03_Retimed.anim`(3이벤트) — [[project_katana_combo_retimer]]가 만든 standalone .anim.
- **Combo2=raw FBX subclip `Frank_RPG_Katana_S1_Combo01_02`(Root_Motion FBX, guid ebd5d44d...) — 리타이머가 안 건드림 → 이벤트 0.**
- 결과: Combo1 정상(1→2 캔슬 advance 됨). **Combo2 진입 후 OnComboEnd가 안 떠 ComboStep이 2로 고착** → Combo2→Locomotion 전이([==0]) 영영 불성립 → 비루프 클립이 마지막 프레임서 freeze. Combo2가 `Action` 태그라 `IsActionPlaying`=true 영구 → `IsBusy` 영구 → **이동·재공격 전부 잠김(소프트락)**. KatanaWeapon 자가치유(line~195 `!IsBusy && _step>0`)는 IsBusy가 안 풀려 발화 못 함.

## 이번 사고와 git reset의 관계
미커밋 M 파일(KatanaMelee.controller / PlayerAnimatorDriver / PlayerInputState)은 **로코모션 Speed티어·facing·DashAttack·Unequip 코스메틱 레이어**만 건드림([[project_katana_locomotion_speed_tiers]]·[[project_katana_unequip_upperbody_layer]]) — 콤보 로직과 무관. 콤보 상태/전이는 커밋본과 동일. 즉 **reset 부작용 아님**. Combo2 이벤트 누락은 그 이전부터 잠복(리타이머가 Combo1/3만 standalone화하며 Combo2 원본 FBX 이벤트가 메타에 없었음 — vexa_humanoid_katana_base가 기록한 "int1/2/3 norm 0.367/0.176/0.206" 중 int2가 사라진 상태).

## 수정 (디스크 PASS)
Combo2 FBX importer에 in-place로 3이벤트 추가([[project_frank_fbx_animevent_gotchas]] 안전패턴: `imp.clipAnimations` 읽어 기존원소 `.events`만 set→재대입→SaveAndReimport. `new[]{}` 재구성 금지=길이팽창). **time=NORMALIZED** 기입:
- OnAttackHit norm 0.200 (int 2) — 블레이드속도 peak 실측 t=0.2267(weapon `Weapon_Sword` 본).
- OnComboWindow norm 0.344 (Combo1의 hit→window 0.117s 갭 정합).
- OnComboEnd norm 0.910 (Combo1 0.909·Combo3 0.917 정합).
검증: 길이 1.1333 불변, 콘솔 에러0. 3 Combo 상태 전부 Hit/Window/End 보유 확인. ★rig "bone length mis-match" 경고는 Frank 표준 translation-DOF discard(비치명, [[project_frank_slash_retarget_gate]]).

## 교훈 (재발 방지)
- **리타이머/클립 교체는 콤보 N단 전부를 균일하게 처리하라.** Combo1·Combo3만 standalone화하고 Combo2를 raw FBX로 남기면 이벤트 일관성이 깨진다. 새 무기/콤보 추가 시 **"모든 단에 OnComboEnd 있나"가 1순위 체크** — 없으면 그 단이 소프트락 지점.
- 진단 골든질문: "막히는 단의 클립에 OnComboEnd 이벤트가 있나?" 없으면 ComboStep 고착=소프트락.

## 미해결 (별개·플레이 게이트)
- **유저 플레이 검증 대기:** 실제로 1→2→3 콤보가 끊김없이 도는지, Combo2 hit 타이밍(norm 0.2) 손맛. 정지 캡처로는 시퀀스 골격만 PASS.
- **별개 잠복(이번 증상 아님):** 드라이버 `TriggerSkill()`이 `Skill01` 트리거를 쏘지만 컨트롤러에 Skill01 파라미터·Any→Skill01 전이 **없음**(파라미터=Counter/DashAttack/Unequip뿐). RMB 스킬이 무동작. 콤보와 무관해 미수정 — 유저 요청 시 별도.
