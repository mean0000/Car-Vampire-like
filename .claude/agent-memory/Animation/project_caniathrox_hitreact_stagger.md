---
name: caniathrox-hitreact-stagger
description: Caniathrox 히트리액트(스태거/플린치) — GetHitBack in-place 클립·AnyState 하드컷·포이즈 게이팅. 룩트모션 0 사실·끊고베기 인터럽트
metadata:
  type: project
---

Caniathrox 호드가 카타나에 맞으면 휘청하는 GetHit(스태거) 비트. "호드가 베이면 버클" 읽고-베기 슬라이스의 정예 경로(하이브리드 넉백 설계).

## ★핵심 실측 (06-23, Animator/curve 직접 측정)
- 팩의 **GetHit 4종(Back/Front/Left/Right) 전부 RootT 커브 있으나 net translation = 0.0000m = 완전 in-place** 플린치(몸통/머리만 휘청, 위치 이동 0). `_RM` 변형 없음.
- 검증법 신뢰: 같은 스크립트로 JumpLunge_RM = 4.6734m(문서 4.67 일치) → curve.Evaluate(0)→(L) RootT.x/z 델타 측정이 정확.
- **edit-mode `anim.Update()`는 루트모션을 transform에 적용 안 함**(endPos 0 고착) → 변위는 RootT 커브로 측정해야지 transform 스텝으로는 못 봄.
- GetHitBack: len=1.000s·30fps·**loop OFF**(플린치는 1회재생 정답)·raw FBX 클립(clipAnimations=0, fileID 1827226128182048838 "Take 001", guid 14f14a1a916b95c448f6e3d712bf77cc). Generic rig(animationType 2, rootMotionBoneName=root) — 컨트롤러 형제들과 동일 패턴.

## ★정직한 한계 (유저에게 보고됨)
"shoved back" 위치 넉백은 **이 클립으로 불가**(루트모션 0). 코드 넉백은 금지(이중소유, applyRootMotion=true 몹). 진짜 후방 변위 원하면 **클립 오써링 필요=내 범위 밖**. 현재=제자리 플린치(휘청)로 "버클" 읽힘 — 위치 셔브 아님. state speed 1.6×로 1.0s→~0.625s 스냅(타이밍 노브, 모션 발명 아님).

## 컨트롤러 (CaniathroxAttack.controller, guid 59d73fdb1caa399499e4298fa4f2e68f)
- 신규 param `getHit`(Trigger). 신규 state `GetHit`(motion=GetHitBack, speed 1.6, writeDefaults true).
- **AnyState→GetHit**: cond getHit·duration 0(하드컷 m_TransitionDuration=0, 제0원칙 crossfade 금지)·hasExitTime false·canTransitionToSelf **false**(플린치 중 같은 프레임 재진입 스터터락 차단).
- **GetHit→IdleAngry**: hasExitTime·exitTime 0.9·duration 0(컷). 완결 후 복귀=컨트롤러 자동.
- AnimatorController API로 빌드(SetDirty+SaveAssets, 하드YAML 회피). 6→7 state. 씬 스포너가 이미 이 컨트롤러 주입(배선 변경 0).

## CaniathroxChaser 와이어링 (코드=상태전환만, 위치 안 만듦)
- `_receiver = GetComponent<EnemyDamageReceiver>()` 지연해석. **OnEnable에서 SubscribeReceiver()**(OnDamaged += OnDamaged, `_subscribed` 가드로 중복0)·**OnDisable에서 Unsubscribe**(대칭, 누수0). 풀 재활용 대비 OnEnable서 _poise/_staggerCdTimer 리셋 + `ResetTrigger(getHit)`(미소비 큐 트리거 제거).
- ★사망 가드: 수신기는 OnDamaged를 **Die() 전에** 쏨(이때 IsDead 아직 false) → 가드는 `IsDead || Hp<=0`(Hp는 이미 차감됨). 치명타=GetHit 금지(시체 플린치·stale 트리거 방지).
- TriggerStagger: SetTrigger(getHit) + 진행공격 잔재 정리(SetApproaching false·CancelTelegraph·ReleaseToken·ResetCycle·speed=1·ResetTrigger attack/bite). 안 하면 GetHit 후 _attackFired/_holdsToken stale.
- Update 맨앞 `if (state==GetHit) return`(제0원칙: 플린치 중 조향/조준/공격 무엇도 안 함, 위치도 0). 쿨다운은 상태무관 상시 틱.

## 스태거 게이팅 모델 (노브 — 유저 ▶ 손맛 판정)
- **포이즈 누적**: 매 히트 _poise += max(1,damage), `poiseThreshold`(기본 2) 이상이면 발동+리셋. enemyHp4·카타나~1dmg → ~2타마다 1회. 1=매타격, 99=무경직.
- **staggerCooldown**(기본 0.6s): 연타 영구락 안전판(플린치 ~0.625s 근처).
- **staggerImmuneDuringStrike**(기본 **false**): false=Lunge/Bite 발사 중에도 끊김(끊고베기 극대, 플레이어 강함). true=발사 완결(하이퍼아머, 윈드업 반응 요구). 윈드업(Coil)·접근은 항상 끊김.
- 트레이드오프 핵심: 매 히트 스태거=영구스턴락(몹이 못 움직임=노잼), 임계 너무 높음=타격 무게0. threshold+cooldown 조합이 균형.

## 유저가 빌드로 느껴 판정할 것
플린치 빈도(poiseThreshold)·무게(state speed 1.6, in-place라 위치셔브 없음 체감)·하이퍼아머 on/off 손맛·"호드 베면 버클" 만족도. 구조(전환·하드컷·이중소유0·생애대칭)는 검증됨, 느낌은 못 봄.
