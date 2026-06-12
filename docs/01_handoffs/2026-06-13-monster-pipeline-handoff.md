# 핸드오프: 2026-06-13 — 몬스터 파이프라인 (분류·공격 문법·FX 조달)

> **세션**: 몬스터 피벗 실행 세션 (06-12 밤 ~ 06-13). 유저 리부트 선언 "좀비가 아닌 몬스터, **사람이 몬스터가 되는 것**으로 변경" → Protofactor 로스터 전수 실측·분류·레벨 지정 → 탑뷰 공격 문법 v1.1 → FX 조달 확정까지.
> **상위 연계**: [[2026-06-13-shader-direction]](아트 대전환 — ithappy 은퇴, 적=Protofactor 전계층 동결) · 나침반 §4.2 "위협적 이상개체를 빠르고 화려하게 처리하고 살아서 퇴근" · [[2026-06-12-threat-pass-session-briefing]](위협 테제)

## 1. 동결/승인된 것

| 항목 | 내용 | 권위 |
|---|---|---|
| **로스터 분류·레벨** | Protofactor Vol.2 30종 → LV1 군체4 / LV2 추격자5 / LV3 포식자8 / LV4 정예7 / LV5 거물6. LV=인카운터 위협 등급(처리 감각으로 정의). 실측 3축(바운즈 0.7~13.9m·애니 동사 전수·인씬 렌더) | [[2026-06-12-monster-roster-classification]] |
| **탑뷰 공격 문법 v1.1** | 제1 분기 = **곡사→장판 / 직사→탄막**(탄 자체가 위협). 위협 형식 9종(접촉/돌진/스윙/곡사/직사탄막/빔/잔류/소환/그랩) + 장판 도형 6종(모양=영역·채움=타이밍) + 탄막 어휘 4종(조준탄=정지사격 처벌·부채탄·링탄·저속유도, 나선·벽탄 금지) + 밀도 규율(직사 사수≤4·장판≤2·빔≤1, 공격 토큰). 30종 전 기술 번역표 완비 | [[2026-06-13-topdown-attack-grammar]] |
| **공정성 캐넌 확장** | "본체는 정보, 위협은 공정" — 발사된 모든 위협(탄·장판·예고선·잔류)은 시야 콘 게이트 면제 | 〃 §2 |
| **색 채널** | 적 위협 = 적색(레드-오렌지) 단색 신규. 기존 보호: 시안=수렴/스캔, 앰버=수신자 경보, 마젠타=신호붕괴 — [[2026-06-13-shader-direction]] 색규약과 정합 확인 필요(후속) | 〃 §5 |
| **FX 조달 (유저 승인)** | **장판=자작 절차 셰이더**(ThreatArc 확장 — `_Progress` 채움·침식 기보유, 원/레인/부채꼴/링 SDF 추가) · **투사체·임팩트·가스=Vefects 3팩 재활용**(Pixel Craft Projectile 5속성+Impact/Flipbook Smoke·Poison·Electric·Charge) | 〃 §5 |

## 2. 산출물 경로

- 분류 권위: `docs/00_authority/2026-06-12-monster-roster-classification.md`
- 공격 문법 권위: `docs/00_authority/2026-06-13-topdown-attack-grammar.md` (v1.1)
- HTML 뷰어 2종: `docs/03_reference/2026-06-13-monster-roster.html`(30종 카드) · `2026-06-13-attack-grammar.html`(장판 채움+탄막 4패턴 CSS 애니 데모)
- 렌더샷 31장: `docs/03_reference/assets/monster_previews/` (512px 인씬 렌더 + `_urp_scene_check.png` 스케일 검증 컷)

## 3. ★함정 기록 (재발 금지)

