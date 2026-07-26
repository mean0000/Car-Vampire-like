---
name: katana-whirlwind-eskill
description: 카타나 E스킬 "전진 선풍참(Whirlwind)" Animator 배선 — ★핵심 발견=Bake Rotation ON이 루트-저작 스핀을 BODY 포즈로 옮겨 제자리 회전 실현(드라이버가 deltaRotation 폐기·비주얼 강제 facing해도 스핀 보임). 클립=2Hand WhirlWind(4회전 이동 팽이)라 단발 스핀+잔심 트림 불가 → exitTime로 finish 정의. 컨트롤러=KatanaMelee. 2026-07-05.
metadata:
  type: project
---

# 카타나 E스킬 전진 선풍참(Whirlwind) — Animator 배선 (2026-07-05)

목표 리듬 "확(코드 2.8m 순간전진)—촥(360 회전 베기 1회)—딱(잔심 스냅정지)". 회전+잔심이 애니 몫. 컨트롤러 = `Assets/_Project/Animations/KatanaMelee.controller`.

## ★★핵심 발견 — Bake Rotation ON = 루트 스핀을 BODY 포즈로 이관(제자리 회전 실현)
플레이어 드라이버(`PlayerAnimatorDriver`)는 **deltaRotation을 적용 안 하고**(회전=코드 소유, transform.rotation을 매 프레임 `_lockedFace`로 강제) OnAnimatorMove서 `ApplyRootStep(deltaPosition)`만 넘긴다. 스핀이 **RootQ(루트 노드)** 에 저작돼 있으면 → deltaRotation 폐기 + 강제 facing에 의해 **스핀이 완전 소실**(캐릭터가 안 돎). 
- **수정 = Bake Rotation Into Pose ON**(meta `loopBlendOrientation:1`). 검증(SampleAnimation on Frank_Katana_Skin 인스턴스): 바디본 spine_03이 **월드에서 1440° 스윕**하는데 **인스턴스 루트 transform yaw는 0.0° 고정**. 즉 스핀이 body 포즈로 옮겨져 캐릭터는 제자리서 몸통이 회전, 루트 facing은 코드 소유 유지. 회전 공격(선풍참·대회전)엔 **BakeRot ON 필수**(공격 일반 컨벤션과 동일이나 여기선 "스핀 가시성"이 사활).
- Bake Y ON(`loopBlendPositionY:1`)·Bake XZ ON(`loopBlendPositionXZ:1`)=grounded+제자리. 검증: 인스턴스 루트 maxXZ=0.0000·maxY=0.0000(전 스핀창). ∴ ApplyRootStep≈0 → 코드 AddGlide(2.8m)가 전진 단독 소유(이중이동0). ★XZ ON=in-place는 공격 컨벤션(XZ OFF=전진보존)과 **반대** — 선풍참은 코드가 전진 소유하므로 클립 이동 제거.

## ★클립 미스매치 (정직 보고 사항)
사용 클립 = `Assets/Frank_Slash_Pack/.../Frank_SlashPack_2Handed/FBX_Animation/Root_Motion/Frank_RPG_2Hand_Skill01_WhirlWind.FBX`(guid 191d0680..., 서브클립 fileID 1827226128182048838, 60fps **177f/2.95s**). Katana(1H) 팩엔 스핀 클립 **없음** → 2Hand WhirlWind 재활용(Frank 공유 휴머노이드라 리타겟됨, 2H 그립이나 카타나 양손 스핀은 자연스러움).
- ★**클립 실측 = 4회전 이동 팽이**: 총 rootYaw **1440°**(4바퀴)·net 전진 **4.013m**. 구조: 코일 윈드업(f0-48, 0.8s, rootY 0.93→0.54 크라우치) → 고속 전방 휘돌기(f48-108, ~1s, peak 2170°/s) → 감속/착지 → 상승 정지포즈(f138-177). **잔심(정지 포즈)은 4바퀴 다 돈 뒤 클립 맨끝에만 존재** → 연속 트림으로 "단발 스핀+우아한 잔심" **분리 불가**(중간을 건너뛸 수 없음).
- 유저 브리프 "360 1회·연속 팽이 금지"와 클립(4회전 팽이)이 근본 충돌. 브리프 item6("정지포즈 없으면 상태 종료 타이밍으로 뚝 끊기는 마무리")이 이 케이스를 축복 → **exitTime로 finish 정의** 채택.

