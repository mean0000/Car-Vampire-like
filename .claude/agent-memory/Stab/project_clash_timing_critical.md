---
name: project_clash_timing_critical
description: 클래시(맞받음) 시스템 QA — IAttackCommit.IsStriking + ParrySlowMotion.Clash + KatanaWeapon ClashFx 지연해석. timeScale 소유권 경합 실발화 리스크 확인.
metadata:
  type: project
---

## 클래시 재설계 (2026-06-24) 리뷰 결과

**IAttackCommit.IsStriking**: SLunge|SBite만 true. 안전(null 가드, Animator 상태 해시).

**ParrySlowMotion.Clash()**: _hitStop=Max(_hitStop, clashFreeze)만 — _slowActive 안 건드림. 설계상 OnParry 슬로모와 프리즈 우선순위는 _hitStop>0 브랜치가 슬로모 타이머를 멈추므로 **Clash가 진행 중 OnParry 슬로모를 일시 중단시키지만 corrupting하지는 않음**. OnParry 슬로모 도중 Clash 호출 시 _hitStop이 연장될 뿐 — 슬로모 resume은 정상.

**★H-1: HitStop.cs(timeScale=0.05) 경합**: EnemyDamageReceiver/CrassorridBrawler가 SmashFeel.HitStop 호출 → HitStop.Do(0.05f). 이 상태서 ParrySlowMotion.Update()가 매 프레임 `Time.timeScale = final`로 덮어씀. HitStop이 복원을 "unscaledTime >= _resumeAt"로 판단하므로, ParrySlowMotion이 중간에 1.0을 써버리면 HitStop은 _active=false되지 않고 다음 Update에서 `Time.timeScale = 1f`를 한 번 더 쓴다(단순 타이밍 경쟁, 사고는 아님). **반대로**, HitStop이 Clash 프리즈 도중 timeScale=0.05를 쓰면 ParrySlowMotion은 hitStopScale(기본 0f) 대신 0.05를 가져야 하는데 이미 _hitStop>0 브랜치에서 `Time.timeScale = hitStopScale(0f)`로 덮어쓴다 → HitStop의 의도(0.05 미세 살아있음)가 무음으로 0으로 꺼진다. **이는 기능 이상이 아니라 HitStop 의도 손실** — 실제 플레이어 경험에 영향 거의 없음(0f vs 0.05f 차이).

**_clashFxResolved 씬 재로드 stale**: Initialize로 _clashFxResolved 리셋이 없음 → 씬 재로드 후 Owner가 새 인스턴스인데 _clashFxResolved=true라 ClashFx()가 stale _clashFx(파괴된 obj 또는 null)를 반환. fake-null 체크 없음이지만 `_clashFx?.Clash()`이라 MRE는 없고 프리즈만 무음 누락.

**_actionGrace(WeaponBehaviour) timeScale=0 중 감쇠**: Time.deltaTime 기반 — timeScale=0 UI(레벨업/상자)에서는 감쇠 정지 → 정상(클립도 멈추니 일관성 있음).

**쿨다운 _clashCdTimer**: Update 맨 위 unscaledDeltaTime으로 감쇠 — 항상 틱 맞음. 조기 반환 전 위치 확인.

Why: 2026-06-24 타이밍-크리티컬 클래시 리뷰
How to apply: 향후 timeScale 소유자 추가 시 ParrySlowMotion vs HitStop 경합 재점검 필요
