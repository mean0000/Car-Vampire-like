---
name: smash-impact-fx
description: Crassorrid 브루트 슬램 임팩트 주스 v1(충격파+먼지+그을림 VFX + 카메라쉐이크 + 히트스탑) — 근접 종 임팩트 재사용 틀. 셰이더 캡처검증, 풀플로우 미검증(플레이판정)
metadata:
  type: project
---

Crassorrid(7m 거구 브루트) 슬램 임팩트 주스 v1 (2026-06-14) — 유저 "덩치 큰데 위협 안 됨, 압박감 없음, 근접도 VFX 필요". 슬램은 이미 빠름(StrikeSpeed 2.8)·AoE 큼(r5). 닿는 *순간의 충격*을 추가. 미커밋(유저 플레이 판정 대기).

**파일**:
- `Assets/_Project/Setting/SmashShock.shader` — 가산(Blend SrcAlpha One) 발광 충격파 링. ★ThreatArc(장판=alpha-blend 바닥데칼)와 의도적 차별: 이건 "터졌다/쾅"(HDR 블룸). 월드미터 SDF(쿼드 localScale==_SizeWorld). _Progress 0→1로 ease-out 확장 + 폭 수축 + 중심 섬광(0~0.25 구간 흰핵) + 끝 페이드. 엣지 노이즈 침식(정답UI 회피).
- `Assets/_Project/Scripts/SmashImpactFX.cs` — 임팩트 1개 인스턴스: 충격파 메시 + 코드빌드 먼지 ParticleSystem(검증 머티리얼 복제) + 그을림 알파쿼드. 자체 수명관리, _life 후 풀 Return.
- `Assets/_Project/Scripts/SmashImpactPool.cs` — TelegraphPool 동형 프리웜/재활용. InitPoolSize(inactive-GO 주입). Resources에서 `VFX/Materials/M_KillBurst_Body` 복제 베이스 로드.
- `Assets/_Project/Scripts/SmashFeel.cs` — 정적 헬퍼. 카메라쉐이크(MMCameraShakeEvent)+히트스탑.
- 드라이버 `CrassorridBrawler.cs` SmashHit 훅에 FireSmashImpact 추가(★기존 _activePad.ForceFull 장판 동기는 불변, *옆에* 추가). 임팩트/쉐이크/히트스탑 SerializeField 노브 다수. _telegraphOrigin 캐시.
- 스포너 `CrassorridLabSpawner.cs` — SmashImpactPool 생성·주입 + SmashFeel.EnsureListeners(JudgeCam).

**★★히트스탑 = 프로젝트 네이티브 HitStop.Do() 재사용 (기존 권위 재정합 — 핵심 교훈)**:
- 처음엔 Feel MMFreezeFrameEvent(timeScale=0)로 짰으나, 프로젝트에 **이미 `HitStop.Do(seconds)`가 있음**(`Assets/_Project/Scripts/HitStop.cs`: timeScale=0.05·중첩 더긴쪽 연장·★OnDestroy 복원 가드·"시간의 사다리" 도큐멘트). 두 시간 소유자가 Time.timeScale 두고 싸우면 복원 경합 사고(Codex 1순위 RISK + HitStop.cs 주석 "다른 시간 연출 생기면 소유권 조율"). → SmashFeel.HitStop은 `global::HitStop.Do()`로 위임 = 단일 시간 소유자, 복원 안전(가드 상속), timeScale=0.05(완전0 아님→영구정지 불가능). **29종 임팩트 전부 HitStop.Do 경유(MMFreezeFrame 짓지 말 것).** 카메라쉐이크는 프로젝트 컨벤션 없어 Feel MMCameraShakeEvent 채택(충돌0, JudgeCam에 MMCameraShaker 런타임 부착).

**★PickupInfo 레이어 VFX = 에디터 정적 cam.Render() 캡처 불가**:
- 충격파/먼지/그을림은 PickupInfo 레이어(시야콘 면제 경로). 이 레이어는 URP_HighFidelity_Renderer의 PickupInfoOverlay RenderObjects(이벤트600) 패스로만 그려짐 → 맨 Camera의 `cam.Render()`(에디터)는 그 패스를 안 돌려 **안 보임**. 게다가 COZY 돔 그라디언트만 잡힘(killburst/cozy 함정 재현). → **풀 임팩트 정적 검증 불가, 플레이모드 필요.** 셰이더 자체는 임시 쿼드를 Default 레이어+직접 머티리얼+탑다운 ortho로 분리 렌더하면 검증 가능(이걸로 충격파 OK 확인).
- 검증 완료: 충격파 셰이더 라이프사이클(p0.08 중심섬광 → p0.45 밝은확장링 → p0.80 페이드 얇은링) 전부 의도대로. 캡처=`docs/03_reference/assets/smash_impact/shock_*.png`. ★먼지/그을림/쉐이크/히트스탑은 정지컷 검증 불가(플레이 판정).

**★29종 근접 임팩트 재사용 틀 (Caniathrox/Dimax에 얹는 법)**:
- SmashImpactPool은 종 무관 — 스포너가 만들어 드라이버에 주입, 드라이버는 SmashHit/ClawHit(컨택 AnimationEvent)에서 `pool.Acquire().Play(origin, radius, color, ...)` 호출. 원점 = 텔레그래프 약속지점(현재 forward 재계산 ❌, 캐시한 origin). Dimax는 텔레그래프 없으니 클로 컨택 본 위치로.
- 충격파 = additive 메시(파티clel 함정 회피, Caniathrox 발광구체 패턴 변형). 먼지 = 코드빌드 PS + **검증 머티리얼 복제**(M_KillBurst_Body — [[killburst-fx]] 흰폴백 함정 회피). 그을림 = 알파블렌드 쿼드 페이드.
- 노브 전부 드라이버 SerializeField(런타임 AddComponent라 코드 default가 먹음, 씬 덮어쓰기 없음).

**리뷰 반영(Stab+Codex 2병렬, 채택)**: ① SmashImpactFX.Update `Time.deltaTime`→`unscaledDeltaTime`(히트스탑 중 충격파 동결/점프 방지, 쉐이크 useUnscaledTime과 일관). ② 히트스탑 MMFreezeFrame→HitStop.Do 위임(위 핵심). ③ 셰이더 null 시 `new Material(null)` 분홍 함정→충격파 메시 생략 가드(Play/Update 전부 null가드). ④ MainColorId==ColorId 중복필드 제거. ⑤ RecycleOldest에 ForceDeactivate 방어. ⑥ gen 필드는 fire-and-forget이라 외부취소 없음 = 미사용 OK(누락 아님, Codex 확인).

**★빌드 시 SmashShock 셰이더 Always Included 등록 필요**(ThreatArc guid b00c294b…처럼 GraphicsSettings.asset에). 에디터 플레이는 Shader.Find로 동작하나 빌드 스트립 위험. 미등록(미커밋 단계).

관련: [[killburst-fx]] [[telegraph-pad-fx]] [[caniathrox-attack-fx]]
