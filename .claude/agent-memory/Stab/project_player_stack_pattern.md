---
name: player-stack-pattern
description: 플레이어 스택 8파일(Brain/Aim/Motor/Weapon/AnimatorDriver/Katana/IDamageable/InputState) QA — 핵심 hazard 패턴 + 안전 전제 (2026-06-18)
metadata:
  type: project
---

## 핵심 아키텍처 전제

- **Tick 순서**: PlayerBrain.Update → Aim.Tick → Motor.Tick → Weapon.Tick → AnimatorDriver.Tick (명시 직렬, SEO 의존 없음)
- **루트 미회전**: PlayerMotor는 transform.position만 이동, 회전은 PlayerAnimatorDriver(비주얼 자식)가 조준 방향으로만 적용
- **무기/드라이버 null 허용**: 이동·조준은 무기/애니 없이도 독립 동작
- **공격 판정 진실**: AnimationEvent(PlayerAnimatorDriver.OnAttackHit → AttackHit 이벤트) → KatanaWeapon.OnHitFrame. 폴백 = cadence × fallbackHitRatio 타이머(_hitDone 가드로 이중 판정 차단)
- **timeScale=0 대비**: Motor/AnimatorDriver 모두 `dt <= 0f` 조기 return 있음

---

## ★ 확인된 버그/위험 패턴

### H-1 — WeaponBehaviour.Initialize 재진입 비안전 (latent, 런 매니저 연결 시 발화)
- Initialize에서 `animator.AttackHit += OnHitFrame`을 하는데, 기존 구독 해제 없이 재호출 가능
- 현재(단일 씬 단일 런)에서는 무해하나, 런 매니저가 무기 교체/재시작 구현 시 이중 구독 + 죽은 인스턴스 잔존
- **픽스**: Initialize 첫 줄에 `if (AnimatorDriver != null) AnimatorDriver.AttackHit -= OnHitFrame;`

### M-2 — KatanaWeapon OverlapSphere 버퍼 32 상한
- 밀집 호드 32+ 콜라이더 시 초과분 silently 누락
- gather = range + 0.5 = 2.3f 반경, 호드 씬에서 재현 가능
- **픽스**: 버퍼 64+로 키우기, n == buffer.Length 경고 추가

### M-3 — KatanaWeapon cadence = 0 방어 없음
- cadence = 0이면 fallbackTimer = 0 → 매 프레임 DoSwingHit 도배
- **픽스**: `[SerializeField, Min(0.05f)]`

### M-4 — PlayerAnimatorDriver moveSource 배선 오류 시 무음 폴백
- moveSource가 루트 체인 밖 오브젝트면 _aim/_motor = null → 조준이 이동 방향으로 무음 교체
- "동작은 하지만 방향이 이상함" 증상, 원인 파악 어려움
- **픽스**: Awake 끝 null 경고 어서션

---

## 안전 확인된 사항

- **RechargeDash while 루프**: dashCooldown <= 0f 조기 return + `_dashCharges < maxDashCharges` 상한 → 무한루프 불가
- **_groundOffset Start 계산**: 첫 Update 전에 반드시 Start 완료 보장 → 0인 채로 Tick 불가
- **AnimationEvent + 폴백 경합**: _hitDone 가드로 직렬화. 동일 프레임 이중 발화 구조적 불가
- **_swingActive 재진입 차단**: `!_swingActive` 조건이 StartSwing 진입 가드 → 스윙 중 새 스윙 불가
- **대각선 이동 1.41× 방지**: Motor.Tick에서 sqrMagnitude > 1 normalize
- **WallGuard 2단 체크**: 슬라이드 벡터도 SphereCast 재검사 → 코너 끼임 방지
- **GetComponentInParent<IDamageable>()**: Unity 2021+ 인터페이스 지원, 올바른 디커플

---

## 잠복 seam (향후 확인 포인트)

- 런 매니저 연결 후 무기 교체 경로에서 H-1 즉시 발화 — 연결 전에 픽스 필수
- Camera.main stale 위험: Tick에서 null 체크는 있으나 파괴된 카메라 인스턴스(non-null MissingRef) 방어 없음
- SampleGround origin.y = 200f 고정: 지형 고도 200m+ 바이옴 추가 시 레이 미스
- IDamageable.TakeHit knockback 0/음수 계약 미정의: 현재 KatanaWeapon은 항상 양수 전달이나 신규 데미지 소스 추가 시 구현체 버그 가능
