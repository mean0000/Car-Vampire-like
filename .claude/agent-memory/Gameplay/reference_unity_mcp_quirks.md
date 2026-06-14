---
name: unity-mcp-runcommand-quirks
description: Unity MCP RunCommand의 asmdef 참조 한계(COZY 등 벤더 타입 직접 참조 불가)와 SetAimState 주입 테스트 무효 함정
metadata:
  type: reference
---

Unity MCP `Unity_RunCommand` 사용 시 재발성 함정 2건 (2026-06-12 확인):

1. **벤더 asmdef 타입 직접 참조 불가** — RunCommand 컴파일 어셈블리는 `DistantLands.Cozy`(COZY) 등 벤더 asmdef을 참조하지 않음. `using DistantLands.Cozy;` → CS0246. 우회: `GetComponentsInChildren<MonoBehaviour>(true)` 순회 후 `b.GetType().Name == "CozyAmbienceModule"` 문자열 매칭 + `SerializedObject`로 프로퍼티 접근(System.Reflection은 하니스 즉사 — 기존 메모리 참조).

2. **PlayerCombat이 SetAimState를 매 프레임 입력으로 덮어씀** — 플레이 모드 검증에서 `PlayerCameraRig.Instance.SetAimState(true)` 주입해도 다음 프레임에 입력 기반으로 false로 환원되어 AimBlend가 0에 머묾. 조준 관련 런타임 검증은 상태 주입이 아니라 리그와 동일 수식 재계산(수학 재현)으로 캡/리드를 검증할 것.

3. **동적 MonoBehaviour 프로브 패턴 (2026-06-12 실증, 2번 함정의 정공 우회)** — RunCommand 스크립트 파일에 `internal class XxxProbe : MonoBehaviour`를 같이 정의하고 `AddComponent<XxxProbe>()` 하면 플레이 모드에서 정상 작동(동적 어셈블리 타입도 OK). 덮어쓰기 함정은 프로브 Start에서 `PlayerCombat.enabled=false`로 차단(종료 시 원복). RunCommand 호출 간 상태 전달은 static이 안 됨(매 호출 새 어셈블리) → **결과를 GameObject.name 문자열에 기록**하고 다음 RunCommand에서 이름 prefix로 찾아 회수. `Application.runInBackground=true`는 프로브 주입 커맨드에서 설정. 수렴 게이트 검증에 사용: 위상별 수치를 `PROBE|p1=…|done` 형식으로 리드백.

4. **컴파일 반영 검증 = 신규 멤버 직접 참조 (2026-06-13 실증)** — 스크립트 편집 후 "Unity가 진짜 내 코드를 컴파일했나"는 콘솔 에러 0만으로 부족(에디터 미포커스 시 리프레시 안 됨). ①RunCommand에서 `AssetDatabase.Refresh()` → ②새 RunCommand에서 방금 추가한 필드/메서드를 **직접 참조**(`so.chargeKick` 등)해 값 로깅. 컴파일 성공+이니셜라이저 값 리드백이면 반영 확정. 리플렉션 불필요(즉사 함정 회피) — Assembly-CSharp 타입은 RunCommand에서 직접 참조 가능(벤더 asmdef만 불가).

관련: Greybox_CombatLab의 COZY 앰비언스는 CozyAmbienceModule 컴포넌트 비활성(씬 프리팹 override)으로 음소거됨 — 벤더 코드 무수정 원칙.
