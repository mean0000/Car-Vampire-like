# 2026-06-28 Death's Door 피벗 + Day 1 질주 손맛 — Handoff

## 세션 아크
유저 "자꾸 방황한다" 좌절 → 두 깨달음으로 풀림:
- **재미 ≠ 아트 퀄리티** (whitebox+큐브로도 재밌는 thalazydog 데브로그 릴)
- **프리미엄 룩 ≠ 엔진** (Death's Door = **Unity**·2인 Acid Nerve, 유니티 공식 Creator Spotlight)
→ 그래픽 북극성 = Death's Door 확정. 코어루프 = 모멘텀 슬래셔. Day 1(질주 손맛) 빌드 + 이동 "8각 끊김" 해결.

## 확정 / 방향
- **그래픽 북극성 = Death's Door** (스타일라이즈드 — 6-19 "실사 A"서 *의식적 피벗*). 부드러움 정체 = 라이팅/포스트/절제 팔레트/강한 실루엣/스냅 애니/주스 (전부 URP 가능, 약점=유기체 애니만 에셋/외주).
- **코어루프 = 모멘텀 슬래셔** (미동결, verb 손맛 검증 중): 질주(3/4)→적 쏟아짐→슬라이드 베기→정지 난전→포위→일점 돌파→재질주.
- **레퍼 쪼갬:** DD=그래픽/연출 · Hades=화려함 규율 · Hyper Light Drifter=대시 속도 · Soulstone=호드 주스.
- **카메라 = DD 근사:** pitch **52** · distance **20** · fov **50** (멀찍이 압축 디오라마, "너무 가까움" 해결).

## 빌드 (씬: `Assets/_Project/Scenes/Labs/RunFeel_Whitebox.unity`)
SlashLab_Closeup서 SaveAs(원본 디스크 보존). 80×80 바닥 + 4m 격자/산재 기둥 기준물(★빈 바닥=속도0 함정 방어) + SprintStreakFX(시안 잔상).

**이동 손맛 — "8각 끊김" 해결.** 멘탈모델: *입력(WASD)=8방향 이산(불가피) / 렌더 움직임=연속(통제가능)*. 스무딩 레이어로 곡선화. ★DD 키보드도 기계적으론 8방향이고, 그 부드러움은 *아날로그 스틱(컨트롤러 360°)+스무딩*에서 옴 → **컨트롤러 꽂으면 GetAxisRaw가 스틱 아날로그로 읽어 코드0줄 360°**(미검증, 테스트 권장).
- `PlayerMotor.cs`: 스프린트 6-21 휴면 해제 + **velocity inertia(MoveTowards, 걷기+스프린트 공통, accel 16/decel 13, 역방향=감속 분리)**. 티어 버스트는 부드러운 가속 빌드로 바뀜(트레이드오프 — 펀치↓ 부드러움↑).
- `PlayerAnimatorDriver.cs`: **faceTurnRate 600°/s 몸 회전 스무딩**(공격/대시 중엔 즉시 스냅).

**달리기 애니 freeze 버그 수정 (Animation 에이전트).** Locomotion Simple1D(Speed)의 **Speed=2 'Run' 티어가 루프 안 되는 단일 클립**(`Frank_RPG_Katana_Run_Stance3` loop=0) → 스프린트 시 1회 재생 후 발 정지였음. → **8방향 `S2_Run` 루핑 세트로 repoint, 각 TimeScale 1.35**(스프린트 케이던스 ~35% 빠르게). ★교훈: 06-21 메모리는 이 클립 loop=1로 고쳤다 했으나 리임포트서 안 살아남음 → 클립 메타 의존 대신 네이티브 루핑 클립으로 repoint가 견고.

## 게이트 (전부 통과)
- **이동 손맛 코드 (Stab+Codex):** MUST-FIX 0. 수정 반영 = 스프린트 역전 시 감속 분리(걷기와 동일 정책)·_aim null 에러로그 대칭·orphan `sprintTurnRate` YAML 청소·스테일 코멘트 정정.
- **런-애니 컨트롤러 수정 (Codex):** 클린(a~d: 8클립 loop=True·TimeScale 1.35·전투상태 무손상·GUID 정상). (e) "driver 미공개 로직" = **오경보** — `TriggerSkillCharge` 등은 HEAD에 없는 *기존 미커밋* Skill01 코드(git 확인), 이번 수정 무관. (워킹트리 전체 미커밋이라 git diff 베이스라인 착시.)

## ★열린 항목 (RESUME)
1. **facing 미확정 (유저 보류 → Day2+ 실제 전투하며 결정).** 3안 비교 **하니스 유지 중** = `PlayerAnimatorDriver`: `enum FacingMode{FaceMovement,FaceMouse,Hybrid}` + `cycleFacingKey`(F) 순환 + OnGUI HUD = **임시 디버그, 확정 후 제거.**
   - 후보: **FaceMovement** (몸=이동방향, 공격만 조준 잠금 `_lockedFace`) / FaceMouse (풀 트윈스틱=마우스, 질주 문워크라 거의 기각) / Hybrid (질주=이동·정지/전투=조준).
   - ★검증: **데스도어 = FaceMovement 모델** (몸은 이동방향 유지, 공격/조준만 별도 입력 = dual-stick-for-attacks, *몸 트윈스틱 아님*). 차이는 *대기 시 몸이 마우스 추종(Hybrid) vs 이동방향 유지(FaceMovement=DD)*뿐 — 질주·공격은 셋 다 동일.
2. **foot-slide @ 최고속(24m/s):** 케이던스 1.35× 고정이라 약간 슬라이드(whitebox 수용). 튜닝 = Run 자식 TimeScale(≤~1.5) or 코드로 `Animator.speed`를 실제 속도 비례 구동(로코모션 중에만).
3. **디테일 애니 폴리시 전체 = 나중 한 패스** (티어별 전용 런사이클·foot-IK·블렌드 Freeform Directional化·전환 다듬기) — 이상적으론 *최종 캐릭(현재 임시 Vexa) 확정 뒤* Animation 에이전트.
4. **★코어루프 카이팅 미해결:** "무엇이 정지전투를 강제하나" — 멈출 이유(draw: 적 벽/목표/돌파게이지) + 돌파 대가(cost). verb 다 깔린 뒤(Day6).

## 다음 (화이트박스 사다리)
Day1 질주 ✅(거의) → **Day2 정지 베기 (카타나 한 방 손맛 = 간지 본체, 코어 동사)** → Day3 슬라이드 베기 → Day4 떼 → Day5 일점 돌파 → Day6 리듬(카이팅 푸는 날).

## 핵심 파일
- `Assets/_Project/Scripts/Player/` — PlayerMotor.cs · PlayerAnimatorDriver.cs · PlayerCameraFollow.cs · PlayerAim.cs · PlayerBrain.cs
- `Assets/_Project/Animations/KatanaMelee.controller`
- `Assets/_Project/Scenes/Labs/RunFeel_Whitebox.unity`
- 메모리: `project_2026_06_28_core_loop_deaths_door`
