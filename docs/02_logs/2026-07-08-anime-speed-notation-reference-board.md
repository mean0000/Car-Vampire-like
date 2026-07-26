# 2026-07-08 애니 속도 표기법 레퍼런스 보드 (웹 조사)

> **목적:** 07-07 유저 지시의 실집행 — "애니메이션다운 연출·방법·UI를 웹에서 찾아와 우리 게임에서 활용할 걸 찾자." 속도 언어 v1([[2026-07-07-speed-language-v1]]) 대기열 ②(표기 폴리시)의 입력 자료.
> **우리 필터:** 쿼터뷰 45°/15m · 실사 A(가독성이 보스) · 스탬프 캐넌(슬래시 트레일 금지) · 티어드 주스(호드 밀도 스케일) · 간지 최상위 · **처리 문법이 왕, 표기는 렌더링 어휘**.
> **판정:** 전부 제안 — 동결은 유저. 각 항목 끝 ✅/⚠️/❌ = 오케 적합도 의견.

---

## 계보 1 — 프레임 저작 (셀 애니를 3D가 연기)

**Guilty Gear Xrd** (GDC 2015, Junya Motomura): 애니메이터가 손으로 잡은 키프레임만 재생(보간 죽임=스텝 애니), 캐릭터별 개별 광원, 노멀 수기 편집, **프레임마다 일부러 형태를 왜곡**(스미어/불완전함). 원칙 = "3D처럼 보이는 건 전부 죽여라."
**Hi-Fi Rush** (GDC 2024 툰 렌더링 발표): 전 월드 툰 셰이딩 + 60fps, FLCL·스파이더버스 레퍼, 120BPM 음악 동기 애니. (스미어/임팩트 프레임 세부는 이번 조사 소스에서 미확인 — 80.lv 기사는 인스타 링크만.)
**Mullet MadJack** (2024): VHS 애니(아키라·GitS 계열) — 셀 시대를 흉내 낸 수작업 프레임 + VHS 셰이더. 핵심 교훈: **극단 속도를 "읽히게" 만드는 건 강한 표면 언어**다.

**성립 조건:** 셀/툰 렌더링에 전면 커밋 + 애니 프레임 저작권(리깅·보간 통제). 셋 다 아트 스타일 자체가 표기법이다.
**우리 적합도:** ❌ **전면 도입 불가 — 실사 A와 정면 충돌.** 스텝 애니·스미어를 실사 메시에 얹으면 "고장"으로 읽힌다(스타일 아니라 버그로 보임). 메모리의 미판정 트레이드오프("실사 A 유지 시 진짜 프레임 조작 영구 포기")가 이 조사로 **확정에 근접** — 프레임 계보는 아트 전환 없이는 못 탄다. 단 아래 계보 2의 *순간 이벤트형* 차용은 별개.

## 계보 2 — 화면 이벤트 (포스트 프로세스·1~3프레임 연출)

- **임팩트 프레임**: 1~3프레임 고대비(흑백 반전 등) 삽입 — 타격 순간의 시각적 충격("뇌가 무게를 등록"). JJK가 대중화한 어휘. **아트 스타일 무관** — 상시 룩이 아니라 순간 이벤트라 실사 위에도 얹힌다.
- **스피드 라인**: 애니/만화식 방사 선. Unity 절차 구현 오픈소스 존재(MirzaBeig **Anime-Speed-Lines** — 포스트 프로세스 비네트 라인). 라디얼 블러도 URP 구현 레퍼 확보(UWA).
- **라디얼 블러/모션 블러**: 실사 계열의 표준 속도 어휘. 카메라 급접근·고속 이동 순간에만.

**우리 적합도:** ✅ **최유력 — 이미 부품을 갖고 있다.** ①처리 스냅샷(임팩트 프레임) v1 **기보유**(재가동 후보로 이미 대기열 ②에 올라 있음) ②대시 속도선+잔상 기보유 ③세계관 스킨 가능 — 흑백 반전 대신 **"처리 문법" 스킨(종결 도장·신호 붕괴 마젠타 캐넌)**. 규율: 티어드 주스 — 매 킬 발화는 노이즈, 엘리트/피니셔/멀티킬에만. 어휘 동결 전 **팔레트 캡처 게이트 필수**(Challenger 조건 유효).

## 계보 3 — UI가 속도를 연기 (Persona 5)

턴제인데 빠르게 *느껴지는* 이유 = UI 모션 그래픽이 속도를 공급: 고속으로 흐르는 숫자/텍스트, 공격적 사선 컴포지션, 시선 유도선(메뉴 열릴 때 중앙 흰 선), 리듬 통제된 애니메이션. Atlus 아트디렉터(스토 마사요시) 강연 소스 존재.

**우리 적합도:** ✅ **채택된 처리 문법(Clock-Out Latency)의 본진.** "티켓 CLOSED·라우팅 라인·시프트 로그"가 정확히 이 계보다 — 속도 = 접수→종결 확인의 빠르기를 **UI 케이던스**로 보여준다. K-행정 레지스터(공문서 세계관)와 서사 정합까지 공짜. 킬 케이던스 UI는 다음 조각(템포 루프)의 판정 대상.

## 계보 4 — 잔상/이동 증거 (HLD·Ruiner)

**Hyper Light Drifter**: 대시마다 잔상 — "메카닉을 본질적으로 즐겁게" 만드는 속도 증거. **Ruiner**(우리 룩/카메라 북극성): 대시 최우선 설계 + 고밀도 VFX·스트로브·크로마틱 어베레이션 — *시각 강도*로 속도를 말함. 단 리뷰들이 "시각 노이즈가 난이도가 됨"도 지적 — 가독성 비용 경고.

