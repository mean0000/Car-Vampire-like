---
name: player-stack-architecture-audit
description: 플레이어 스택 전체(12파일) 아키텍처/프로덕션 준비성 종합 감사 (2026-06-20). 9차원 점수 + 이슈 목록 + PASS/FAIL 평결.
metadata:
  type: project
---

# 플레이어 스택 아키텍처 감사 (2026-06-20)

**평결: PASS (6.9/10) — 구조적 High 없음, 단 경계 조건 집중 보강 필요**

## 9차원 점수

- A. SOLID: 8/10 — SRP/OCP 양호. LSP 계약 명확. WeaponBehaviour 추상 충분히 얇음. 단점: PlayerHealth가 IDamageable 구현하면서 PlayerMotor 직접 참조(DIP 경미 위반).
- B. 파이프라인 명확성: 9/10 — Brain 주석에 Aim→Motor→Weapon→Animator 순서 명시. _dashAppliedThisFrame 플래그로 이중적용 막음. SEO 암묵 의존 없음.
- C. 모듈화/응집도: 8/10 — 5시스템 경계 명확(입력/이동/무기/애니/체력). ParrySlowMotion→PlayerHealth 단방향 의존 깔끔. 누수=PlayerAfterimage가 PlayerMotor 폴링(이벤트 비용 아님, 설계적 선택).
- D. 데이터 중심 설계: 7.5/10 — ComboAttackSet SO 통합 진실소스 우수. 하드코딩 폴백(range=1.8, arc=50, dmg=3) 무음 은닉 → 밸런스 수치가 코드에 숨음(M급).
- E. 게임플레이 경계 분리: 8/10 — PlayerBrain이 busy 플래그로 모터-무기 의존 단방향화. 단점: ParrySlowMotion이 PlayerHealth 찾기에 FindObjectOfType 폴백 사용(M급).
- F. 작업 규칙 일관성: 8.5/10 — 네이밍/주석 밀도/헤더 스타일 파일 간 일관. 유일 불일치=PlayerAfterimage 일부 영어 주석.
- G. 세이브 준비성: 7/10 — 상태 대부분 로컬(재시작=OK). 위험=PlayerHealth._hp가 공개 프로퍼티만 있고 복원 메서드 없음(초기화 경로 단일). ParrySlowMotion이 씬에서 timeScale 보유=세이브 시점 타이밍 주의.
- H. 빌드 안정성: 6/10 — ★HitboxDebugManager에 #if UNITY_EDITOR 없이 [ExecuteAlways]+FindObjectsOfType 런타임 상주. Debug.Log 7개 프로덕션 노출(KatanaWeapon 2+PlayerHealth 2+KatanaWeapon Counter 1 등). asmdef 없음→전역 네임스페이스. Shader.Find("ZombieCrush/AfterimageGhost") 빌드 스트립 위험.
- I. 버전 관리 안정성: 8/10 — .meta 별도 파일 구조 양호. BakeMesh 버퍼 Mesh를 OnDestroy에서 Destroy — 씬저장 안전. 대형 바이너리 없음. 단점: PlayerAfterimage의 동적 생성 GO("DashGhost")가 에디터 정지 시 씬에 잔류 가능(히든 .meta 없음).

## 핵심 이슈 목록

### H-1: HitboxDebugManager — #if UNITY_EDITOR 없는 런타임 상주
- 파일: Assets/_Project/Scripts/Debug/HitboxDebugManager.cs:11
- [ExecuteAlways]+FindObjectsOfType<Collider>(0.4초마다)+FindObjectOfType(lazy cache) = 빌드에 그대로 포함, 매 0.4s OverlapAll
- 수정: #if UNITY_EDITOR~#endif 또는 클래스 전체를 Editor/폴더로 이동

