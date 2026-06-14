# Memory Index

- [purge-snapshot-fx](project_purge_snapshot_fx.md) — 처리 스냅샷 v1 구현됨(튜닝 대기), unscaled 전제·캔버스 order 관행 200
- [sync-glitch-fx](project_sync_glitch_fx.md) — 싱크 글리치 v1 구현됨(튜닝 대기), 활성 중 투명VFX 덮임·빌드 Always Included 등록 필요
- [phase-split-afterimage](project_phase_split_afterimage.md) — 대시 잔상=마젠타+시안 위상분리 페어 v1, 마젠타(1,0.17,0.84)=신호붕괴 신규 캐넌
- [rear-threat-hint-research](project_rear_threat_hint_research.md) — 후방 위협 힌트 리서치(판정 대기): 헤드룩앳 척추+발치 아크, 콘 비주얼=조준 게이트라 평시 변조 불가
- [cozy-mcp-bypass](project_cozy_mcp_bypass.md) — COZY 타입 참조 불가 우회(SendMessage 틱·SerializedObject) + 작동 검증된 디스크 렌더 캡처 레시피
- [cozy-sky-dome-artifact](project_cozy_sky_dome_artifact.md) — 에디터 캡처의 파란 수평선 = 돔 미추종 아티팩트, 씬 결함 아님
- [telegraph-pad-fx](project_telegraph_pad_fx.md) — 장판 3~6 v1 캡처검증 완료, 이벤트600 깊이 생존(ZTest LEqual OK), ToneGateLab 백업 사본 생성됨
- [killburst-fx](project_killburst_fx.md) — 킬 버스트 v1(시안코어+마젠타엣지), ★URP 파티클 머티리얼은 코드생성 불가→검증머티리얼 복제 필수. ⚠️"Vefects 셰이더 전부 surface(URP 미작동)"는 Pixel Craft/Flipbook/Combat 팩 한정 — "Stylized VFX URP" 팩은 네이티브URP(slash-trail-fx 참조)
- [slash-trail-fx](project_slash_trail_fx.md) — 카타나 슬래시궤적 (C)하이브리드 v1(기성 시안슬래시+ThreatArc 범위). ★Vefects "Stylized VFX URP" 팩=네이티브URP(색프로퍼티 리컬러, BIRP함정 비해당)·표준쿼드로 충분(셰이더 자체스크롤)·SlashVfxPool 자가부트스트랩 싱글톤. 정적검증 완료, 라이브판정 대기
- [caniathrox-attack-fx](project_caniathrox_attack_fx.md) — 개형 적 공격 룩슬라이스 v1 캡처검증 완료(텔레그래프+도약+임팩트링+스핏). ★29종 틀: 포즈/궤적분리(SampleAnimation+placeRoot부모)·ThreatArc 재활용·발광구체로 파티클함정 회피·ScanLit_v2 라이팅 이식
- [playmode-stale-asm-block](project_playmode_stale_asm_block.md) — 플레이모드 캡처 막힘 1순위=컴파일에러로 진입거부+MCP 도메인리로드 큐만됨. 콘솔에러부터 보고 stale 충돌이면 유저 에디터 포커스 요청
- [monster-toon-tonegate](project_monster_toon_tonegate.md) — MonsterToon 셰이더 신설+§6 재판정 비교캡처 v1(유저 눈 판정 대기). ★툰 함정: LIGHT_LOOP=InputData 요구·평탄화는 액센트색 죽임(색조보존 필수)·near-black/스페큘러 몬스터는 PBR 우월·아웃라인만 보편승리·15m거리 PBR/툰 차이 무의미. 파킹 몬스터 루트 비활성→클론 SetActive(true)
- [smash-impact-fx](project_smash_impact_fx.md) — 브루트 슬램 임팩트 주스 v1(가산충격파+먼지+그을림 VFX·카메라쉐이크·히트스탑). ★히트스탑=프로젝트 HitStop.Do 재사용 필수(MMFreezeFrame 금지=시간소유자 경합)·PickupInfo 레이어는 에디터 정적캡처 불가(플레이 필요)·29종 근접 임팩트 재사용 틀. 셰이더 캡처검증·풀플로우 플레이판정 대기
- [stage1-vfx-audit](project_stage1_vfx_audit.md) — 9종 VFX 판단: ThreatArc 전종 재활용가능·신규셰이더0·오버드로우완화3방향·Kupolojuve전격만미커버·잭팟1순위=Fulgurodonte
- [venosaur-claw-impact-fx](project_venosaur_claw_impact_fx.md) — Venosaur 클로 컨택 임팩트 v1(SmashShock 절제재활용·r1.2·0.14s·손본폴백). 시각미검증(KatanaController 에러 해소 후 캡처)
- [reference_enemy_attack_vfx_industry](reference_enemy_attack_vfx_industry.md) — 업계 적 공격 VFX 4축 리서치(3층분해/호드관리/탑다운/위계) + 현행 정합 분석 + 9종 권고 5개
- [vfx-director-infra](project_vfx_director_infra.md) — VfxDirector 싱글톤+VfxLayers 상수 v1. 호드 culling(maxConcurrent=4)+wind-up 글로우. Venosaur 임팩트 첫 전환. 잔무=KatanaCtrl 에러 해소 후 시각검증+나머지 renderQueue 교체
- [monster-aura-board](project_monster_aura_board.md) — 오라 v2 확정(청록폐기): 레드오렌지단일+밝기차등. MonsterSignatureAura.cs v1(시각미검증). AuraGrade LV1~5 SSOT.
