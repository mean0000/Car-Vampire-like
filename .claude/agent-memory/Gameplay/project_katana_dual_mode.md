---
name: katana-dual-mode
description: 카타나 거합/참격 증명 슬라이스 — KatanaController(C# 헬퍼) 패턴, 검증 훅, 손맛 노브 위치
metadata:
  type: project
---

카타나 거합↔참격 2모드 = `KatanaController.cs`(MeleeAttacker와 같은 *순수 C# 헬퍼*, PlayerCombat 소유). MonoBehaviour 아님 — 런타임 AddComponent 수명주기 함정 회피.

**아키텍처 (재사용 토대):**
- PlayerCombat이 `forceKatanaForTest`(SerializeField, 기본 true)면 Awake에서 `EquipKatana()` → `_katana` 생성(`_melee` 대신). `_kind=Melee`라 총 비주얼 풀 init 스킵.
- Update 근접 분기: `_katana != null`이면 카타나 경로, 아니면 기존 `_melee.Tick`. 디버그키 1/2=거합/참격 모드 토글(SetMode=하드캔슬+리셋).
- 입력: LMB(attackHeld)=평타, RMB(aimHeld/aimPressed/aimReleased 엣지)=거합 충전·발도 릴리스 / 참격파(5단 임계 시).
- 히트=`ZombieController.TakeMeleeHit`(반환=킬 bool). 발도=`SphereCastAll`(캡슐 캐스트 폭 0.4 반경) 경로 관통. 평타·참격파=OverlapSphere 부채꼴(MeleeAttacker.Swing 패턴 복제).
- 피격 콤보 손실=`PlayerController.OnPlayerDamaged`(static event) 구독.
- self-cancel=`_player.IsDashing` 상승엣지→`OnDashStarted`(거합=충전/발도 하드컷·게이지 보존, 참격=콤보 보존+윈도우 갱신).

**손맛 노브 위치 (전부 인스펙터 노출):**
- PlayerCombat 인스펙터 → `iaiKnobs`(거합: lightSpeedMult 0.55·chargeTimeStill 1.2/Moving 2.4·lungeDist 2.5~6·killRefill 0.35·lungeCooldown 0.4) / `slashKnobs`(참격: maxTier 5·hitsPerTier 3·cooldownCutPerTier 0.15·comboWindow 2.0·hitLoss 1~2·wave 90°×5m·waveDmgMult 3).
- `KatanaController.IaiKnobs`/`SlashKnobs` = `[System.Serializable]` 중첩 클래스(인스펙터 직렬화됨).

**검증 훅 (리플렉션 회피 — 공개 Debug 접근자):**
- 정적: `PlayerCombat.DebugFireHeld`(LMB 시뮬), `DebugAimHeld`(RMB 홀드 시뮬), `DebugAimReleaseOnce`(발도 릴리스 엣지 1프레임).
- 인스턴스: `pc.DebugHasKatana/DebugKatanaMode/DebugKatanaComboTier/DebugKatanaCharge01/DebugKatanaLunging/DebugKatanaCharging`, `pc.DebugSetKatanaMode(int)`, `pc.DebugKatanaForceDashCancel()`.

**검증 실측 결과(2026-06-14, CombatLab 플레이):** 참격 콤보 0→5 가속(킬 체인)·12→6 처치 / 거합 충전 still 1.2s 채움·발도 5.06m 이동·캡슐킬·killRefill 0.35 환류 정확 / self-cancel 거합 charging=false 게이지 0.51 보존·참격 combo 2 보존. 컴파일 0 에러.

**함정 (이 세션 실측):**
- 발도가 플레이어를 적 무리로 던지면 *그랩(IsGrappled)→locked=true→충전 영구 0*. 검증 시 좀비를 멀리 치우고 1마리만 핀해야 깨끗.
- 에디트(SerializeField 추가) → 도메인 리로드 → *플레이 중이면 stale 세션* 발생(이전 KatanaController 시각물이 GameObject.Find에 잡히는데 `_katana`는 null). 검증 전 반드시 *완전 정지 → 깨끗하게 재진입*.
- forceKatanaForTest는 *씬 오버라이드 함정* 무사(SerializedObject로 True 확인) — 새 필드가 코드 default true를 받음.
