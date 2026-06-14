---
name: telegraph-pad-fx
description: ThreatArc 스타일 3~6 텔레그래프 장판 v1 구현·캡처 검증 완료 — PickupInfoOverlay(600)에 깊이 어태치 생존(ZTest LEqual 작동), 톤 튜닝 대기
metadata:
  type: project
---

적 공격 텔레그래프 장판 v1 (2026-06-13): ThreatArc.shader에 스타일 3=원/4=레인/5=부채꼴/6=링 확장. 월드 미터 공간(_SizeWorld, 전제: 쿼드 localScale==_SizeWorld) SDF — 크기 무관 외곽선 두께 일정. 외곽선=예고(상시·무침식), 채움=카운트다운(+전진선 하이라이트 0.3), 침식 0.18(질감 수준). 색=레드-오렌지(1, 0.30, 0.08).

**핵심 실측**: PickupInfoOverlay RenderObjects(이벤트 600=AfterRenderingPostProcessing, 투명 큐, 레이어 13)에서 **깊이 어태치가 살아있다** — `_ZTest`=4(LEqual)로 캡슐이 장판을 정상 차폐(캡처 확인). Always 폴백 불필요. `ZTest [_ZTest]` 기본값 8(Always)이라 기존 후방힌트(스타일 0~2) 머티리얼 동작 불변.

랩: Greybox_TelegraphLab.unity + TelegraphLab.cs(debugProgress 노브, -1=1.2s 루프) + Editor/TelegraphLabCapture.cs(씬 빌더·캡처 메뉴 — 플레이 모드에서 StandardRequest 디스크 렌더, [[cozy-mcp-bypass]] 레시피). 캡처=docs/03_reference/assets/telegraph_lab/.

⚠️세션 중 ToneGateLab.unity가 dirty 상태로 열려 있어 `SaveScene(saveAsCopy:true)`로 `Assets/_Project/Scenes/ToneGateLab_UNSAVED_BACKUP_0613.unity` 백업 후 씬 전환(원본 무변경). 백업 파일 거취=유저 판단.

**How to apply:** 실전 장판은 ZombieController 공격 시퀀스가 _Progress를 와인드업 시간으로 구동하면 됨(랩 패턴 복사). 톤(알파·침식·전진선 강도)은 유저 노브 미판정 상태.

리뷰 반영(Stab+Codex 채택 2건, 2026-06-13): ① TelegraphLab에 OnDestroy 수명 정리 — 머티리얼 4+지면 머티리얼+쿼드 GO Destroy(null 가드). ② 부채꼴 분기 `_AngleDeg` clamp(1, 180) — iq pie SDF가 반각>90°에서 부호 반전돼 장판 통째 소실. **실전 부채꼴도 전각 180° 초과 금지**(문법 최대치).
