---
name: katana-locomotion-speed-tiers
description: KatanaMelee.controller Locomotion 블렌드트리 구조(Speed 1D × MoveXY 2D) + Shift 스프린트 run 티어 배선. ★현행(06-28)=Run 티어=S2_Run 8way @ TimeScale 1.35(Walk와 동일 클립·무기OUT). ★Run_Stance3 단일클립 폐기(loopTime meta 편집이 reimport서 0으로 되돌아가 freeze 재발). Built 2026-06-21, fixed 2026-06-28.
metadata:
  type: project
---

# 카타나 로코모션 Speed 티어 (2026-06-21, ★06-28 Run 티어 수정)

## ★★2026-06-28 스프린트 freeze 수정 — 현행 진실(아래 06-21 Run_Stance3 서술은 폐기됨)
- **증상**(유저): Shift 스프린트 시 run 애니가 1회 후 freeze(발 멈춤). 걷기는 정상.
- **근본원인**: Run 티어(Speed=2, blendtree `-8800000000000000002`)가 단일 클립 `Run_Stance3`(guid d5a6fa7a...)였는데 **디스크 meta가 `loopTime: 0`**(=비루프). 06-21에 loopTime 0→1 직접 편집했으나 **그 meta 편집이 살아남지 못함**(reimport/git revert로 0 복귀) → 비루프 클립이 1회 재생 후 마지막 프레임 동결.
- **수정**(=옵션 a, 가장 견고): Run 티어를 **Walk와 동일한 S2_Run 8way 세트로 repoint**(8자식, 전부 네이티브 `loop:1`·무기 OUT) + 각 자식 `m_TimeScale: 1.35`(스프린트=빠른 런 read + 발슬라이드 완화). 컨트롤러 YAML 직접 편집.
  - **왜 견고한가**: FBX 클립 meta 편집(loopTime)은 reimport서 안 살아남는다(이미 1번 실패). 네이티브로 loop=1인 클립(S2_Run, Walk서 검증됨)을 가리키면 meta 의존 0. + 8way 방향성(좌/우/뒤 스프린트 정상) + 무기상태 Walk와 통일(임계서 칼 깜빡임 박멸).
- **드라이버 코드 무변(의도)**: `blend = Clamp(0,2)` 그대로 둠 = Speed가 2(run 티어)에 닿아야 함(클램프-1로 줄이면 run 티어가 영영 안 켜짐 — 하지 말 것). 주석만 현행 반영.
- **검증**(MCP): Locomotion top=Speed Simple1D 3자식 / thr2 Run blendtree 8자식 전부 isLooping=True·timeScale 1.35 / 콘솔 에러0. SAVED. 손맛(속도감·슬라이드 정도)=유저 플레이 게이트.
- **남은 발슬라이드**: 실속도 9/15/24 m/s(sprintTierSpeeds)인데 measure cap maxSpeed=12라 blend≈2 고정. 고정 cadence(TimeScale 1.35)는 24 m/s를 다 못 가림(화이트박스 허용). 진짜 해법=속도비례 재생속도(blendtree 자식 TimeScale은 정적이라 파라미터 구동 불가→코드가 Animator.speed를 *로코모션 한정*으로 스케일하거나 별도 1D Speed→playbackSpeed 곡선). 노브=각 Run 자식 m_TimeScale.

---

## (구) 2026-06-21 빌드 — ★Run_Stance3 부분은 위 06-28 수정으로 폐기됨

`Assets/_Project/Animations/KatanaMelee.controller` Locomotion 상태의 블렌드트리. 유저가 Shift 스프린트(PlayerMotor sprintSpeed=8.5, 평소 moveSpeed=5) 추가 → run 티어에 `Frank_RPG_Katana_Unequip_Run_Faster_Velocity` 배선 요청.

## ★작업 전 진실: Speed 파라미터가 죽어 있었다
- PlayerAnimatorDriver는 측정 평면속도 → **Speed 블렌드(0=idle, 1=walk@walkSpeedRef5, 2=run@runSpeedRef8.5)** 를 매 프레임 set. MoveX/MoveY(facing 프레임 투영, 45° 스냅)도 set.
- 그런데 **기존 Locomotion 트리는 Speed를 안 썼다** — MoveX/MoveY 단일 2D Freeform Directional(BlendType 2)뿐. 즉 코드는 Speed를 보냈지만 트리가 무시 → 걷기/달리기 구분이 아예 없었고, 방향만 바뀜.
- 기존 9노드 = `Stance1_Idle`(중앙) + `8Way_S2_Run_*` 8방향(**무기 OUT/뽑은 Stance2 런 사이클**). 즉 평속에서도 이미 "런 클립"이 돌고 있었다(방향만 블렌드).

## 구축한 구조 (Unity API로 검증됨)
**Locomotion = 1D Speed 트리(blendParam=Speed, MaxThreshold=2), 3자식:**
- thr 0 → `Stance1_Idle` (clip 직접)
- thr 1 → **Walk** 서브트리(2D Freeform Directional, MoveX/MoveY, **8자식** = 기존 S2_Run 8way 그대로 = 현행 평속 룩 보존, 무기 OUT)
- thr 2 → **Run** 서브트리(2D, MoveX/MoveY, **1자식** = ★**`Frank_RPG_Katana_Run_Stance3`**(In_Place, guid d5a6fa7a90648034a95e930df0b0f160) — **칼 든 채 달리는 제대로 된 런**. 디졸브/팔내림/칼숨김 특수처리 전부 폐기(2026-06-21))

