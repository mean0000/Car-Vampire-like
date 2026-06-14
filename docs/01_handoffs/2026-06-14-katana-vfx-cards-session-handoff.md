# 핸드오프: 2026-06-14 — 카타나 카드/색팔레트/슬래시 VFX 세션

> **방대한 세션.** 상세 결정은 전부 *메모리*에 박혀있다(아래 §2 포인터). 이 문서 = *지금 상태 + 다음 액션 + 열린 실타래*. `/clear` 후 이 문서 + 메모리로 재개.

---

## 0. ★지금 상태 + 바로 다음 액션 (재개 1순위)

**카타나 슬래시 VFX v2를 막 재시공함 (레퍼 기반 교정). 유저 플레이 스크린샷 판정 대기 중.**

- v1 슬래시 = 인게임 형편없음(유저 격노) → 진단: ①바닥에 깔림 ②정적 ③잡탕(SDF채움+마젠타).
- v2 교정(`Assets/_Project/Scripts/SlashVfxFX.cs` *시각만* 수정, 컴파일 클린):
  - 평면 **42° 세움**(가슴 높이 1.1) — 바닥 스티커 폐기
  - 스윕 = Vefects 머티리얼 자체 UV 스크롤(원래 있었는데 바닥에 눕혀 안 보였던 것)
  - 핫화이트 코어 + 시안 엣지, **평타 마젠타·SDF 채움 부채꼴 제거**
- ★**검증 = 유저 플레이 스크린샷** (edit-mode 정적은 UV 안 움직여 못 봄 → 플레이모드 필수).

**▶ 재개 액션:**
1. 유저: `Greybox_CombatLab` Play → 키1(거합)·키2(참격) 공격 → **슬래시 스크린샷**
2. 오케: 스크린샷 *보고* 조정. 1순위 노브 = `PlaneTiltDeg=42`(평면각, 0=빌보드/90=바닥), `SlashHeight=1.1`. (전부 SlashVfxFX.cs 상단 static)
3. ★**안 보고 "완성" 절대 금지** — 이번 세션 최대 교훈(아래 §4).

---

## 1. ★이번 세션 핵심 교훈 (재개 시 반드시 준수)
- **그래픽/VFX는 캡처 루프** — 안 보고 "완성" 선언 = 이번 세션 2번 사고. 유저 스크린샷이 가장 확실한 검증.
- **불가능은 불가능하다고 말한다**(유저 지시) — 예: 모든 VFX GIF 생성 불가, VFX 퀄 캡처 없이 판정 불가.
- **자산 사기 전 보유분 확인** — 슬래시 팩 추천이 성급했음(유저 라이브러리에 차고 넘침). 문제는 *통합*이지 자산 아님.
- **레퍼 추적 의무** — 임의설계로 슬래시 만들다 망함. 레퍼(Hyper Light Drifter 선평면·realtimevfx 메시스윕) 위에서 재설계.

---

## 2. 이번 세션 닫힌 결정 (메모리 포인터 — 상세는 거기)
- **카타나 65장 카드 카탈로그** = `[[project_2026_06_14_card_system_decided]]` + 권위 `docs/03_reference/2026-06-14-katana-card-catalog.md` + HTML 갤러리. 거합/참격 2트리, 잎4(벽력일섬/반격베기/플리커/검기)+변형36+신화2.
- **카드 시스템** = 등급 3종 하이브리드(①숫자롤 C/R/E ②전설잎+변형 ③신화 와일드카드). 3택+prereq 자격풀, 리롤=메타자원. 신화풀 gd 드래프트됨.
- **색 팔레트 v1** = `[[project_2026_06_14_color_palette]]` + 보드 `docs/00_authority/2026-06-14-color-palette-board.html`. 나=시안(액션)/금(보상)/마젠타(비용), 적=녹청백황+레드오렌지, 세계=채도억제 골든아워, ★카드등급=표준(흰/파/보라/주황/진홍빨강), ★UI=Cyberpunk2077 퀵핵 스킨.
- **컨트롤(린5)** = `[[project_2026_06_14_katana_controls_session]]`. LMB평타·RMB특수·Space회피(완벽회피=패링흡수)·Tab보조무기·E궁극·F상호작용·1/2스왑.
- **캐넌**: 플레이어 self-cancel(`[[feedback_player_self_cancel_canon]]`)·무기마다 고수/뉴비(`[[feedback_weapon_difficulty_mix]]`)·공격가독=VFX먼저 애니나중(`[[feedback_attack_readability_vfx_first]]`).
- **증명슬라이스 Phase1** = `[[project_2026_06_14_proof_slice_phase1]]`. KatanaController(거합/참격 base, CombatLab 키1/2). Stab+수정 완료.

## 3. VFX 자산 현황 (전부 보유, 구매 불필요)
- **Vefects 1263개**(슬래시·플립북·AoE·아니메·Combat Hit) — 임포트됨. 슬래시 = Stylized Shuriken `Slashes Piercing`(Generic=시안 리컬러).
- **Piloto Studio = Ultimate Loot VFX 199개**(보상/돈 이펙트) — 임포트됨.
- **GabrielAguiarProductions = VFX Graph Mega Pack Vol.4** — ⚠️*껍데기*. `VFXGraph_MegaPackVol4_...URP_v1.3.unitypackage` 더블클릭 임포트해야 이펙트 나옴.
- **Feel** = 주스(히트스탑/쉐이크). **ExplosiveLLC RPG Mecanim** = 검 애니(나중 무게 레이어). **Jorjouto/ACS** = 애니 컴포저(VFX↔애니 프레임 싱크 도구, 보유).
- 슬래시 형상 카탈로그 HTML = `docs/captures/2026-06-14-vfx-slash-catalog/`. 데모 Overview 씬 = `Vefects/Stylized VFX URP/.../Demo Overview Shuriken.unity` 등(Play하면 다 보임).
- ★주스 스택 = 슬래시(보유) + 임팩트(Vefects/KillBurst 보유) + 히트스탑/쉐이크(Feel 보유). "고퀄=한 이펙트 아니라 레이어 스택".

## 4. 열린 실타래 (다음 세션들)
- **슬래시 VFX v2 판정**(즉시) → 통과하면 발도/참격파도 같은 교정 + 주스 스택(임팩트+히트스탑) 얹기.
- **카타나 §17.1 4판정**(거합 차지 재활용/발밑텔/1.2s/참격 딜레마) — 미응답.
- **카드 시스템 Q-F**(신화 빈도/캡 노브)·잎 변형 per-잎 작성·이름 Story 렉시콘 동결.
- **거합≠참격 손맛 게이트**(설명없이 자발구별) — 플레이 판정 미완.
- **애니 레이어**(나중): RPG Mecanim 검 애니 → ACS로 VFX 프레임 싱크.
- 색 팔레트 동결 시 → `shader-direction.md §3` 갱신.
- ⚠️Missing-prefab 에러 40개 = 도시 씬 깨진 환경 참조(비치명, 우리 코드 무관). 정리하려면 어느 씬/팩인지 확인.

## 5. 주의 (함정)
- Max5 다운그레이드 → 비용 규율(CLAUDE.md): 기계타이핑=Sonnet-Gameplay, 작업단위 /clear.
- 병렬 세션 = 에디터 1세션 전담(다른 세션이 점유 시 정적 검증만).
- SlashVfxFX 수정 시 PlayFan/Pierce/Wave 시그니처·KatanaController 배선 깨지 말 것(Stab 통과분 유지).
