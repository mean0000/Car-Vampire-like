---
name: katana-unequip-upperbody-layer
description: 달리기 진입 시 상체만 칼 납도(Unequip) 코스메틱 오버레이 레이어 — KatanaMelee.controller layer 1, Action태그 없음
metadata:
  type: project
---

달리기 시작 시 "상체로 칼 집어넣기" 순수 이동 연출(2026-06-21). combat 액션 아님 — 액션 레일(IsActionPlaying=layer0 Action태그)과 완전 무관, busy/공격 영향 0.

**KatanaMelee.controller (`Assets/_Project/Animation/`) layer 1 "UpperUnequip":**
- mask=`UpperBody.mask`(guid 802696c7d6b1df644ac81db43b54eddd, fileID 31900000) — 트윈스틱서 만들었다 지운 걸 git(5b4fdc5a7)서 그대로 재생성. 휴머노이드 muscle mask(m_Elements:[], m_Mask 13비트): **ON=Body/Head/양Arm/양Fingers/양HandIK · OFF=Root/양Leg/양FootIK**. (m_Mask 문자열 그대로 재사용)
- blend=**Override**(m_BlendingMode 0), **m_DefaultWeight 0**(평시 안 보임)
- 상태: `UB_Empty`(default, m_Motion fileID 0 빈모션) + `Unequip`(클립 Frank_RPG_Katana_Unequip, **tag 비어있음=Action 없음**)
- AnyState→Unequip(trigger `Unequip` mode1, dur0 CUT, toSelf off) · Unequip→UB_Empty(exitTime0.9 hasExit dur0)
- fileID 대역=2220000000000000000~004 (신규 안 겹침). 트리거 param `Unequip`(type9) 추가.

**드라이버 계약(코드가 배선):** 레이어 **인덱스 1**, 트리거 `Unequip`, 상태 `Unequip`/`UB_Empty`, 이벤트 `OnWeaponSheathed`. weight 게이팅=트윈스틱 동형으로 코드가 SetLayerWeight(1, IsName("Unequip")?1:0). IsName("Unequip")로 판별 OK(layer 1 stateInfo).

**Unequip 클립 실측(`Frank_Slash_Pack/.../In_Place/Frank_RPG_Katana_Unequip.FBX`):**
- guid 842d4c82334a13844910705479c89dcb, internalID 1827226128182048838, type3
- **2.833s @ 60fps, 170프레임**(Frank 콤보 30fps과 다름! Unequip은 60). loop=0/loopTime=0 이미 비루프(meta 손 안댐).
- 휴머노이드 muscle 커브(transform 아님). RightHandT.y(IK손 높이)로 납도 타이밍 판독: 0.15 정점→0.55 재상승→**n0.80서 -0.05, n0.85부터 -0.07 평탄(손이 허리 안착)**. RightHandT.x·Right Arm Down-Up도 n0.85부터 평탄. ★칼이 손 떠나는=손이 칼집 안착 직전 = **추정 n0.82(t2.323s)**.
- AnimationEvent `OnWeaponSheathed`를 meta events에 직접 추가(`time: 0.82`=정규화, 임포터가 ×2.833=2.323s 확인). in-place 변형(events:[]만 교체)이라 재구성 아님=안전(memory frank_fbx_animevent_gotchas 패턴).

**재발도(stop) 클립 없음:** 팩에 Equip/Draw 클립 부재(Unequip/Unequip_Idle/Unequip_Run만). 멈출 때 메시 다시 켜기는 코드가 OnWeaponDrawn 없이 처리.

검증: 디스크 import PASS — layers 2, layer1 weight0/Override/mask=UpperBody, Unequip tag 빈값(Action 없음 확인), 이벤트 1개 n0.820, loop False. (Frank rig-mismatch/translation-DOF 경고=비치명 표준). ★연출 느낌·납도 타이밍 정확도(n0.82 추정)=유저 빌드 판정.
