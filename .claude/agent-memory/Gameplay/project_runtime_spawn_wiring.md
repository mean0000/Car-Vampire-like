---
name: runtime-spawn-wiring
description: 런타임 스폰 시 AddComponent 직후 Awake 동기 실행 함정(의존 필드 null) + 하니스 플레이모드 강제 paused + 컨트롤러 빈 껍데기 진단
metadata:
  type: project
---

2026-06-13 Caniathrox 룩 랩 미니 전투(여러 마리 추격) 셋업 실측. 세 가지 재발성 함정.

**1. AddComponent 직후 Awake 동기 실행 → 의존 필드 null 함정 (★가장 비쌌음)**
활성 GameObject에 `AddComponent<T>()`를 호출하면 `T.Awake()`가 **그 자리에서 즉시 동기 실행**된다. 그래서 `var c = go.AddComponent<Chaser>(); c.modelAnimator = ...;` 순서면 Awake가 필드 할당 **전**에 돌아, Awake 안의 null 가드(`if (modelAnimator==null){enabled=false;}`)가 컴포넌트를 죽인다. 증상: 컴포넌트 `enabled=False`, 컨트롤러 스왑 안 됨(프리팹 기본 컨트롤러 유지), Update 안 돎.
- **How to apply:** 스폰 인스턴스는 `Instantiate` → **`SetActive(false)`** → 필드 와이어링 → `SetActive(true)` 순서로. 비활성 오브젝트에 AddComponent하면 Awake가 SetActive(true)까지 지연돼 필드 채워진 상태로 발화. (CaniathroxAttackDemo처럼 Awake 자기초기화를 유지하면서 외부 와이어링을 양립시키는 표준 패턴.)

**2. 하니스가 플레이모드를 강제 paused로 고정 → 자동 멀티프레임 검증 불가**
`EditorApplication.isPlaying=true`는 성공하지만 `isPaused=True`로 고정되어 `Time.time=0`에 멈춤. `EditorApplication.isPaused=false`로 풀어도 **다음 RunCommand 경계에서 다시 paused로 환원**. `EditorApplication.update` 관찰자는 paused와 무관하게 돌지만 게임 `Time`/`Animator`/`FixedUpdate`는 안 돎 → 매 프레임 같은 죽은 상태(nt=0.00 고정, dist 불변) 기록.
- **How to apply:** 애니/물리 진행이 필요한 검증은 하니스 자동화로 불가 — 유저가 직접 ▶. 셋업 무결성(스폰 개수·필드 와이어링·컨트롤러 스왑·applyRootMotion·독립 Animator 인스턴스)은 paused 상태에서도 단발 RunCommand로 확인 가능하니 거기까지만 자동 검증하고 모션 검증은 유저에게 넘긴다. [[playmode-verify-pattern]]의 "멀티프레임=EditorApplication.update" 패턴은 **paused가 안 걸릴 때만** 유효 — 이 랩에선 무력.

**3. .controller가 빈 껍데기일 수 있다 — 상태머신 없이 파라미터만**
CaniathroxAttack.controller가 873바이트 빈 껍데기로 발견(`m_StateMachine: {fileID: 0}`, AnimatorStateMachine 블록 0, AnimatorState 0, 파일 내 객체 1개뿐). 정상 컨트롤러는 수만 바이트(RifleLocomotion=19868). git untracked(`??`)였고 파일 수정시각이 내 RunCommand 컨트롤러 스왑 직후 — **에디터 메모리엔 상태머신이 있었는데 빈 레이어로 재직렬화되며 손상됐을 가능성**. 증상: 스왑 후 상태 해시=0, IdleAngry/Approach 어느 IsName도 false, applyRootMotion=true여도 움직임 0.
- **How to apply:** "상태머신 보존" 류 작업 전 **컨트롤러 무결성을 먼저 확인**: `grep -c "AnimatorState:" file.controller`(0이면 빈 껍데기), 파일 바이트수 비교. 빈 껍데기인데 "수정 금지"면 채우는 것=헌법 위반, 빈 채로면 동작 불가 → 즉시 유저 보고(이 케이스가 정확히 그 교착). RunCommand에서 `EditorApplication.isPlaying=false`와 에셋 검사를 **같은 커맨드에 넣지 말 것**(stop이 도메인 리로드/재직렬화 유발해 참조 무효+NullRef). 분리.
