---
name: sprint-streak-fx
description: 달리기 속도감 VFX v1 — SprintStreakFX.cs, 방향성 스트릭+버스트, 컴파일 검증 완료
metadata:
  type: project
---

달리기 속도감 VFX `SprintStreakFX.cs` v1 구현 완료 (2026-06-21).

**Why:** 탑다운 부감에서 raw 속도가 안 읽힘 → 이동 반대 방향으로 흐르는 파티클 스트릭으로 속도 판다.

**구조:**
- Trail Stream: 이동 반대 방향 연속 스트릭, SprintTier(0/1/2) → 방출량/크기 단계별 ↑
- Burst Flash: SprintBurstedThisFrame 엣지에 짧고 굵은 라인 버스트 1회
- 색=시안 HDR, 블렌드=가산(Additive), Stretch RenderMode — 탑다운 부감에서 선으로 읽힘

**부착 위치:** 플레이어 루트 또는 비주얼 GO. motor를 Inspector 직접 연결하거나 부모/자신 자동 탐색.

**노브 (Inspector):**
- `tierEmitRate` — Tier별 초당 방출 수(기본 18/36/60)
- `particleLifetime` — 꼬리 길이 (기본 0.18s)
- `stretchLength` — 선 늘어남 배율 (기본 6)
- `streakSpeed` — 뒤로 날아가는 속력 (기본 3.5 m/s)
- `burstCount` — 버스트 파티클 수 (기본 22)
- `trailColor` / `burstColor` — HDR 시안 색 (블룸 세기 = RGB 크기)

**Stab 수정 이력 (H/M):**
- H-1: OnDestroy 추가 + DontSave 제거 → GO 누수 방지
- H-2: sh==null 최종 폴백(InternalErrorShader) → 빌드 크래시 방지
- M-1: SprintBurstedThisFrame 체크를 IsSprinting 가드 안으로 이동
- M-2: tierEmitRate 배열 0 길이 Awake 방어

**검증 상태:** 컴파일 에러 0, Stab H-2건 수정 완료. 동적 흐름 손맛 = 유저 플레이 판정.

**How to apply:** 파티클 코드생성 컴포넌트 — [[killburst-fx]] 함정(머티리얼 복제 필요) 비해당. 자족이라 드라이버 배선 불필요.
