# 2026-07-08 속도 표기 일괄 시공 핸드오프 (RESUME)

> **상태:** Slice 0(07-07) Codex 리뷰 회수·픽스 + 애니 속도 표기법 웹 조사(레퍼 보드) + **표기 원자 4종 일괄 시공 완료·Stab+Codex 게이트 통과 — 전부 미커밋·유저 플레이 판정 대기.**
> 총지휘 = Fable. 기저 문서 = [[2026-07-07-speed-language-slice0-handoff]](Slice 0 노브·함정, §6=오늘 시공 요약) · 조사 권위 = [[2026-07-08-anime-speed-notation-reference-board]] · 기획 권위 = [[2026-07-07-speed-language-v1]].
> 미커밋 덩어리 = 07-05 E 선풍참 델타 + 07-07 Slice 0 + **오늘 델타** — 판정 통과 시 일괄 커밋.

---

## 0. 오늘 한 일 (요체)

1. **Slice 0 Codex 리뷰 회수**(어제 백그라운드 완주분) — Crit/High 0. M-2(캔슬 후 블렌드아웃 클립의 고스트 swish) → `KatanaWeapon.OnSwishWhoosh`에 `_step >= 1` 가드 픽스. M-1(무음 폴백)은 사변적 방어로 기각(리타이머가 이벤트 자동 재저작+무음은 즉시 가청).
2. **유저 의도 실집행** — "애니 표기법은 웹에서 찾아와 우리 것에 쓸 걸 찾자는 거였다" → 웹 조사 → **레퍼 보드**: 계보 5개. ❌프레임 저작(GG Xrd/Hi-Fi Rush — 셀 커밋 전제, **실사 A와 충돌로 봉인**) / ✅화면 이벤트(임팩트 프레임)·UI(P5)·잔상(HLD/Ruiner)·카메라(GoW/ZZZ)는 조건 성립+부품 기보유.
3. **유저 "한번 다 적용해서 봐볼까? 우리만의 방식으로"** → 표기 원자 4종 일괄 시공(§1).
4. **게이트**: Stab+Codex 병렬 — 수렴 지적(티켓 위치 트윈 경합) 포함 전 지적 픽스, 컴파일 클린(상세 = 07-07 핸드오프 §6 게이트 항목).

## 1. 시공 델타 (전부 미커밋)

