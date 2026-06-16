---
name: katana-anim-trigger-pattern
description: KatanaController FireAttackTrigger 1회 캐시 패턴 — AnimatorController 교체 후 stale 함정 + 수명주기 실행 순서 레이스 (2026-06-16)
metadata:
  type: project
---

## 핵심 결정/사실

KatanaController(순수 C# 헬퍼)가 Animator 트리거를 발사하기 위해 `_animator = owner.GetComponentInChildren<Animator>()`를 생성자에서 캐시하고, `_attackTriggersChecked` 플래그로 파라미터 존재 여부를 1회만 조회하는 패턴.

**Why:** `_animator.parameters` 조회는 배열 할당이라 매 프레임 금지 — PlayerLocomotionAnimator의 `_firingParamChecked` 패턴과 동형.

**How to apply:** 아래 stale 함정을 항상 함께 확인.

---

## ★ 확인된 버그 패턴 (H-1, 2026-06-16 리뷰)

### 실행 순서 레이스

- `PlayerCombat`은 `[DefaultExecutionOrder(-10)]`, `PlayerLocomotionAnimator`는 순서 미지정.
- `forceKatanaForTest=true` 상태에서 `EquipKatana()`가 **PlayerCombat.Awake()**에서 호출 → 이 시점 `runtimeAnimatorController`는 씬 초기값(라이플/권총 스탠스).
- 첫 공격 시 `FireAttackTrigger` → `_attackTriggersChecked = false`라 파라미터 스캔 → **카타나 파라미터(KatanaLight/Lunge/Wave)가 초기 컨트롤러에 없으면 모두 false로 캐시** → 이후 영구 무음.
- `ApplyStance()`가 `katanaController`로 스왑해도 `_attackTriggersChecked = true`라 재검사 없음.

### 올바른 픽스

```csharp
RuntimeAnimatorController _cachedControllerForTriggers;

void FireAttackTrigger(int hash)
{
    if (_animator == null) return;
    var current = _animator.runtimeAnimatorController;
    if (current != _cachedControllerForTriggers)
    {
        _attackTriggersChecked = false;
        _hasLightTrigger = _hasLungeTrigger = _hasWaveTrigger = false;
        _cachedControllerForTriggers = current;
    }
    // 이하 기존 1회 캐시 로직 동일
}
```

---

## 안전 확인된 사항

- `_animator` Destroy 없음 (캐시 참조, 소유 아님) — Cleanup에서 건드리지 않음, 주석으로 명시.
- `PlayerController.OnPlayerDamaged` 구독/해제 대칭 — 생성자/Cleanup 쌍, 이번 변경이 안 건드림.
- `999999` 보장킬 데미지 — int 범위 내, TakeMeleeHit 첫 인수가 int라면 오버플로 없음.
- `[Min(0)] ambushXp` + `XPManager?.AddXP` null 조건 — 무음 폴백 의도적.
- `katanaController` 신규 SerializeField — 필드 추가만, 기존 rifle/pistol 연결 보존 확인.

---

## 잠복 seam (향후 확인 포인트)

- 카타나 ↔ 총 라이브 스왑 경로(`ApplyRanged` → `EquipKatana` 재진입): 새 KatanaController 인스턴스는 `_attackTriggersChecked=false`로 시작하므로 재캐시 필요. `_cachedControllerForTriggers` 픽스가 이를 자동 처리.
- `IsKatanaEquipped` (`_kind==Melee && _katana!=null`): 일반 MeleeAttacker 경로(`_melee!=null`)가 공존. 일반 근접 무기 추가 시 PLA가 katanaController로 잘못 스왑 가능.
- 해시 문자열 오타: `KatanaLight/Lunge/Wave` 파라미터명이 Animator 에셋과 불일치 시 무음 실패 — 코드-에셋 간 검증 수단 없음, 배선 시 육안 확인 필수.
