# 2026-07-07 속도 언어 게이트 + Slice 0 시공 핸드오프 (RESUME)

> **상태:** 속도 언어 v1 경쟁 게이트 완료(오케 안 패배 → 처리 문법 시험 채택) + **Slice 0(원자 배선) 시공 완료·Stab 통과·Codex 교차 리뷰 회수 완료(07-08, Critical/High 0·M-2 고스트 swish 가드 픽스 반영) — 미커밋·유저 플레이 판정 대기.**
> 총지휘 = Fable. 권위 문서 = [[2026-07-07-speed-language-v1]](원안+Codex 원문+Challenger 평결+§10 갱신 입장 전부). 메모리 = `project_2026_07_07_speed_language_gate`.
> 기저: 07-05 E 선풍참 델타도 여전히 미커밋([[2026-07-05-skill-kit-whirlwind-handoff]]) — 이번 판정 통과 시 함께 커밋.

---

## 0. 오늘 결정 (요체)

1. **유저 진단 확장**: E 속도감 → **"게임 자체의 속도감 + 특색 부재, 포인트가 없다"**. 유저 언어: "벨 때마다 무언가가 있어야" · 엘리트 거대+카메라 전환(유저 발안) · 애니 속도 표기법에 "맞어!! 난 이런걸 원했어".
2. **경쟁 게이트(P0)**: 오케=신호 문법(글리치) vs **Codex 독립안=Clock-Out Latency**("속도=접수→종결 확인의 빠르기") vs **Challenger=대안 B**(사후처리부 직장인 축). Codex·Challenger가 서로 모른 채 **관료제 처리 축으로 수렴 → 오케 안 패배, 위계 뒤집힘**: 처리 문법이 왕, 글리치는 렌더링 어휘.
3. **Challenger 필수 수정 수용**(v1 §10): 템포/루프 슬라이스 2번째로 전진 · 마젠타 상시 바닥 폐기+캡처 게이트 · **크레딧(저작감)=판정 포인트**(자동 스펙터클 보류) · 체인 발도=카타나 시그니처 재표기 · 유저 픽 복권(엘리트 카메라 헤드라인).
4. **유저 판정**: "일단 이번에 해볼 기획으로 생각해보자, 해봐야 아는거니까" = **시험 채택(가설·동결 아님, 검증=시공+플레이 귀납)**.

## 1. Slice 0 시공 완료 (오늘 델타 — 전부 미커밋)

**코드 4파일:**
- `PlayerAnimatorDriver.cs` — `SwishWhoosh` 이벤트 + `OnSwishWhoosh()` 수신부(기존 릴레이 문법).
- `PlayerAfterimage.cs` — `EmitBurst(duration)` 공개 API: 대시 외 순간 전진(스킬 런지)에 잔상 창.
- `KatanaWeapon.cs` — `swishOnAnimEvent` 토글(기본 true, A/B 롤백 노브) · SwishWhoosh 구독/해제(재진입 가드 편입) · BeginCombo/Advance 코드 발화를 `!토글` 게이트 · `Afterimage()` 지연 캐시 + BeginActionSlot 런지에 `EmitBurst(lunge.duration)`.
- `Editor/KatanaComboRetimer.cs` — ★Stab H-1 픽스: `OnSwishWhoosh`를 4번째 저작 이벤트로 추가(**소스 시간 `strikeStart` 앵커** — 리맵 후 hit−0.0318s, 현행 .anim과 동일. Stab 제안 코드의 좌표계 오류를 교정했음).

**애니/에셋:**
- 콤보 리타임 `.anim` 3개(`S1_Combo01_0{1,2,3}_Retimed`) — `OnSwishWhoosh` 이벤트(각 OnAttackHit −0.0318s = 윈드업/스트라이크 경계, messageOptions=DontRequireReceiver). Animation 에이전트가 read-back 검증(4이벤트·순서 정상).
- `Audio/Combat/` 신규 5 wav(구매팩 발췌 — AtomLab 선례): `whirl_whoosh_heavyA/windA/snappyA`, `swish_lightA/B`.
- `Katana_Whirlwind.asset` — sfx.clip=**whirl_whoosh_heavyA**, volume **0.14**.
- `RunFeel_Whitebox.unity` — KatanaWeapon.swishClip **Vefects 플레이스홀더→swish_lightA**(씬 저장됨).

