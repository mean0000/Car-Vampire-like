# 2026-06-19 핸드오프 — 발사체 룩랩 · 죽음 연출 스펙 · 발광 트리트먼트

**대상 독자:** Unity 전담 세션 + 다음 세션. **작성 세션 = 비-Unity 트랙**(종일 다른 세션이 Unity MCP/에디터 점유 → 이 세션은 unity-mcp·`Assets/` 쓰기 안 함, Blender·docs·리서치만).
**연동 메모리:** [[project_2026_06_19_emissive_treatment_blender_workflow]] · [[project_2026_06_19_death_staging_research_spec]] · [[project_2026_06_19_artdir_realistic_confirmed]]

---

## TL;DR (3개 산출물, 전부 판정/구현 대기)
1. **발사체 룩랩** (메시 vs 빌보드) — Unity에 빌드 완료, **유저 플레이 판정 대기**. 단 유저가 "이펙트는 배경 다음"으로 **보류**.
2. **죽음 연출 티어 스펙** — 리서치 종합 제안서, **유저 동결 대기**. 동결 시 Unity 세션 구현.
3. **발광 트리트먼트** (어두운 바디+발광부위+블룸) — Blender 컨셉 증명 완료, **유저가 Unity서 확인 예정**.

---

## 1. 발사체 룩랩 (메시 vs 빌보드) — Unity 빌드 완료, 판정 보류
**왜:** 웹+Codex 리서치가 "물리형 불렛 = 메시(Codex) vs 빌보드(웹)"로 발산 → 유저 결정 "둘 다 프로토→Unity 실측".

**빌드된 것 (전부 컴파일·셰이더 렌더 확인됨, 정적):**
- `Assets/_Project/Shaders/AcidGlobBillboard.shader` — 카메라 보는 쿼드 + SDF 코어+글로우(가산). 신규.
- `Assets/_Project/Shaders/AcidGlobMesh.shader` — 저폴리 프레넬 림 발광(균일 가산=형태 죽음 함정 회피). 신규.
- `Assets/_Project/Scripts/ProjectileLookLab.cs` — 룩랩 스포너(45° 카메라·Bloom·어두운 바닥·메시 레인 vs 빌보드 레인, 동일 탄속/색/크기, 카메라 쪽으로 날아옴). 신규.
- `Assets/_ProjectileLookLab.unity` — 비교 씬(위 컴포넌트 1개).

**테스트 방법 (Unity 세션):** `Assets/_ProjectileLookLab.unity` 열고 ▶Play. 좌 레인=메시(각진 프레넬), 우 레인=빌보드(SDF 코어+글로우). Inspector 노브로 색/크기/속도 실시간. **판정 기준 = 거리·움직임에서 어느 쪽이 더 잘 읽히나.**

**검증 경계 (게이트, 비누설):** 내가 직접 검증=컴파일+셰이더 정적 렌더(마젠타 아님, 의도대로). **미검증=실제 45°+Bloom+움직임 가독성**(MCP 플레이모드 막혀 못 봄) → **유저 플레이 판정 몫**. 자기 미적 인증 안 함.

**상태:** ⏸ **유저가 "이펙트는 배경 더 만져진 뒤"로 보류.** 기존 `ProjectilePool`은 안 건드림(외과적). 승자 결정 시 풀에 접고 ScriptableObject 9노브 패턴 시스템으로 확장(양 리서치 공통 권고).

## 2. 죽음 연출 티어 스펙 — 제안서, 동결 대기
- **문서:** `docs/02_logs/2026-06-19-monster-death-staging-research-spec.md` (+ 보기용 `.html` 동일 경로)
- **근거:** 웹+Codex 거의 완전 수렴(수렴=신뢰).
- **요지:** 티어=뽕 경제. 잡몹 싼죽음(물리0) / 엘리트 디졸브셰이더+약히트스탑 / 보스 RayFire파쇄+슬로모. 티어상승=오버킬·노히트·궁극킬 트리거.
- **핵심 기술:** 디졸브/정점폭발 셰이더는 **살아있는 스킨드메시 위 작동=BakeMesh 불필요**(GPU스키닝). RayFire=특별킬 전용. **MaterialPropertyBlock 필수**(material.SetFloat=인스턴싱 깨짐). 색=레드오렌지→킬 시안플래시→회색.
- **★미결(§8):** 잡몹 죽음 디졸브셰이더(웹) vs 스케일팝(Codex). 기본값=Codex 보수안(팝)으로 시작, 프로파일 싸면 잡몹 디졸브 승격.
- **상태:** ⏳ **유저 동결 대기.** 동결 시 → Unity 세션 1차 구현=디졸브 셰이더 + 잡몹/엘리트 죽음 컨트롤러 + Feel 와이어링.

## 3. 발광 트리트먼트 (몬스터 룩) — Blender 증명, Unity 확인 대기
- 레퍼(거미 로우폴리)=어두운 바디+발광 부위(이빨/볏/눈)+블룸. Caniathrox에 증명.
- **워크플로:** Unity OBJ export→블렌더 decimate+flat→어두운 바디 머티리얼→유저가 발광 부위 면 선택→발광 머티리얼 할당→numpy 후처리 블룸(블렌더5.1 블룸API 막혀 우회).
- **유저 `.blend` 저장함**(휘발 안 됨). **상태:** 유저가 Unity서 직접 확인 예정. Unity 정합(URP HDR 발광+Bloom, rig 보존)은 미착수.
- ⚠️ 아트방향 [[project_2026_06_19_artdir_realistic_confirmed]]=실사 A 동결인데 이 트랙은 몬스터 로우폴리화(path B) 탐색 = 긴장. 발광 발사체/VFX는 무관(발광체). 몬스터 메시 로우폴리화는 미해소.

---

## 임시 산출물 (워크스페이스 루트 — 정리/.gitignore 검토 대상)
`_caniathrox_export.obj` · `_blender_caniathrox*.png` · `_glow_teeth_v*.png` · `_proj_candidates*.png` · `_glob_shader_check.png`
→ Blender 컨셉/검증 캡처. 커밋 불필요, `.gitignore`로 거를지 다음 정리 세션 판단.

## 오너별 다음 액션
- **유저:** ①죽음 스펙 동결 판정(§8 기본값) ②(배경 진척 후) 발사체 룩랩 플레이 판정 ③발광 트리트먼트 Unity 확인.
- **Unity 세션:** 죽음 스펙 동결되면 구현 착수. 발사체 룩랩은 유저 판정 후 풀 통합.
- **이 비-Unity 세션:** 종일 가용 — 배경/설계/리서치/Blender 트랙(유저 지시 대기).

## 미검증 플래그 (§1.5)
죽음/발사체 리서치 = dev-blog/inference 등급. RayFire 가격/세일 **미검증**(7월 구매 예정). RayFire 스킨드 내부베이크 전 구성 커버 여부=구매 후 테스트.
