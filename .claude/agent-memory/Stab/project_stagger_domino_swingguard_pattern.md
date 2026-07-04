---
name: project_stagger_domino_swingguard_pattern
description: 억제 스택(스태거·도미노·스윙가드·멀티킬) 07-04 QA — 화이트박스 프리미티브 스쿼시 콜라이더 함정 + 이중 방향벡터(잠금 아im vs 라이브 몸방향) 가드 오설정 패턴
metadata:
  type: project
---

2026-07-04 QA 대상: EnemyDamageReceiver(staggerOnHit/staggerDuration/staggeredDamageMult/IsStaggered/ApplyStagger), SwarmChaser.cs(신규), KatanaWeapon(IsSwingActive/critOnWindup/multiKillFreezeCount), WhiteboxVerbLabSpawner(SetStaggerOnHit). Critical 0 / High 2 / Medium 5.

**★H-1 재발 가능 패턴 — 화이트박스 프리미티브(GameObject.CreatePrimitive) 스쿼시 함정**: 별도 model 자식 없이 콜라이더+메시+RB가 같은 transform에 있는 프리미티브를 `transform.localScale.y`로 스쿼시하면 콜라이더도 같이 줄어 피벗(중심) 기준 대칭 축소 → 바닥이 뜬다(원래 스케일 1일 때 딱 접지하던 캡슐이 0.78 스케일 시 (1-0.78)*halfHeight만큼 붕 뜸) → 중력이 다시 끌어내려 "가라앉았다 팝"하는 눈에 띄는 아티팩트.
**Why**: 이 프로젝트의 몬스터들(Caniathrox 등)은 이미 `model` Transform을 콜라이더/루트와 분리해 비주얼 오프셋을 그쪽에만 적용하는 관례가 있는데(EnemyDamageReceiver.knockback 코드 참고), 화이트박스 랩 스포너는 프리미티브 하나로 다 때워서 이 분리가 없다.
**How to apply**: 앞으로 "화이트박스/프리미티브 기반 임시 몹"에 시각적 스쿼시·바운스·틴트 이상의 스케일/트랜스폼 조작을 추가할 때는 반드시 "이 transform에 물리 콜라이더가 같이 있는가"부터 확인. 있으면 자식 비주얼 분리 또는 위치보정 필수.

**★H-2 재발 가능 패턴 — 이중 방향벡터 시스템에서 가드/판정 기준 벡터 혼선**: 이 게임은 "조준형" 컨트롤이라 몸 방향(PlayerAnimatorDriver의 facingMode: FaceMovement/FaceMouse/Hybrid, F키로 런타임 전환 가능한 비교 하니스 내장)과 무기 판정 방향(KatanaWeapon._aimDir, 콤보 단 시작 시 1회 잠금)이 서로 다른 갱신 주기를 가진 독립 벡터다. 새 시스템(SwarmChaser 스윙가드 등)이 "플레이어가 보는/공격하는 방향"이 필요할 때 무심코 `player.transform.forward`(몸)를 쓰면, 실제 무기 판정은 잠금된 `_aimDir`를 쓰므로 스윙 도중 조준을 바꾸거나 facingMode를 FaceMovement로 전환하면 두 벡터가 갈라진다.
**Why**: KatanaWeapon은 이미 `DebugAimDir`(디버그 전용)로 이 구분을 내부적으로 인지하고 있음 — 즉 설계자도 "몸 방향 ≠ 판정 방향"을 알고 있었지만 SwarmChaser 쪽에서 그 사실을 놓쳤다.
**How to apply**: 앞으로 몬스터/드라이버 쪽에서 "플레이어가 지금 공격 중인 방향"이 필요하면 반드시 무기 컴포넌트가 노출하는 판정용 방향(`_aimDir`/`AttackAimDir` 계열)을 쓰고, 몸 forward는 순수 애니메이션/카메라 용도로만 취급할 것. player-stack-pattern, katana-combo-pattern 메모리와 연결.

**기타 확인된 안전 패턴(재확인, 새로울 것 없음)**: TakeHit 배수→리프레시 순서 정확, edr null 캐스트 단락평가 안전, `_step` 수명 4중 경로(OnComboEnd/moveCancel/Cancel/자가치유) 전부 0 귀결 확인, 크리+멀티킬 동시 시 if/else-if + ParrySlowMotion 자체 쿨다운(0.13s) 이중 방어로 더블프리즈 없음, 전파(Propagate) 킬 미집계 정확, staggerOnHit/critOnWindup 기본값 false + 랩 씬 외 오버라이드 0(grep 검증)으로 크로스씬 오염 없음.

**잠복(Medium) 이슈 — 지금은 안 터지지만 값이 바뀌면 터지는 것들**: OnDamaged applied화가 CaniathroxChaser 포이즈 누적과 암묵결합(staggerOnHit 켜는 순간 발현), ResetReceiver()에 _staggeredUntil 리셋 누락(현재 호출자 0, 풀링 연결 시 발현), TakeHit의 _staggeredUntil 무조건대입 vs ApplyStagger의 Mathf.Max 연장 비대칭(도미노 지속시간이 staggerDuration보다 길어지는 순간 발현).