**게이트:** Stab **통과** — 오늘 델타 실질 버그 0건(구독 대칭·토글×이벤트 매트릭스·UpperBodyCombo 레이어 발화·EmitBurst 경계 전부 직접 추적 반증/검증). H-1(리타이머 소실 지뢰)만 필수 수정 → **픽스 반영 완료**. 정보성: L-1(PlayActionSfx는 meleeSfxEnabled 마스터 토글 밖 — Whirlwind 소리는 토글 무시가 정상) · L-3(타 랩 씬 4개도 swish 타이밍이 이벤트 발화로 암묵 전환 — 클립 배정은 무변).
**Codex 교차 리뷰 = 회수 완료(07-08, 세션 로그 22:43 완주분)** — Critical 0 · High 0 · **Medium 2 · Low 2 · Non-Issue 검증 4**(구독 대칭·클립 이벤트 존재·Whirlwind 에셋 값 재확인).
- **M-1**(수용·코드 무변): swishOnAnimEvent=true인데 클립에 OnSwishWhoosh가 없으면 무음 폴백 없음. 노출면=콤보 3클립뿐이고 리타이머가 이벤트를 자동 재저작(H-1 픽스)·read-back 검증됨 + 무음은 랩에서 즉시 가청 → 사변적 폴백 추가 안 함(§2 Simplicity).
- **M-2(픽스 반영)**: OnSwishWhoosh 릴레이에 상태 가드 부재 → 블렌드아웃/캔슬된 클립의 지연 이벤트가 고스트 swish 재생 가능. → `_step >= 1` 가드 추가(OnComboWindow와 동형; Cancel/ResetCombo가 _step=0이라 유효). KatanaWeapon.cs OnSwishWhoosh.
- **L-1**(무변): 신규 필드 swishOnAnimEvent는 타 씬 YAML에 부재 → 코드 default(true) 적용 = 의도값. §4 함정 기록과 동일.
- **L-2**(무변·의도): EmitBurst=스케일드 타임 정합 — 슬로모/정지 시 런지와 잔상이 같은 시간축에서 함께 얼음(게임플레이 타임 도메인 의도).

## 2. ★RESUME 순서

1. ~~**Codex 리뷰 결과 회수·반영**~~ ✅07-08 완료 — 지적 원문 유저 보고·M-2 가드 픽스 반영(§1 게이트 항목).
2. **유저 플레이 판정 (RunFeel_Whitebox)**:
   - E: "확(런지+시안 잔상+속도선)→휘익(벨 때)→딱"으로 읽히는가. 소리 무게 판정(후보 스왑 §3).
   - 평타: 소리가 칼날과 함께 나가는가 — Play 모드에서 `swishOnAnimEvent` 토글 A/B(구방식=입력 즉시·칼 선행).
   - **핵심 질문: 이래도 여전히 밋밋한가?** → 그게 Challenger 예측("원자만으론 안 나온다")의 실측이고 다음 조각의 근거.
3. **판정 통과 → 커밋** — 기저 07-05 E 델타 + 오늘 델타 + 문서(속도 언어 v1·핸드오프) 일괄.
4. **다음 조각 = 최소 템포 루프 조립**(Challenger 수정으로 전진됨): 달려드는 적 + 킬 케이던스 + 시그니처 1개(후보: Closure Click/거합 4비트) + 음악 1단 — 유저 손 튜닝, **언어는 여기서 귀납**. 착공 전 비트 지도 유저 합의(협업 계약 §1).

## 3. 노브 지도

| 노브 | 위치 | 현재값 | 효과 |
|---|---|---|---|
| E whoosh 후보 | Katana_Whirlwind.asset > sfx.clip | heavyA | `windA`(바람결)·`snappyA`(스냅) 드래그 스왑 |
| E whoosh 볼륨 | 〃 > sfx.volume | 0.14 | 캐넌 첫 볼륨 0.03~0.15 |
| ★swish 타이밍 | RunFeel > Katana > swishOnAnimEvent | true | false=구방식(입력 즉시) A/B |
| swish 음원 | 〃 > swishClip | swish_lightA | `swish_lightB` 대기 |
| swish 볼륨/피치 | 〃 swishVolume/Pitch/Jitter | 0.1 / 1.0 / ±0.05 | |
| 잔상 밀도/수명 | PlayerAfterimage interval/lifetime | 0.012 / 0.28 | 런지 0.1s ≈ 8장 |
| 이벤트 시점 | KatanaComboRetimer StrikeLead/StrikeSpeed | 0.07 / 2.2 | 재실행 시 자동 재저작(픽스됨). 수기 .anim 조정은 리타이머와 어긋나니 상수로 |

## 4. ⚠️함정 기록

