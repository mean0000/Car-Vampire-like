# 2026-07-05 기술 키트·E 선풍참 핸드오프 (RESUME)

> **상태:** 슬롯화·관용 P0 커밋 완료. **E 선풍참 시공 완료·게이트(Stab+Codex) 통과 — 미커밋, 유저 손맛 판정·튜닝 대기(=내일 첫 작업).**
> 총지휘 = Fable. 메모리 = `project_2026_07_05_blade_specialization_direction` · `project_2026_07_05_weapon_action_slots` · `project_2026_07_05_melee_forgiveness_audit` · `project_katana_whirlwind_eskill`(Animation).

---

## 0. 오늘 커밋 (전부 main)

| 커밋 | 내용 |
|---|---|
| `edf90f6d6` | chore: Hack&Slash 사운드팩 gitignore 등록 |
| `af85ba896` | **무기 액션 슬롯화** — WeaponActionSet SO 5슬롯(RMB/E/R/반격/대시) + 공통 어휘(Hit/Vfx/Sfx/Timing/Animator/Charge) + WeaponVfxSpawner 단일화 + SkillSet 폐기 + Data/Combat 재배치. 새 스킬 = 에셋(코드 0) |
| `bf1cd670d` | **방향성 붕괴(채널 7)** — IDeathStager+DirectionalCollapse. ★유저 판정 "너무 어색" → 보류(§4 처방) |
| `7945bd854` | **관용 P0** — 표면거리 판정(SurfacePoint)·초근접 구제(LOS는 상시)·커서 적 픽킹(PoE2 방식). 권위=`02_logs/2026-07-05-melee-forgiveness-audit.md` |

## 1. ★미커밋 워킹트리 (내일 판정 후 커밋)

**E 선풍참(Whirlwind) 전체** + 문서 2건:
- 코드: `WeaponActionData.cs`(WeaponLungeData·WeaponDensityData 신규 블록) · `WeaponActionSet.cs`(lunge/density 필드) · `KatanaWeapon.cs`(BeginActionSlot 런지·DoActionHit 밀도환류·_lastSwingDirectHits·MakeRuntime 트리거 실존검증) · `PlayerAnimatorDriver.cs`(HasTrigger) · `PlayerMotor.cs`(CancelGlide) · `PlayerBrain.cs`(Died→하드컷, E/R 키)
- 에셋: `Data/Combat/Katana_Whirlwind.asset` · `Animations/KatanaMelee.controller`(Whirlwind 상태) · Frank `..._WhirlWind.FBX.meta`(베이크+이벤트) · 랩 씬 5개(skillAction 배선)
- 문서(untracked): `02_logs/2026-07-05-katana-skill-kit-draft.md`(gd) · `02_logs/2026-07-05-blade-specialization-reference-board.md`(전직 레퍼 보드)

## 2. 확정 사항 (유저 판정 완료 — 뒤집지 말 것)

- **전직 3종 방향(유저 발안)**: 찌르기(突)/베기(斬)/발도(拔刀) — 기존 거합/참격 2트리의 확장(베기≈참격 뉴비, 발도≈거합 고수, 찌르기=신규). 초반=베기 위주. **일섬(관통 돌진)=발도 전직에 파킹**(해금 뽕 순간 카드). 구조 동결은 gd 재정합 후(§6).
- **E = 전진 선풍참**(베기): 확확—딱 리듬(유저: "확확 움직이는 연출이 있어야 재미" — 단 확확은 '딱' 대비로 서는 것). 방사 넉백 ❌ → 스태거 축적(VS Garlic 자멸 교훈). 밀도 보상 = 직접 명중 3+ 시 쿨 30% 환류.
- **R = 하이브리드 게이지**(킬 뼈대+화려함 가속, 순수 쿨다운 기각, 디제틱 UI 최소) — §6 다음 시공 후보.
- **종합 테제**: 셋 다 "선(線)"의 변주 — 찌르기=꽂고/베기=잇고/발도=저장했다 끊는다. **억제 경제 삼각**: 찌르기=깊게, 베기=넓게 쌓고, 발도=수확.

## 3. ★RESUME 1순위 — E 선풍참 손맛 판정·튜닝 (유저: "내일 수정해야 할 것 같아")

**판정 씬 = RunFeel_Whitebox**(호드 밀도, 현재 열려 있음). E키 발동.

**조립 상태**: E → 조준 방향 2.8m/0.1s 등속 런지(확) → Whirlwind 상태(휘돌기 1회 컷, 360° 판정 r3.2·dmg5·kb0) → exitTime 컷+0.08 블렌드(딱). 3마리+ 직접 명중 시 쿨 30% 환류.

