---
name: combat-sfx-inventory
description: 전투 사운드 에셋 실측 인벤토리 — 이미 임포트된 보유분(Vefects 전투 오디오·Footsteps·총기)과 신규 추천 후보 (2026-06-21)
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

## 현재 배선 상태
- `PlayerAttackSfx.cs`: AttackHit(블레이드 정점) → 2D PlayOneShot, `swingClip` 단일 A/B 슬롯, vol 기본 0.6(★캐넌 0.03~0.15 위반 — 임시 비교용이나 정식 전환 시 하향 필요). comboStep<1(스킬/반격) 제외 로직 있음. 정식 변주 시스템(콤보 단별 풀·no-repeat·피치지터+임팩트 계층)은 Sound 에이전트가 대체 예정으로 명시돼 있음.
- 믹서 없음(코드 직결) — [[audio-infra-map]]. 덕킹/버스는 미도입.