- **리타이머 재실행 = 이벤트 재저작** — 이제 OnSwishWhoosh 포함(H-1 픽스). 단 .anim을 수기로 미세 조정하면 다음 재실행이 되돌림 — 조정은 리타이머 상수로.
- **콤보 클립 = 리타임 독립 `.anim`(type:2)** — FBX 서브클립 아님. 이벤트/조정은 .anim에(07-07 재확인).
- Whirlwind 사운드는 `meleeSfxEnabled` 밖(PlayActionSfx 경로) — 마스터 토글 테스트 시 "안 꺼진다" 착각 주의(L-1).
- 신규 SerializeField(swishOnAnimEvent)는 기존 씬에서 코드 default 적용 — 씬 덮어쓰기 함정은 *기존 필드*에만 해당.

## 5. 대기열 재배치 (속도 언어 §7 기준)

⓪ Slice 0 = 본 문서(판정 대기) → ① 최소 템포 루프 조립(§2-4) → ② 시그니처/표기 폴리시(거합 4비트 붕괴 재도전=07-05 §4 처방 유효 · 임팩트 프레임=처리 스냅샷 v1 재가동 · Codex 어휘 픽) → ③ R 궁극·전직 gd 재정합(07-05 대기열 승계). 팔레트 캡처 게이트는 표기 어휘 동결 전 필수.
**②의 입력 자료 = [[2026-07-08-anime-speed-notation-reference-board]]**(07-08 웹 조사 — 계보 5개·활용 우선순위·판정 포인트 3개. 요체: 프레임 계보=실사 A와 충돌로 봉인, 화면 이벤트·UI·카메라 계보=조건 성립·부품 기보유).

## 6. 07-08 표기 일괄 시공 (유저 지시 "다 적용해서 봐볼까, 우리만의 방식으로" — ② 표기 원자를 앞당김)

**신규/수정 (미커밋):**
- `SpeedLanguageDirector.cs` (신규) — 킬(AnyDied) 3티어 지휘: 매 킬=티켓 스탬프("№0042 처리 종결", P5 계보·시안/엘리트 금) + 연속 처리 ×N · 멀티킬(0.12s 내 3)=임팩트 프레임 · 엘리트=+ 시네마틱 펀치인+FOV 펀치+히트스탑 0.08. UI 전부 코드 생성(DOTween·unscaled). ⚠️폰트=맑은 고딕 OS 동적(랩 플레이스홀더 — 본게임=Pretendard SDF+Story 카피 패스).
- `PurgeSnapshotFX.cs` — 스킨 2종(A=PaperInk 종이+잉크 / B=SignalCollapse 블랙아웃+마젠타) + 집중선(만화 방사선, 컷 동안만·중앙 공백·매 컷 결번/지터). **F9=A/B 라이브 토글**(팔레트 캡처 게이트).
- `EnemyDamageReceiver.cs` — MaxHp·LastHitFrom 접근자 2줄.
- `Editor/SpeedLanguageLabWiring.cs` (신규) + `RunFeel_Whitebox.unity`에 Director GO 배선·저장.

**노브(전부 Director 인스펙터):** multikillCount/Window · eliteHpThreshold · **debugEveryNthElite=7**(랩 판정용 — N킬마다 엘리트 취급, 본게임 승격 시 0) · 스킨 · 시네마틱 weight/hold·FOV 펀치·히트스탑 · 티켓 수명/슬라이드/색/스트릭 창.
**판정 질문:** ①킬 케이던스가 UI로 읽히나(티켓이 속도를 공급하나, 소음인가) ②임팩트 프레임 A vs B(F9) ③엘리트 펀치인 수위(과하면 weight↓) ④이 표기들이 "우리 것"으로 느껴지나(간지 法).
**게이트(07-08 완료):** Stab+Codex 병렬 — Critical 0. **수렴 지적**: 티켓 위치 트윈 경합(Stab H-1≡Codex M-1, 멀티킬에서 티켓 겹침 확실 재현) → posTween 단일 소유+kill-전-재트윈+Despawn 회수로 픽스. 추가 픽스: 집중선-잉크 커플링 분리(Stab M-2, InkBlob 스트립 시 동반사 방지) · 엘리트 토글이 멀티킬 발화 삼킴(Codex M-3, 삼항→OR) · 세션 카운터 OnEnable 리셋(Stab M-3) · OnDisable 완전 idle(Stab L) · 배선 크로스씬 Find(Codex M-2) · 폰트 null 경고(Codex L) · 라인 앵커 명시(Stab L). 무변 결정: 스트릭 라벨 시안 고정(=나의 케이던스, 의도) · EmitBurst 스케일드 타임(07-08 L-2 결정 승계). 픽스 후 컴파일 클린. 팔레트 A/B는 인게임 F9 라이브 판정으로 대체(정지 캡처보다 상위).
