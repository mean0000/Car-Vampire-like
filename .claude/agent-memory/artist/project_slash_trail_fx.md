---
name: slash-trail-fx
description: 카타나 슬래시 궤적 VFX (C)하이브리드. ★v2 재시공(06-14, v1 격노)=공중 선 크레센트(PlaceSlashFlat 세움 PlaneTiltDeg)·평타 채움부채꼴 제거·코어 핫화이트 평타마젠타폐기. 스윕=Vefects 자체UV스크롤. 정적 포즈만 검증가능, 스윕은 플레이모드 필수
metadata:
  type: project
---

카타나 슬래시 궤적 VFX v1 (2026-06-14): 유저 요구 "공격 범위/궤적이 안 보여 답답 — 휩쓰는 슬래시가 팍". (C)하이브리드 = 기성 화려한 슬래시(뽕) + ThreatArc SDF(정직한 범위).

**★핵심 발견 — Vefects "Stylized VFX URP" 팩은 진짜 네이티브 URP다 ([[killburst-fx]] 함정 비해당).**
- 셰이더 `SH_VFX_Vefects_Slash_URP_New`/`SH_VFX_Vefects_Piercing_URP_New` = Amplify 생성, `"RenderPipeline"="UniversalPipeline"` 태그 + `UniversalMaterialType=Unlit`. killburst가 못 박은 "Vefects 3팩 전부 BIRP surface→마젠타"는 *다른 팩*(Pixel Craft/Flipbook/Combat) 얘기. 이 팩은 URP에서 정상 작동.
- **리컬러 = 머티리얼 색 프로퍼티 교체로 끝.** 텍스처는 회색 마스크/노이즈/침식이고 색은 `_Color_1`(코어)·`_Color_2`(엣지)·`_Emissive_Color`(발광) 3개가 담당. 텍스처 추출 불필요. `M_VFX_Slash_Generic_Add`는 *이미 거의 시안*(Color_1 = 0.05,0.37,1)이라 순수 시안(#2DE2E6=0.176,0.886,0.902)으로만 밀면 됨. Piercing/Circle은 레드오렌지라 리컬러 필요.
- 블렌드: Generic_Add = `_Src=1 _Dst=1`(가산), Piercing = `_Src=5 _Dst=10`(알파). 셰이더 Blend는 `_Src`/`_Dst`(Amplify명)를 읽음 — URP표준 `_SrcBlend`/`_DstBlend` 아님.
- 슬래시 텍스처는 UV0 스크롤(_Slash_Speed) → **표준 Unity Quad로 충분**(Vefects 메시 불필요, 셰이더가 자체 애니메이트). 700KB 프리팹 인스턴스 대신 쿼드+머티리얼이 풀링·틸트·스케일 제어에 유리.

**구현 구조** (SmashImpactFX/TelegraphPad 패턴 재사용): `SlashVfxPool`(자가부트스트랩 싱글톤 Instance — 몬스터처럼 스포너 주입 안 받음, KatanaController 첫공격에 1회 참조) + `SlashVfxFX`(인스턴스: 슬래시 쿼드 + ThreatArc 범위 쿼드 + KillBurst 스파크). 3 진입점: `PlayFan`(참격/거합 평타, tier01 콤보스케일), `PlayPierce`(발도 레인), `PlayWave`(참격파 마젠타엣지). KatanaController가 자작 LineRenderer 트레일(`ShowTrailArc`/`ShowTrailLine`)을 풀 호출로 대체 — 단 `_footRing`(게이지)·`_waveArc`(실시간 히트박스 엣지 추적)는 보존, `_slashTrail`은 고아화(폴백으로 메서드 잔존).

**디스크 author 머티리얼** (Resources/VFX/Slash/Materials/): `M_SlashArc_Cyan`(가산 시안호)·`M_SlashPierce_Cyan`(알파 시안찌르기)·`M_SlashWave_CyanMagenta`(Color_1 시안코어+Color_2 마젠타엣지=오버드라이브 캐넌). 셰이더/텍스처 guid 보존 복사 + 3색만 교체. 풀이 Resources.Load해 복제 주입(흰폴백 회피).

**렌더 경로**(동결 재활용): PickupInfo 레이어(13)→이벤트600 콘면제. 슬래시 renderQueue 3050 + ZTest Always(공중궤적 깊이무시 항상보임), 범위 SDF 3000 + ZTest LEqual(바닥장판처럼 깊이존중). unscaledDeltaTime(히트스탑 일관).

**노브 위치**(유저 시각판정 대기): SlashVfxFX 내 — 빌보드 틸트 `PlaceSlashFlat`(15~18°, 부감 단축보정 — 틸트축 검증 필요)·호 확대 `1+0.4*tier01`·마젠타 혼합 `tier01*0.25`(5단 25%)·HDR밝기 `Cyan*Lerp(0.85,1.6)`·수명 `_slashLife`·범위 fillAlpha. SlashVfxPool poolSize=6.

**미검증/판정대기**: 라이브 캡처 보류(에디터 타세션 점유). 정적검증만 완료(타입·시그니처 일관, 머티리얼 YAML 유효). `M_SlashPierce_Cyan.mat.meta`는 타세션 미임포트(리프레시 시 자동생성). ★틸트 방향·슬래시텍스처가 부감에서 읽히는지·범위SDF와 궤적 중첩 가독성 = 유저 눈.

────────────────────────────────────────
**★v2 재시공 (2026-06-14) — v1이 인게임에서 형편없어 유저 격노. SlashVfxFX.cs만 수정(시각만, 배선 무수정).**
v1 3대 오판 → 교정:
1. **바닥 스티커였다** → `PlaceSlashFlat`을 *세움*. v1=`Euler(90-tilt, yaw, 0)` tilt 15~18°(=거의 바닥, X회전 72~75°)였다. v2=`Euler(PlaneTiltDeg, yaw, 0)`, **PlaneTiltDeg는 수직(0)~바닥(90) 측정** → 42°면 바닥에서 48° 일어섬. 높이 0.7(발치)→1.1(가슴). 정적 포즈 캡처로 `normalDot(up)=0.67` 확인(v1≈1.0=납작). ★이게 1순위 노브.
2. **정적이었다** → 평타 ThreatArc 채움 부채꼴 *완전 제거*(`SetupRangeFan` 삭제, `_rangeMr.enabled=false`). 슬래시 크레센트(diameter=실제 reach)가 범위를 말함. 스윕 자체는 **Vefects 머티리얼 자체 UV스크롤**(`_Slash_Speed 0.8`/`_Emissive_Slash_Speed 1`)이 담당 — 셰이더 재작성 안 함. 발도/참격파 범위는 `FillAlpha=0`+`EdgeAlpha 0.4~0.45`로 **희미한 엣지 선만**.
3. **잡탕이었다** → 평타 코어=핫화이트 HDR(`Color.white*Lerp(1.1,2.0,tier01)`), 엣지=시안(머티리얼 `_Color_2`). **평타 마젠타 폐기**(`Magenta` 필드도 삭제 — 색 캐넌 위반). 참격파만 마젠타 엣지 유지(머티리얼 _Color_2=오버드라이브 캐넌).

**v2 노브**(SlashVfxFX 상단 static): `PlaneTiltDeg=42`(평타, 0빌보드~90바닥)·`SlashHeight=1.1`·`PierceTiltDeg=35`/`PierceHeight=0.85`(발도)·`WaveTiltDeg=72`/`WaveHeight=0.25`(참격파 AoE, 바닥에 가깝게). 코어밝기 `Lerp(1.1,2.0)`·수명 `_slashLife`.
**★검증 한계**: 정적 포즈/지오메트리/색만 확인 가능(스윕은 `_Time` UV스크롤이라 edit-mode 동결→블롭으로 보임). **스윕·최종 크레센트 모양 = 반드시 플레이모드**. 유저가 플레이 스크린샷으로 판정. 정적 캡처를 "완성"으로 읽지 말 것.

관련: [[killburst-fx]] [[telegraph-pad-fx]] [[smash-impact-fx]]
