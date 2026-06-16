---
name: mcp-runcommand-interactive-trap
description: unity-mcp RunCommand에서 AssetDatabase.DeleteAsset 등이 "User interactions not supported" 에러 → delete 빼고 재시도
metadata:
  type: feedback
---

unity-mcp `Unity_RunCommand`에서 `AssetDatabase.DeleteAsset(path)` 호출이 `UNEXPECTED_ERROR: User interactions are not supported for MCP tool calls`로 실패한 적 있음(2026-06-16, KatanaLocomotion 빌드 첫 시도).

**Why:** MCP는 비대화형(-NonInteractive 류) 실행이라, 내부적으로 다이얼로그/확인 프롬프트를 띄우는 에디터 API가 즉시 실패한다. DeleteAsset이 .meta/락 상태에 따라 프롬프트를 유발할 수 있음.

**How to apply:** 컨트롤러/에셋 생성은 delete-then-create 패턴 피하고 `AnimatorController.CreateAnimatorControllerAtPath`만으로(없으면 생성, 있으면 덮어씀). 굳이 지워야 하면 별도로. `AssetDatabase.Refresh()`도 의심 후보 — 꼭 필요할 때만. 에러나면 에셋이 롤백돼 미생성 상태일 수 있으니 LoadAssetAtPath로 존재 확인부터.
