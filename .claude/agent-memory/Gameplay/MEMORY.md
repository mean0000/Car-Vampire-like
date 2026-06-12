# Gameplay Agent Memory Index

- [플레이 검증 실전 기법](project_playmode_verification_tricks.md) — 조준 강제 코루틴 트릭, superSize 캡처, RT 덤프 진단, 플레이모드 이탈 대응
- [애니 클립 배선 실측 기법](project_anim_clip_wiring.md) — Mixamo 좌우 분류(avgSpeed/angSpeed), 아크 Bake XZ 함정, 컨트롤러 스왑 파라미터 재주입
- [playmode-verify-pattern](project_playmode_verify_pattern.md) — RunManager 씬 전부 timeScale=0 부팅(CombatLab 포함)→StartMission 필수, RunCommand 멀티프레임 검증 패턴
- [player-anim-conventions](project_player_anim_conventions.md) — 플레이어 애니 규약 Speed/MoveX/MoveY/Reload/Firing + Mixamo 임포트 컨벤션(Bake OFF=인플레이스), 플레이어=SM_Casual_Male
- [unity-mcp-runcommand-quirks](reference_unity_mcp_quirks.md) — RunCommand 벤더 asmdef 참조 불가→타입명 매칭; SetAimState 덮어쓰기→동적 MonoBehaviour 프로브(+GameObject.name 리드백)로 우회
