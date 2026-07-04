# 2026-07-05 원자 손맛 랩 (AtomLab) — 핸드오프 (RESUME)

> **상태:** 카타나 "정지 한 방" 손맛 원자 랩 구축·커밋(`d2c3864dd`). Hack&Slash 실물 팩 사운드 배선.
> **다음 = ①R1 사운드 판정(유저 귀 + Fable) → ②붕괴(채널 7) 빌드.**
> 총지휘 = **Fable**(model:fable, 지령+판정) · 실행/게이트지기 = **Opus** 세션. 메모리 = `~/.claude` project_2026_07_04_atom_feel_lab.

---

## 0. 왜 원자로 돌아갔나 (재프레이밍)

유저: "너가 주는 결과가 아쉽다 · 우리 게임에 어울리는 게 없다." → 진단: 최근 세션(억제 스택·무빙 평타·상하체 분리)은 *일반 액션 손맛 엔지니어링*이라 **이 게임(사후처리부·뽕·화려함=실적=생존)을 안 말한다.** 이어 유저 "손맛 *자체*가 밋밋" → 뿌리 = **원자(정지 한 방 = 06-28 유저 명명 "간지 본체")를 못 굳힌 채 5개 시스템을 쌓은 것**(= 프로젝트가 반복 지적당한 "기능 추가로 재미 도망"). → 호드·억제·무빙 전부 끄고 **한 마리·정지·한 방**부터.

## 1. ★Fable 진단 (총지휘 독립 판정) — 동결

- **밋밋의 본체 = 결과 측 부재.** 입력 측 신호(카메라킥·쉐이크·글라이드·스냅·슬래시)만 있고, "세계가 베였다"는 신호는 흰 점멸 하나뿐. **"베기의 간지는 검이 아니라 피해자가 증언한다."** 예산은 입력 측을 더 키우는 게 아니라 **결과 측으로 이동**.
- **Floor work 확정.** 70/30 교훈("무게는 게임감서 안 나온다")은 "닿았다는 *물리적 가독*"까지 부정한 게 아니다.
- ★**게이트 질문 = "베였다고 몸이 믿냐"** (~~"무게감 있냐"~~ = 천장, 경제 몫). **전환점 감지기 = "깔끔한데 심심해" 문장** → 손맛 종료, 스테이크(경제)로 전환. 예산 감지기 = 히트스탑 ±10ms 다섯 번째 만지면 함정 재진입.

## 2. ★최고 레버 = 방향성 붕괴 (거합 문법) — 채널 7, R1 후 해금

벤다 → 히트스탑 정적 한 박자 → **반 박자 늦게** 표적이 **슬래시 벡터 방향으로** 갈라지며 무너진다. **넉백 절대 금지**(방망이 동사 = 싸구려). 거합 "납도 후에야 쓰러진다" 문법 · 수평 반토막 궁극과 한 계보.
- 소유 = **Gameplay(model:opus) 절차적**(화이트박스 캡슐엔 애니 클립 없음) + 접촉 버스트 VFX = **artist**.
- ★**R1 통과 전 착수 금지.**

## 3. ★DRY 사운드 아이덴티티 (Fable) — 동결

- **살+뼈 "촥-턱"**(금속 "챙" 아님) · DD 저역 묵직 · **드라이**(리버브/잔향 금지 = **사무적 학살**, 젖은 영웅검 ❌). **삼각 = 즙(고어) × 무게(DD) × 드라이(사무).**
- **조달 판정: 진짜 팩 사되 R1 블로커 아님** — 합성/플레이스홀더로 "소리 유무 A/B" 검증 → "소리가 최대 레버" 확인 **후** 지출(투자 근거 확보 후). 팩 = "완성 카타나 소리"가 아니라 **레이어 재료**.
- 유저 **Hack and Slash Sound Library 구매·임포트**(★`.gitignore` = 로컬 전용). ★**`Audio/Gore` 폴더 = 우리 정체성.** 채택: impact = **Sword Stab Impact 02** · swish = **Whoosh Short Low 01**(현재 유저 판정 중 잠시 제거 → impact만). 제외(Fable 5필터): 금속 Weapon Clash · Metal · Cinematic · Reverb Shimmer · Schwing. `Handling/Sword Sheath` = 추후 거합 납도/발도.

## 4. ★줌펀치 = 스킬에만 (확정)

카메라 `NotifyHit`(FOV 줌펀치)를 **스킬/반격/대시베기에만**, 평타 콤보 제외(유저: 매 평타 줌은 과함, 스펙터클을 피크에 아낀다). 카메라 킥/스냅은 평타 유지(줌펀치만 제거). 구현 = `KatanaWeapon.FireHitFeedback(camKick, finisher, zoomPunch)` — 평타 호출부만 `zoomPunch:false`.

