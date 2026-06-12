---
name: cozy-mcp-bypass
description: Verified RunCommand bypasses for COZY (no type refs) and the working disk-render capture recipe for graphics verification
metadata:
  type: project
---

Unity_RunCommand의 동적 어셈블리는 COZY(DistantLands) 타입을 참조 못 한다 (CS0246). System.Reflection은 하니스 즉사(메인 메모리 기록). 검증된 우회 2종 (2026-06-12 실증):

1. **읽기** = `SerializedObject(comp).FindProperty(...)` — 단 atmosphereProfile은 CozyWeather가 아니라 프리팹 child 컴포넌트(fileID 3919892245518314166)에 산다. 씬 YAML grep이 더 빠를 때 많음.
2. **강제 틱** = `sphere.SendMessage("RaiseUpdateWeatherWeights"/"RaiseUpdateFXWeights"/"RaisePropogateVariables"/"RaiseCozyUpdateLoop"/"UpdateShaderVariables")` + `DynamicGI.UpdateEnvironment()`. CozyDuskSetup.SetTimeFromPrefs 메뉴는 시간 덮어쓰기+씬 강제저장 부작용이 있어 틱 용도로 쓰지 말 것.

**디스크 렌더 레시피 (작동 확인)**: Main Camera를 `Object.Instantiate`로 복제(URP AdditionalCameraData+post 플래그 보존) → Camera/Transform/AdditionalCameraData 외 Behaviour 전부 disable → `RenderPipeline.StandardRequest` + `SubmitRenderRequest` → RT ReadPixels → PNG. 플레이모드 캡처와 팔레트 일치 검증됨.

**Why:** MCP Camera_Capture는 죽은 프레임(메인 메모리), COZY는 MCP에서 틱이 안 돎 — 그래픽 검증은 이 경로뿐.
**How to apply:** CombatLab/ScanLit 계열 씬에서 룩 검증·캡처 산출 요청 시 이 레시피 그대로. 검증 시 휘도 통계(min/avg/max, sub-20 비율)를 같이 뽑으면 노출 원칙(바닥 25~35, 클리핑 금지)을 수치로 판정 가능.

관련: [[cozy-sky-dome-artifact]]
