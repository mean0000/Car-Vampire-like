---
name: atomlab-onecut-scaffold
description: 카타나 "정지 한 방" 손맛 원자 테스트 랩(_AtomLab_OneCut) — 채널 토글 스캐폴드 패턴 + 씬 배선 위치
metadata:
  type: project
---

2026-07-04 구축. 목적 = 정지 더미 1기로 손맛 채널(사운드/플래시/쉐이크/히트스탑/입력피드백/콤보스냅)을 개별 on/off해 격리 판정.

**씬:** `Assets/_Project/Scenes/Labs/_AtomLab_OneCut.unity` — `_CombatSlice_ReadAndCut.unity`에서 SaveAs(원본 무수정 확인됨). 루트: Player·Main Camera·Arena_Ground·Obstacle_0~3·TelegraphPool·CombatSliceSpawner(비활성)·**WhiteboxVerbLab(비활성화함 — WhiteboxVerbLabSpawner+PerformanceGauge+CrassorridSliceSpawner 3개가 한 GameObject에 번들)**·**AtomLab_Dummy**(신규, 캡슐 1기)·**AtomLabRig**(신규).

**더미:** Player 정면 2m(y=1, 캡슐 프리미티브 기본 pivot 보정), 레이어=Zombie(7), URP/Lit 머티리얼 복제(`new Material(rend.sharedMaterial)` — WhiteboxVerbLabSpawner와 동일 관례, Shader.Find 회피), EnemyDamageReceiver.SetMaxHp(50), Rigidbody 없음.

**코드:**
- `Assets/_Project/Scripts/EnemyDamageReceiver.cs` — `#region ★AtomLab 디버그 채널 토글`: SetFlashEnabled/SetShakeEnabled/SetHitStopEnabled. Awake에 원값 캐시(`_origFlashTime` 등), off=0 스왑.
- `Assets/_Project/Scripts/Player/KatanaWeapon.cs` — 동일 패턴: SetSfxEnabled/SetInputFeedbackEnabled(comboKick/finisherKick/hitGlideEnabled). 이 클래스에 Awake()가 원래 없어서 신규 추가(캐시 전용, 기존 로직 없음).
- `Assets/_Project/Scripts/AtomLabRig.cs`(신규) — 키 1~6(사운드/플래시/쉐이크/히트스탑/입력피드백/콤보스냅)+7(예약, 미배선)+R(리셋)+K(1방킬 토글). OnGUI HUD. 6번은 클립에 구운 스냅이라 완전재현 불가 — Animator.speed 1.0↔0.6 베스트에포트.

**실측(이 세션 KatanaWeapon.comboSet 값, 참고용):** 콤보 3단 range=4/4.2/4.5m, arcHalf=62/66/82°, forwardOffset=0.45/0.45/0.55m, lineCutEnabled=true(width 1.6m). 더미 2m 배치는 전 단 여유 있게 사거리 내.

**패턴(재사용 가치):** 손맛 채널을 "Awake 캐시 원값 → off=0/false 스왑, on=캐시 복원" 형태의 public setter로 노출하면 게임 코드 오염 없이 런타임 A/B 격리가 가능. 다음에 다른 무기/이펙트 채널 격리 랩을 만들 때 이 스캐폴드를 그대로 복제.

**주의:** 이 세션 도중 `Assets/_Project/Audio/AtomLab/`(swish_A/B, impact_fleshA/B/C .wav)가 병렬 Sound 에이전트 산출물로 나타남 — Gameplay는 건드리지 않음(오케가 배선 예정, KatanaWeapon.impactClip/swishClip 필드 값 변경 금지 지시 준수).
