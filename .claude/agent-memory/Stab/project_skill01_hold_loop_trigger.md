---
name: skill01-hold-loop-trigger
description: Skill01 차징 홀드 셀프루프(0.10s crossfade) + AnyState trigger InterruptionSource: 0 = trigger 소멸 패턴. 2026-06-26 실발화.
metadata:
  type: project
---

## 규칙/사실

AnyState trigger 전이가 InterruptionSource: 0인 상태에서, 목적지 상태에 짧은 self-loop crossfade(0.10s)가 있으면 trigger가 소멸된다(Unity: "trigger는 발화 여부와 무관하게 평가 시 소멸"). 이는 [[animator-interruptionsource-trigger-eating]] 패턴.

**발화 조건:**
1. AnyState → X 전이의 InterruptionSource: 0 (또는 current active transition의 InterruptionSource: 0)
2. 현재 상태에 self-loop crossfade 존재 (Hold→Hold, duration 0.10s)
3. 루프 주기 대비 crossfade 비율이 높음(여기서는 21-42%)

**2026-06-26 실사례:**
- KatanaMelee.controller: Skill01Hold self-loop (`&-6907483453423336899`) `m_InterruptionSource: 0`
- AnyState→Skill01Strike trigger 소멸 → RMB 릴리스 무시 → Hold 지속
- **수정:** Hold→Hold self-loop `m_InterruptionSource: 0 → 1`
- **동형 M-1:** Charge→Hold (`&7120574291084219080`) 동일 수정 권장

**Why:** Unity는 AnyState trigger를 평가 시 소멸시킨다. InterruptionSource: 0인 active crossfade는 모든 인터럽트를 차단하므로 trigger가 평가되지만 전이는 발화하지 못한 채 소멸.

**How to apply:** 짧은 자기루프(self-loop, exitTime 기반) crossfade가 있는 상태로의 AnyState trigger 전이를 리뷰할 때 반드시 확인. 루프 주기 대비 crossfade 비율이 10% 초과 시 High 이슈.

## 대조: 직접 상태 전이 trigger는 소멸하지 않음

SkillCancel(Hold의 직접 아웃고잉 전이)은 trigger가 소멸되지 않고 crossfade 종료 후(최대 0.10s 지연) 발화. AnyState 전이만 이 취약점이 있음.

## IsInSkillChargeWindup crossfade 위양성

`GetCurrentAnimatorStateInfo(0)`는 transition 중 source 상태를 반환 → Charge→Hold 0.08s 동안 IsName("Skill01Charge") = true. 수정: `!IsInTransition(0) &&` 가드 추가.

## 검증된 안전 전제

- Strike 런지 보존: Skill01Strike ∉ IsName 억제 목록, Action 태그로 _attacking = true
- deltaPosition 누적 없음: 매 프레임 fresh 계산
- Combo/Counter/DashAttack 루트모션 정상
- _attacking = true 조건: Skill01Charge·Hold 모두 Action 태그
