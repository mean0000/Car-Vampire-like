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
- [★하데스 Boon 실측 + 엠버="하데스 원본" 명제 판정 + 단일무기 락인 대안](reference_hades_boon_vs_vs_drafting.md) — 2026-06-17. ★엠버=하데스 직접원본 명제=부분수정(인과 과장: 하데스엔 "계열 락인" 없음, 엠버 락인은 DMD/Brotato쪽). 하데스서 우리에 직접이식=레어도(숫자축)+듀오(G3시너지) 둘뿐(이미 박힘). 단일무기 락인 대안=20MTD 트리(첫노드→관련2개 출현→궁극) / DMD 신3게이트. "누를때만"=Boon과 충돌0(완벽콤보 보상). 기존 무기권위(6무기/엘/키스톤/퇴근)=피벗으로 폐기, 카드문법+카타나2트리만 생존
- [★카드-내용 설계 문법](reference_card_content_grammar.md) — 레퍼가 *카드 1장 내용을* 짜는 법(구조 아님). "+5% 함정" 답=6동사클래스. PoE키스톤(규칙변경+대가 실측4)·Hades Aspect Guan Yu(동사변경)·Hades Duo/Gemini(시너지활성)·VS 진화(형태변태)·20MTD/DMD 스코프. 두 altitude 분리(PoE=메타트리, 노드정체성만 훔침)
- [★검 액션 뽕 엔진 6종](reference_sword_action_pong_engines.md) — 검 뽕=*연쇄 가속* 축. GoT 스탠드오프 스트릭(3연 즉살 체인)·MGR 잔타츠(직접 절단선+킬=재충전, 우리 추출코어 동형)·세키로 체간 처결·로닌 카운터스파크·Katana Zero 플로우클리어·무쌍 격노(게이지→난동→화면와이프). 점/면보다 연쇄가 본진
- [★VS 보호/영토/패링 선례](reference_vs_protect_territory_parry.md) — 2026-06-16 웹리서치. ★VS코어=카이팅(이동만이 방어)→농성·영토 정지점거는 정면충돌. 선례: DRG:S 에스코트모드(캡처존+호드, 호불호·간헐모드면 작동)/Death Must Die 닷지롤(포지셔닝→반응게임 전환=패링 정당근거, 단 상시회피)/Rock&Road(VS+타워디펜스=과설계신호). 결론=보호/영토는 간헐이벤트면OK·상시척추면 위험
- [★급습+호드+성장 결합 추적](reference_ambush_horde_growth.md) — 2026-06-15 웹리서치. ★결합=드묾(빈공간,함정아님). 3문제 답: 급습이유=Dishonored 아드레날린(미발각→전투강화, "no trouble"넘어)+백스탭배수 3~30x(과감해야 작동)/스타일쿠션=Desperados유한증원(발각도 이김)vs Intravenous2·Dishonored1 반례(한쪽만보상)/호드리듬=L4D Peak Fade(스폰0+stress decay)+DRG:S 어그로캡32+예산제+RoR2크레딧. 차용Top5

- [★단순입력→파워판타지 메커니즘 실측](reference_simple_input_power_fantasy.md) — 2026-06-25 핀 사냥. 관전자함정 해법=자동깔되 결정에 보상(점수=돈)더. DMC오토어시스트(×0.8수동보상)·Hi-Fi Rush(자동싱크+타이밍뎀)·★Warframe Slash Dash("선긋기" 실존:방향입력→사거리자동연쇄+모멘텀)·Katana Zero Dragon·Prototype(3D수직의존=탑다운갭). 우리 화려함=돈이 이미 절반
- [★최근 distinctive 액션로그라이트 실측](reference_distinctive_action_roguelikes.md) — 2026-06-25 Explore조사. 탑다운서 "잘해보임" 성공/실패 사례 + 진부함경계(신선함=실행이지 피치아님)
- [★★근접호드 억제·배려·처치경제·액션감 카탈로그](reference_horde_suppression_care_economy.md) — 2026-07-04 웹리서치. ★진단=유저4불만은 "1뿌리(타격에 stagger상태 없음→넉백이 시간을 안만듦)+1처리량(클리브 없음)". ★레인=배려는 방어(회피/패링) 아니라 *공격적 어포던스*(클리브·관통·마그네티즘·처형)로. Klein "회피의존=겁쟁이설계". VT2 스태거(받는뎀+20%·mass−25%)+클리브(관통=cleave÷mass)+푸시. Hades벽꽝. DeadCells 관통롤+dodge offset. Sifu 동시공격스로틀. Doom/GoW 상태게이트처형. 이식후보5=①스태거상태②클리브③셔브④관통대시⑤처형. 묶음=①+②먼저
- [★평타 반복 재미장치 (타격감·배려) 카탈로그](reference_combat_fun_devices.md) — 2026-07-04 웹리서치. ★기존 빈칸=에스컬레이션·맥락변주·프레이즈릴리스(v0/억제는 per-hit+방어배려만). 가변히트스탑(SF6 무게비례·flat→계단화가 최저비용 고레버)·Smash 프리즈진동·Musou 스펙터클/정리감·HLD 메트로놈·ULTRAKILL 스타일→회복. Top5=①맥락감응(히트스탑3f→8f)②콤보3타 라이징③Heat+청소릴리스(무UI)④자동리타겟(회전only=전진프리즈통과)⑤처형피니셔. ★재정합=v0 전진런지/0.3~0.5m 붙임=07-04 평타전진금지와 충돌→회전/아크확장만 생존

## 핀 / 코어 동사
- [★코어 핀 사냥 (파워판타지 vs dread 갈림길)](project_pin_point_hunt.md) — 2026-06-25. ★진짜충돌=메커니즘아니라 *감정목표*(Prototype 파워판타지 vs 06-24 dread vs §0.1 한마리쫄깃). 읽고-베기 거취=제거❌/관대화+톤전환하면 핀엔진으로 생존(메커니즘 양립, 충돌점=관대함·톤). 핀후보=오케"선을고른다"(Slash Dash선례). 유저 톤판정이 1번

## 판정 기준
- [★뽕 우선 (경제분류/farm 스프레드시트 ❌)](feedback_pong_over_systematization.md) — 무기/성장 트리 단일 판정=뽕(시각 파워판타지 러시). §17.10 친화=경제루트류 systematization이 뽕 죽임. 트리 운전대=뽕, farm분류=부차. 극단=더 큰 뽕 에스컬레이트
