---
name: caniathrox-attack-fx
description: Caniathrox(개형 추격자 LV2) 공격 룩/연출 슬라이스 v1 — 텔레그래프+도약+임팩트링+스핏 풀사이클, 캡처검증 완료. 29종 몬스터 공격연출의 틀
metadata:
  type: project
---

Caniathrox 공격 "룩 슬라이스" v1 (2026-06-13) — 데미지·AI·히트판정 제외, 비주얼+타이밍+속도감만. 나머지 29종 몬스터 공격 연출의 틀.

**파일**:
- `Assets/_Project/Scripts/CaniathroxAttackDemo.cs` — 연출 드라이버. 위상시계(초) 기반 `ApplyPhase(t)` 단일 진입점, `debugFreeze`(-1=자동루프 / 0~1=위상고정 캡처). VFX 전부 런타임 생성+OnDestroy 정리.
- `Assets/_Project/Scripts/Editor/CaniathroxLabCapture.cs` — 씬 빌더 + 연속 위상 스윕 캡처(10컷). 메뉴 `ZombieCrush/Caniathrox Lab/Build Scene`·`Capture Sweep`.
- `Assets/_Project/Scenes/Greybox_CaniathroxLab.unity` — 랩 씬.
- 캡처: `docs/03_reference/assets/caniathrox_lab/cani_NN_pXXX.png` (위상값 파일명).

**사이클 구조(위상 구간 누적)**: 웅크림(Roar 포즈, 0.15s) → 도약(JumpBite 포즈 + 포물선 코드보간, 체공 0.5s, 텔레그래프 원 r1.5 등장+0.6s EaseInQuad 채움) → 착지(임팩트 링 r1.5→2.5 확산 0.15s+페이드 0.1s, 히트스탑 0.045s) → 스핏(Spit 포즈, 윈드업 0.2s, 글롭 탄속 10m/s, 머즐 0.05s) → 휴지(IdleAngry 0.35s). 막판 0.18s 2회 깜빡.

**★재사용 아키텍처 (틀의 핵심)**:
- **포즈/궤적 분리**: `_placeRoot`(부모)가 도약 위치(포물선) 코드 소유, model(자식)에 `AnimationClip.SampleAnimation(model.gameObject, t)`로 포즈만 샘플. SampleAnimation이 루트를 클립원점으로 덮어도 placeRoot가 위치 소유라 무해 — [[project_animation_inplace_gotchas]] 함정 우회. `animator.applyRootMotion=false` 런타임 설정(에셋 무변경).
- **클립 직접 샘플** = 컨트롤러/스테이트머신 불필요, 프레임 정확한 위상 고정(캡처) 가능. 프리팹 원본 컨트롤러·루트모션 안 씀.
- **텔레그래프 재활용**: ThreatArc 스타일3(원) 그대로. 임팩트 링 = 스타일6(링)을 쿼드 localScale+`_SizeWorld` 동반 확장으로 shockwave화(셰이더 무수정). ThreatArc 이미 Always Included라 신규 셰이더 등록 불필요. TelegraphLab MakeQuad 패턴 복사(X+90°, y=0.05, renderQueue=3000, ZTest=4 LEqual, PickupInfo 레이어).
- **스핏 글롭 = 발광 구체(URP/Lit emission, MeshRenderer)** — [[killburst-fx]] URP 파티클 머티리얼 함정 회피용으로 파티클 대신 메시 사용. 트레일=LineRenderer+URP/Particles/Unlit 가산. 색=레드오렌지(1,0.30,0.08) 단색 — 산성이라도 노란끼 금지(색 캐넌).

**라이팅 이식(ScanLit_v2 "Frozen Golden Hour")**: ToneGateLab 실측값 하드코딩 — 워밍키 디렉셔널(1,0.78,0.55 ×2.6 Soft섀도우, 쿼터니언 raw 그대로) + 쿨필(0.75,0.8,0.95 ×0.8 무섀도우) + 트라이라이트 앰비언트(sky 0.88,0.88,1.364×2.2 / equator 1.496,0.898,0.636×1.87 / ground 0.748,0.449,0.318×1.87) + 공유 볼륨 프로파일 `Assets/_Project/Setting/Greybox_ScanLit_v2_Post.asset`. ToneGateLab의 ember 포인트라이트는 세트 드레싱이라 제외.

