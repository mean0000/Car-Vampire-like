---
name: audio-infra-map
description: ZombieCrush 오디오 인프라 실측 지도 + 시스템 검증에서 밟은 함정들 (2026-06-12)
metadata:
  type: project
---

# 오디오 인프라 실측 (2026-06-12 위협 패스)

## 라우팅 현황
- **믹서 없음** — 전 소스가 리스너 직결(코드 볼륨). _Project에 AudioMixer 에셋 0개(Feel의 MMSoundManagerAudioMixer만 존재, 미사용). 믹서/덕킹은 B-005b로 연기된 상태. **AudioMixer 에셋은 스크립트로 생성 불가**(AudioMixerController = internal 에디터 API) — 믹서 도입 시 유저가 에디터에서 수동 생성 필요.
- 리스너 = AudioListenerRig 1기(위치=플레이어, 회전=카메라 yaw만). 씬에 리스너 1개 확인(CombatLab).
- 소리사다리 표준: min 1.5 / max 25 / Logarithmic (SfxOneShot 기준, 좀비 보컬도 동일 정렬).

## 함정 (실제로 밟음)
- **PlayOneShot은 src.clip을 설정하지 않는다** — `isPlaying && clip==X`로 "무엇이 재생 중인지" 판별 불가. 종료 예정 시각(now + clip.length/pitch) 추적으로 우회. **Why:** 첫 구현에서 동시 으르렁 카운터가 항상 0이 되는 결함으로 실증. **How to apply:** PlayOneShot 기반 동시 발성 제한은 시간 추적 또는 priority 잔존값으로.
- **CombatLab도 시작 시 timeScale=0** — Office만이 아니다. 검증 플레이는 `Object.FindObjectOfType<Run.RunManager>().StartMission()` 필수 (네임스페이스 = `Run`, ZombieCrush.Run 아님).
- **Refresh 직후 즉시 플레이 진입 = 구버전 어셈블리 레이스 의심** — isCompiling=False여도 임포트 전일 수 있음. 한 번 절규 미발화 미스터리(priority 128 잔존)가 이걸로 설명됨. 컴파일 확인은 신규 타입 typeof 직접 참조로.
- **플레이어 방치 = 사망 → timeScale 0** — 좀비 곁에 텔레포트해 두고 다음 명령까지 시간이 흐르면 런이 끝나 있다. 검증 시퀀스는 짧게.
- 검증 트릭: 절규/으르렁 발화 증거 = AudioSource.priority 잔존값(절규 100/으르렁 160/미발화 128) + 공개 디버그 카운터(ZombieVocalDirector.DebugGrowlsFired/DebugScreamsFired/DebugVoiceCount). 어그로 유발 = `zombie.TakeDamage(1)` (즉시 Chase 진입 실증).

## B-004 발사 사운드 채널 (PlayerCombat — 수정 금지 구역, 점검 결과)
- 구조: 플레이어에 2D 소스 3개 — _gunAudio(오프셋 재생, Play() 재시작=연사 누적 차단), _reloadAudio(PlayOneShot), _tailAudio(pitch 0.5 잔향, ≥3발 연사 종료 시). 명중 thud = SfxOneShot 3D(MeleeSfx.ThudClip 재사용, vol 0.06).
- **★씬 직렬화 덮어쓰기 실증: CombatLab 씬 shotVolume=0.65가 코드 default 0.185를 이김** — 라이브 실측 vol=0.650. 발사음 크기 논의는 항상 씬 값 기준으로.
- MeleeSfx는 아직 AudioSource.PlayClipAtPoint 사용(B-005a의 SfxOneShot 전면 대체에서 누락된 잔재 — Linear rolloff·GC·동시발성 무제한).
- ZombieController.EmitGrunt: Chase 진입 시 그르렁(SfxOneShot, 절차 폴백 클립, 쿨다운 5s) — 게임플레이 인식 전파 메커니즘에 묶여 있음(Gameplay 소유). [[threat-pass-sounds-built]]의 절규와 같은 순간 이중 발성.
- 소음→어그로 사슬: 6초 연사에도 어그로 0/12 실측(좀비 전원 Dormant 유지) — 죽어 있음. 세션 1 임무 4 소관으로 보고함.
