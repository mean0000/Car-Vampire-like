---
name: project_locoRate_lean_sync_pattern
description: 2026-07-11 조작감 동기화(LocoRate 발속도종속 + 달리기 lean + stopTime 딱멈춤) QA — High 1(씬간 moveSpeed/walkSpeedRef 불일치), Medium 3(OnEnable lean 잔존/facing폴백 엣지/YAML trailing space)
metadata:
  type: project
---

2026-07-11 PlayerAnimatorDriver.cs(LocoRate+lean)/PlayerMotor.cs(stopTime)/KatanaMelee.controller(LocoRate 파라미터)/5개 씬/KatanaComboRetimer.cs(per-combo windupSpeed) QA. Critical 0.

★H-1(신규 확정 버그, 미수정) = 씬간 값 일괄편집의 부작용: `_AtomLab_OneCut.unity`/`_CombatSlice_ReadAndCut.unity`는 `PlayerMotor.moveSpeed=7`인데 `PlayerAnimatorDriver.walkSpeedRef/runSpeedRef`는 오늘 4.31/5.82로 일괄변경 — moveSpeed(7)>runSpeedRef(5.82)라 평시 걷기(Shift 안 누름)에도 Speed blend가 2.0(상한)에 포화 + LocoRate까지 얹혀 "그냥 걷는데 풀스프린트로 보임". 오늘 이전엔 walkSpeedRef=5/runSpeedRef=8.5라 blend≈1.57(비포화)로 문제 없었음 — 즉 오늘 도입된 신규 회귀. `RunFeel_Whitebox`/`_PlayerStackTest`/`SlashLab_Closeup`은 moveSpeed가 이미 4.31이라 정합. **교훈: 여러 씬에 같은 SerializeField 값을 일괄 적용할 때, 그 필드와 커플링된 다른 필드(여기선 moveSpeed)가 씬마다 다르면 일부 씬에서만 조용히 깨진다 — 일괄편집 후 "씬별 관련 필드 값 diff 표" 만들어 교차검증 필수(디폴트 재정의 함정의 변종: 이번엔 코드-vs-씬이 아니라 씬-vs-씬 불일치).**

★lean/facing 분리 아키텍처(_facingRot 순수상태 분리 + transform.rotation에만 lean 합성) 검증 결과 **메인 경로는 안전**: Tick()이 매프레임 `transform.rotation=_facingRot`(순수, 201행 상당)로 리셋한 *직후*에 `transform.forward`를 읽어 이동투영 기준을 삼기 때문에, 지난 프레임 lean 잔여가 다음 프레임 오염시키는 문제는 실제로 안 생김(Rodrigues 회전식으로 직접 검증 — 이동방향=조준방향 정합 시 lean이 수평성분 전혀 왜곡 안 함). 단 두 좁은 구멍: ①OnEnable이 `transform.rotation`(leaned 가능)을 그대로 `_facingRot`에 캡처 — 비활성→재활성 시점에 lean>0이었으면 잘못된 순수-facing 베이스라인 캡처(자기수렴 ~40ms, RotateTowards가 고침). ②facing 폴백 체인(aimFace/moveFace→transform.forward)이 `face`가 정확히 0벡터인 극단 프레임(조준 방향 완전 0 + 정지)엔 transform.rotation 리셋 자체가 스킵되어 leaned 잔여를 읽음. 둘 다 Low~Medium, 근본수정은 동일: fallback을 `transform.forward` 대신 `_facingRot * Vector3.forward`(순수 yaw 재구성) 기준으로 통일.

★손편집 YAML 트레일링스페이스 진단법(재사용 가능): 컨트롤러/에셋을 손으로 편집한 도구가 진짜 Unity 저장을 거쳤는지 판별하려면, 건드리지 않은 동종 빈 필드(`m_Tag: ` 등)와 건드린 블록을 grep 대조 — Unity 자체 직렬화기는 빈 문자열 필드에 항상 후행공백 포함(`key: `)로 쓰는데, 외부 텍스트치환 도구는 트레일링스페이스를 날리는 경우가 있음(`key:`). YAML 스펙상 파싱 결과는 동일(null/empty 스칼라)해서 실질 위험은 낮지만, 씬 필드 순서가 C# 선언순서와 정확히 일치하는지 검사하면 "진짜 Unity 세이브였는지"의 보조 증거가 됨(이번 케이스는 5개 씬 필드 순서 전부 일치 = Unity 세이브 확인, 컨트롤러만 트레일링스페이스 불일치 = 별도 편집 도구 사용 정황).

LocoRate 파라미터는 Base "Locomotion"과 UpperBodyCombo "UB_Loco" 두 상태 모두에 배선 확인(컨트롤러 diff) — [[project_katana_combo_strike_snap_assessment]] 계열의 07-04 Fix B 위상정합(두 로코모션 클럭 동시 weight>0 금지)과 충돌 없음. 둘 다 같은 LocoRate를 공유해 이즈아웃 중에도 배속이 항상 일치.

KatanaComboRetimer.cs per-combo windupSpeed(Combo1=3.5 압축, 2/3=1.25 유지) 수치 실측 교차검증 완료: S1_Combo01_01_Retimed.anim에서 OnSwishWhoosh@0.0849/OnAttackHit@0.1167(int=1)/OnComboWindow@0.1699/OnComboEnd@0.4813/m_StopTime=0.5384 전부 태스크 주장과 일치. OnSwishWhoosh가 오늘부로 리타이머 정식 저작 목록에 편입(이전엔 재실행 시 소실 위험 있었던 걸로 추정, 주석이 "Stab H-1 소실 방지"라 자칭 — 실제 이전 세션 기록엔 없어 검증 안 됨, 신뢰는 하되 "미검증 출처" 취급).
