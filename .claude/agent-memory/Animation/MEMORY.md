# Animation Agent Memory Index

## 플레이어 리타게팅
- [한쪽 발만 어긋나는 리타게팅 비대칭 — 근본원인·수정법](project_retarget_foot_asymmetry_fix.md) — 소스 FBX 왼발이 A-포즈서 꺾여 CreateFromThisModel이 muscle-zero로 구움→리타게팅 상수 roll. 진단=타깃에 known-good 클립+native rig 둘 다 샘플로 소스/타깃 가름. 수정=skeleton[] T-pose 결함다리를 FK모델공간 미러(naive 컴포넌트 미러 ❌, Unreal 본축 X-반사 아님). L roll 0.32→0.17(R0.10 수렴). Reflection AvatarSetupTool=하니스 즉사. NewKatana=git미추적

## ★1차 스테이지 9종 애니 판독 (구현 전 — 신체분류·틀맵·배역이견)
- [1차 스테이지 9종 애니 도메인 판독(06-14)](project_stage1_roster_anim_read.md) — 프리뷰 실측 신체분류+클립킷 인벤토리(부족 0)+틀 재활용 맵(Venosaur=클로틀 직재활용·성체Crustaspikan=브루트틀·비행2종=신규틀). ★배역이견: Fulgurodonte=직립아님(저자세 절지)·Kupolojuve=날갯짓아닌 부유해파리·Lacercharias=진짜 Roll 상태머신. 난이도: Carcinoptera>Crustaspikan성체>Kupolojuve>Fulgurodonte>Lacercharias>Venosaur

## 종별 (근접돌진=Caniathrox, 원거리=Venodonte, 클로월=Dimaxillosaurus, ★접근브루트+장판=Crassorrid, ★묵직클로월=Venosaur — 신규6종 1번)
- [Venosaur 클립 킷 — LV3 묵직 브루저, ★L/R 전진 비대칭(L2.413/R4.094m)](project_venosaur_clip_kit.md) — Dimax 클로월 직재활용. 30f/1.0s(Dimax 35와 다름!). 컨택 f12/norm0.4. 4분할 경계 9/15/21/30 재유도. ClawHit Strike norm0.5(L/R동일). 프리팹=Tint_Green(베이스). Animator 루트
- [Venosaur 상태머신 ★v2 — 벽 속도 게인↑ + 강약대비 램프 + separation↑ (유저 ▶ 3건 재튜닝)](project_telegraph_driver_venosaur.md) — Dimax v8 라우팅 직계승. ★★v2(06-14): ①게인 1.0→**1.5**(지속 7.72m/s>걷기5.5=벽! 공식 4.906×gain×0.92) ②램프 강약대비 Windup**0.70**(느린응축 텔레그래프0.433s)→Strike**2.4**(스냅0.083s=5.2배대비=위협) →Follow1.3→Recov1.7 ③separation 2.6/1.0→**3.6/1.6**(겹침박멸, heading만 휨=전진보존). L/R 1.5 동등배율=비대칭 보존. 헤드리스 통과(LRLRLRLRL·백슬라이드0·7.72m/s). ★stale-assembly함정=Katana(타세션) 컴파일에러로 어셈블리 빌드실패→컨트롤러 직접bake+sim 리터럴로 우회. 라이브 손맛=유저 ▶
- [Crassorrid 클립 킷 — LV4 7m 브루트, 전방 스매시 내려찍기](project_crassorrid_clip_kit.md) — SmashAttack_RM 1.667s/50f 전진3.514m·Y0(grounded). ★임팩트 frame20(손최저 = SmashHit norm0.333). 3분할 Windup(0.5×,팔5.28들어올림)/Strike(1.25×,내려찍기)/Recovery(1.4×). Run_RM 9.57m/s→5.0 감속. Animator 루트
- [Crassorrid 상태머신 v1 — ★ThreatArc 텔레그래프 첫 통합](project_telegraph_driver_crassorrid.md) — Idle→Roar→Approach(루트모션)→SmashWindup(장판 스폰·채움 fill1.133s)→SmashStrike(SmashHit=ForceFull 발동)→Recovery. 접근형(Caniathrox 차용). ●r3 전방원. gen가드·OnDisable Cancel. 2중리뷰 6건 반영(OnDisable ResetCombatState·InitPoolSize·엣지가드·배율가드)

