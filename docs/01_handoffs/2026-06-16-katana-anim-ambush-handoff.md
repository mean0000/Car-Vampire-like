# 핸드오프 — 카타나 애니 배선 + 급습 볼트 (2026-06-16)

> 재시작 후 이 문서부터 읽고 이어간다. ⚠️ **재시작 이유 = unity-mcp 툴 로드** (아래 §4). 재시작하면 툴이 뜬다 → 바로 §3 Animation 에이전트 착공.

## 0. 한 줄 요약
급습 볼트 코드는 들어갔고(보장 즉살+즉발 XP, Stab+Codex 통과) 슬래시 VFX도 뺐다. **플레이테스트에서 드러난 진짜 문제 = 카타나 애니 미배선**(권총 자세 + 공격 모션 0). 다음 = Animation 에이전트로 카타나 스탠스+공격 모션 배선. MCP 툴 로드 위해 재시작 대기.

---

## 1. 이번 세션 한 일
- **급습 볼트 코드 구현 완료**(스펙 동결: 근접카타나 / 자격 `<Chase` / 보장 즉살 / XP만 크게). 권위=`docs/00_authority/2026-06-16-ambush-bolt-spec.md`.
  - `ZombieController.cs:131` — `IsUnaware => _state < ZombieState.Chase && !IsDead`
  - `KatanaController.cs` StepLunge — 발도 적중이 무경계면 데미지 999999 보장킬 + `XPManager.Instance?.AddXP(_iai.ambushXp)`
  - `KatanaController.cs` IaiKnobs — `ambushXp=12` 노브
  - Stab+Codex 병렬 리뷰 Critical 0 통과.
- **슬래시 VFX 제거**(유저 지시) — KatanaController의 SlashVfx 4곳(거합 평타 PlayFan / 발도 PlayPierce / 참격 평타 PlayFan / 참격파 PlayWave) 제거 + 고아 `charge` 지역변수 정리. **발밑 게이지 링(_footRing)은 남김**(유저가 "그것도 뺄까?"에 답 안 함 — 필요시 제거).
- **MCP 정리**: 한때 개인+회사 `.mcp.json` 둘 다 삭제 → 카타나 애니 작업 위해 **개인만 재복구**(naju-poko 회사는 삭제 유지). 개인 1핀이라 멀티에디터 분산 사고 구조적 차단.

## 2. ⚠️ 진짜 문제 = 카타나 애니 미배선 (플레이테스트 발견)
**증상:** 카타나 들어도 ①캐릭터가 권총 자세 ②공격 모션 안 나옴.
**루트원인(급습 코드와 무관 — 기존 구조 문제):**
- `PlayerLocomotionAnimator.cs:39~108` `ApplyStance()` — 스탠스가 **라이플/권총 둘뿐**. `CurrentGunClass`로 분기. 카타나 장착 시 `_gunClass`가 기본 `Pistol`로 남아 → **권총 컨트롤러**가 걸림 = 권총 자세. 카타나 스탠스 자체가 없음.
- `KatanaController.cs` — **Animator 호출 0개**. 베기/발도는 히트 판정+노이즈+스프링범프만. 공격 클립 재생 트리거 없음 → 공격 모션 안 남.
- → 너희 애니 헌법("애니가 진실, 코드는 상태전환·이벤트만")의 정반대. 코드가 때리는데 몸이 안 움직임.

## 3. 다음 착공 = Animation 에이전트 (재시작·툴로드 후 즉시)
**목표:** 카타나 스탠스 + 공격 모션 배선.
1. **카타나 스탠스 컨트롤러** — 카타나 든 idle/걷기/달리기(권총 자세 해소). 스왑 경로 추가: `PlayerLocomotionAnimator`가 카타나 장착을 감지(`PlayerCombat._kind==Melee && _katana!=null` 노출 필요)해 카타나 컨트롤러로 ApplyStance.
2. **공격 모션** — 검증된 3연베기 클립을 공격 상태로. KatanaController가 베기/발도마다 Animator 트리거(상태 시퀀스: 접근→정지→공격, crossfade 뭉갬 ❌).
3. **검증 자산(이미 있음, throwaway):**
   - 클립: `Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animations/2Hand-Sword/` (어제 리타게팅 호환 검증 OK).
   - throwaway 컨트롤러: `Assets/_Project/_SidekickTest/SK_AnimTest.controller` (Idle→A1→A2→A3 루프) — 참고/재활용.
   - throwaway 씬: `Assets/_Project/Scenes/_SidekickAnimTest.unity`.
   - 빈 sink: `Assets/_Project/_SidekickTest/SidekickAnimEventSink.cs` (RPG 클립 `Hit` 이벤트 = 실제 타격 타이밍, 정식 전투에선 PlayerCombat/Katana가 받아야).
4. **무대:** `Assets/_Project/Scenes/Labs/Greybox_CombatLab.unity` — Synty Starter_02 여우 카타나 플레이어 + 좀비. `forceKatanaForTest=true`라 시작 시 카타나 장착. 거합=`1`, 발도=RMB 홀드→릴리스.

## 4. ⚠️ MCP 상태 (재시작 이유)
- 개인 `.mcp.json` 복구·**Connected**(`claude mcp list` 확인). 회사 naju-poko 삭제 유지.
- **단 `/mcp` 재연결로는 툴이 세션에 안 올라옴**(ToolSearch 0건 재현) → **Claude Code 완전 재시작** 필요(메모리 `unity_mcp_multi_editor_routing` ★반복증상). Unity 에디터는 끄지 말 것(relay 붙어 있어야 함).
- 재시작 직후 `ToolSearch "unity scene gameobject"`로 툴 뜨는지 확인하고 착공.

## 5. 대기 중(애니 후) — 급습 볼트 플레이게이트
애니 붙으면 CombatLab에서: 무경계 좀비에게 발도 → 보장 처형되나 + XP 즉발 스파이크 느껴지나 + "한 번 더?" 켜지나(스펙 §5). 노브 `ambushXp`(현 12)·발도 거리/속도 손맛 튜닝. 안 켜지면 무엇이 빠졌나 분해(호드/정면 viable은 다음 볼트).

연동 메모리: `ambush_bolt_spec`, `unity_mcp_multi_editor_routing`, `serializefield_scene_override_trap`(씬 값 세팅 함정), `combat_anim_sourcing`.