## 5. AtomLab 리그 (커밋 `d2c3864dd`)

- 씬 `Assets/_Project/Scenes/Labs/_AtomLab_OneCut.unity` — 원본 `_CombatSlice_ReadAndCut` SaveAs, **무수정 보존**. 억제 5시스템 off 고정.
- `AtomLabRig.cs`: 정지 더미(HP50) 1기 + 채널 키토글 — **1**소리 **2**플래시 **3**쉐이크 **4**히트스탑 **5**입력킥/글라이드 **6**스냅(Animator.speed 프록시=한계) **7**붕괴(예약, 미배선) · **R** 리스폰 · **K** 1방킬(HP1↔50) · OnGUI HUD. 시작 = 전 채널 ON(현행 풀 손맛 베이스라인).
- 디버그 setter(`EnemyDamageReceiver`/`KatanaWeapon`) — 리플렉션 없음, 런타임 0↔Awake 원값 스왑, `#region ★AtomLab 디버그 채널 토글`.
- **R1 플레이 절차:** Play → 키 1~5 다 꺼 제로 베이스라인 → 키 1만 켜 사운드 델타 → "소리 하나로 닿았다 몇 %".

## 6. 이중음 사고 + 수정 (★로컬 전용 주의)

콤보 슬래시 프리팹 `VFX_Slash_Earth`(Vefects)에 `SFX_Slash_Earth`(playOnAwake=true) 내장 → `PlayerAttackVfx.StripEmbeddedAudio`가 있으나 **playOnAwake가 Instantiate 도중 이미 발성 후라 사후 Stop()이 못 막음.** 수정 = **프리팹 AudioSource `clip=null` + `playOnAwake=false`**(로컬 뮤트).
- ⚠️ **Vefects는 `.gitignore` = 미버전.** 재임포트 시 뮤트 소실. **로버스트 대안(미착수 후보) = `PlayerAttackVfx`에서 비활성 인스턴스화로 playOnAwake 원천 차단**(버전관리되는 코드 픽스).
- 별건: swish에 full slash(Blade Slash Short)를 쓰면 impact와 **베임 2회 겹침** → swish는 **순수 whoosh**여야 함(Blade Slash Short 부적합, Whoosh 계열로 교체).

## 7. 게이트 기록

- **Stab 통과** (Critical 0 · 소프트락/영구오염 없음): KatanaWeapon 신규 Awake 안전 확정(베이스 WeaponBehaviour Awake 없음) · 디버그 setter 직렬화 오염 없음(Play 종료 자동복원) · 씬 지오메트리 정합(레이어7·콜라이더·사거리·LOS → 스윙→TakeHit→플래시 성립) · **M-1**(SetFlashEnabled(false) 흰 스파이크) 수정 반영 · **M-2**(줌펀치 스코프)는 §4 설계 변경으로 해소.
- **Codex 미완** (백그라운드 장시간 전환) — 회수 후 반영. 리그는 Stab 통과 + 유저 플레이 검증됨.

## 8. RESUME 체크리스트

1. **R1 사운드 판정** — 임팩트만으로 "소리 하나로 '닿았다' 몇 %" · Sword Stab Impact **01/02/03** 변이 · **swish 복구 여부** → **Fable R1 판정**(60%+면 나머지 채널 순위 재편).
2. **붕괴(채널 7) 빌드** — R1 통과 후. Gameplay(opus) 절차적 + artist 접촉버스트. 게이트 = "베였다고 몸이 믿냐" + "반 박자 늦음이 거합 간지냐 랙이냐"(★슬래시 파티클 scaled면 히트스탑 중 같이 얼어 랙 — unscaled 정리 필요).
3. **대기 옵션:** 3변이 랜덤(`impactClip` → `impactClips[]` 배열) · 로버스트 이중음 코드픽스 · Codex 회수.

## 9. 주요 노브 / 경로

- 사운드: `KatanaWeapon` impactClip/swishClip(씬-로컬 오버라이드) · `impactVolume 0.5`/`swishVolume 0.35`(팩 라우드니스 미상, 귀 튜닝) · `meleeSfxEnabled`.
- 팩 경로: `Assets/Hack and Slash Sound Library/Audio/{Gore,Whoosh,Slash}/` (전부 gitignore·로컬).
- 커밋 `d2c3864dd` (22파일 — 벤더 팩·Vefects 제외).
- 이전 핸드오프: `docs/01_handoffs/2026-07-04-combat-recast-session-handoff.md`(억제 스택·무빙 평타 세션).