- [Caniathrox 클립 킷 실측 루트모션](project_caniathrox_clip_kit.md) — "_RM"≠루트모션. Run_RM 2.46m/cyc, Jump_RM 4.67m+0.28m(진짜 도약), JumpBite_RM 0(제자리!). ★파생 JumpLunge_RM(Y bake 0)·JumpCoil(응축). Y bake=clipAnimations lockRootHeightY
- [Caniathrox 공격 v7 상태머신 — 거리분기+Coil응축+Coil중 플레이어 예측조준](project_caniathrox_attack_statemachine.md) — Bite/Coil(0.4)→Lunge(JumpLunge_RM 1.8). ★v7 Coil중 predicted 요격조준·Lunge 회전0. leadTime 0.5. speed는 에셋실측(옛 0.6/1.3 오기)
- [Venodonte 클립 킷 — 산성 사수, 사격 in-place·이동 Crawl](project_venodonte_clip_kit.md) — 3AcidShotCombo 1.333s 제자리·스러스트정점 norm 0.225/0.425/0.625=3발. CrawlForward_RM 2.940m/s. Animator가 루트에. 클론+이벤트로 발사
- [Venodonte 사수 상태머신 v1 — 예측 안 함(정지사격 처벌)](project_venodonte_attack_statemachine.md) — Idle→Reposition→Aim(Taunt압축0.47s)→Fire(이벤트3발)→Idle. globSpeed7. Caniathrox 추격과 정반대 철학. 사거리유지 약하게(군체섞임)
- [포식자 "모았다가 팍" 연출 문법](feedback_pounce_grammar.md) — 응축(느린 in-place)+발사(빠른 Y억제 돌진)+ExitTime CUT 자동연결. 위로뜨는 Y가 "개구리"의 정체
- [LabPlayer 걷기/질주 + 적 접근속도 노브](project_lab_movement_knobs.md) — walk5.5/sprint9.0(Shift홀드), approachSpeed7.0(걷기<적<질주). Run_RM 네이티브 4.094m/s 실측
- [루트모션은 Animator 스텝으로만 실측](feedback_measure_rootmotion_by_stepping.md) — 정적 커브·"_RM" 이름·옛주석 신뢰 금지. 사고 #2 재발방지
- [전이 패턴 — CUT vs 블렌드 경계](feedback_transition_patterns.md) — 블렌드=로코모션 이음새 한 곳만. 비루프 로코모션=자기루프 전이로 지속
- [다발 공격 발동 = AnimationEvent (코드 타이머 아님)](feedback_animevent_fire_timing.md) — N연사·콤보힛은 클립 모션정점에 이벤트로 박는다. time=정규화값, SendMessage는 Animator 같은 GO만. 클론 사본에 추가
- [ProjectilePool — 투사체 공유 시스템 (원거리 종 토대)](project_projectile_pool_pattern.md) — 자작 레드오렌지 발광구(URP Unlit 가산, _EmissionColor 없음→HDR _BaseColor). Vefects 비호환 회피. 스핏·부채·링·유도 재사용
- [Dimaxillosaurus 클립 킷 — 직립 클로, ★현행=클로월 + 스윙 이즈 4분할(v7)](project_dimaxillosaurus_clip_kit.md) — ★★각 단발=Windup0~9f(1.9)/Strike9~16f(1.35,ClawHit norm L0.464·R0.549)/FollowOut16~22f(2.3)/Recovery22~35f(2.5) 이즈 램프. 합=풀2.218m 손실0. ClawHit=Strike만(검증 L@0.108s·R@0.128s=절대frame12.25/12.85). (구)2분할 Swing0.733/Recov0.433·트림·콤보4.6m 보존
- [Dimax 상태머신 ★v9=AdvanceGain 1.3× 전진증폭(OnAnimatorMove) + v8 끊임없는좌우 + v7 이즈4분할 + 장판 드라이버(보존)](project_telegraph_driver_dimax.md) — ★★v9: 속도불변·*거리만* ×1.3(루트모션 증폭=유저승인 헌법확장, deltaPosition×gain은 *증폭*이지 *발명* 아님). 회전1×. applyRootMotion=true 유지(델타 채워두고 OnAnimatorMove가 자동적용 위임=이중적용0, false면 델타죽음). ★프리팹 Animator=루트 1개·드라이버도 루트 AddComponent→OnAnimatorMove 발화보장(자식함정 검증통과). 3.75→~4.9 m/s(걸어선 못빠짐). 노브=AdvanceGain. ▶발미끄럼/궤적휨 플레이확인. ★v8: Recovery→**반대손 Windup 직행(Idle 우회)**=쉼 소멸. 추적 FaceTarget을 각 클로 Windup(cocking)에 이관. ★헌법 미세개정(유저승인): 회전O=Roar/Idle/**Windup**, 회전0=Strike/FollowOut/Recovery. 전이순서 핵심=L_Recov→R_Windup(if chainR) **먼저**+→Idle(폴백) **나중**, 둘다 exit0.98. chainGap/_gapTimer/_comboEntered 제거→_windupSetup/_recovChained 엣지가드. turnSpeed=추적노브. watcher갱신 불필요(상태명/수 불변, states10/params3 MCP 디스크검증). ★유저 ▶ spam과다 못읽힘 위험 플레이확인. (v7 골격=Windup1.9/Strike1.35/Follow2.3/Recov2.5·ClawHit=Strike만·4 CUT연속·speed4개 SSOT 보존)
- [BlendTree 파라미터는 반드시 Float (Bool이면 "not float type")](feedback_blendtree_param_must_be_float.md) — 코드 컨트롤러 빌드 시 blendParameter=Float 필수. 두 번 물림(Caniathrox·Dimax 재빌드 회귀). 빌드 스크립트 AddParameter를 Float로 고치는 게 진짜 수정(인스펙터 교정은 재빌드마다 회귀)
