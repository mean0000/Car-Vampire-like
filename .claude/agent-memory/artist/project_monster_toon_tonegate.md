---
name: monster-toon-tonegate
description: MonsterToon 셰이더 신설 + §6 "괴수 셀셰이드 금지" 재판정 비교 캡처 v1(유저 눈 판정 대기). 툰 함정 3종 + 몬스터별 적합도 결론
metadata:
  type: project
---

Protofactor 괴수에 툰 셰이더를 박아 Synty 로우폴리(Toon City) 월드와 어울리는지 재판정하는 비교 캡처 (2026-06-13). §6 "괴수 셀셰이드 금지"는 월드가 절제된 리얼일 때 나온 B등급 판정이라, Synty 로우폴리로 월드가 바뀐 지금 재판정(유저 지시). **최종 미적 콜은 유저 눈 — 캡처만 산출, 동결 아님.**

**파일**:
- `Assets/_Project/Shaders/MonsterToon.shader` — 신규 URP 툰(테스트 자산). ActorRimLit 스켈레톤 기반(=이 프로젝트에서 컴파일 보장). 핸드롤 HLSL(Shader Graph 아님 — 그래프 JSON 손저작은 취약, HLSL이 검증 가능한 길. 유저 의도="URP 툰 램프+아웃라인"은 충족). 노브: `_RampSteps`(밴드수)·`_RampSmoothness`·`_LitBoost`·`_ShadeTint`/`_ShadeFloor`·`_SatFlatten`/`_DetailFlatten`(평탄화, 기본 OFF)·`_OutlineWidth`/`_OutlineColor`·`_RimIntensity`.
- `Assets/_Project/Scripts/Editor/MonsterToonToneGate.cs` — 비교 캡처 리그. 메뉴 `ZombieCrush/Tone Gate/Capture Monster Toon Comparison`.
- 캡처: `docs/captures/2026-06-13-monster-toon-tonegate/` — 몬스터3종(Caniathrox·Fulgurodonte·Crassorrid)×{A=원본PBR / B=툰기본3밴드 / C=툰그래픽2밴드} 근접 9컷 + `_WIDE_A/B`(실 JudgeCam 15m 게임거리, 베이스라인 야간노출) 2컷.

**셰이더 구조(틀)**: 패스0=아웃라인(인버티드 헐, `Cull Front`, 뷰공간 노멀 압출 `TransformWViewToHClip`, 깊이쓰기). 패스1=툰 포워드(`GetMainLight`→NdotL 0.5+0.5 wrap→`PosterizeLight` N밴드 양자화→`lerp(shadeTerm, litTerm, band)`, 색조보존). 패스2/3=ShadowCaster/DepthOnly(URP 표준 include).

**★툰 함정 3종 (재사용 핵심)**:
1. **`LIGHT_LOOP_BEGIN`(클러스터 경로 `_CLUSTER_LIGHT_LOOP`)는 스코프에 `InputData inputData`를 요구** — `inputData.normalizedScreenSpaceUV`로 타일 조회. PBR 핸드롤로 InputData를 안 만들면 "undeclared identifier 'inputData'" 컴파일 에러(렌더 시점에만 뜸, `ShaderUtil.ShaderHasError`는 기본셋만 봐서 못 잡음). → 루프 전에 최소 InputData(positionWS + normalizedScreenSpaceUV) 선언.
2. **색조보존이 필수 — 평탄화(desaturate/detail-flatten)는 몬스터 정체성을 죽인다**. 첫 시도에서 `_SatFlatten`/`_DetailFlatten`을 평균알베도로 당겼더니 Fulgurodonte(전기블루+마젠타 다리)·Crassorrid(마젠타 변이 촉수)의 **포화 액센트가 회색으로 증발** → §3 색규약(자연 액센트색=괴수 위협언어)과 정면충돌. 해법=평탄화 기본 OFF, 알베도 색 그대로 유지하고 **라이팅만 포스터라이즈 + 아웃라인**. 그림자도 `albedo×lerp(shadeTint,white,_ShadeFloor)`로 색조 유지(검정 크러시 금지). `_LitBoost`로 라이트 밴드 펀치.
3. **어두운 알베도는 툰으로 못 살린다** — `albedo×boost`는 0.05×1.2=0.06이라 곱셈으로 못 밝힘. Fulgurodonte처럼 본질이 near-black이거나 정체성이 스페큘러/이미시브(전기·젖은·금속)면 PBR이 그 하이라이트로 form을 살리는데 툰은 양자화로 그걸 죽임 → 툰이 오히려 손해.

**★몬스터별 적합도 결론 (정직 읽기 — 유저 판정 입력용)**:
- 툰이 **잘 받는 타입**: 매트한 중~밝은 알베도(Crassorrid 창백한 몸통, Caniathrox 주둥이). 램프 밴드가 또렷이 읽히고 일러스트풍으로 끌려옴.
- 툰이 **안 받는 타입**: near-black 알베도 / 스페큘러·이미시브가 정체성인 개체(Fulgurodonte 전기충). 툰=어두운 덩어리로 퇴화, PBR이 명백히 우월.
- **아웃라인 = 유일한 보편 승리**: 전 몬스터·전 거리에서 "Synty에 앉힘"에 기여. 단 두께 과하면(graphic 2.6) form을 잡아먹음 → 2.0 권장.
- **15m 게임거리(`_WIDE`)에선 PBR vs 툰 차이 거의 무의미** — 둘 다 작은 어두운 실루엣, 아웃라인 엣지만 미세하게 크리스피. §6 "톱다운 15m=면디테일 소실, 아웃라인만 남음" 예측 실측 확인. **툰의 가치는 클로즈업(스킬컷인·정산·타이틀)에서만.**
- **하이폴리 실루엣은 못 지운다**: 거미다리·털뭉치·촉수 지오는 셰이딩과 무관하게 남음(§6 정직조항 그대로). 툰=리얼괴수를 "스타일라이즈드 정교괴수"(보더랜드식)로 *끌어오지만* Synty 단순 실루엣엔 못 맞춤.

**★캡처 리그 함정 (재사용)**:
- **파킹 몬스터 루트 비활성 함정**: `_ToneGateStage`의 14종 중 활성은 일부뿐(Caniathrox만 활성, Fulgurodonte·Crassorrid는 `rootActiveSelf=False`). `Object.Instantiate`는 비활성 상태를 그대로 복제 → 클론이 안 보임(혹은 마젠타 유령). **클론 후 `SetActive(true)` 필수.** 사전배치 모델 전반 지뢰([[project_rear_threat_hint_cleanup]] Init 누락과 같은 계열).
- **격리 무대 레시피**: 도시에서 멀리(X400,Z400) Synty Pavement_1A_4x4(**실측 4×4m** — 인접 배치는 4m 간격, 8m면 틈 보임) 13×13 + Building_13A 2동 측면 후방 + 공정 조명(워밍키 front-key Euler(42,25,0)=카메라 뒤위에서 +Z로 진행해 카메라향 면 라이팅 + 쿨필). **노출은 비교 가독용**(베이스라인 야간 아님 — 야간 실측은 `_WIDE`). 거리맞춤=`dist=radius/tan(halfFov)*1.7`로 크기 무관 동일 프레임.
- 검증=에디터 디스크렌더(`SubmitRenderRequest`, 플레이모드 불필요 → stale-asm 블록 회피). 원본 머티리얼 복원·씬 미저장·임시오브젝트 전부 OnEnd 정리 확인.

관련: [[caniathrox-attack-fx]] [[cozy-mcp-bypass]] [[killburst-fx]]
