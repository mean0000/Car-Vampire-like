---
name: katana-player-controller
description: 주인공 카타나 KatanaLocomotion.controller 구조·클립킷·★루트모션 OFF 예외(코드가 위치 소유) — 평소 헌법의 명시적 반례
metadata:
  type: project
---

주인공(Synty Sidekick, Humanoid) 카타나 공격을 받는 스탠스 컨트롤러 `Assets/_Project/Animations/KatanaLocomotion.controller` 신규 작성·배선 완료(2026-06-16).

**★★루트모션 OFF가 헌법 예외(이 무기에 한함):** `PlayerLocomotionAnimator.Awake`가 `applyRootMotion=false` 박고, PlayerController가 transform.position 100% 소유. 발도 돌진(StepLunge)·참격파 확장(StepWave) 전진은 *코드*가 구동. 따라서 카타나 공격 클립은 root motion 켜면 코드 이동과 **이중 날아감**. 여기선 "애니=포즈/타이밍 진실, 코드=위치 진실"로 역전. 단 "한 동작 중엔 그 동작만(crossfade 뭉갬 ❌)" 헌법은 유효.
- **함정 회피 확인:** Great Sword 클립들은 root 커브가 살아있고(lockRootPositionXZ=false, keepOriginalPositionXZ=true) 안 잠겨있지만, applyRootMotion=false라 런타임에 **그냥 무시**됨 → 클립 import 안 건드려도 제자리 재생. 검증: KatanaLunge(전진 root 커브 최대) 30프레임 틱 후 maxRootMove=0, maxVisMove=0 확정.

**코드 계약(KatanaController.cs — 건드리지 말 것):** 공격 성립 시 `SetTrigger` 3개 — `KatanaLight`(거합/참격 평타 공용), `KatanaLunge`(발도 돌진), `KatanaWave`(참격파). 미존재 파라미터 가드 내장(무음). 컨트롤러 스왑 감지해 재스캔.

**컨트롤러 구조(RifleLocomotion 템플릿 미러링):**
- 파라미터 8개: Speed/MoveX/MoveY(Float, 로코모션 계약), Dash(Bool)/DashSpeed(Float, 기본1), KatanaLight/Lunge/Wave(Trigger). ★Reload/ReloadSpeed/Firing 레이어는 **뺌**(카타나 장전·발사 안 함, 코드 가드됨) → 아바타마스크 의존 제거·경량.
- Base Layer만(UpperBody 없음): `Locomotion`(Simple1D on Speed, idle/walk/run = great sword idle 2.0s·walk 1.367s·run 0.733s) + `Dash`(Corkscrew Evade, speedParam=DashSpeed, Any→Dash on Dash, Dash→Loco on !Dash) + 공격 3상태.
- **공격 = 풀바디 베이스 상태(상체 override 레이어 ❌).** 그레이트소드 스윙이 풀바디라 깨끗한 전신 takeover가 정답(상체만 올리면 다리=로코모션·상체=스윙 → 두 클립 한 프레임 혼합 = 제0원칙 위반). Any-State→공격상태(트리거 If), CUT 진입 dur=0.02(near-instant, 스미어 0), 복귀=ExitTime 0.85 + dur 0.12 짧은 블렌드. WriteDefaults=false(전신 클립이라 완전 takeover).

**최종 클립 매핑:** KatanaLight→`great sword slash`(1.267s), KatanaLunge→`great sword slide attack`(2.133s, 제자리 상체 찌르기로 읽힘), KatanaWave→`great sword high spin attack`(1.867s 회전 광역). 전부 `Great Sword Pack/` Humanoid. Sidekick 리타겟 깨짐 0(캡처 확인 — 전신 슬래시 포즈 정상).

**배선:** CombatLab 씬(`Scenes/Labs/Greybox_CombatLab.unity`) 플레이어 인스턴스의 `CharacterVisual`(+disabled `CharacterVisual_OLD_CasualMale`) PLA.katanaController에 SerializedObject 직접 세팅 후 SaveScene(SerializeField 씬 덮어쓰기 함정 회피). `forceKatanaForTest=true`라 랩이 카타나 강제장착 → 런타임 runtimeController=KatanaLocomotion 스왑 확인.

**검증된 것(구조):** 트리거→해당 상태 전환·클립 재생·루트 이동 0·리타겟 정상. **유저 플레이게이트로 넘길 것:** 손맛/타이밍, 톱다운 이동끊김(풀바디 commit) 거슬림 여부, 공격↔로코모션 복귀 블렌드 0.12s 느낌, ExitTime 0.85 컷 타이밍. 정지캡처로 판정 불가.
