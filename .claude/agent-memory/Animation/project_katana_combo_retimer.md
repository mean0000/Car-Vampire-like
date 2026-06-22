---
name: katana-combo-retimer
description: Re-runnable Editor script that NON-UNIFORMLY retimes a humanoid muscle clip (faster windup/recovery, normal contact) by physically resampling all curves + remapping events. Combo1 windup + Combo3 recovery sped up 1.5x (2026-06-21).
metadata:
  type: project
---

# 카타나 콤보 비균일 리타이밍 (2026-06-21)

유저 요구: 전체 속도(uniform m_Speed)가 아니라 **구간별** — Combo1 윈드업만 빠르게, Combo3 회수만 빠르게, 타격+캔슬창 가독은 평속 보존. 굼뜬 불쾌함 제거.

## ★메커니즘 = 클립 물리 리샘플 (나·Codex 독립 수렴 #1안)
Unity는 클립에 "시간별 속도 커브"가 없다. 비균일 리타이밍의 유일한 정답 = **모든 커브 키타임을 piecewise time-map T()로 물리 재배치**해 새 .anim을 굽는 것. 대안들 기각:
- 상태 m_Speed = uniform만(불합격).
- 상태 분할(윈드업/스트라이크/회수 각 다른 speed) = ★루트모션 이중적용 위험(콤보는 BakeXZ OFF로 전진 보존, OnAnimatorMove가 `_attacking`중 적용 — 상태 쪼개면 구간별 속도로 런지 느낌 깨지고 CUT 이음새 늚). 헌법 "한 동작=한 상태"에도 어긋남. → 단일 리샘플 클립이 정답.
- firstFrame/lastFrame 트림 = 워프 못함(자름만), 하드 컷 팝 위험.

## 스크립트 = `Assets/_Project/Scripts/Editor/KatanaComboRetimer.cs` (재실행 가능)
메뉴 `ZombieCrush/Animation/Retime Katana Combo1+Combo3`. **상수 2개만 만지고 메뉴 재실행 → .anim 덮어씀(guid 보존, in-place EditorUtility.CopySerialized). FBX·Animator 배선 안 건드림.**
- `Combo1_WindupSpeed` (현재 1.5) = 시작→OnAttackHit 구간 가속.
- `Combo3_RecoverySpeed` (현재 1.5) = OnComboWindow→끝 구간 가속.
- 경계는 클립 이벤트에서 읽음(OnAttackHit/OnComboWindow) — 하드코딩 norm 아님.

### 핵심 구현 디테일 (재현용)
- 소스 = FBX 서브클립(읽기전용) `AssetDatabase.LoadAllAssetsAtPath` → `humanMotion=True` 클립(137 float bindings = 머슬/IK/루트, objectRef 0).
- 커브: `GetCurveBindings`→`GetEditorCurve`→경계에 앵커 키 삽입(`AddKey`, 보간값)→키타임 `Map()`, **탄젠트 in/out ×구간속도(체인룰: 시간 ×1/s 압축 ⇒ slope ×s)**, weight 불변→`SetEditorCurve`.
- 설정: `Get/SetAnimationClipSettings`로 humanoid 메타(loop 등) 복사 → humanMotion 보존됨(검증 True).
- 이벤트: `GetAnimationEvents`→각 .time에 같은 `Map()` 적용→`SetAnimationEvents`. intParameter/messageOptions 전부 보존.
- `dst.EnsureQuaternionContinuity()` 후 CreateAsset 또는 CopySerialized(기존 덮어쓰기).
- ★type: .anim 메인에셋 참조는 컨트롤러 YAML서 `fileID: 7400000, type: 2`(FBX 서브클립은 1827226128182048838/type 3과 다름).

## 첫 패스 결과 (1.5×, 디스크 검증)
- **Combo1_Retimed** guid `3291e7ea318ce084893c6f7ed7b5fdca`: 1.0s→**0.8777s**. Hit 0.367→**0.245s**(빨라짐), Window 0.362s(★hit→window 갭 0.117s = 원본과 동일=평속 보존), End 0.798s(norm0.909).
- **Combo3_Retimed** guid `702d382967fb89b42a3eb108fabfdebd`: 1.05s→**0.8113s**. Hit **0.216s**(원본과 동일=평속), Window **0.334s**(원본과 동일=평속), End 0.755s(norm0.931, 빨라짐).
- 둘 다 137 bindings·3 events·humanMotion=True·isLooping=False·에러0.

## 코드 안전성 (KatanaWeapon.cs는 오케스트레이터 소유, 미터치)
이벤트 구동(시간 안읽음)이라 리타이밍 안전. 단 2개 소프트상수 확인됨:
- `inputBufferTime=0.5s` — Combo1 윈드업 빨라지면 OnComboWindow가 **앞당겨짐**(버퍼 여유 ↑) = 안전.
- `Time.time-_lastAdvanceTime<0.1f` stale-end 가드 — OnComboEnd가 상태진입 0.1s 후보다 한참 뒤여야. 둘 다 0.75~0.80s = 안전.
- **코드 변경 불필요** — 클립/컨트롤러만으로 완결.

## 미검증(유저 빌드 게이트)
실제 손맛(굼뜸 제거됐나, 1.5×가 과한가/모자란가)은 플레이로만 확정. 재튜닝 = 위 상수 바꿔 메뉴 재실행.
[[project_vexa_humanoid_katana_base]] [[project_frank_fbx_animevent_gotchas]] [[feedback_player_self_cancel_canon]]