- **`SpeedLanguageDirector.cs` (신규)** — 킬(EnemyDamageReceiver.AnyDied) 3티어 지휘. 매 킬=티켓 스탬프("№0042 처리 종결" 시안/엘리트 "특이개체 종결" 금)+연속 처리 ×N · 멀티킬(0.12s 내 3)=임팩트 프레임 · 엘리트=+시네마틱 펀치인+FOV 펀치+히트스탑 0.08. UI 전부 코드 생성(DOTween unscaled·티켓 풀·posTween 단일 소유).
- **`PurgeSnapshotFX.cs` (수정)** — 스킨 2종: **A=PaperInk**(종이+잉크, 원본)/**B=SignalCollapse**(블랙아웃 #0B0A12+마젠타 — 신호 붕괴 캐넌) + **집중선**(만화 방사선 — 컷 동안만, 중앙 공백, 매 컷 결번/지터, 잉크 셰이더와 생사 분리).
- **`EnemyDamageReceiver.cs`** — `MaxHp`·`LastHitFrom` 공개 접근자 2줄.
- **`Editor/SpeedLanguageLabWiring.cs` (신규)** — RunFeel 전용 배선 메뉴(대상 씬 루트만 검색·멱등). `RunFeel_Whitebox.unity`에 Director GO 배선·저장됨.
- **`KatanaWeapon.cs`** — OnSwishWhoosh `_step >= 1` 가드(Slice 0 Codex M-2 픽스).
- 문서: 레퍼 보드(02_logs) · 07-07 핸드오프 §6/게이트 추기.

## 2. ★RESUME — 유저 플레이 판정 (RunFeel_Whitebox, Play 후 호드 갈기)

| 순간 | 나와야 하는 것 |
|---|---|
| 매 킬 | 우상단 "№0042 처리 종결" 시안 스탬프 + 연속 킬 시 "연속 처리 ×N" |
| 한 스윙 3킬(0.12s) | 임팩트 프레임 1컷(기본 스킨 B: 블랙아웃+마젠타 실루엣+집중선) |
| 7킬째(랩 디버그 엘리트) | 금색 "특이개체 종결" + 1컷 + 카메라 펀치인+히트스탑 |
| **F9** | 스킨 A/B 라이브 전환 — **팔레트 캡처 게이트의 판정 실행**(정지 캡처 대신 인게임 실물) |

**판정 질문:** ①티켓이 속도를 공급하나, 소음인가(P5 계보 가설 실측) ②1컷 스킨 A vs B ③엘리트 펀치인 수위 ④**이 표기들이 "우리 것"으로 느껴지나(간지 法)** — 그리고 Slice 0 질문 승계: E "확-휘익-딱" 읽힘·swish 토글 A/B·**"이래도 여전히 밋밋한가"**.
판정 후: **통과 조각만 커밋**(07-05+Slice 0+오늘 일괄) → 다음 조각 = **최소 템포 루프 조립**(달려드는 적+킬 케이던스+시그니처 1+음악 1단 — 착공 전 비트 지도 유저 합의). 기각 조각은 노브 0이 아니라 **컴포넌트/토글 off**로 내리고 기록.

## 3. 노브 지도 (SpeedLanguageDirector 인스펙터)

| 노브 | 기본값 | 효과 |
|---|---|---|
| multikillCount / Window | 3 / 0.12s | 임팩트 프레임 발동 문턱 |
| eliteHpThreshold | 8 | 본게임 엘리트 판별(스포너 HP) |
| **debugEveryNthElite** | **7** | ★랩 판정용 — N킬마다 엘리트 취급. **본게임 승격 시 0** |
| skin | B(SignalCollapse) | F9 라이브 토글과 동기 |
| eliteCinematicWeight / Hold / FovPunch / HitStop | 0.55 / 0.4 / 5° / 0.08 | 펀치인 수위 — 과하면 weight부터 ↓ |
| ticketLifetime / slideTime / maxTickets | 0.9 / 0.09 / 6 | 스탬프 케이던스 |
| streakWindow | 1.6s | ×N 유지 창 |

(Slice 0 노브 — E whoosh 후보/볼륨·swish 토글·잔상 밀도·리타이머 상수 = 07-07 핸드오프 §3.)

## 4. ⚠️함정 기록

- **티켓 위치 = posTween 단일 소유** — 위치를 만지는 트윈 추가 시 반드시 기존 핸들 Kill 후 재트윈(같은 anchoredPosition을 두 트윈이 잡으면 늦게 끝난 쪽이 슬롯을 되돌린다 — Stab H-1≡Codex M-1 수렴 지적, 멀티킬에서 확실 재현이었음).
- **집중선은 잉크(_inkMat)와 생사 분리 계약** — InkBlob 셰이더가 스트립돼도 집중선은 산다. 재커플링 금지(Stab M-2).
- **폰트 = 맑은 고딕 OS 동적(랩 플레이스홀더)** — 본게임 승격 시 Pretendard Dynamic SDF + Story 카피 패스("처리 종결"/"특이개체" 어휘는 렉시콘 미경유 임시).
- **PurgeSnapshot 발작 가드 0.5s** — 엘리트+멀티킬이 0.5s 내 연속이면 두 번째는 스미어만(화이트아웃 스킵). 의도된 강등이니 "안 나온다" 착각 주의.
- **snapshotOnElite/Multikill은 OR 독립 발화**(Codex M-3 픽스) — 삼항으로 되돌리지 말 것.
- 승계: 리타이머 재실행=이벤트 재저작 · Whirlwind 사운드=meleeSfxEnabled 밖 · 콤보 클립=.anim(07-07 §4).

## 5. 대기열 (변동 없음 — 속도 언어 §7 기준)

⓪ Slice 0+표기 일괄 = **본 문서(판정 대기)** → ① 최소 템포 루프 조립 → ② 표기 폴리시 잔여(거합 4비트 재도전 · Codex 어휘 픽 · 표기 어휘 동결=팔레트 판정 후) → ③ R 궁극·전직 gd 재정합.
