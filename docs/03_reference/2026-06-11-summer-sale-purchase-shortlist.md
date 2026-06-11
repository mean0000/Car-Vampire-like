# 2026 썸머 세일 구매 쇼트리스트 (최종 5개)

> 2026-06-11 판정. 세일 목록 스크린샷 14장 → 게임 적합도 TOP 10 → **"Claude가 99% 만들 수 있는 건 전부 제외"** 필터 적용 후 생존 5개.
> 원칙: **코드는 이미 보유 자산(=Claude). 데이터(애니메이션·소리·아트)와 재현 불가능한 툴체인만 산다.**

---

## ✅ 생존 5개 — 구매 권고 (우선순위순)

### 1. Zombie Animations Set — RamsterZ
- **링크**: https://assetstore.unity.com/packages/3d/animations/zombie-animations-set-220537
- **내용물(웹 검증됨)**: 총 99개 애니메이션 + 인플레이스 변형 22개. 피격 리액션 6, 걷기 12, 달리기 9, 기어오기 9, 공격 6(페어드 36), 아이들 14, 넘어짐 2, 사망 포함
- **왜 사나**: 게임필 교과서 ②"15m 부감에선 화폐=실루엣 변위" — 피격·비틀거림이 곧 타격감. 5계열 로스터의 보행 변주 재료
- **주의**: 좀비 모델 미포함(애니메이션 전용). Synty 휴머노이드 리그에 리타게팅해서 사용
- **꽂히는 볼트**: 피격사다리, 좀비 5계열 동사 차별화

### 2. Universal Sound FX — Imphenzia
- **링크**: https://assetstore.unity.com/packages/audio/sound-fx/universal-sound-fx-17256
- **내용물(웹 검증됨)**: 1만 개+ 카테고리화 WAV(16bit 44.1kHz). 무기·임팩트·UI·발소리 등 전 장르, 유니티 라벨 태깅, 클리핑 없음
- **왜 사나**: 게임필 교과서 ③"소리=타격감의 절반, 시각 쌓기 전 소리부터". 현재 빌드 무음 상태
- **꽂히는 볼트**: **B-004 연사 빈자리(히트스탑·머즐·사운드)** — 다음 1순위 볼트 직격
- **⚠️ 퍼블리셔 확인**: 세일 썸네일의 "Universal Audio Bundle"/"Universal Sound Pack"이 동명 유사품일 수 있음. **반드시 Imphenzia 확인**

### 3. Hot Reload | Edit Code Without Compiling — The Naughty Cult
- **링크**: https://assetstore.unity.com/packages/tools/utilities/hot-reload-edit-code-without-compiling-254358
- **기능**: C# 수정 시 도메인 리로드/컴파일 없이 플레이 모드 유지한 채 즉시 반영(변수 상태 보존). PlayMode/EditMode/온디바이스
- **왜 사나**: 필-볼트 프로세스 = 빌드→느껴보고→수치 조정 루프. 이 루프의 회전 속도 자체를 사는 것. 콘텐츠가 아니라 **개발 속도**
- **주의**: Asset Store판은 개인/소규모 팀 라이선스 (우리 해당)

### 4. GPU Instancer Pro + Crowd Animations 애드온 — GurBu Technologies (2개 세트)
- **본체**: https://assetstore.unity.com/packages/tools/utilities/gpu-instancer-pro-290293
- **애드온**: https://assetstore.unity.com/packages/tools/animation/gpu-instancer-pro-crowd-animations-323280
- **기능**: 컴퓨트 셰이더로 본 트랜스폼을 GPU에서 계산(프리베이크), 같은 메시·머티리얼의 애니메이션 인스턴스 수백~수천 마리를 인스턴스별 다른 클립으로 재생
- **왜 사나**: DarkSwarm 반면교사=물량 갭. 스폰 디렉터 Peak 호드 인구는 CPU 애니메이터로 못 버팀. 유일한 보더라인(VAT 기본형은 자작 가능)이지만 URP 통합·LOD·피격 플래시·사망 전환까지 양산 품질은 자작 리스크가 몇 주 단위
- **⚠️ 함정 2개**: ① Crowd 애드온은 **Pro 본체 필수**(단독 작동 X) ② 구버전 "GPU Instancer - Crowd Animations"(145114)와 혼동 금지 — **Pro용(323280)**을 살 것
- **타이밍**: 합산 최고가 항목. 호드 밀도 볼트가 큐 앞에 올 때 사도 늦지 않음 (단 세일가 vs 정가 차액 고려)

### 5. GUI Pro - Survival Clean — LAYERLAB
- **링크**: https://assetstore.unity.com/packages/2d/gui/gui-pro-survival-clean-194741
- **내용물(웹 검증됨)**: PNG 소스 1700+, 픽토그램 300 + 아이템 아이콘 90, 프리팹 429+, 데모 씬 52. **아트 전용 — 코드/애니메이션 미포함**
- **왜 사나**: "정보 레이어=상시 클린" 독트린과 톤 일치. 사무실·계약·정산 화면이 Phase 1 실작업이고 UI 아트는 솔로 개발 최대 시간 싱크
- **주의**: 와이어링은 전부 우리 몫 (그건 Claude 일이라 OK)

---

## ❌ 탈락 — Claude가 직접 짠다 (필요해지는 볼트에서)

| 에셋 | 대체 방안 | 규모 감 |
|---|---|---|
| Easy Save | strain·정산·사무실 데이터 JSON 직렬화 자작 | ~100줄 |
| SensorToolkit 2 | 시야콘·LOS·LKP 이미 자작 완료. 잔여=청각 센서(이벤트 브로드캐스트) | ~수십 줄 |
| Final IK | 상체 조준 = 유니티 **공짜 Animation Rigging 패키지**(Multi-Aim Constraint). 풀바디 IK는 부감에서 안 보임 | 패키지 설치 |
| Text Animator | TMP 메시 정점 조작으로 글자별 효과 자작 | 중소 |
| Animancer Pro | Playables API로 클립 재생/크로스페이드/상체 레이어 자작 | 2~300줄 |

> **Animancer 재구매 조건**: 자작 Playables 플레이어가 루트모션·전환 중 이벤트 등 엣지케이스에서 헛돌기 시작하면 그때 산다. 애니메이션 시스템은 엣지케이스가 더러운 동네라는 걸 인지하고 출발.

## TOP 10에서 처음부터 뺀 것들 (재론 방지)

- **Emerald AI / Behavior Designer Pro 2**: ZombieController 이미 가동 중 → 갈아타기=순수 churn
- **Gaia/GeNa/물 계열(Stylized Water, KWS2, Crest)**: 도시 게임 + 그래픽 신규 패스 동결(생산 헌장)
- **HAZE/Better Fog/Volumetric Light Beam**: 동결 존. 손전등 콘은 STEP9에서 해결됨
- **FishNet/Dissonance/uMMORPG**: Blindspot 교훈 — 싱글 PvE가 해자, 멀티에 코어 재미 묶지 않기
- **아웃라인·셰이더 툴**: ConeGhost/LKPGhost 자작 가동 중

## 구매 시 체크리스트

1. 퍼블리셔 일치 확인: RamsterZ / Imphenzia / The Naughty Cult / GurBu / LAYERLAB
2. GPU Instancer는 **Pro 본체 + Pro용 Crowd 애드온** 짝 맞추기 (구버전 ID 145114 아님)
3. 결제 전 개별 페이지에서 세일가·최근 리뷰 확인 (썸네일 해상도 한계로 가격 미검증)
