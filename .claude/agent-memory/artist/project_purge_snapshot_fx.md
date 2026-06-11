---
name: purge-snapshot-fx
description: 처리 스냅샷 임팩트 프레임(화이트아웃+잉크+스미어) v1 구현됨 — 시각 검증/튜닝 대기, 튜닝 수치는 PurgeSnapshotFX.cs 상단 const
metadata:
  type: project
---

처리 스냅샷(Purge Snapshot) v1 구현 완료(2026-06-11) — 수렴샷 킬 1컷: 종이 화이트아웃 0.067s 컷 온/오프 + InkBlob 절차 셰이더 잉크 실루엣 + 회백 스미어. 유저 시각 검증·튜닝 대기 상태.

**Why:** 킬 연출 "순간의 무게" 강화. 색상 예산상 흑/백/회백만 — 마무리 보상색은 기존 시안 킬 링이 받음. 빨강 금지.

**How to apply:** 튜닝 요청 시 PurgeSnapshotFX.cs 상단 const 블록(지속·간격·크기·스트레치·스미어)과 InkBlob.shader의 radius/warp(0.9)/위성 스레숄드를 만진다. 전부 unscaled 전제(히트스탑 timeScale 0.05) — scaled로 되돌리면 4프레임이 1.3초가 된다. 오버레이 캔버스 sortingOrder 관행: HUD Canvas=0, RunLoopSetup=50, PurgeSnapshot=200. v1은 텍스트/아이콘 금지(미니멀), CombatFeelConfig 비연동(자기완결) — 연동은 별도 결정 필요.