**캡처 검증 완료(10컷 시각판정)**: 풀사이클 전부 읽힘 — 도약 시 높이단서 그림자(공중=작고옅음), 텔레그래프 외곽선→채움 카운트다운, 임팩트 링이 펜에 비해 확장(="장판이 터졌다"), 스핏 글롭+트레일이 더미 명중. 색 캐넌 전구간 유지. 디스크렌더 레시피=[[cozy-mcp-bypass]] 그대로.

**v2 카메라 교정 + 스핏 색 교정 (2026-06-13, 오케스트레이터 1차 반려 후)**:
- **JudgeCam = 게임 권위값으로 고정** (docs/00_authority/2026-06-10-camera-system.md: 피치45°·FOV50·거리15m밴드). v1은 euler 45°였는데도 "탑다운"으로 읽혔다 — ★함정: **euler가 45°라도 카메라 위치가 틀리면 탑다운으로 보인다.** v1 pos=(2.5,15,-13)은 높이15m/후퇴13m라 1:1(진짜45°)보다 가파르고 너무 멀어 지면이 압축됨. 교정=aimPoint에서 (위=dist·sin / 뒤=dist·cos)로 후퇴: aim(-0.5,1.3,0)·dist12 → pos(-0.5,9.79,-8.49)·euler45·FOV50. 거리는 권위 15m 밴드 근거리단 12m로(모션 스냅 가독 위해 최대 당김). aimPoint Y=1.3으로 지상몹은 화면중앙 아래·도약정점은 상단여유.
- **★발광 구체 노랑 탈색 함정 (재사용 핵심)**: 작은 HDR 발광 구가 노랑/흰끼로 뜨는 원인 2개 합산 — ①URP/Lit이면 워밍키 태양(1,0.78,0.55)이 표면을 비춰 색 오염 ②ScanLit_v2 블룸 tint가 크림(1,0.95,0.88)이라 모든 하이라이트를 따뜻하게 당김(threshold 1.1·intensity 0.5). **해법=구체를 URP/Unlit HDR baseColor로**(라이팅 오염 차단) + R을 G의 3배+로(블룸 코어가 흰끼 대신 오렌지로 핀다). 글롭 base(1.6,0.48,0.13)·머즐(2.0,0.55,0.14). ★블룸 프로파일은 공유 베이스라인이라 건들지 말고 액터 머티리얼로 해결. 29종 발광 VFX 전부 이 패턴.
- 스핏 더미는 무광 회색(_Smoothness 0.05)으로 — 스펙큘러 핫스팟이 분홍 과발광하던 것 제거.
- v2 캡처=`cani_v2_NN_pXXX.png` 12컷(액션 밀집: 웅크림~정점~슬램~임팩트 8컷+스핏 4컷). 전 구간 시각검증 완료(도약/슬램/임팩트링/스핏 전부 읽힘, 색 레드오렌지 유지).

**다음 조정 후보(유저+오케스트레이터 캡처 판정 대기)**: ① 속도감 — 정지컷으론 판정 불가, 실플레이 체감으로 도약 체공 0.5s·채움 0.6s 재판정 필요(채움 ease-in 가속 곡선 강도 포함). ② 임팩트 링 두께(_InnerR01=0.62)·확산 거리(r2.5). ③ 스핏 트레일 길이(0.6m)·글롭 발광강도(emission ×4). ④ 히트스탑은 현재 모션정지만(timeScale 미사용) — 게임플레이 단계에서 카메라쉐이크 합류 시 재설계. ⑤ 머즐 플래시 위치(입 근사 — placeRoot forward 0.9+up 1.0, 실제 본 어태치로 정밀화 가능.

관련: [[telegraph-pad-fx]] [[killburst-fx]] [[cozy-mcp-bypass]] [[project_animation_inplace_gotchas]]