1. **"임포트됨 ≠ 사용 가능"** — 팩 머티리얼 51개 중 48개가 빌트인 Standard = URP 전부 마젠타였음. **URP/Lit 일괄 변환 완료**(BaseMap/Color/Normal/Metallic/Emission 매핑, 의심 0 재검증). 신규 팩 도입 시 셰이더 호환 검사 필수.
2. **몬스터 팩에 FX 0** — 모션 FBX뿐. 파티클·투사체 프리팹 없음, **투석 바위 메시도 미동봉**(환경 팩 재활용 또는 단순 메시 필요).
3. **시야 콘 합성 패스가 월드 투명 VFX를 지움**(후방 힌트 실측) — 장판 렌더 시공 시 ①콘 이후 패스 ②오버레이 캔버스(검증된 길) ③URP Decal 중 택1 예약.
4. AssetPreview 캐시는 에디터 재시작에 휘발 — 룩데브 캡처는 인씬 JudgeCam 렌더(`RenderPipeline.SubmitRenderRequest`, SRP에서 Camera.Render() 불가)가 정석.
5. 클립은 in-place/`_RM` 쌍 — 와이어링 시 [[project_animation_inplace_gotchas]] 함정 적용 대상. 킷 목록은 파일명 기반 — 종별 와이어링 단계에서 클립 실물 확인.

## 4. 다음 큐 (권장 순서)

1. **톤게이트 캡처** — 도시 블록+베이스라인 라이팅에서 몬스터 대표종+Vefects 투사체 동시 캡처 → 유저 판정. [[2026-06-13-shader-direction]]의 톤게이트 A/B 캡처와 **한 세션으로 합류 권장**(리얼 크리처+플립북 VFX+로우폴리 도시+셀셰이드 주인공 4자 동거를 한 번에).
2. **장판 셰이더 시공** — 도형 4종 SDF+채움. TA 트랙 정합 존.
3. **MonsterDef SO 데이터화** — 분류표+§6 번역표가 스키마 원천(LV/역할/킷→형식/도형/크기/타이밍/탄속). 스포너·AI 연동 토대.
4. 기존 좀비 60종(ithappy)·ZombieController 계열 거취 — [[2026-06-13-shader-direction]]이 ithappy 은퇴를 동결했으므로 실행 계획(씬 정리·코드 계승 범위) 수립 필요. MonsterController가 ZombieController를 포크할지 계승할지 판정.
5. 명명/로어 — "사람이 변한 이상개체" 코드명 체계, Story 에이전트·렉시콘 경유.

## 5. 판정 대기 (유저)

- 톤게이트 (1번 큐의 캡처로)
- 레벨 경계 2건: Limadon LV3↔4 · Serpmare LV4↔5
- Funglicane 사망 전기 노바(자폭 형식 대용 제안) 채택
- 콘 밖 위협 표시: 완전 표시 vs 채도 -20% 변조
- Serparmat 몸통 접촉 피해 · 투석 2.0s 체공 · 촉수 순차 0.3s · 잔류 3~8s · 탄속 사다리(7/10/14) — 전부 노브

## 6. 저장소 정책 (06-13 커밋 시 확정)

- **Protofactor 팩(3.3GB) = gitignore** — 거대 팩 무시 정책(Synty·Top_Down 전례) 준수. 단일 파일 최대 69.8MB라 기술적으론 가능했으나 .git 10.2GB에 3.3GB 추가는 부적절.
- **변환의 영속화 = 에디터 유틸 커밋** — 변환된 .mat은 팩과 함께 무시되므로, 검증된 변환 로직을 `Assets/_Project/Scripts/Editor/ProtofactorUrpConverter.cs`(메뉴: Tools/ZombieCrush/Convert Protofactor Materials to URP, 멱등)로 보존. **새 머신 절차: 에셋스토어 임포트 → 이 메뉴 1회 실행.** .gitignore에도 동일 절차 주석.
- 커밋 대상: .gitignore + 변환 유틸 + 분류·문법 md 2 + HTML 2 + 렌더샷 31 + 본 핸드오프. (병렬 세션의 스테이징분은 경로 한정 커밋으로 비간섭)
