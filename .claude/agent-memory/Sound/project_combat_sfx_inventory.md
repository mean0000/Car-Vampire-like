---
name: combat-sfx-inventory
description: 전투 사운드 에셋 실측 인벤토리(Vefects 오디오·Footsteps·총기) + 카타나 베기 2층 SFX 배선(KatanaWeapon swish/impact, Day2 2026-06-28)
metadata:
  type: project
---

# 전투 SFX 인벤토리 (2026-06-21 큐레이션)

## ★보유·임포트됨 (신규구매 전 우선 채굴) — "임포트 안됨 ≠ 미보유" 반대 케이스: 이건 *이미 디스크에*
- **Vefects/Combat Flipbook VFX/Audio/WAV/** — 깊은 임팩트 라이브러리. Fer/One_Shot에 `SFX_Vefects_Hit_01~09`, Extra Hits `Hit_10~15`, Sergi 버전 중복, Loop류. **호드 타격 변주 풀의 본진**(15+ 원샷). Radial_Spiky_Hit도 있음.
- **Vefects/Flipbook VFX/Audio/WAV/** — `Vefects_SFX_Slash_Classic.wav`, `Vefects_SFX_Slash_Fire.wav`(카타나 스윙 후보), `Vefects_SFX_Impact_01`, `Impact_Blood_01`, `Shrapnel_01~03`, `GunShot_02`, `Charge_01`.
- **Vefects/Anime VFX URP/Sounds/WAV/** — `SFX_Gun_Shot_Impact`, `Arrow_Shot_Hit`, `Lightning_Hit` 등.
- **Assets/Resources/SFX/Guns/** — pistol/rifle/shotgun fire+reload (B-004 발사 채널이 쓰는 실음원, 이미 배선).
- **Footsteps - Essentials/** — DirtyGround/Grass 등 surface별(발소리 시스템이 DirtyGround만 임포트로 씀, [[player_footsteps_built]]).
- **The Complete UI Sound (500+ SFX)** — 보유(reference_owned_assets), UI 버스용. 미임포트.

> 함정: Vefects 오디오는 *VFX 팩 번들*이라 "전투 사운드 팩"으로 따로 안 보임. 슬래시/임팩트 실음원이 이미 깔려 있다 — `PlayerAttackSfx.swingClip` A/B 슬롯에 `Slash_Classic` 먼저 꽂아 검증 가능. 단 Vefects는 *애니/스타일라이즈드 톤*이라 다크 도심·카타나 "짧고 날카로움"과 맞는지는 유저 귀 판정.

## 신규 후보 (라이브 검증 2026-06-21)
- **Sonniss GDC Bundle** (gdc.sonniss.com) — 무료·로열티프리·상업가능·무표기. 단 큐레이션 안 됨(통짜 라이브러리) = 골라 쓰는 수고. AI/ML 학습용 금지.
- **Whoosh - Ultimate Melee Swing (Cyberwave Orchestra)** — $24, 스윙/우시 전문 9.6MB. 카타나 스윙 단순·날카로움에 정조준.
- **Hack & Slash Melee Combat (Shapeforms)** — $21.99, 스윙+임팩트 큐레이션.
- David Dumais Melee Pack 1 = $59.99 1200개(과함, 호드 가독성엔 오버).

## 현재 배선 상태 (2026-06-28 Day2 베기 SFX 갱신)
- **★KatanaWeapon이 콤보 베기 SFX 단일 소유 (2층, RunFeel_Whitebox 씬에서 가동).** PlaySkillSfx와 동일 2D one-shot 패턴 재사용.
  - **swish**(휘두름): `PlayMeleeSwish()` ← BeginCombo + Advance(스윙 시작, 헛스윙 포함·ungated). 클립=Vefects `Slash_Classic`(guid `1aa4c21b174363345ac76e3276e18fa0`). 첫값 vol **0.10**·pitch **1.0**·jitter ±0.05.
  - **impact**(thud/cut): `PlayMeleeImpact()` ← FireHitFeedback(connected에서만 호출 → 헛스윙 자동 무음, 가드 일원화). 클립=Vefects `Impact_01`(guid `0fc17236f67ee6b4b93e09eee791ee4e`). 첫값 vol **0.14**·pitch **0.95**(DD 묵직 살짝 down)·jitter ±0.04.
  - 마스터토글 `meleeSfxEnabled`(롤백). swish/impact 소스 분리(피치 독립). ★swish 타이밍=코드(BeginCombo) 임시 — 정석은 클립 OnSwishWhoosh AnimationEvent(Animation 에이전트, 추후 이관).
  - ★음색 판정 = 유저 귀 미검증(나·Codex 못 들음). Vefects=애니/스타일라이즈드 톤이라 DD 묵직·절제와 맞는지 의문 → 안 맞으면 swap 풀(Hit_01~08·Impact_Blood_01·Stylized Slashes). impact가 가벼우면 pitch↓.
- **PlayerAttackSfx = 비활성(m_Enabled 0).** 이전엔 `SFX_Slash_Generic`(guid 6e0bdec...)를 AttackHit=히트프레임에 vol 0.6·ungated로 냄 → 신규 2층과 히트프레임서 3중 적층·0.6이 지배. 자체 docstring이 "Sound 에이전트가 정식 변주로 대체" 명시 → 비활성으로 자리 비움(재활성=롤백).
- **Vefects WAV 임포트 규격 = 이미 캐넌 준수**: loadType 0(Decompress On Load·짧은 SFX 정석)·Vorbis q1·44100·preload. 임포트 변경 불요.
- 믹서 없음(코드 직결) — [[audio-infra-map]]. 덕킹/버스는 미도입.
