# 2026-06-13 몬스터 톤게이트 캡처 세션

> **목적**: Protofactor 리얼 크리처 + Vefects 플립북 투사체 + 로우폴리 도시(Synty) + 주인공(SM_Casual_Male) **4자 동거**가 그래픽 베이스라인 v1 라이팅(Frozen Golden Hour, COZY, 포스트 스택)에서 한 화면에 서는가 — 유저 눈 판정용.
> **무대**: `Assets/_Project/Scenes/ToneGateLab.unity` (ScanLit_v2 라이팅 스택 SaveAs + Greybox_City CityMap 이식. 원본 두 씬 디스크 무손상). 사거리 (-5, 115) 도심 협곡.
> **카메라**: 게임플레이 확정 카메라 복제(45°/15m/FOV50) = 실플레이 시점 판정. `_closeup` 접미사만 근접 보조컷.
> **검증 루프**: 인씬 JudgeCam → `RenderPipeline.SubmitRenderRequest` → PNG 디스크 (MCP Camera_Capture 죽은 프레임 함정 우회).

## ★세션 중 발견한 함정 (기록)

1. **Vefects 3팩 = URP 비호환** ("임포트됨 ≠ 사용 가능" 신규 사례, Protofactor 마젠타의 재연).
   - Combat Flipbook(×122)·Pixel Craft(×119) = BIRP 셰이더 → 전부 마젠타.
   - Flipbook 팩의 "SH_Vefects_Unlit_Flipbook_URP"조차 Unity 6 URP에서 마젠타 (구버전 URP용. ShaderHasError=False는 거짓 신호 — SRP 호환을 안 봄).
   - 투사체 프리팹 루트 PS가 빌트인 `Particles/Standard Unlit` 기본 머티리얼 = 화면 절반 마젠타 쿼드.
   - **금일 우회**: 캡처 인스턴스 한정 `URP/Particles/Unlit` 가산 변환(텍스처 자동 매핑 + 커스텀 버텍스 스트림 제거). 팩 원본 무수정. **영구 변환 유틸은 후속 작업** (에로전/디스토션 등 고급 기능 손실분은 캡처에 반영됨 — 실제 채택 시 셰이더 포팅 판단 필요).
2. 베이스라인 라이팅 이식 시 COZY 시간 EditorPrefs(0.743=17:50, 태양 지평선 아래)가 씬 저장값을 덮음 → **t=0.625(15:00 Frozen Golden Hour)로 재고정** 필수.
3. 병렬 세션과 에디터 공유 — 씬이 중간에 바뀔 수 있음. 매 커맨드 씬 가드(활성 씬 확인→재오픈) 필요.

## 캡처 목록 (판정 항목 매핑)

| 파일 | 내용 | 판정 항목 |
|---|---|---|
| 01_LV12_pack_acidfire | LV1 Venodonte×3(틴트)+Funglicane, LV2 Caniathrox + 산성 투사체 비행/임팩트 | 톤게이트 본판정 + 투사체 |
| 02_LV3_predators | Dimaxillosaurus(변이 앵커①)+Venosaur | 톤게이트 |
| 03_LV34_limadon | Limadon ↔ Venosaur(LV3)·Serpenopod(LV4) 동반 스케일 비교 | Limadon LV3↔4 |
| 04_LV4_elites | Fulgurodonte(앵커②)+Crassorrid(앵커③) | 톤게이트 |
| 05_LV45_serpmare | Serpmare ↔ Fulgurodonte(LV4)·Crustaspikan(LV5) 비교 | Serpmare LV4↔5 |
| 06_LV5_event | Crustaspikan 단독 — "사건" 스케일 | 톤게이트 |
| 07_funglicane_nova | Funglicane + 전기 블래스트 + 사망 노바 모사(Electric burst) | 노바 채택 |
| 08_cone_full_color / 09_cone_desat20 | 동일 구도(LV3 캐스트), 전체 프레임 채도 -20 근사 (실구현은 콘 밖 개체만 적용) | 콘 밖 위협 표시 |
| 10a_desat_zomboid45 / 10b_desat_mid15 / 10c_desat_vivid | §5 베이스 탈색 슬라이더: sat -45 / -15 / +8 (기준값 0) | 셰이더 방향 미적 콜① |
| 11_closeup_anchors | 인간 변이 앵커 3종(Dimaxillosaurus·Fulgurodonte·Crassorrid) 근접 | 톤게이트(머티리얼 디테일) |
| 12_closeup_oneonone | 한 마리 대면(Caniathrox)+산성탄 근접 | 위협 본게이트 보조 |
| _test_01 / _test_02 | Vefects 마젠타 증거 → 변환 후 비교 (함정 기록용) | — |

전기 틴트는 HDR(1.2,1.9,2.6)로 블룸 태움. 산성=녹(0.55,1,0.35). 인스턴스 변환이라 에로전·디스토션 등 Vefects 고급 질감은 빠진 상태 — 톤(플립북이 PBR 월드에 앉는가)만 판정 대상.
