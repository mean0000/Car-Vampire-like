---
name: animevent-fire-timing
description: 다발 공격(N연사·콤보 히트)의 발동 타이밍은 코드 타이머가 아니라 클립의 AnimationEvent로. 모션 실측 정점에 박는다
metadata:
  type: feedback
---

다발 공격(N연사 사격·콤보 N힛·다단 히트)의 각 발동 순간은 **반드시 클립의 AnimationEvent**로 트리거한다. 코드 타이머(`yield wait 0.15s ×3`)로 N발 쏘면 헌법 위반.

**Why:** 제2원칙(애니가 진실, 코드는 위치/포즈/타이밍 안 만듦). 스펙의 "0.15s 간격 3발"은 *의도*일 뿐 — 실제 클립의 머리/팔 모션은 다른 리듬을 가진다(Venodonte 3AcidShotCombo 실측 스러스트 간격 0.267s, 스펙 0.15s가 아님). 모션과 발동이 어긋나면 "쏘는 동작 따로, 탄 나가는 타이밍 따로"가 되어 가짜로 읽힌다. 발동을 모션 정점에 못박아야 "동작이 곧 발사"로 읽힌다.

**How to apply:**
1. 발동 순간 = 클립의 **실측 모션 정점**(사격=머리/입 전방 스러스트 최대, 클로=팔 휘두름 정점). Animator 스텝으로 본(예 FangUpperLeft) model-local 변위를 추적해 국소 최대 norm을 찾는다([[measure-rootmotion-by-stepping]]).
2. 그 정점 norm에 `functionName` AnimationEvent를 박는다. shotIndex는 `intParameter`로 구분.
3. ★FBX 클립이면 **클론 사본**에 events 추가(원본 .meta 보존 — JumpLunge_RM 전례). 클론은 Bash 파일복사 + 새 guid .meta 직접 작성(`AssetDatabase.CopyAsset`은 MCP "User interactions" 에러).
4. ★events `time:`은 메타에 **정규화값[0,1]**으로 쓴다 — 임포터가 ×clip.length로 초 변환(0.225 → 0.300s on 1.333s clip). 직접 초로 쓰면 한 번 더 스케일됨(함정, 2026-06-13 Caniathrox·Dimaxillosaurus 둘 다 당함 — `Editor`에서 `clipAnimations[].events`에 초를 넣지 말 것). **부수효과(이득):** 정규화 time은 **클립 길이 변동에 불변** → generic 클립이 import마다 길이가 흔들려도(Dimax 콤보 1.833↔1.949s) 컨택 정점 추적 유지. `AnimationUtility.SetAnimationEvents(clip, ...)`는 초로 정확하나 **reimport 때 wipe**돼 비내구 — importer 정규화 경로가 정답.
5. ★SendMessage 도달: AnimationEvent는 **Animator와 같은 GameObject의 컴포넌트** 메서드만 부른다. 드라이버가 다른 GameObject면 안 옴 — 드라이버를 Animator와 같은 오브젝트에 두거나 릴레이 컴포넌트를 그 오브젝트에 붙인다. (Venodonte는 Animator·드라이버 둘 다 루트 → OK.)
6. 검증: 풀시퀀스 Animator 스텝 시뮬에 이벤트 캐처 컴포넌트를 붙여 N발 발화·정점 norm 확인. msgOpt=DontRequireReceiver면 메서드 없어도 에러 안 남(폴백).

투사체 발사·콤보 히트박스 활성·다단 이펙트 스폰 전부 이 방식. [[venodonte-clip-kit]] [[projectile-pool-pattern]] [[transition-patterns]]
