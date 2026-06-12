---
name: playmode-verify-pattern
description: RunManager 씬은 전부 timeScale=0 부팅(CombatLab 포함) — StartMission 필요. RunCommand 멀티프레임 검증은 EditorApplication.update 델리게이트+파일 로그로.
metadata:
  type: project
---

RunManager가 있는 씬은 **Office뿐 아니라 전부**(Greybox_CombatLab 포함) timeScale=0으로 부팅된다. 플레이 모드 자동 검증 전에 반드시 `StartMission()` 호출 필요.

**Why:** 2026-06-12 Ch44 캐릭터 전환 검증에서 상태머신이 Time.time=0에 영원히 갇힘 — RunManager.Awake가 Office 상태로 timeScale=0 설정. 기존 메모리는 "Office=timeScale0"이라고만 기록돼 있어 CombatLab에서 재발.

**How to apply:**
- 플레이 검증 시퀀스: ① `Application.runInBackground=true` + 플레이 진입 → ② RunManager를 **타입명 문자열로 탐색**(`mb.GetType().Name=="RunManager"`, 네임스페이스가 RunCommand에서 직접 참조 안 됨) 후 `SendMessage("StartMission")` → ③ 검증 본체.
- 멀티프레임 검증(애니/물리 실측)은 `EditorApplication.update`에 델리게이트 상태머신 등록 — **RunCommand 호출 경계를 넘어 생존**한다(도메인 리로드 없을 때). 결과는 static이 아니라 **파일에 기록**(RunCommand마다 별도 어셈블리라 static 공유 불가).
- 인플레이스 실측은 캡처만으론 안 보임 — **힙본(GetBoneTransform(Hips)) XZ 오프셋의 maxOffset/프레임당 jump**를 수치로 재라(슬라이드=드리프트, 스냅백=큰 jump). Ch44 합격치: offset 0.06m / jump 0.014m.
- RunCommand에서 `AssetDatabase.DeleteAsset` 호출 = "User interactions are not supported" 즉사. 멱등성은 존재 체크로 처리.
- RunCommand 코드에서 `Mesh` 타입은 네임스페이스 충돌 — `UnityEngine.Mesh`로 풀네임 필수.
