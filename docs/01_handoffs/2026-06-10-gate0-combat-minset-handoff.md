# 핸드오프: 전투 질감 게이트0 최소셋 구현 (2026-06-10 저녁)

> 스펙 권위: `docs/00_authority/2026-06-10-combat-texture-foundation.md` §8 게이트0 최소셋.
> 구현 = Fable 직접(게임감 존), 리뷰 = Stab+Codex 병렬 완료, 검증 = 플레이모드 라이브 테스트(아래).

## 구현 완료 — 3항목 전부

| # | 항목 | 구현 |
|---|---|---|
| ① | **피격 사다리 + 히트스탑 + 3계층 탄 판정** | flinch(0.15s, 윈드업 취소)→stagger(0.4s, 런지도 취소)→knockdown(1.5s 다운+기상, 다운 중 피해 ×1.5) 누적 사다리 + knockdown마다 문턱 ×1.5 내성(스턴락 방지). 히트스탑 = **피격자만** 정지(`_hitStopTimer`+animator.speed 0) — 전역 MMTimeScale은 ZombieController.Die/MeleeAttacker에서 **제거**(PlayerController의 플레이어 피격 슬로모는 어제 피격 피드백 설계라 유지). 3계층 = 탄도 세그먼트↔좀비 축 수평거리 d: ≤0.4 풀히트(정지)/≤0.7 스침(50%+flinch만, 정지)/≤1.0 그레이즈(소량+비틀 확률, **탄 비정지**·스파크만). 킬샷 = hitStop×2.5 시체 프리즈 후 제거(콜라이더 즉시 off). 킬 사운드 0.15s 창 클램프 |
| ② | **인식 미니멀 — Dormant→Alert→Chase + 그르렁** | 배치 좀비 기본 = Dormant(정지). 시야 = 원뿔+가림은 기하, **틱당 확률 롤 누적 게이지**(거리 폴오프·플레이어 노출 배수·개체 분산 ±30%). 게이지 0.34↑=Alert(응시 1.5s 정지 → 느린 접근 0.35×), 1.0=Chase. 식으면 마지막 인지 지점 Investigate. **소리는 어떤 크기든 Investigate까지만**(시야 확정 없이 Chase 금지 — 떼어내기 보장. 구식 chaseThreshold 직행 제거). Chase 진입 시 그르렁(반경 8m, Dormant/Idle→Investigate만, 쿨 5s). 인식 틱 = 0.2s 스태거드(개체 위상 분산)+거리 LOD(25m+ → ×3) |
| ③ | **런지 윈드업 + grapple** | Chase→사거리 2.6m→**Windup 0.5s**(상체 젖힘 — Animator "Attack" 트리거 있으면 클립, 없으면 Visual 코드 틸트 폴백 + 사운드 큐 슬롯)→**Lunge**(방향 윈드업 종료 순간 고정 = 커밋, 9m/s·0.35s, 벽에 막히면 즉시 후딜)→접촉 1.0m = **Grapple**(잡는 순간 물기 20 + 홀드 2.5s, Space 5연타 탈출 = 좀비 경직 0.8s+넉백 / 타임아웃 = 물기 1회 더). 빗나가면 후딜 0.6s(회피 보상). 대시 무적이면 헛손질. 플레이어는 grapple 중 이동·대시·사격 전부 잠금 |

## 수치의 거처 (헌장 3보장 — 전부 SO)

- **`Assets/_Project/Data/CombatFeel.asset`** (`CombatFeelConfig`, 신규): 3계층 반경/배수, 히트스탑 normal/kill, 킬 피드백 클램프 창. Greybox_ScanLit_v2의 Player→PlayerCombat에 와이어링 완료(비우면 코드 기본값 폴백).
- **`ZombieConfig`** (필드 추가 — 기존 에셋은 스크립트 기본값 자동 적용): Perception/Hit Reaction/Lunge & Grapple 3섹션. 사다리 문턱 = 3타 stagger/6타 knockdown/윈도우 2.5s/내성 ×1.5.

## 검증 (플레이모드 라이브, Greybox_ScanLit_v2)

- 단일 좀비 통제 테스트: Dormant→피격→Chase→(접근)→Windup→Lunge→Grapple **풀사이클 자율 반복** — 플레이어 HP 100→0 (물기 20×5, grab+타임아웃 페어) ✓
- 사다리: 풀히트 연타 누적 3타 stagger, 6타 **Downed** 상태 진입 ✓
- grapple 중 플레이어 사망 → 좀비 Recover 정상 복귀(고착 없음) ✓ / 사망 시 RunManager Died 정산 수렴 ✓
- 킬: 콜라이더 즉시 off + 시체 프리즈 후 제거 ✓ / 세션 콘솔 에러 0 ✓
- 리뷰 반영: 플레이어 파괴 시 grapple 해제(OnDestroy), grapple 중 노출 1.5 고정, 문턱 역전 방어, 세그먼트 투영 클램프, 공개 히트 메서드 `_config` 가드(룩데브 조각상 NRE 실측 수정), CombatFeelConfig OnValidate 반경 순서 강제

## 남은 것 / 관찰 사항

1. **유저 플레이테스트가 진짜 판정** — 랩 통과 기준(§7): 다섯 비트 체감. 특히 Alert 응시가 "쟤가 날 봤나?"로 읽히는지, 윈드업 0.5s가 회피 가능한 무게인지, 3계층이 손맛 차이로 느껴지는지.
2. **그르렁 전파 멀티좀비 실관측 미완** — 단일 좀비 랩이라 로직만 검증(가드 포함). 다중 배치에서 "한 마리 깨면 주변이 Investigate로 일어나는 풍경" 확인 필요.
3. **Animator "Attack" 트리거 미와이어링** — 현재 코드 틸트 폴백으로 동작. ithappy Zombie_Attack 클립을 ZombieAnimator에 트리거로 붙이면 자동 승급(코드 수정 불필요).
4. **그르렁/윈드업 사운드 클립 미할당** — ZombieController 인스펙터 슬롯만 존재(null-safe).
5. **⚠ 관찰: kills=11 미스터리** — 스포너 42마리 라이브 세션에서 플레이어 무행동인데 RunStats.Kills가 11까지 증가. 원인 미규명(디스폰은 AddKill 안 함). 다음 플레이테스트에서 재현 관찰 요.
6. **⚠ unity-mcp RunCommand에서 System.Reflection(GetField+BindingFlags) 사용 시 UNEXPECTED_ERROR로 하니스 자체가 죽음** — try/catch도 무용. 우회 = 공개 디버그 접근자(`ZombieController.DebugState`/`HasConfig` 추가됨).
7. 룩데브 조각상 좀비(Seam_* 등)는 Init 안 받아 HP 0 — config 가드로 무적이지만, 정식 랩 씬을 만들 땐 Init 경로 필수.
8. 후속(게이트0 통과 후): ④카메라 커서 오프셋+킥 ⑤조준 원뿔+레이저 고지 ⑥잔여 인식 상태 ⑦소리 사다리 ⑧배치 인구+스폰 5규칙(디렉터와 통합).