## 배선 (구현 완료·구조 PASS)
- **파라미터**: `Whirlwind`(Trigger) 신규 추가.
- **상태** `Whirlwind`(Base Layer=layer0, 전신): tag **Action**(필수 — busy 레일·baseOwnsUpperBody 웨이트0=전신 표시 자동편입, 드라이버 무수정), speed **1.5**, WD false, cycleOffset 0, motion=WhirlWind 클립.
- **AnyState→Whirlwind**: `[Whirlwind If]` **CUT**(dur0·hasExitTime false·self false). (E=TriggerAction→SetTrigger, TriggerAction이 Dash bool 리셋해 AnyState 경쟁 없음.)
- **Whirlwind→Locomotion**: **hasExitTime true·exitTime 0.384**(=f68, 바디가 코일 후 정방향으로 1회전 복귀=finish)·dur **0.08**(fixed). exitTime가 "몇 바퀴/finish" 노브.
- **이벤트**(meta 직접 편집, normalized time·Gotcha1): `OnAttackHit`(int0)@**norm0.305**(f54=휘돌기 첫 블레이드 패스·빠른타이밍)·`OnComboEnd`@**norm0.35**(f62, exit 0.384보다 앞=LESSON A 보장발화). 검증: 런타임 OnAttackHit@0.90s·OnComboEnd@1.03s, 길이 2.95 불변(팽창0).
- 코드 흐름: E→`TryBeginInstantAction(_skillRt)`→`BeginActionSlot`→`TriggerAction("Whirlwind")` + `AddGlide(2.8m)`. `_activeAction`=whirlwind → OnAttackHit=`DoActionHit`(range3.2·arc180·dmg5)·OnComboEnd=`EndAction`. busy=Action태그 자동.

## ★인터페이스 갭 (오케스트레이터 몫 — 미해결)
`Katana_Whirlwind.asset`(WeaponActionSet, Assets/_Project/Data/Combat/)은 이미 존재·완비: triggerName **Whirlwind**(내 트리거와 일치)·lunge.distance **2.8**·charge off(즉발)·hit range3.2/arc180/dmg5·cooldown6. **하지만 씬 `_AtomLab_OneCut.unity`의 KatanaWeapon.skillAction = {fileID:0}(미할당)** → E 누르면 무동작. **finish=skillAction 슬롯에 Katana_Whirlwind.asset 할당**(인스펙터 드래그 1회). 입력배선=게임플레이 경계라 내가 씬 편집 안 하고 보고만(경계 규율).

## 노브 (유저 플레이 판정 — 모션 느낌 못 봄)
- **state speed**(1.5, 범위 1.2~1.8): 스냅/윈드업 길이 주 레버. 윈드업이 재생창의 71%(코일 0.8s)라 speed가 사활 — 느리면 올려라. speed1.5서 윈드업≈0.53s·hit@0.60s·finish@0.77s.
- **Whirlwind→Locomotion exitTime**(0.384=단발 휘돌기→정방향 finish. **↑0.95면 풀 4회전 플러리시+내장 잔심**): 회전수/finish 레버. ★OnComboEnd와 페어 — 프리셋 바꾸면 OnComboEnd도(0.35↔0.90) 옮겨라(안 옮기면 풀플러리시서 busy 조기해제·이동누수).
- **exit blend dur**(0.08: 0=하드컷 "딱"↑ / 0.1=소프트 안착).
- OnAttackHit(0.305 첫 패스)·OnComboEnd(0.35).
- ★A/B 프리셋: 단발(현행 exit0.384) vs 풀플러리시(exit0.95+OnComboEnd0.90) — exitTime/이벤트 몇 값으로 스위치, 재임포트 불요(클립 무트림).

## 측정법 메모 (재사용)
- 스핀 카운트: RootQ*Vector3.forward를 XZ heading으로, DeltaAngle 언랩(euler.y는 바디 틸트서 김벌 노이즈). 
- 스핀-가시성: Frank_Katana_Skin 인스턴스+Animator(avatar=Frank_Katana_SkinAvatar)+`clip.SampleAnimation(inst,t)` 프레임마다 → 바디본 월드 yaw vs 인스턴스 루트 yaw 대조. **SampleAnimation은 이 클립서 작동**(LESSON B "stale"는 비휴머노이드 본 월드속도 한정 — 휴머노이드 본 회전 재구성은 신뢰, [[project_player_lowerbody_loco_override]] 07-04 확인과 일치).
- ★MCP RunCommand success:false=Frank 리그 경고(벤나인, reimport시만) — `[Log]` 라인 읽어라([[project_frank_fbx_animevent_gotchas]]).
