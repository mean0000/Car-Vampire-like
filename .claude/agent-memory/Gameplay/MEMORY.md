# Gameplay Agent Memory Index

- [플레이 검증 실전 기법](project_playmode_verification_tricks.md) — 조준 강제 코루틴 트릭, superSize 캡처, RT 덤프 진단, 플레이모드 이탈 대응
- [애니 클립 배선 실측 기법](project_anim_clip_wiring.md) — Mixamo 좌우 분류(avgSpeed/angSpeed), 아크 Bake XZ 함정, 컨트롤러 스왑 파라미터 재주입
- [playmode-verify-pattern](project_playmode_verify_pattern.md) — RunManager 씬 전부 timeScale=0 부팅(CombatLab 포함)→StartMission 필수, RunCommand 멀티프레임 검증 패턴
- [player-anim-conventions](project_player_anim_conventions.md) — 플레이어 애니 규약 Speed/MoveX/MoveY/Reload/Firing + Mixamo 임포트 컨벤션(Bake OFF=인플레이스), 플레이어=SM_Casual_Male
- [unity-mcp-runcommand-quirks](reference_unity_mcp_quirks.md) — RunCommand 벤더 asmdef 참조 불가→타입명 매칭; SetAimState 덮어쓰기→동적 프로브 우회; 컴파일 반영 검증=Refresh 후 신규 멤버 직접 참조
- [runtime-spawn-wiring](project_runtime_spawn_wiring.md) — AddComponent 직후 Awake 동기실행=의존필드 null 함정(SetActive 토글로 우회); 하니스 플레이모드 강제 paused; .controller 빈 껍데기 진단(grep AnimatorState)
- [caniathrox-crowd-ai](project_caniathrox_crowd_ai.md) — 군중 추격 AI v2: ★Approach 중 회전 허용(헌법 재해석=방향은 의도)+steering/separation/surround/AttackTokenPool 4기법, Lunge/Spit 회전 0 엄수
- [brute-slam-coordinator](project_brute_slam_coordinator.md) — 거대 브루트 동시 슬램: 수 게이팅(토큰)→각·박자 분산. BruteSlamCoordinator 정적, 플랭킹 재배치(slotAngleDeg 설정), stale 방위 누수 3중 차단
- [brute-standoff-rhythm](project_brute_standoff_rhythm.md) — Crassorrid 1:1 교전: 스탠드오프 정렬+정면콘 커밋(지나침 방지, 본체착지 정합), 느린회수(RecoverySpeed 0.65+SetupData), 큰딜레이=쿨다운(멀뚱❌), 뱅뱅금지(마주보기) vs 플랭킹(대기) 합성
- [katana-dual-mode](project_katana_dual_mode.md) — 카타나 거합/참격 증명 슬라이스: KatanaController(C# 헬퍼) 패턴·손맛 노브 위치(iaiKnobs/slashKnobs)·Debug 검증 훅·그랩/stale세션 함정
- [vfx-judgment-sync-pattern](project_vfx_judgment_sync_pattern.md) — ★VFX 크기=실제 판정값 동기(공정), 화려함=밝기/HDR/트레일만. 진행형 궤적=핸들+Gen가드 드라이버(실제 누적거리/반경), 시간원 정렬, 풀 stale캐시·스파크수명·self-bootstrap풀 함정
- [mapgen-road-graph-engine](project_mapgen_road_graph_engine.md) — MapGen E-2~E-4: 도로그래프(degree 자동분류 End/Corner/T/Cross+5m스냅+inset)·컨테이너회랑·복합지면·식생. ★교차피스 정면 미상→Base오프셋 상수로 캡처후 1회 보정. LMHPOLY는 FamilyRoots밖
