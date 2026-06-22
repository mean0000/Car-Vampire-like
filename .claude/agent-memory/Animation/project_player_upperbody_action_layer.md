---
name: player-upperbody-action-layer
description: KatanaMelee.controller 풀 트윈스틱 개편 — 상체 액션 레이어(마스크+Override) 신설, Base에 Loco+Dash만, idle 패스스루=script layer-weight 게이팅. 06-21.
metadata:
  type: project
---

# 카타나 풀 트윈스틱 — 상체 액션 레이어 (2026-06-21)

유저 확정: "공격하면서 자유롭게 이동" = 상체=공격·하체=로코모션·몸=조준향·클립 전진런지 폐기. 플레이어 self-cancel 캐넌이라 몬스터 commit-lock 비적용(이 결정이 구 '제자리 커밋'을 대체).

## 만든 에셋
- `Assets/_Project/Animation/UpperBody.mask` (guid `802696c7d6b1df644ac81db43b54eddd`, AvatarMask fileID 31900000). Humanoid 그룹 마스크(transform path 아님). **m_Mask hex = 13그룹×8자(104자)**, 각 그룹 `01000000`(on)/`00000000`(off): Root0·Body1·Head1·LeftLeg0·RightLeg0·LeftArm1·RightArm1·LeftFingers1·RightFingers1·LeftFootIK0·RightFootIK0·LeftHandIK1·RightHandIK1. 검증=`GetHumanoidBodyPartActive` 그대로 나옴.
- `Assets/_Project/Animation/KatanaMelee.controller` = **2 레이어**로 개편.

## 레이어 구조 (디스크 검증 PASS, 에러0)
- **Layer 0 = Base Layer**(mask 없음, Override, defaultWeight 0[기존값 유지, Animator가 0번은 항상 1로 취급]). 상태=Locomotion(default 2D FreeformDir)+Dash. AnyState→Dash만.
- **Layer 1 = UpperBodyAction**(mask `UpperBody`, Override, **defaultWeight 0**). 상태=**UB_None(default, 빈 motion)**+Combo1/2/3+Counter+Skill01(전부 tag `Action`). AnyState→Counter>Skill01>Combo1(우선순위 보존). 5 액션 클립 바인딩 보존.

## ★idle 패스스루 = 스크립트 layer-weight 게이팅 (Mechanism B, 검증해서 선택)
- **함정 재확인**: 마스크된 Override 레이어가 weight 1로 *빈 상태*(UB_None)에 있으면 상체가 default 포즈로 freeze(=T포즈 트랩, 유저 경고지점). 빈상태 패스스루는 weight 1에선 틀림.
- **정답**: 레이어 defaultWeight=0 → 비공격 시 Base 풀바디 로코모션이 상체까지 그대로(팔흔듦 살아있음). 액션 재생 중에만 코드가 weight=1로 올림. UB_None은 weight 0일 때만 머무니 freeze 안 보임.
- 대안 폐기: 상체 레이어에 Loco 블렌드트리 복제(double-eval·Base 댐핑과 desync).

## 루트모션 런지 폐기
- 마스크가 Root(그룹0)·다리 제외 → 액션 클립의 root/전진 커브가 상체 레이어에선 안 적용됨(위치는 하체/Base가 소유). 별도 작업 불필요. (단 코드 OnAnimatorMove가 deltaPosition 적용하면 여전히 전진 → 오케스트레이터가 _attacking 중 ApplyRootStep 중단해야 진짜 제자리. ★코드측 미검증, 핸드오프에 명시.)

## ★스크립트 계약 (오케스트레이터 PlayerAnimatorDriver 고칠 것)
1. **IsActionPlaying = layer 1 읽기** (현재 `GetCurrentAnimatorStateInfo(0)`/`GetNextAnimatorStateInfo(0)` → **인덱스 1로**). Action 태그는 layer 1 상태에만 있음.
2. **레이어 weight 제어 필요**: `SetLayerWeight(1, IsActionPlaying ? 1 : 0)`. 즉발 위해 하드 1/0 권장(블렌드 페이드는 액션끝 dur0.15 전이와 코디 필요). BeginAction 진입 유예 동안에도 1로 올려야 진입 1프레임 갭서 상체 안 죽음 → busy(유예)에 weight 연동 추천.
3. **공격 루트모션 끄기**: 런지 폐기면 _attacking 중 `ApplyRootStep` 호출 중단(또는 OnAnimatorMove 게이트에서 _attacking 빼고 IsDashing만). 대시 루트모션은 Base layer라 그대로 살림.
4. Combo→UB_None 전이 dur=0.15(블렌드). weight를 OnComboEnd서 즉시 0으로 떨구면 이 0.15는 안 보임.

## 함정/검증
- ★**stale 콘솔 에러**: 중간 편집(exit redirect 후 UB_None 미정의 윈도우)서 "Broken PPtr 2001..."·"Transition INVALID" 11건 — 타임스탬프 고정, 이후 clean reimport서 신규0. 최종 디스크=null 전이0·UB_None 정의됨(line 790 SM·830 state). 유저 에디터서 빨간거 보이면 콘솔 Clear 1회로 사라짐(에셋 무결).
- fileID 대역: UpperBodyAction SM=`2000000000000000001`·UB_None=`2001000000000000001`(기존 1112/1114와 비충돌).
- ★검증 한계: 움직이며 베기 손맛·상체freeze 실재여부=유저 빌드 판정. 구조(레이어/마스크/전이/null0)만 디스크 PASS.
