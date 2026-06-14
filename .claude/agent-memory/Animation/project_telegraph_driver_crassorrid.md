---
name: project-telegraph-driver-crassorrid
description: Crassorrid 접근형 브루트 상태머신·AI + ★ThreatArc 텔레그래프 첫 게임 통합(스폰→윈드업 동안 채움→임팩트 이벤트 ForceFull 발동). 네 번째 몬스터 틀.
metadata:
  type: project
---

네 번째 몬스터 틀 = **접근형 브루트 "접근→정지→예고원 차오름→내려찍기 광역"**(Caniathrox 돌진·Venodonte 원거리·Dimax 클로월에 이은). Crassorrid LV4 정예 스매시. ★메인 신규 = **지어두고 한 번도 안 쓴 ThreatArc 텔레그래프(TelegraphPad/Pool)의 첫 소비자** — Venodonte ProjectilePool에 대응하는 재사용 시스템의 첫 가동.

## 만든 파일 (미커밋 — 유저 ▶ 플레이 판정 대기)
- `Assets/_Project/Scripts/CrassorridBrawler.cs` — 접근형 브루트 AI(CaniathroxChaser 패턴 차용: Idle→Roar→Approach 루트모션→스매시). ★텔레그래프 첫 소비. AnimationEvent 콜백 SmashHit.
- `Assets/_Project/Scripts/CrassorridLabSpawner.cs` — 랩 스포너. ★결정적 차이 = TelegraphPool *실제 생성·주입*(Dimax는 제거했음).
- `Assets/_Project/Scripts/Editor/CrassorridLabSetup.cs` — ★진실원 빌드스크립트(SmashAttack_RM 3분할·SmashHit 주입·컨트롤러 6상태 빌드).
- `Assets/_Project/Scripts/Editor/CrassorridLabCapture.cs` — 씬빌드+디스크 캡처(watcher 상태명=Roar/Approach/SmashWindup/SmashStrike).
- `Assets/_Project/Animations/CrassorridBrawler.controller`(6상태) + `CrassorridRM/Crassorrid@SmashAttack_RM.fbx`(3분할 사본).
- 씬 `Assets/_Project/Scenes/Greybox_CrassorridLab.unity`. 캡처 출력 `docs/03_reference/assets/crassorrid_lab/`(플레이 후).
- ★재사용 토대 수정: `TelegraphPool.cs`에 `InitPoolSize(int)` 추가(#if 없는 런타임 주입 — SerializedObject 에디터전용 함정 회피).

## 상태머신 (6상태 / 3파라미터 — Caniathrox 접근형 + 단일 시그니처 공격)
```
Idle ──(attack 트리거)──▶ Roar(speed5, 오프너 1회)
  │                          │ ExitTime0.92 + isApproaching
  │(isApproaching)           ▼
  └──────────────────▶ Approach(Run_RM 루트모션, 자기루프 지속)
                             │ (smash 트리거 = 도착 smashRange4.0 + 토큰)
                             ▼
                       SmashWindup(0.5×, 장판 스폰·차오름) ──CUT──▶ SmashStrike(1.25×, ★SmashHit=장판 발동) ──CUT──▶ SmashRecovery(1.4×) ──ExitTime0.95──▶ Idle
```
- 파라미터: attack(Trig)·isApproaching(Bool)·smash(Trig). ★BlendTree 없음(단일 Approach 상태) → "Float여야" 함정 없음.
- 전이 전부 dur0. 스매시 3구간 ChainCut(exit0.99/dur0) 연속(같은 take 분할 = 비트동일 = crossfade 아님).
- ★회전 경계: 회전 O = Roar/Idle/Approach(Steer)/★SmashWindup(Steer, turnSpeed제한). 회전 0 = SmashStrike/SmashRecovery(내려찍기 궤적·전진 보존). Windup은 cocking이라 조준 허용(헌법 정신 — 내지르기 전).

## ★★텔레그래프 첫 통합 — 스폰/채움/발동 동기 (이 슬라이스의 핵심, 재사용 패턴)
- **스폰**: SmashWindup 진입 1회(`_windupSpawned` 엣지가드) → `telegraphPool.Acquire()` → `pad.SpawnCircle(전방원점, r3, fillDuration, hold, 레드오렌지, 알파들)`. 원점 = `model.position + model.forward*2.5`(스매시 전방 3.5m 전진 감안). ★스폰 *시점* 전방에 고정(채움 중 안 옮김 = 공정한 약속, 회전해도 장판 안 따라 돔).
- **채움**: fillDuration = `SmashWindupToImpactSeconds()` = 윈드업실시간(15f/30÷0.5=1.0s) + 스트라이크임팩트까지(5f/30÷1.25=0.133s) = **~1.133s**(LV4 윈도 1.0~1.4s 정중앙). Pad.Update가 _Progress 0→1 구동.
- **발동**: SmashStrike 임팩트 프레임(20) AnimationEvent **SmashHit** → `pad.ForceFull(_activeGen)`로 채움 완료=발동(클립 미세 어긋남 보정) + 향후 광역 히트 훅. 발동 후 `_activePad=null`(Pad가 holdAfterFull 뒤 자동 반납 — 이중반납 방지).
- ★gen 세대 가드: `_activeGen=pad.Gen` 저장 → ForceFull/CancelImmediate에 전달(풀 회수로 주인 바뀐 stale 패드 오발 차단, H-2 패턴).
- ★OnDisable `CancelTelegraph()` = 시전 중 사망/비활성 시 차오르던 장판 즉시 취소(시체 위 "닿는다" 거짓말 방지 = 공정성 §북극성6).
- ★렌더 경로 검증: ThreatArc 셰이더 FOUND + PickupInfo 레이어=13(콘면제 경로 라이브). §5 함정(시야콘 합성이 투명VFX 지움) = PickupInfoOverlay 재드로우로 회피(재발명 금지).

## ★2중 리뷰 반영 (Stab+Codex 2026-06-14 — 적용 완료)
1. **(convergent HIGH) OnDisable ResetCombatState 누락** → 추가. 풀링/재활성 시 stale `_smashFired`/`_windupSpawned`/`_engaged`가 다음 교전서 스매시·장판 무음 스킵하는 비대칭 차단(Dimax 패턴 계승).
2. **(Stab Critical C-1) TelegraphPool poolSize 주입 #if UNITY_EDITOR** → `TelegraphPool.InitPoolSize()` 추가로 #if 제거(빌드 무력화 함정 회피). 스포너 inactive GO→AddComponent→InitPoolSize→SetActive 순서 유지.
3. **(Stab H-1) _windupSpawned 리셋을 Strike 분기에서만** → 상단 엣지가드 `if(s!=SWindup)_windupSpawned=false`로 이관(Strike 건너뛴 비정상 복귀서도 자가치유).
4. **(Stab H-2) _smashFired 후 Approach 배율 누수** → `if(!_smashFired)`로 가드(전이 프레임 배율 누수 시 Windup 0.26× 슬로우 desync 차단).
5. **(Stab H-3) Animator-on-root 가정** → Awake에 `modelAnimator.gameObject != gameObject` LogError 어서션(SmashHit SendMessage 도달 가정 문서화).
6. **(Stab L-3) Windup 회전 FaceSteer(스냅)→Steer(turnSpeed 제한)** — 거구 스냅 회전 제거(브루트 무게 + 윈드업 중 측면 잡기 = 공정성).

## 노브 (유저 ▶ 튜닝 — 드라이버 SerializeField/const)
| 노브 | 시작값 | 의미 |
|---|---|---|
| WindupSpeed | 0.5 | ★윈드업 무게/텔레그래프 길이. ↓=더 느리고 무겁게(피하기 쉬움), ↑=빠른 윈드업. const(SetupData 재실행). |
| StrikeSpeed | 1.25 | 내려찍기 폭발성. const. |
| smashRange | 4.0 | 스매시 발동 거리(스매시 전진 3.5m 감안). |
| approachSpeed | 5.0 | 거구 접근 속도(걷기5.5보다 살짝 느림 — 추격 약함, 위협은 슬램). 배율=5.0/9.5728. |
| turnSpeed | 180 | 브루트 추적(작게=묵직, 측면 잡기 허용). |
| telegraphForwardOffset/Radius | 2.5/3.0 | 장판 위치·크기. |
| telegraphColor/알파들 | 레드오렌지 | ★음색=유저 귀/눈 판정(베이스라인 라이팅). |
| maxAttackTokens | 1 | 동시 스매시 수(거구 여럿 동시 슬램 불공정 → 1~2). |

## 유저 ▶ 판정 대기 (정지캡처로 못 봄 — MCP 플레이모드 막힘, 흐름/체감은 플레이로만)
- ★**브루트 무게감** — 큰 느린 윈드업 + committed 슬램이 "묵직·위협적"으로 읽히나(북극성: 속도감보다 위협감). 굼떠 보이면 실패지만 빠를 필욘 없음 — WindupSpeed가 무게 노브.
- ★**예고원 가독성** — 전방 ●r3 원이 "여기 떨어진다"로 읽히나. telegraphForwardOffset 2.5가 실제 착탄(전방 3.5m 전진)과 맞나.
- ★**채움 타이밍 피할만한가** — fillDuration 1.133s 동안 대시1+이동으로 빠져나갈 수 있나(LV4 약속). 너무 짧으면 WindupSpeed↓.
- ★**텔레그래프 음색** — 레드오렌지 톤·알파·외곽선 두께(artist/유저 튜닝).
- ★**접근→정지→슬램 사이클** — restBeforeApproach 0.5 호흡이 자연스럽나, smashRange 4.0 거리가 "닿는" 느낌인가.
- ★**발 미끄럼** — Run_RM 9.57→5.0 감속(0.52배율)이라 발이 빠르게 구르되 이동 느림. 거구 15m 탑다운서 경미한가.

연동: [[project_crassorrid_clip_kit]]·[[project_telegraph_pad_shader]](셰이더 본체)·[[project_telegraph_driver_dimax]](장판 드라이버 보존 — Crassorrid가 첫 소비)·[[project_caniathrox_attack_statemachine]](접근형 회전경계 원형)·[[feedback_animevent_fire_timing]]
