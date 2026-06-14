---
name: reference_shader_post_inventory
description: "ZombieCrush 보유 셰이더/포스트 자산 카탈로그 — 신규 셰이더 명세 전 여기부터 확인(중복 방지)"
metadata:
  type: reference
---

신규 셰이더/포스트 명세 전 기존 자산 확인용. 위치·역할만 — 코드는 파일이 진실.

**액터 셰이더** (`Assets/_Project/Shaders/`):
- `ActorRimLit.shader` — 풀 URP PBR(UniversalFragmentPBR) + view-dir Fresnel 림(이미시브 가산, 암부에서도 발광). HDR `_RimColor`/`_RimPower`/`_RimIntensity`. GI/APV/그림자/포그 전부 지원. → MonsterActor 베이스, 괴수 가독성 핵심
- `MonsterToon.shader` — 핸드롤 툰: 인버티드헐 아웃라인(Pass0 Cull Front) + 색조보존 램프 포스터라이즈 + 평탄화(`_SatFlatten`/`_DetailFlatten`). 톤게이트 테스트용. 노멀맵 안 샘플(이미 flat). 컷인/매트개체용 도구로 보관
- `AITagOutline.shader` / `LowPolyFlat.shader`(은퇴 예정)

**FX 셰이더** (`Assets/_Project/Setting/`):
- `ThreatArc.shader` — 장판 텔레그래프(원/레인/부채꼴/링 월드미터 SDF). AfterPost 재드로우 렌더경로 → 발밑 링 데칼 재사용 가능
- `TiltShift.shader` + `M_TiltShift.mat` — 스크린스페이스 틸트시프트(화면Y 기반, 거리DOF 아님). 조준 게이트 전용
- `ConeGhost.shader` / `LKPGhost.shader` — 시야콘 밖 좀비 탈색 고스트/잔상
- `SyncGlitch.shader` — 마젠타 신호붕괴 글리치(sync0.7+/사망)
- `InkBlob.shader` / `SmashShock.shader` — 임팩트 프레임(화이트아웃+잉크+스미어). ⚠️Shader.Find 빌드 스트립 → Always Included 등록 필요
- `WallCutaway.shader` — 벽 차폐 페이드
- `UIAdditive.shader`

**포스트 자산** (`Assets/_Project/Setting/`):
- `Greybox_ScanLit_v2_Post.asset` — 마스터 포스트 스택(ACES·블룸 thr1.1·비네트0.12·필름그레인·SplitToning). 마스터 승격 예정
- `StyleLab_Post.asset` · `Post FX.asset` · `Greybox_ScanLit_Post.asset`(구버전)
- `TiltShiftConeDriver.cs` — 시야콘 틸트시프트 런타임 구동

⚠️ Vefects 3팩 = BIRP surface라 URP 변환 불가 → 텍스처 추출+Feel 머티리얼 복제로 우회. 신규 VFX 조달 시 URP 호환 선확인.
⚠️ 미적 검증 = JudgeCam→PNG→Read (MCP Camera_Capture 죽은 프레임). MonsterToon은 명세 Shader Graph였으나 .shadergraph JSON 손저작 취약 → HLSL로 우회(프로젝트 관행).

연동: [[project_anti_crude_framework]]
