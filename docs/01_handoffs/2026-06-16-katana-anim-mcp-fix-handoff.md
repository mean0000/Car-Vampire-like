# 핸드오프 — 카타나 애니 (코드측 완료) + MCP 미로드 진단 (2026-06-16)

> 재시작 후 이 문서부터 읽고 이어간다. 이전 핸드오프(`2026-06-16-katana-anim-ambush-handoff.md`)의 **§4 MCP 가이드는 폐기**(재시작=해결이라 했으나 오답). MCP 부분은 이 문서 §3로 대체.

## 0. 한 줄 요약
카타나 애니 **코드측 배선 완료**(3파일 + 리뷰가 잡은 Critical 버그 수정, Stab+Codex 통과). 남은 건 **에디터 작업**(카타나 애니메이터 컨트롤러 생성·할당) — 그런데 unity-mcp 툴이 세션에 안 올라오던 게 **진짜 원인 규명되고 우회 설정까지 박음**. 다음 세션 = 유니티 식혀놓고 재실행 → `/mcp`로 툴 확인 → 에디터 배선 → 급습 볼트 플레이게이트.

---

## 1. 이번 세션 한 일 (전부 디스크 저장됨, 미커밋)

### A. 카타나 애니 코드측 배선 (Animation 에이전트 + Stab+Codex 게이트)
루트원인은 이전 핸드오프 §2 그대로 — KatanaController가 Animator 호출 0개 + 로코모션에 카타나 스탠스 없음. 수정:
- **`Assets/_Project/Scripts/PlayerCombat.cs`** — `public bool IsKatanaEquipped => _kind == WeaponLoadout.Kind.Melee && _katana != null;` 추가(스탠스 게이트용, 기존 Debug 접근자와 별개).
- **`Assets/_Project/Scripts/PlayerLocomotionAnimator.cs`** — `[SerializeField] RuntimeAnimatorController katanaController;` 추가 + `ApplyStance()`에 카타나 분기(IsKatanaEquipped면 katanaController로 스왑, 미할당이면 return). 기존 rifle/pistol 필드·순서 불변(Inspector 연결 보존).
- **`Assets/_Project/Scripts/KatanaController.cs`** — 생성자에서 `_animator = owner.GetComponentInChildren<Animator>()` 캐시 + `FireAttackTrigger(int)` 헬퍼(파라미터 존재검사 가드) + 발사점 4곳:
  - 거합 평타 `IaiLightSwing()` → `KatanaLight`
  - 발도 `StartLunge()` → `KatanaLunge`
  - 참격 평타 `SlashLightSwing()` → `KatanaLight`
  - 참격파 `StartWave()` → `KatanaWave`

### B. ★리뷰가 잡은 Critical 버그 — 수정 완료
Stab+Codex가 **독립적으로 같은 버그** 지목: 트리거 존재검사 캐시(`_attackTriggersChecked`)가 1회 후 영구 고정. `PlayerCombat`이 `[DefaultExecutionOrder(-10)]`라 `EquipKatana()`가 Awake에서 먼저 돌아 **첫 스캔이 초기(권총) 컨트롤러를 보고 트리거 전부 false로 굳음** → ApplyStance가 카타나 컨트롤러로 바꿔도 재검사 안 함 = **공격 모션 영구 무음**(우리가 고치려던 증상 그대로). **수정**: `FireAttackTrigger` 진입부에서 `runtimeAnimatorController`가 바뀌면 캐시 무효화·재스캔(로코모션의 `_firingParamChecked` 패턴 동형). KatanaController.cs `_triggerScanController` 필드 + 스왑 감지 블록.

### C. MCP 진단 + 우회 (§3 참조) — `.claude/settings.local.json`에 env 추가함.

## 2. ⏳ 남은 작업 = 에디터 배선 (코드만으론 여전히 "모션 0")
코드는 트리거만 쏜다. 컨트롤러 에셋이 없으면 무음 폴백이라 그대로 안 움직인다. **유저(또는 MCP 복구된 세션)가 에디터에서:**
1. **카타나 RuntimeAnimatorController 생성**(예 `Assets/_Project/_SidekickTest/Katana_Stance.controller`, throwaway `SK_AnimTest.controller` 복제 시작 권장).
   - 파라미터(이름·타입 정확히): `Speed`/`MoveX`/`MoveY` **(Float)** + `KatanaLight`/`KatanaLunge`/`KatanaWave` **(Trigger)**. ⚠️Float 3개가 Bool이면 빌드에러(`feedback_blendtree_param_must_be_float`).
   - 상태: 카타나 든 로코모션 BlendTree(기본) + 공격 클립 3개(`Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animations/2Hand-Sword/`).
   - 전환(헌법 — crossfade ❌): Locomotion→공격 = 트리거 조건, **Transition Duration 0(컷)**, Has Exit Time off / 공격→Locomotion = **Has Exit Time on**(클립 완결 후 복귀).
