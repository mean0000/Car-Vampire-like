---
name: sync-glitch-fx
description: 싱크 글리치 v1 구현됨(튜닝 대기) — 니어플레인 쿼드+OpaqueTexture 방식의 한계 2개(투명VFX 덮임, 빌드 셰이더 스트립 등록 필요)
metadata:
  type: project
---

싱크 글리치(SyncGlitch.shader + GlitchFX.cs) v1 구현 완료 — 2026-06-11. 스펙 동결분 그대로, 시각 튜닝은 유저 검증 대기.

**Why:** 동기화율 임박/사망의 시그니처 연출. PsyOps 무드, 마젠타/시안은 RGB 분리 부산물(직접 칠하기 금지), 빨강 금지.

**How to apply:**
- 한계 1 (비자명): 씬 컬러 = _CameraOpaqueTexture(불투명만). 글리치 활성 중에는 **투명 큐 VFX(파티클·시야콘 등)가 화면에서 지워진다** — 알파1 재구성 덮어쓰기라서. 유저가 "글리치 중 이펙트 사라짐"을 보고하면 이게 원인. 해결하려면 풀스크린 패스(RendererFeature) 전환 필요 = 렌더러 에셋 수정 허용이 떨어져야 가능.
- 한계 2: Shader.Find 의존 → 빌드 전 Graphics > Always Included Shaders에 ZombieCrush/SyncGlitch 등록 필수(미등록 시 경고 로그+영구 비활성 폴백은 구현돼 있음).
- 타이밍 전부 unscaled 전제([[purge-snapshot-fx]]와 같은 뿌리 — 사망 timeScale=0/히트스탑 0.05). 셰이더에 Time 내장변수 쓰지 않고 C#이 _GlitchTime 주입.
- 외부 트리거 API: `GlitchFX.Burst(strength, duration)` — 향후 엘 파편 획득 등에서 호출.
