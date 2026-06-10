# 핸드오프: 그래픽 베이스라인 v1 확립 (2026-06-10)

> 다음 세션 시작점. 상세값은 전부 **`docs/2026-06-10_그래픽_베이스라인_v1.md`** (라이팅 바이블)에 있음 — 이 문서는 경위와 다음 작업만.

## 오늘 한 것 (완료)

1. **그래픽 기준점 = Hell Express 확정** + 라이팅 5원칙 추출 → 바이블 서문에 명문화
2. **룩데브 4층 구조 채택**: 글로벌(잠금) / 프로파일(에셋) / 씬 로컬(레시피) / 상태(블렌딩). 씬마다 그래픽 재작업 금지
3. **Greybox_ScanLit_v2** = 룩데브 랩. COZY 설치(임베디드 패키지 복사) + Frozen Golden Hour(시간 15:00 고정 + 색 상수 동결) + zenith 쿨 카운터포인트 (0.35, 0.40, 0.75)
4. **사고 2건 해결**: ① COZY fogColor 알파=농도 → 알파 1.0이 화이트아웃 유발 (알파 0.06/0.10/0.25/0.45/0.75, fogStart 14/30/45/70으로 확정) ② 거리 DOF·포그는 탑다운에서 무효 → **스크린스페이스 틸트시프트 부활** (기존 TiltShift.shader, Blit.hlsl include를 core 패키지 경로로 수정)
5. **벤더 정책**: COZY는 "현재 선택" — 동적 날씨가 출시 기능이면 유지, 맵당 고정 무드면 커스텀 MoodProfile 검토. 결정은 기획 마일스톤에서
6. **룩데브 배치**: `_LookdevSet` 루트에 좀비 5(플레이스홀더 캡슐) + 프롭 3종 + Dust Motes

## 작업 큐 (바이블의 "우선순위 작업 큐" 섹션과 동일)

- ~~3~~ ✅ 좀비 이미시브: `M_ZombieThreat`(레드 ×1.6) / `M_ZombieSignalThreat`(핑크-레드 ×2 — 1차 옐로 시도는 안전색 충돌로 반려). COZY 데모 머티리얼 의존 제거
- ~~4~~ ✅ 톤 seam 검증: 20m에선 미미, 클로즈업에선 ithappy(고채도 라운드) vs Synty(저채도 패싯) 차이 분명 → 클로즈업 노출 시 보정 필요. `LookdevCloseupCam` 상비. ~~캡슐→실모델 프리팹 교체는 애니메이터 통합 필요 = 게임플레이 작업으로 이관~~
- ✅ **좀비 프리팹 실모델 통합 (2026-06-10 완료)**: Zombie/ZombieSignal 캡슐 → ithappy Skeleton.001 리그(SMR 8, "Visual" 자식) + 루트 Animator(ZombieAnimator + Humanoid AnimationsAvatar, AlwaysAnimate). Idle/Run = ithappy 자체 클립, Speed 임계값 12/15→0.5/0.3 (기존은 사실상 고장). 머티리얼은 ithappy 원본 유지(위협 이미시브 액센트는 다음 판정). 시그널 구분 = 스케일 2.3. ⚠ **피벗 함정 수정**: 리그 피벗=발이라 ZombieController `_groundOffset`을 bounds.center 기준→피벗 기준으로 수정 (안 그러면 1.8m 부유). 플레이모드 검증 완료(접지·Idle 재생·콘솔 0). 잔여: ① 아레나 밖 보이드 스폰(기존 스포너/레벨 이슈) ② 스폰 시 `localScale=one` 리셋이라 클론(1.8m)과 배치본(스케일2=3.6m) 크기 불일치(기존) ③ 공격/사망 클립(Zombie_Attack/Death_*) 미와이어링
- **다음 = 5**: 빛기둥·바닥 브레이크업·로컬 고임 안개(스텔스 장치) — 시가지 맵 단계. 좀비 위협 이미시브 액센트(실모델 기준 20m 캡처 판정)도 대기

## 도구·환경 메모

- Unity MCP 연결 = **이 프로젝트(Car-Vampire-like/ZombieCrush)**, naju-poko 아님
- `Tools/COZY/*` 메뉴 (CozyDuskSetup.cs): 값은 EditorPrefs 주입, 결과는 EditorPrefs("CozyDusk_Result") 회수. 백그라운드 에디터 대응 강제 틱 내장
- MCP RunCommand 함정: COZY 하이어라키 GetComponentsInChildren 순회 시 NRE → 에디터 스크립트+메뉴로 우회. 스크립트 수정 직후 같은 커맨드에서 메뉴 실행 금지(구버전 실행됨)