## ★Unequip_Run 폐기 → Run_Stance3 (2026-06-21 교체)
- 이전 run 티어=`Unequip_Run_Faster_Velocity`(달리며 칼 납도)는 칼 깜빡임/사라짐 문제로 폐기. 디졸브 머티리얼(MAT_Katana_Dissolve) 접근도 동시 폐기.
- 신 run 클립=`Run_Stance3` In_Place 변형. **35f@60fps(~0.567s) loop, humanMotion.** 우손 풀 그립 커브(Thumb/Index/Middle/Ring/Little + Right Arm DOF) = 무기 든 손 자세 유지 = 칼 든 런 확인(구조). hasRootT/Q=true이나 keepOriginalPositionXZ=ON으로 런타임 XZ 폐기=제자리(코드 이동 소유).
- **meta 직접 수정**: loopTime 0→1(안 하면 1회 후 freeze), keepOriginalPositionXZ 0→1. ForceUpdate 리임포트로 적용 검증(loopTime=True/keepXZ=True/keepY=True).
- ★**8방향 스프린트 없음**(Run 서브트리 1자식=전진 전용). 좌/우/뒤 스프린트해도 전방 런 재생. 8way 원하면 owned `8Way_Run` 세트로 자식 확장(walk 티어처럼). Run_Stance3 단일 클립은 전진만.
- 디졸브 정리: `_PlayerStackTest.unity` Katana_Mesh 렌더러 submesh0 머티리얼 MAT_Katana_Dissolve → `SG_Frank_Katana_Sword`(guid f8babcc98fd31054cbfac40cb0054e43) 원복(씬 YAML 직접, 씬 미오픈이라 디스크=저장상태). submesh1=Blade 불변. KatanaDissolve.shader/MAT_Katana_Dissolve 에셋은 죽음연출 재활용 위해 남김.

fileID: 톱 Speed 트리 = `-6802273172499591872`(Locomotion 상태가 참조, 유지). Walk=`-8800000000000000001`, Run=`-8800000000000000002`(YAML 직접 신규). Run 서브트리로 둔 이유 = 나중 8way 확장 in-place.

## ★두 가지 보고 한계(유저 손맛 판정)
1. **무기 상태 불일치 = 칼 깜빡임 위험.** Walk(thr1)=무기 OUT, Run(thr2)=`Unequip`=무기 납도(집어넣음). 스프린트 임계(속도~6.75=walk/run 중간) 넘나들 때 칼이 칼집에 들어갔다 빠졌다 보일 수 있음. 거합(iai) 연출로 의도면 OK, 깜빡임이면 어색. **owned 대안=`8Way_Run` 세트(무기 OUT 풀 8way)** — Run 서브트리 1자식 guid만 교체하면 무기 OUT 통일+8way 동시 해결(1줄 스왑). 유저가 모양 보고 택.
2. **8방향 스프린트 없음.** `Unequip_Run`은 전진 전용 1클립 → 좌/우/뒤로 스프린트해도 전방 런이 재생(몸은 옆으로 가는데 애니는 앞). 대안=위의 `8Way_Run` 세트.

## 클립 함정 — loopTime
- `Unequip_Run`(Root_Motion 폴더 출처) 원본 meta = **loopTime: 0** → 한 번 재생 후 마지막 프레임에서 얼어붙음. 작동하는 S2_Run 클립들은 loopTime:1. **meta에서 loopTime 0→1 직접 수정 필수**(안 하면 스프린트 중 애니 정지). keepOriginalPositionXZ도 0→1로(코드 구동 in-place이라 루트 XZ 보존).
- 루트모션: 이 클립은 Root_Motion 변형이지만 driver의 OnAnimatorMove가 `_attacking‖IsDashing` 때만 적용 → 로코모션 중엔 클립 전진 변위 폐기 = 제자리(코드 구동과 충돌 0). 다리는 달리고 몸은 코드가 옮김(정상).

## 전환 무결
- run↔walk↔idle = 같은 Speed 1D 트리 내 블렌드(로코모션 속도 이음새 — 헌법 허용). 별도 상태전환 없음.
- run→공격/대시/반격/스킬 = AnyState CUT(TransitionDuration 0) — 속도 티어 무관 클린 컷. 헌법 준수(동작 정체성 안 뭉갬).

## 권장 튜닝
- runSpeedRef(driver 8.5) == sprintSpeed(8.5) 정합 OK. 단 스프린트가 실제 8.5에 "딱" 닿아야 Speed 블렌드가 정확히 2(run 100%) 도달. 미달이면 walk/run 사이 영구 블렌드 → 칼 깜빡임 상시화. sprintSpeed를 runSpeedRef보다 살짝 높이거나(예 9.0) speedDamp 줄이면 run 티어 확실히 안착.