**노브 지도 (수정 지점):**
| 노브 | 위치 | 현재값 | 효과 |
|---|---|---|---|
| ★상태 speed | KatanaMelee.controller > Whirlwind | **1.5** (1.2~1.8) | 윈드업이 재생 71%라 굼뜸/스냅의 사활 레버 |
| ★exitTime | Whirlwind→Locomotion 전이 | **0.384**(휘돌기 1회) ↔ **0.95**(풀 4회전+클립 내장 잔심) | 회전수 A/B — 한 값 교체. OnComboEnd(0.35→0.90)와 페어 |
| exit 블렌드 | 같은 전이 | 0.08 | 0=하드컷 "딱"↑ |
| 런지 | Katana_Whirlwind.asset > lunge | 2.8m / 0.1s | 파고드는 거리/스냅 |
| 판정 | 〃 > hit | r3.2 · dmg5 · 쿨6s | |
| 밀도 | 〃 > density | 3히트 / 30% | |
| 이벤트 | FBX .meta | OnAttackHit 0.305 / OnComboEnd 0.35 | 항상 < exitTime 유지 |

**게이트가 판정으로 넘긴 항목:**
1. **런지→스윕 순서**(Stab M-2): 2.8m 전진 *후* 회전이라 원래 자리 등 뒤 적이 사거리 밖으로 밀릴 수 있음 — "파고들어 벤다"가 읽히나? (거슬리면 lunge 축소 or 판정 시점 논의)
2. **E 입력 버퍼 없음**(Stab M-3): 콤보/대시 중 E 씹힘 — 연타 답답하면 버퍼 후속 비트.
3. Codex 짚음: 커서가 적 몸통 위면 런지가 그 적 방향(P0 커서 픽킹 파생 — 의도).
4. VFX 링 없음(artist 후속)·SFX 비어 있음(에셋 Inspector에 로컬 Whoosh 드래그 가능 — gitignore 에셋이라 SO에 커밋 참조 금지).

**⚠️함정 기록**: Frank WhirlWind FBX .meta의 `loopBlend*`=Bake Into Pose 직렬화 값(스핀 in-place의 근거) — "죽은 값"으로 오인해 되돌리면 4m 루트모션 부활=런지 이중이동(Stab L-1 오진을 메모리로 기각한 사례).

## 4. 붕괴(채널 7) 재도전 처방 (보류 중 — 원인 진단 완료)

유저 판정 "너무 어색" → 발도 리서치가 원인을 특정: 업계 표준 = **4비트(①베기 ②정적—간지 비트로 채움 ③딸깍 납도음 ④일제 붕괴)**인데 프로토는 ②가 빈 캡슐 멍때림·③ 부재. "②의 길이와 ③의 사운드가 손맛 전부"(TV Tropes Delayed Causality·DMC3 버질 정본). 재도전 부품: 납도 포즈/무음 + **Hack&Slash `Handling/Sword Sheath` 사운드(보유!)** + 접촉 버스트 VFX + 실몹. 코드(IDeathStager/DirectionalCollapse)는 게이트 통과 상태로 커밋돼 있음 — 랩 키 7로 off 가능.

## 5. 관용 P1/P2 잔여 (P0 완료 — 감사 문서 참조)

P1(판정 후 후속): 히트 윈도우(단발→3~6프레임) · 소각도 조준 스냅(상한 10~15°, 커서 불가침) · LOS 멀티레이. P2: 액션 모양 패리티(lineCut을 WeaponHitData로) · 적 근접타격 LOS 추가 · GoW식 흡인. ⚠️실몹 붕괴 확장 시 스포너 `_alive` 슬롯 1.3s 점유(Stab L-1) 필독.

## 6. 대기열 (판정 후 순서)

1. **E 판정 통과 → 커밋** (미커밋 델타 전체 + 문서 2건)
2. **R 궁극 시공** — 가짜 반토막(긴 라인 즉사+디졸브+줌/화이트아웃=재활용 자산) + 게이지 컴포넌트 1개(킬+화려함 가속·디제틱 UI). 드래프트 §4 참조
3. **전직 구조 gd 재정합** — 해금·전환·카드풀 배분·2트리 캐넌 개정·전직별 슬롯 구성 (레퍼 보드 §4 미결 목록)
4. 사운드 두 갈래 통합(PlayerAttackSfx vs KatanaWeapon swish/impact — R1 이후 과제) · 붕괴 재도전(§4) · 원자랩 잔여(3변이 랜덤·이중음 코드픽스)

## 7. 오늘 세운 시스템 자산 (다음 스킬부터 공짜)

새 스킬 = `WeaponActionSet` 에셋 1개(판정·타이밍·VFX·SFX·트리거·차징·런지·밀도 전부 데이터) + Animator 상태 1개(Animation 에이전트) + 슬롯에 드래그. E/R 입력·게이트·워치독·자가치유·사망 하드컷 전부 기성 레일.
