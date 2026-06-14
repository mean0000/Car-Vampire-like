---
name: playmode-stale-asm-block
description: 플레이모드 캡처가 막힐 때 1순위 의심 = 컴파일 에러로 인한 진입 거부 + MCP 도메인리로드 큐만 되고 실행 안 됨. 콘솔 에러부터 확인.
metadata:
  type: project
---

플레이 모드 진입(EnterPlaymode / `EditorApplication.isPlaying=true`)이 MCP RunCommand에서 willChange=True→다음틱 False로 매번 롤백되면, 1순위 원인은 **프로젝트에 컴파일 에러가 있어 Unity가 플레이 진입을 거부**하는 것. RunCommand 자체는 별도 동적 어셈블리라 컴파일/실행 성공으로 떠도, 메인 `Assembly-CSharp`에 에러가 있으면 플레이는 막힌다. → **막히면 `Unity_GetConsoleLogs(logTypes=Error)` 부터 본다.**

**2026-06-13 실측 사례 (Caniathrox GIF 캡처 시도 중 발견)**: ExplosiveLLC RPG Character Pack의 `CharacterState` 에러 10건("does not contain a definition for 'Jump'/'Fall'/'Move'..."). 진단: 디스크엔 `RPGCharacterAnims.Lookups.CharacterState`(enum, Idle/Move/Jump/Fall) 하나뿐인데, 로드된 어셈블리엔 **글로벌 namespace의 stale `CharacterState`**(enum, Idle/Walking/Trotting/Running/Jumping)가 남아 충돌. 그 글로벌 enum 소스는 디스크 어디에도 없음(과거 삭제됨) = 순수 stale 어셈블리 잔류물. RPG 코드가 글로벌 것을 잡아 Jump 못 찾음.

**진단 레시피 (리플렉션 OK — 타입 조회는 하니스 즉사 안 함, 실측 확인됨)**:
`Type.GetType("FullName, Assembly-CSharp")` 가 NULL인데 `AppDomain.CurrentDomain.GetAssemblies().SelectMany(GetTypes).Where(name==X)` 가 다른 namespace/멤버의 동명 타입을 반환 → stale 충돌 확정. enum이면 `Enum.GetNames(t)`로 멤버 찍어 디스크 정의와 대조. 멤버가 디스크 어디에도 없으면(`Grep`) stale 확정.

**Why:** 디스크 기준 재컴파일만 한 번 깨끗이 돌면 glob enum이 사라져 에러 자동 해소. 문제는 **MCP 백그라운드에선 도메인 리로드/플레이 진입이 큐잉만 되고 실행 안 됨** — `EditorUtility.RequestScriptReload()`·`CompilationPipeline.RequestScriptCompilation()`·`AssetDatabase.Refresh()` 다 호출해도 글로벌 enum 그대로 남았다(AssetDatabase.Refresh는 컴파일 에러 시 어셈블리 교체 안 함=stale 유지). MCP가 에디터 틱을 펌프 못 하는 게 근본(플레이 진입 불가와 동일 뿌리).

**How to apply:** 캡처/플레이검증이 안 열리면 콘솔 에러 확인 → stale 충돌이면 **유저에게 "에디터 창 한 번 클릭(포커스) → 자동 재컴파일되면 에러 풀림" 요청**이 가장 확실. 그 후 플레이 진입+캡처 메뉴 가능. 코드로 도메인 리로드 강제하는 동기 API는 없음(전부 큐잉형).

**부수**: 이 환경에서 Bash·PowerShell 권한이 거부될 수 있음 → ffmpeg/magick 합성 같은 셸 의존 단계는 유저 실행 필요. unity-mcp RunCommand는 열려 있음.

관련: [[cozy-mcp-bypass]] [[caniathrox-attack-fx]] [[project_unity_mcp_reflection_gotcha]]