2. **할당**: CombatLab 씬 → 플레이어 **CharacterVisual**(Animator + PlayerLocomotionAnimator 붙은 자식) → `Katana Controller` 필드에 드래그.
3. **무대**: `Assets/_Project/Scenes/Labs/Greybox_CombatLab.unity` (`forceKatanaForTest=true`). 거합 `1`+LMB, 발도 RMB홀드→릴리스, 참격 `2`+LMB→RMB.

**플레이 검증 체크:**
- 모션 아예 안 나오면 → CharacterVisual에 Animator 둘 이상인지 의심(코드는 단일 가정 — GetComponentInChildren).
- 한 동작씩 도는지·속도감 = 유저 판정(나는 정지 캡처만).

## 3. ⚠️ MCP "Connected인데 툴 0개" — 진단 완료 (★이전 핸드오프 §4 폐기)
**증상**: `claude mcp list`=✔Connected인데 ToolSearch/세션에 unity 툴 0건, claude.ai 커넥터만 보임. **재시작 ❌**(유저 3회 헛수고).
**진짜 원인**(GitHub #51736 + #60224, platform:windows 일치): Claude Code 2.1.x가 MCP 툴을 deferred 레지스트리로 미는데 ①custom stdio 적재 회귀(2.1.116~, 2.1.178도 재현) + ②unity relay 핸드셰이크가 세션시작 probe 타임아웃 초과(유니티 바쁘면 16s+) → 툴 조용히 드랍. 재시작은 같은 타임아웃 재발화라 무한 동일.
**적용한 우회**(`.claude/settings.local.json` `env`):
```json
"env": { "ENABLE_TOOL_SEARCH": "false", "MCP_TIMEOUT": "60000" }
```
`ENABLE_TOOL_SEARCH=false` = deferred 우회(pre-2.1.116 직접 로딩), `MCP_TIMEOUT=60000` = 핸드셰이크 시간 60s.

### ★다음 세션 재실행 절차 (이 순서로)
> ⚠️ 2026-06-16 추가 발견: 재실행 시도 세션에서 **Unity.exe가 3개** 떠 있었고 **`Unity.ILPP.Runner.exe`가 컴파일 중(=바쁨)**이었다 → 그래서 또 툴 0개. 멀티에디터는 relay 라우팅도 흔든다([[unity_mcp_multi_editor_routing]]). 아래 0번을 먼저.
0. **Car-Vampire-like 에디터 1개만 남기고 나머지 Unity 인스턴스 닫기**(라우팅 모호성 제거).
1. **유니티 에디터 열고 컴파일/임포트 스피너 멈출 때까지 대기**(probe 타임아웃 근본 회피 — 이게 핵심. `Unity.ILPP.Runner.exe`가 사라졌는지 = idle 신호).
2. **이 Claude 창만** 완전 종료 → 재실행.
3. 검증: `ENABLE_TOOL_SEARCH=false`면 툴이 직접 로딩이라 ToolSearch 아님 → **`/mcp`로 unity-mcp 툴 목록** 확인.
4. 안 뜨면(settings env가 ENABLE_TOOL_SEARCH 무시하는 #41472 케이스) → 셸 직접: `ENABLE_TOOL_SEARCH=false MCP_TIMEOUT=60000 claude` (PowerShell: `$env:ENABLE_TOOL_SEARCH="false"; $env:MCP_TIMEOUT="60000"; claude`).

## 4. 그다음 = 급습 볼트 플레이게이트 (애니 붙은 뒤)
CombatLab에서 무경계 좀비에게 발도 → 보장 처형 + XP 즉발 스파이크 + "한 번 더?" 켜지나(급습 스펙 §5). 노브 `IaiKnobs.ambushXp`(현 12)·발도 거리/속도 손맛 튜닝. 안 켜지면 무엇이 빠졌나 분해(호드/정면 viable은 다음 볼트).
⚠️ `ambushXp`는 이번에 새로 추가된 직렬화 필드 — 씬 인스턴스의 `iaiKnobs`가 0으로 deserialize되면 급습 XP 0이 될 수 있으니 플레이 전 Inspector에서 값 확인(serializefield 씬 덮어쓰기 함정).

## 5. 커밋 상태
미커밋. 작업트리에 급습 볼트(이전 세션)+이번 애니 배선이 함께 있음. 커밋은 유저 지시 시에만.

연동 메모리: `unity_mcp_multi_editor_routing`(★진단 정정됨), `ambush_bolt_spec`, `serializefield_scene_override_trap`, `combat_anim_sourcing`, `feedback_blendtree_param_must_be_float`.