**우리 적합도:** ✅ **이미 시공됨**(대시 잔상 → Slice 0에서 스킬 런지로 확장). 확장 여지: 연속 처형 시 잔상/속도선 강도 스케일(단 가독성이 보스 — Ruiner의 노이즈 경고 새길 것).

## 계보 5 — 카메라/시간 조작 (실사 계열 + ZZZ)

**God of War**(PS 블로그 개발자 인터뷰): 실사 계열은 프레임을 안 건드리고 **카메라와 시간**을 조작 — 타이트 카메라 워크·셰이크·러ンブル·기하학적 혈흔·(추적용) 풀백 카메라. **ZZZ 체인 어택**: 스턴된 적에게 **컷인 카메라 전환**으로 스타일리시 모션 — 유저 발안(엘리트 카메라 전환)의 실전 검증 사례.

**우리 적합도:** ⚠️ **조건부 — 부품은 있고 위험도 있다.** HitStop 전역·카메라 투스테이트·줌펀치(스킬전용) 기보유 = 이 계보를 이미 절반 타고 있음. 엘리트 카메라 전환은 **쿼터뷰에서 컷=가독성 단절**이 리스크(호드 한복판 컷인은 피격 정보 상실) — ZZZ는 근접 3D 카메라라 조건이 다름. 후보 축소: **엘리트 '처형 확정' 순간 한정**(무적 처리 or 전장 정리 후) 짧은 펀치인/컷인 → 유저 판정 비트.

---

## 종합 — "우리 것에서 활용할 것" 우선순위 (제안)

| 순위 | 기법 | 근거 | 발동 규율 |
|---|---|---|---|
| 1 | **임팩트 프레임 재가동**(처리 스냅샷 v1 + 처리 문법 스킨) | 부품 기보유·아트 충돌 없음·간지 직격 | 엘리트/피니셔/멀티킬만(티어드) |
| 2 | **킬 케이던스 UI**(P5 계보 → 티켓 CLOSED) | 채택 기획의 본진·서사 정합 | 다음 조각(템포 루프)에서 검증 |
| 3 | **스피드 라인 국소 발화**(MirzaBeig 계열 포스트) | 오픈소스·저비용·순간 이벤트라 실사 안전 | 런지/대시/처형 순간만, 상시 금지 |
| 4 | **엘리트 카메라**(유저 픽, ZZZ 검증) | 간지 헤드라인·단 가독성 리스크 | 처형 확정 순간 한정, 판정 비트 |
| ❌ | 스텝 애니·스미어(프레임 계보) | 실사 A와 충돌 — 셀 커밋 전제 | 아트 대전환 없인 봉인 |

**판정 포인트(유저):** ①위 우선순위 동의 여부 ②임팩트 프레임의 스킨 — 정통 흑백 반전 vs 처리 문법(마젠타/신호) — 팔레트 캡처 게이트에서 A/B ③엘리트 카메라의 발동 조건 수위.

## 출처

- [GG Xrd GDC 2015 (GDC Vault)](https://www.gdcvault.com/play/1022031/GuiltyGearXrd-s-Art-Style-The) · [ArcSys 공지](https://www.arcsystemworks.com/guilty-gear-xrds-art-style-the-x-factor-between-2d-and-3d-talk-from-gdc-2015-is-now-available-online/)
- [Hi-Fi Rush 툰 렌더링 GDC 2024](https://gdcvault.com/play/1034330/3D-Toon-Rendering-in-Hi) · [음악 동기 애니(Game Anim)](https://www.gameanim.com/2023/09/08/hi-fi-rush-music-synced-animation/) · [Wikipedia](https://en.wikipedia.org/wiki/Hi-Fi_Rush)
- [Mullet MadJack 아트 분석 (Creative Bloq)](https://www.creativebloq.com/3d/video-game-design/why-mullet-madjacks-retro-vhs-era-anime-art-style-still-stands-out)
- [P5 UI 개발 패널 (Persona Central)](https://personacentral.com/persona-5-panel-concept-development-ui/) · [P5 UI 분석 (Medium/Mark Tan)](https://medium.com/@marktan_98815/persona-5-a-masterclass-in-ui-design-6e0470d2020f)
- [임팩트/스미어 프레임 정의 (Wikipedia)](https://en.wikipedia.org/wiki/Smear_frame) · [ArhFoundation 임팩트 프레임](https://www.arhfoundation.org/impact-frames-meaning)
- [MirzaBeig Anime-Speed-Lines (GitHub, Unity)](https://github.com/MirzaBeig/Anime-Speed-Lines) · [라디얼 블러 Unity 구현 (UWA)](https://blog.en.uwa4d.com/2022/09/22/screen-post-processing-effects-radial-blur-and-its-implementation-in-unity/)
- [HLD 리뷰 — 대시 잔상 (GameSpot)](https://www.gamespot.com/reviews/hyper-light-drifter-review/1900-6416400/) · [Ruiner 리뷰 (PC Gamer)](https://www.pcgamer.com/ruiner-review/) · [Ruiner (Gamecritics)](https://gamecritics.com/brad-gallaway/ruiner-review/)
- [GoW 전투 개발자 인터뷰 (PS Blog)](https://blog.playstation.com/2022/10/04/game-developers-explain-what-makes-god-of-war-2018s-combat-tick/) · [ZZZ 체인 어택 (Fandom)](https://zenless-zone-zero.fandom.com/wiki/Chain_Attack)

**미검증 표기:** Hi-Fi Rush의 스미어/임팩트 프레임 구체 기법(소스 미확보 — GDC 영상 직접 확인 필요) · Sekiro 세부(이번 소스에 없음). 단정 안 함.
