---
name: melee-sfx-pattern
description: 카타나 임시 SFX 배선(swish/impact 2D lazy AudioSource) QA — 2026-06-28 리뷰. Critical/High 0
metadata:
  type: project
---

Day2 임시 베기 SFX 배선 리뷰(2026-06-28). KatanaWeapon.cs 신규 블록 + RunFeel_Whitebox.unity 씬 직렬화.

**결론: Critical/High 0. 구현 견고.**

## 안전 확인된 전제 (재검증 불필요)

- **씬 직렬화 완벽 일치**: meleeSfxEnabled/swishVolume(0.1)/swishPitch(1)/swishPitchJitter(0.05)/impactVolume(0.14)/impactPitch(0.95)/impactPitchJitter(0.04) 전부 코드 default와 일치. SerializeField 씬 덮어쓰기 함정 없음.
- **AudioClip GUID 유효**: swishClip=`Vefects_SFX_Slash_Classic.wav`, impactClip=`Vefects_SFX_Impact_01.wav`. 둘 다 실파일 매핑.
- **재초기화 중복 add 없음**: `Initialize()` 재호출 시 `_swishSource`/`_impactSource` null 체크가 중복 AddComponent 차단.
- **connected 가드 일원화**: 4개 공격 경로(DoSwingHit/DoCounterHit/DoDashAttackHit/DoSkillHit) 모두 `if (connected) FireHitFeedback(...)` 통과 후에만 `PlayMeleeImpact`. 헛스윙 impact 오발 경로 없음.
- **Random.Range 모호성 없음**: System 미임포트 → UnityEngine.Random.Range 명확 해석.
- **PlayerAttackSfx 비활성화 깔끔**: m_Enabled=0, OnEnable/OnDisable 대칭으로 AttackHit 미구독. 다른 스크립트 런타임 의존 없음.

## 발견된 이슈

- **M-1 피치 0.0 바닥**: swishPitch min=0.5, swishPitchJitter max=0.5 → 극단값에서 pitch=0.0 가능. AudioSource.pitch=0 = 무음. Fix=`Mathf.Max(0.01f, pitch+jitter)` 클램프.
- **L-1 Counter/DashAttack swish 누락**: BeginCounter/BeginDashAttack에 PlayMeleeSwish 없음. 의도(갑작스러운 타격 느낌)인지 누락인지 확인 필요.
- **L-2 DoSkillHit 이중 사운드**: 스킬 적중 시 PlaySkillSfx + PlayMeleeImpact 둘 다 울림. Sound 에이전트 핸드오프 시 "레이어 유지 vs 단일 소유" 재결정 필요.
- **L-3 PlayerAttackSfx.Awake 고아 AudioSource**: m_Enabled=0에서도 Awake 실행 → source AudioSource 생성(미사용). 기능 영향 없음.

## 패턴 정보

- PlaySkillSfx(`_skillSfxSource`)와 동형 lazy AudioSource 패턴. 향후 SFX 추가 시 동일 패턴 사용 가능.
- KatanaWeapon의 AudioSource 3개: `_skillSfxSource`(스킬), `_swishSource`(스윙), `_impactSource`(타격). 전부 spatialBlend=0, playOnAwake=false.

**Why:** 임시 플레이스홀더 SFX 배선이라 Sound 에이전트 정식 교체 전 패턴 문서화 필요.
**How to apply:** 향후 SFX 리뷰 시 이 lazy AudioSource 패턴은 안전 패턴으로 검증됨. pitch 클램프 누락만 체크.
