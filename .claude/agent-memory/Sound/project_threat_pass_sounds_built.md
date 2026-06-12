---
name: threat-pass-sounds-built
description: 2026-06-12 압박 사운드 2종 시공 — 노브·주파수 전수, 시스템 검증 완료, 음색 판정 대기
metadata:
  type: project
---

# 위협 패스 사운드 시공 (2026-06-12) — 음색 판정 대기 상태

신규 파일(둘 다 자가 부트스트랩 DDOL, 씬·프리팹 무수정):
- `Assets/_Project/Scripts/AmbientBedDrone.cs`
- `Assets/_Project/Scripts/ZombieVocalDirector.cs` (+ ZombieVocalClips 정적 클래스 동거)

## 채택 주파수/볼륨 (전부 플레이스홀더 — 유저 귀 판정 전)
- **베드**: 49+98+196Hz 옥타브 스택(맥놀이 원천 차단) + 저역 노이즈(lp 0.015, ×0.20), 호흡 = LFO 0.125/0.25Hz 진폭 변조. 루프 8s 정수 사이클. vol **0.05**. 페이드 인 3s / 아웃 0.8s. 게이트 = masterEnabled && timeScale>0 && 플레이어 존재.
- **으르렁** 3변형: base 68/76/85Hz, rasp 21~27Hz, 1.3s, 좀비별 고정 피치 0.85~1.15 + 발화별 ±7%. vol **0.10**. 간격 5~11s, 거리 게이트 30m, 동시 4마리 한도.
- **절규** 2변형: 170→640 / 210→760Hz 지수 글라이드 + 9Hz 비브라토 + 고역 찢김, 0.85s. vol **0.15**. IsAggro 상승 엣지(폴링), 25m 게이트, priority 100(으르렁 160).
- TensionAudioDirector(기각된 3단 레이어)는 CombatLab 씬에서 masterEnabled:0 — 내 베드와 저역 충돌 없음 확인.

## 알려진 미결
- **어그로 순간 이중 발성**: ZombieController.EmitGrunt(Chase 진입 그르렁, Gameplay 소유)와 내 절규가 같은 엣지에서 동시 발화. 겹쳐 들리면 → screamVolume 0 또는 통합 협의(인터페이스 경계라 멈춤). [[audio-infra-map]]
- 스폰 시 이미 어그로인 좀비는 절규 미발화(prevAggro 초기화 정책, RearThreatHint와 동일).
- 디버그 카운터(DebugGrowlsFired 등)는 검증용으로 남겨둠 — 노브 세션에서 발화 확인에 유용.

## 노브 번역 사전 (유저 묘사 → 노브, 누적 갱신할 것)
- 아직 유저 음색 판정 없음. 예상 실패 모드 매핑:
  - "웅웅거려/맥놀이" → 베드 노이즈 게인(×0.20) 축소 또는 49Hz 단독화 (옥타브는 이론상 안전, 룸 모드 가능성)
  - "안 들려" → bedVolume 0.05→0.08~0.10 단계 상향
  - "으르렁이 기계 같아" → rasp Hz 변형 폭 확대, 간격 랜덤 폭 확대
  - "절규가 찢어져/아파" → screamVolume 하향, 고역 찢김 게인(0.45) 축소
