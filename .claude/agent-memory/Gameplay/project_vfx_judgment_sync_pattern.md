---
name: vfx-judgment-sync-pattern
description: VFX 크기/형상=실제 히트박스 값 동기(공정), 화려함=밝기/HDR/트레일. 진행형 궤적은 핸들+Gen가드 드라이버로 실제 누적거리/반경 구동
metadata:
  type: project
---

★유저 헌법(2026-06-14, 카타나 슬래시 VFX 합본 수정): **VFX 크기/형상 = 실제 판정 값으로 구동(동기). 화려함 = 밝기/HDR/트레일로만 — 오해 부르는 크기 오버슈트 ❌.** → 정직(공정) + 뽕 둘 다.

**Why:** 슬래시 VFX가 판정보다 40% 넓게(콤보 크기 확대) / 목표 거리 전체로(벽 막혀도 허공까지) / 즉시 max로(확장 연출 없이) 그려져 "시각≠판정" 불공정. 5단 화려함을 크기로 표현한 게 화근.

**How to apply (이 VFX 패밀리 = SlashVfxFX/SmashImpactFX 쌍둥이 + 향후 근접 종 임팩트):**
- 평타류(1회 스폰): VFX range = *실제 최대 히트 거리*. `SwingFan`은 `gather = range+0.5`까지 각도 안이면 타격 → VFX도 `range+0.5`. 콤보 크기 스케일 제거, 단수는 색·밝기(`Cyan * Lerp(0.85,1.6,tier)` HDR>1)·마젠타 틴트로만.
- 진행형 궤적(발도 돌진·참격파 확장): **즉시 목표값으로 안 찍는다.** 스폰 시 길이/반경 0(또는 base)에서 시작 → 드라이버(KatanaController)가 매 프레임 `Drive*(실제값)` 호출.
  - 발도: 실제 누적 이동 = `Vector3.Dot(pos - lungeStart, lungeDir)`(돌진축 투영). 벽에 막혀 moved=0이면 traveled 정지 → VFX도 거기서 멈춤.
  - 참격파: `StepWave`의 실제 `radius = Lerp(range, waveRange, t)`를 `_Progress = radius/maxRange`로(SizeWorld는 maxDiameter 고정, 셰이더가 _Progress로 채움 반경 결정).
- **핸들+Gen 가드:** 드라이버가 `_fx = pool.Acquire()` 핸들 보관 + `_fxGen = _fx.Gen` 캡처. 매 프레임 `if (_fx != null && _fx.Gen == _fxGen) _fx.Drive(...)`. 풀이 재활용하면 `_gen++`로 Gen 바뀌어 stale 드라이브 차단. 액션 종료/캔슬(StepLunge 종료·StepWave 끝·OnDashStarted·HardCancelAll) 시 핸들 null(FX는 자체 페이드로 Return).
- **시간원 정렬:** 진행형 VFX의 Update 페이드는 드라이버와 *같은 시간원*. 카타나 로직이 `Time.deltaTime`(StepLunge/StepWave)이면 VFX Update도 `deltaTime`(unscaled ❌ — 히트스탑 중 궤적만 앞서가 드라이브와 어긋남). ※쌍둥이 SmashImpactFX는 외부 드라이버 없는 1회 스폰이라 unscaled 유지 — 드라이브 동기가 필요할 때만 deltaTime.
- **풀 stale 캐시 금지(C-1):** 풀 참조 getter는 `if (_pool == null) _pool = Pool.Instance;`로 매번 재확인. 영구 `_resolved` bool 캐시는 씬 리로드로 풀 파괴 시 *fake-null*(파괴된 UnityEngine.Object) 영구 침묵 함정.
- **스파크/입자 수명 포함(H-1):** 전체 `_life = Max(slashLife, rangeLife, sparkLife)`. 스파크 = `ps.main.duration + startLifetime.constantMax`(~0.68s). 빠지면 입자 살아있는데 Return → 다음 Acquire에 잔류/끊김.
- **self-bootstrap 싱글톤 풀은 InitPoolSize 불필요:** 스포너 inactive-GO 주입 풀(SmashImpactPool/TelegraphPool)과 달리 self-bootstrap(SlashVfxPool)은 주입 창이 없음 → poolSize는 인스펙터 노브만. 진행형 궤적(~0.7s)이 평타 연타와 겹치니 풀 여유 10.
