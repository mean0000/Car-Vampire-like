# GameDesign Agent Memory Index

## 제안 / 판정
- [무기·성장 제안 문서 (판정 대기)](project_weapon_progression_proposal.md) — 2026-06-13~14 무기체계+성장3층. ★★v0.9 드론=엘 분리(엘=서사만/드론=별도 자율무기 7노드 트리·AFK가드4겹)+§17.10 6무기 지배친화=런경제루트(Codex테스트). v0.8 6무기 트리·§16 v2 카드문법. v0.6 두 기둥. Q18~Q42. ★Q20(드론싱크)+Q40(드론컨트롤성립)+Q42(친화=경제루트) 최중요. docs/03_reference/2026-06-13-weapon-progression-proposal.md
- [★카드-내용 문법 + 6무기 트리](project_card_grammar.md) — §16 v2(2층·G0 연료복권·G3 심장 SoD Exodia·가지별 엔진) → §17 6무기 전개(12엔진) → ★★v0.9 드론=엘 분리(AFK가드4겹·드론=유일 컨트롤)·§17.10 지배친화(Codex 테스트). ★★카타나 §17.1.D 잎 전면교체(06-14): 구 납도/연속베기/격노/무한콤보 폐기→거합{벽력일섬 기동발도·반격베기=방어궁 E 패링}/참격{플리커 블링크자동·검기발사=중거리8~10m면}. 고수↔뉴비 비대칭·lv 공간렌즈+인카운터 분리 박음. 미결=검기 디제틱(비인가출력)·반격베기 방어궁 재정합·저격 분리. 척추 42·≈53~58장. Q28~Q42

- [★★카타나 전체 카드 카탈로그 구현 스펙 (65장)](project_katana_card_catalog.md) — 2026-06-14 증명 슬라이스용. 척추7+시너지6+연료9+잎변형36(4잎×[변형3+재분기6])+신화2. 등급 롤 위치 엄수=①숫자 시너지·연료 15장만/②결정론 42장(잎·변형·재분기)/③신화 2. Gameplay가 ScriptableObject 구현. docs/03_reference/2026-06-14-katana-card-catalog.md
- [★독자 카타나 안 v1 (오케 합치 대기)](project_katana_cascade_design.md) — 2026-06-14 유저 "우리 팀만의 카타나? 뽕 우선". ★테제="카타나 뽕=연쇄 가속(escalating chain)"→점/면 대신 **일태도(一太刀) 단일 코어**: 거합 게이지→처형→게이지 환류→가속. 분기=게이지를 어떻게 *터뜨리나*(연(連)=체인 폭주 / 살(殺)=잔타츠 환류 / 류(流)=플로우 멈춤해제). 레퍼=GoT 스탠드오프 스트릭+MGR 잔타츠+Katana Zero+무쌍격노. 세계관=수입 절단구
- [★★카드 시스템 전체 (등급=3종 하이브리드 확정·신화풀)](project_card_system_research.md) — 2026-06-14 v2.0. ★★유저가 "등급 NO" 기각→**3종 하이브리드 확정**: ①숫자등급(인스턴스 파워·Hades)·②전설 잎+변형(결정론 도달→변형갈래→재분기·PoE키스톤)·③신화 와일드카드(새 명사/동사·가끔RNG·화려함=돈 가중·잭팟). reframe="등급 어디 사느냐"·충돌0·스택. v1.0 틀린전제 폐기(SoD 실제 에센스등급·Balatro 3직교축). ★§6 신화풀: M-1[전개식 모노필라멘트](유저시드·기동빌드·🔴)·M-2[잔영도살]·M-3[압류집행](디제틱·🟢)+풀게임 5후보. 남은골격=Q-B(개수)·Q-C(리롤)·★Q-F(신화 빈도/캡/곡선 노브). docs/03_reference/2026-06-14-card-system-research.md

## 재정합 결론
- [무기·성장 권위 재정합](project_weapon_progression_reconcile.md) — progression🟥폐기/cards🟡재배선/demo🟥/E-002🟢척추. ★런 구조 대전환: 싱크=마스터시계 폐기(위협 다이얼)·"싱크가 화력/드론 대가" 무효→카드 −스탯(Brotato). 자원축 소음·시계→킬·기동·화력·정밀(코드 enum 재정합)

## 레퍼런스
- [로그라이트 시스템 레퍼](reference_roguelike_systems.md) — 클래식4(SoD 샤시엔진/Hades II 3층/Brotato 병합/Backpack) + 최신작6(Windblown 스왑어택/HLB SyCom/Gunfire Gemini/Roboquest 잠재력해금/DMD Sign/ZERO Sievert 추출현금)
- [메타 경제 레퍼 8종](reference_meta_economy.md) — ★런 밖 재화 사용처(유저 "가장 빈 칸"). HLB 2층추출(영구/임시=stake)·Hades II Grasp+골드환전·Dead Cells 옵션not스탯·Hunt 바운티(회피-최적 해소)·Tarkov 은신처(프레임만)·ZERO Sievert·StS 승천·Rogue Legacy 몰수
- [★카드-내용 설계 문법](reference_card_content_grammar.md) — 레퍼가 *카드 1장 내용을* 짜는 법(구조 아님). "+5% 함정" 답=6동사클래스. PoE키스톤(규칙변경+대가 실측4)·Hades Aspect Guan Yu(동사변경)·Hades Duo/Gemini(시너지활성)·VS 진화(형태변태)·20MTD/DMD 스코프. 두 altitude 분리(PoE=메타트리, 노드정체성만 훔침)
- [★검 액션 뽕 엔진 6종](reference_sword_action_pong_engines.md) — 검 뽕=*연쇄 가속* 축. GoT 스탠드오프 스트릭(3연 즉살 체인)·MGR 잔타츠(직접 절단선+킬=재충전, 우리 추출코어 동형)·세키로 체간 처결·로닌 카운터스파크·Katana Zero 플로우클리어·무쌍 격노(게이지→난동→화면와이프). 점/면보다 연쇄가 본진
- [★급습+호드+성장 결합 추적](reference_ambush_horde_growth.md) — 2026-06-15 웹리서치. ★결합=드묾(빈공간,함정아님). 3문제 답: 급습이유=Dishonored 아드레날린(미발각→전투강화, "no trouble"넘어)+백스탭배수 3~30x(과감해야 작동)/스타일쿠션=Desperados유한증원(발각도 이김)vs Intravenous2·Dishonored1 반례(한쪽만보상)/호드리듬=L4D Peak Fade(스폰0+stress decay)+DRG:S 어그로캡32+예산제+RoR2크레딧. 차용Top5

## 판정 기준
- [★뽕 우선 (경제분류/farm 스프레드시트 ❌)](feedback_pong_over_systematization.md) — 무기/성장 트리 단일 판정=뽕(시각 파워판타지 러시). §17.10 친화=경제루트류 systematization이 뽕 죽임. 트리 운전대=뽕, farm분류=부차. 극단=더 큰 뽕 에스컬레이트
