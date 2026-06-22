---
name: player-footsteps-built
description: 플레이어 발소리 시스템 시공 — 거리기반 보폭+표면별 풀+변주. 노브 전수·런/워크 임계 판정 미결. 음색·케이던스 손맛 유저 귀 대기 (2026-06-19)
metadata:
  type: project
---

# 플레이어 발소리 시스템 (2026-06-19, _PlayerStackTest)

## 구조
- `PlayerFootsteps`(자족 MonoBehaviour, Player 루트) — PlayerBrain.Tick 끝에 `_footsteps?.Tick()` 1줄. PlayerMotor를 GetComponentInParent로 읽음.
- `SurfaceFootstepSet` SO(표면ID→walk/run 클립 배열) + `SurfaceTag`(바닥 콜라이더용 선택 식별자).
- 재생 = 자족 2D AudioSource(PlayOneShot). **★3D 안 씀** — 리스너가 Main Camera(15m 위/45°)라 3D면 거리감쇠로 내 발소리가 죽는다. 좀비(SfxOneShot 3D)와 채널 정책 다름.
- 타이밍 = **거리기반 보폭**(AnimationEvent 안 씀 — 8방향 블렌드트리 이벤트 겹침 회피). XZ 이동거리 적산 → stride 도달 시 1스텝, 잔여 보존.

## 임포트 규격 (적용됨)
- Footsteps - Essentials 짧은 SFX = DecompressOnLoad + PCM + ForceToMono + preloadAudioData(SampleSettings로 이동됨, AudioImporter.preloadAudioData는 obsolete) + loadInBackground off.
- 현재 DirtyGround만 임포트(walk 10 + run 10). 표면 추가 시 같은 규격으로.

## 노브 현재값 (씬 인스턴스)
walkStride 1.6 / runStride 2.0 / runSpeedThreshold 5.5 / minMoveSpeed 0.4 / volume **0.10**(보수적 시작) / volumeJitter 0.12 / pitch 0.92~1.08 / spatialBlend 0 / suppressDuringDash on.

## ★미결 — 유저 판정 필요
1. **음색/믹스/케이던스 손맛 = 유저 귀** (난 못 들음). 첫 vol 0.10은 캐넌 보수값.
2. **★런/워크 임계 모순**: 씬 moveSpeed=4.31 < runSpeedThreshold=5.5 → **평상 이동은 항상 walk 풀**. run 풀은 대시꼬리(>5.5)에서만. 의도가 "평속=달리기"면 runSpeedThreshold를 ~3.5로 낮춰야 함. 유저 의도 확인 필요.
3. 거리기반 보폭이 런 케이던스 대비 맞는지(애니 발 디딤과 동기)는 플레이로만 확정. @4.31m/s·stride1.6 = 2.69steps/s(371ms/step) 계산값.

## 함정/메모
- `result.Log`(Unity MCP)는 {0}/{1} object args만 — {2:F2} 포맷 지정자 넣으면 throw. 산수는 C# 계산 후 값만 넘겨라.
- AudioMixer 여전히 없음([[audio-infra-map]]) — 발소리 AudioSource는 믹서 미라우팅(코드 볼륨). 믹서 도입 시 이 source.outputAudioMixerGroup 연결 지점.