### H-2: Debug.Log 프로덕션 노출
- KatanaWeapon.cs:110,204 — ArmCounter/BeginCounter 매 발동 로그
- PlayerHealth.cs:92,95 — 매 피격/사망 로그
- PlayerMotor 없이 무음 경고(PlayerAnimatorDriver:85는 Error 적절)
- 수정: 전부 #if UNITY_EDITOR 또는 제거

### M-1: 하드코딩 폴백 수치가 코드에 숨음
- KatanaWeapon.cs:249 — comboSet 미할당 시 range=1.8, arc=50, dmg=3
- 동작은 하지만 Inspector에서 보이지 않아 밸런스 수치가 코드에 매몰
- 수정: 폴백을 Inspector [SerializeField]로 노출하거나, comboSet 미할당 자체를 Error로 올려 무음 폴백 차단

### M-2: ParrySlowMotion — FindObjectOfType 폴백
- ParrySlowMotion.cs:42 — health=null 시 FindObjectOfType<PlayerHealth>()
- 씬에 PlayerHealth 복수 존재 시 임의 픽업. Inspector 연결 미수행을 숨김
- 수정: Awake에서 null이면 LogError, 폴백 제거

### M-3: PlayerAfterimage — DashGhost GO 에디터 잔류
- PlayerAfterimage.cs:256 — new GameObject("DashGhost") 가 씬 오염
- 에디터 정지 시 DDOL 없어도 씬 계층에 잔류(Destroy 미보장)
- 수정: hideFlags = HideFlags.DontSave 또는 특정 부모 하위 생성

### L-1: asmdef 없음 — 전역 네임스페이스
- 빌드 시간 증가·이름 충돌 잠재. 솔로 프로젝트라 즉각 위험 낮음
- 수정: 중기 _Project.Player, _Project.Debug asmdef 분리 고려

### L-2: Shader.Find 빌드 스트립 위험
- PlayerAfterimage.cs:279 — Shader.Find("ZombieCrush/AfterimageGhost")
- 빌드 Always-Include Shaders에 미등록 시 스트립 → null → enabled=false 무음 퇴화
- 수정: Project Settings > Graphics > Always Included Shaders 등록 확인

### 확인된 안전 사항
- PlayerBrain.OnDestroy Parried -= ArmCounter 구독해제 대칭 정확
- KatanaWeapon.Initialize 재진입 가드(AnimatorDriver null check+해제 선행) 올바름
- WeaponBehaviour.OnDestroy → Cleanup() 파생 체인 안전(base.OnDestroy 주석 경고 포함)
- _dashAppliedThisFrame 플래그로 대시종료프레임 이중이동 해결됨(이전 H-2 수정 확인)
- counterMaxDuration 워치독으로 _countering 고착 방지(이전 H-1 수정 확인)
- ParrySlowMotion.OnDisable Restore() 호출로 timeScale 잔존 방지
- PlayerHealth.TakeHit damage<=0 조기반환 가드 올바름

## 무기 OCP 확장성 평가
대검/권총/드론 추가 시:
- WeaponBehaviour 추상만 구현하면 됨 — PlayerBrain 수정 불필요 (OCP 통과)
- ComboAttackSet SO 복사로 데이터 분리 (D 통과)
- PlayerAnimatorDriver는 파라미터명 고정(Speed/ComboStep/Dash/Counter) — 새 무기가 다른 파라미터 이름 필요 시 드라이버 수정 필요 (잠재 OCP 위반, 현재는 카타나만이라 미발화)

**Why:** 이 감사는 종합 아키텍처 준비성 평가로 단순 버그 QA를 넘어 SOLID/파이프라인/빌드안정/세이브 등 9차원을 커버. 실제 프로덕션 배포 전 H-1(HitboxDebugManager 빌드 상주)과 H-2(Debug.Log 노출)는 반드시 처리.

**How to apply:** 향후 플레이어 스택 QA 시 "HitboxDebugManager 빌드 격리 완료됐나?" + "Debug.Log 제거됐나?" 를 첫 체크포인트로.
