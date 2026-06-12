---
name: cozy-sky-dome-artifact
description: Blue Default-Skybox horizon in edit-mode JudgeCam captures is a capture artifact (COZY dome doesn't follow JudgeCam), not a scene defect
metadata:
  type: project
---

에디터(틱 없는) JudgeCam 캡처에서 수평선에 파란 Default-Skybox가 새어 보이면 — 씬 결함이 아니라 캡처 아티팩트다.

**Why:** COZY Sky 돔 = Weather Sphere의 child(0). CozyWeather.cs:683~726이 매 틱 `farClipPlane/1000` 스케일 + 추적 카메라 위치로 재배치한다. 에디터 MCP 렌더에선 틱이 없어 돔이 마지막 위치/스케일에 동결 — JudgeCam이 스피어 중심에서 ~100m만 벗어나도 돔 원면이 far plane 밖으로 밀려 그 픽셀에 스카이박스(파랑)가 노출된다. 플레이 중에는 돔이 메인 카메라를 추종하므로 발생 안 함 (2026-06-12 b004 플레이 캡처로 교차 확인). 씬에 저장된 돔 스케일 차이(예: ScanLit 20 vs CombatLab 12.08)도 같은 이유로 런타임 잔여값 — 동기화 불필요.

**How to apply:** 오프센터 캡처에서 파란 하늘이 보여도 "스카이 배선 깨짐"으로 진단하지 말 것. 정확한 하늘이 필요한 캡처는 스피어 위치 근처에서 찍거나, 캡처 직전 스피어를 JudgeCam 위치로 임시 이동+강제 틱([[cozy-mcp-bypass]]) 후 원복.
