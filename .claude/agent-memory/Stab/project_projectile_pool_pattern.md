---
name: projectile-pool-pattern
description: 공유 풀링 투사체(ProjectilePool/AcidGlob, Venodonte 사수) 리뷰에서 확인한 멤버십 정합성·AnimationEvent 발사 경로 검증법과 잠복 seam
metadata:
  type: project
---

2026-06-13 Venodonte 사수 수직 슬라이스(ProjectilePool/AcidGlob/VenodonteShooter/VenodonteLabSpawner) QA 리뷰에서 확인. Critical 0 — 검증된 레퍼(CaniathroxChaser/AttackTokenPool)를 충실히 복제했고 가장 깨지기 쉬운 이벤트 경로가 빈틈없이 배선됨.

**Why:** 원거리 종(스핏·부채탄·링탄·유도탄)이 이 풀을 재사용할 예정이라 같은 패턴이 복제된다. 안전한 부분과 잠복 구멍을 한 번 갈라두면 후속 종 리뷰가 빨라진다.

**How to apply (공유 풀링 투사체 리뷰 시):**

1. **풀 멤버십 정합성 추적법 (이 패턴의 핵심 안전 증명):** 객체가 `_idle`·`_live` 중 *정확히 한 곳*에만 존재하는지 = 상호배타 전이인지 확인. ProjectilePool에서 검증됨: Return은 `_live.Remove`→조건부 `_idle.Enqueue`, Fire는 `_idle.Dequeue`→`_live.Add`. RecycleOldest는 `_live`만 만지되 직후 `Launch`가 객체를 *완전 재초기화*(origin/velocity/life/trail 전부 덮어씀)하므로 `_live`에서 제거→재추가해도 멤버십 안 깨짐. **"RecycleOldest가 비행 글롭을 두 번 등록한다"는 직관적 의심은 Launch 재초기화 때문에 틀림** — 추적으로 반증할 것.
2. **이중 Despawn 차단 = `_active` 플래그 선행 토글:** AcidGlob.Despawn이 `_active=false`를 Return 호출 *앞에서* 세팅 + Update의 `if(!_active) return` → 한 비행당 Despawn 1회 보장. Return의 `!_idle.Contains` 가드는 추가 백신(실은 불필요하나 방어). 명중-반납 경로가 게임플레이에서 추가되면 이 `_active` 가드가 이중반납을 막는 단일 방벽이 됨 — 명중 코드도 반드시 `_active` 경유.
3. **글롭은 풀의 자식(SetParent(pool.transform)) → 풀 파괴 = 글롭 동반파괴.** 따라서 "글롭이 죽은 풀에 Return" 함정은 발생 안 함(글롭이 먼저 사라짐). Despawn의 `if(_pool!=null)`는 Unity fake-null(== 오버로드)도 잡음. 단 *풀보다 사수가 오래 사는* 구조가 생기면 사수의 `projectilePool` 참조가 stale — 현재는 스포너가 풀·사수를 같은 Awake에서 묶어 동시생사라 안전. **이 동시생사 계약이 깨지는 PR(씬 전환 진입/풀 DDOL화)이 오면 재검증**(bootstrap-attacher #7과 동일 클래스).
4. **AnimationEvent 발사 경로 3중 검증법 (이번에 가장 깨질 위험 컸던 곳, 클린 통과):**
   - (a) 컨트롤러 Fire 상태의 m_Motion GUID가 *이벤트 박힌 클론*을 가리키는가 (원본 무이벤트 FBX 아님). grep으로 controller의 m_Motion guid ↔ 클론 .fbx.meta guid 대조.
   - (b) 클론 .meta의 events: 블록에 functionName·intParameter·`messageOptions`(1=RequireReceiver) 확인. RequireReceiver는 수신자 없으면 *에러 로그*(무음 아님)지만 글롭은 안 나감.
   - (c) 수신 메서드가 `public` + **Animator와 같은 GameObject**에 있는가. SendMessage는 Animator의 GO만 친다 — Animator가 자식이면 루트의 드라이버는 못 받음. Venodonte는 Animator가 루트=드라이버도 루트라 도달 OK(Caniathrox는 Animator가 자식이라 구조 다름). SendMessage는 `enabled=false` 컴포넌트도 호출하므로 null 가드가 별도로 필요(FireAcidGlob의 pool/target/model null 가드가 그 역할).
5. **soft-cap RecycleOldest = 시각 순간이동(기능 버그 아님):** 풀 고갈 시 비행 글롭이 화면에서 사라지고 새 발사로 바뀜 = 회피 학습 배신. 기본값(maxAttackTokens 2·enemyCount 5·globLifetime 3·cooldown 1)에선 3초창 worst ~6~7발 << 24라 미발생. 튜닝 노브(토큰 4·적 12·수명↑·쿨0)로 24 초과 시 발화 — **유저 게임감 판정 항목**으로 표기(요청 않은 풀확대는 CLAUDE.md상 강제 금지).
6. **0벡터 발사 방향 폴백의 의미론적 약점:** 플레이어가 muzzle XZ에 정확히 겹치면 `dir.xz≈0` → AcidGlob.Launch가 `transform.forward`(=글롭 재활용 잔재=이전 발사 방향)로 오발. 발사자 좌표계(model.forward)가 글롭 잔재보다 의미 있음 — 폴백을 FireAcidGlob 측에서 잡거나 0벡터면 그 발 스킵 권장.

**검증된 안전 전제(재확인 불요):** 토큰 대칭(OnDisable ReleaseToken+_holdsToken 가드+Release의 _inUse>0 언더플로우 방어, 파괴·비활성·완결 전경로 누수0) / static Roster ClearStaticState(SubsystemRegistration) / 속도 단일진실원(Update 맨위 speed=1f) / 스폰 SetActive(false)→와이어링→SetActive(true) 순서 / GetComponentInChildren<Animator>가 비활성 오브젝트·루트포함 둘 다 커버 — 전부 CaniathroxChaser 검증패턴의 정확한 복제.
[[zombiecrush-review-hotspots]] [[bootstrap-attacher-hazards]]
