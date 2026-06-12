---
name: playmode-verification-tricks
description: Greybox_CombatLab 플레이 검증 실전 기법 — 조준 강제(코루틴 타이밍 창), superSize 캡처, 마스크 RT 덤프, 플레이모드 임의 이탈 대응
metadata:
  type: project
---

Greybox_CombatLab 플레이 모드 검증에서 실제로 작동 확인된 기법들 (2026-06-11 시야 콘 마스크 세션).

**Why:** 게이트 파일(PlayerCombat/PlayerCameraRig 등)은 수정 금지인데, 조준 같은 입력 의존 상태를 검증해야 하는 상황이 반복된다. 아래 기법은 게이트를 건드리지 않고 통과한 검증 루트.

**How to apply:**
- **조준 강제**: `PlayerCombat.Update`가 매 프레임 `rig.SetAimState(Input.GetMouseButton(1)...)`로 덮어쓴다 → 코루틴(`yield return null` 재개 지점 = Update 이후·LateUpdate 이전)에서 매 프레임 `rig.SetAimState(true)`를 다시 주입하면 rig가 LateUpdate에서 소비하기 전에 이긴다. 코루틴 호스트는 `rig.StartCoroutine(...)`로 아무 씬 MonoBehaviour나 사용 가능.
- **캡처 해상도**: 에디터 게임 뷰가 작아(~456px) 판독 불가 → `ScreenCapture.CaptureScreenshot(path, superSize 2~4)`. 어두운 영역의 해치 패턴은 그레이박스 바닥 격자 텍스처가 어두워진 것 — 아티팩트로 오판 주의.
- **RT 진단**: 런타임 생성 HideAndDontSave 카메라는 `Resources.FindObjectsOfTypeAll<Camera>()`에서 이름으로 찾고, targetTexture를 ReadPixels→PNG 덤프하면 "마스크가 틀렸나 vs 합성이 틀렸나"를 즉시 분리할 수 있다.
- **플레이모드 임의 이탈**: RunCommand 사이에 플레이 모드가 이유 없이 꺼지는 일이 반복됐다(셰이더 디스크 편집 후 특히). 검증 시퀀스는 **한 RunCommand의 코루틴 안에** 몰아넣고, 매 커맨드 첫 줄에서 `EditorApplication.isPlaying` 확인 + 필요 시 재진입할 것. 플레이 진입 시 콘솔이 클리어되니 로그 의존 검증은 파일 존재로 대체.
- **RunManager.StartMission()은 인스턴스 메서드** — `Object.FindFirstObjectByType<Run.RunManager>()`로 찾아 호출(정적 호출은 CS0120).
- **에디터 일시정지가 코루틴을 동결시킨다 (2026-06-11 실측)**: 검증 코루틴이 리포트를 안 쓰면 `EditorApplication.isPaused`부터 확인 — 퍼즈 중엔 `yield return null`이 영원히 안 깨어난다. 매 검증 커맨드 첫 줄에서 `isPaused = false` 강제. 또한 **대기는 `Time.realtimeSinceStartup` 말고 프레임 카운트로** — 퍼즈 동안 realtime은 계속 흘러서 언퍼즈 직후 모든 대기가 한꺼번에 만료, 측정이 LKP LateUpdate 처리 전 프레임에 몰려 오염된다.
- **CombatLab 텔레포트 검증 함정**: 플레이어 스폰(65,1,-40)은 남쪽(-Z) 5m에 외곽벽(L0_A_PerimS) — 전방 8m 배치는 벽 뒤라 LOS 차폐로 "콘 안인데 숨김"이 정상 동작이다(버그로 오판 주의). 콘 가시성 양성 케이스는 전방 4m 사용. 가시성 판정 디버깅은 InCone과 LOS(Linecast, Obstacle=8)를 반드시 분리 측정.
- **씬 배치 좀비는 Init 호출자가 없다**: ZombieSpawner는 자기가 Instantiate한 것만 Init. 씬 좀비는 _config NULL이면 TakeBulletHit 첫 줄 가드에서 데미지 통째 무시(무적), _currentHP도 0(미초기화). 치사성 검증 전 z.Init(player, pos)로 HP=maxHP 세팅할 것.
- **RunCommand 대형 페이로드 거부 (2026-06-11 실측)**: ~5KB+ 코드의 RunCommand가 "User interactions are not supported for MCP tool calls" 오류로 거부된다(2회 재현). 같은 내용을 작은 단계별 커맨드로 쪼개면 전부 통과. 거대 검증 코루틴 한 방 대신 **스텝 분할 + 플레이 모드 백그라운드 유지(runInBackground)** — MCP 호출 사이에 실시간이 흘러 스태거드 스폰 등은 자연 대기된다.
- **플레이 중 도메인 리로드 = 정적 Instance 전멸**: RunCommand 실패(partial) 후 `RunManager.Instance` 등 모든 싱글톤이 null인데 isPlaying=true인 상태가 나온다 — 컴포넌트는 `FindFirstObjectByType`으로 멀쩡히 찾아진다. 진단 시 Instance null≠오브젝트 소멸. Awake 재실행이 없어 정적만 죽은 것.
- **런타임 SO(에셋) 수정은 영속된다**: 검증용으로 RunConfig 같은 SO 값을 플레이 중 바꾸면(헬기 딜레이 18→2 등) 플레이 종료 후에도 에셋에 남는다. **반드시 에디트 모드에서 원값 복원+SaveAssets.** 지갑 같은 디스크 세이브(meta_save.json)도 검증 입금을 TrySpend로 원복할 것.
- **탈출 지점 AFK 검증 함정**: CombatLab C구역 Dormant 좀비가 EP에서 5~6m라 텔레포트 즉시 어그로→수 초 내 플레이어 사망. 귀로 웨이브(AlertTo Chase)도 ~10m를 금방 도달. 뱅킹 검증은 ①EP 반경 30m 선제 킬스윕 ②헬기 딜레이 임시 단축(사후 복원) 조합이 안정적. EP에서 생존 유지가 필요하면 모니터 코루틴에서 매 프레임 `PlayerController.Heal(99999)`가 잘 먹힌다(2026-06-11 실측 — 좀비 4마리가 붙어도 HP 100 유지).
- **검증 모니터는 unscaled 시간으로 (2026-06-11 실측)**: RunManager.Settle(정산/사망/헬기 도착)이 `Time.timeScale=0`을 박는다 — `Time.time` 기반 종료 조건의 모니터 코루틴은 영원히 안 끝나 리포트를 못 쓴다(EP 검증은 헬기가 ~18s에 자동 정산되므로 거의 항상 걸림). `Time.unscaledTime` + `yield return null`로 작성하고, 필요 시 정산 후 timeScale=1 수동 복구. 부수 효과: timeScale 복구는 "코루틴 동결이 아니라 페이즈 가드가 스폰을 끊는다"의 양성 증명에도 쓸 수 있다.
- **좀비 정리는 Destroy ≠ 킬**: 측정장 청소로 `Object.Destroy(zombie.gameObject)`를 쓰면 죽음 이벤트/strain 드롭 없이 사라진다(지갑 오염 0). 단 ZombieSpawner가 인구를 리스폰하므로 수 초 내 새 클론(스포너 위치)이 측정에 섞인다 — 관측 대상은 스폰 위치로 필터링할 것.
- **Unity MCP 부재 세션 폴백 (2026-06-12 실측)**: Gameplay 에이전트 세션에 Unity MCP(RunCommand 등)가 아예 안 뜨는 경우가 있다 — ToolSearch "+unity"로 먼저 확인. 없으면 플레이 검증은 불가하므로 `dotnet build Assembly-CSharp.csproj`(프로젝트 루트의 Unity 생성 csproj, 참조 포함 풀 빌드)로 컴파일 0 에러만 확정하고, 수치 검증은 코드 경로 수동 재현으로 대체 + 인게임 검증을 미해결로 보고. `-p:BuildProjectReferences=false`는 CS0006 홍수가 나므로 풀 빌드가 깔끔하다(에디터 열려 있어도 충돌 없음).
- URP FullScreenPassRendererFeature 셰이더에서 `UNITY_MATRIX_I_VP` + `SampleSceneDepth` + `ComputeWorldSpacePosition`은 게임 카메라 기준으로 올바르게 작동한다(블릿 행렬로 오염되지 않음 — 실측 확인). 의심될 땐 `frac(wpos.xz*0.1)` 디버그 출력으로 월드 격자가 기하를 따라가는지 보면 된다.
