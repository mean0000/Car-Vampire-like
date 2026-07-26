using UnityEngine;

/// <summary>
/// ★무기 액션 공통 어휘(2026-07-05 교통정리) — 판정/타이밍/VFX/SFX 데이터 블록의 단일 정의.
/// 그동안 같은 개념이 네 모양(ComboAttackStep 평면 필드 / SkillSet 중첩 클래스 / ChargePhantomSet.SlashVfxData /
/// KatanaWeapon 인라인 필드)으로 살던 것을 이 파일 하나로 통일한다. 모든 SO(<see cref="WeaponActionSet"/>·
/// <see cref="ComboAttackSet"/>)가 이 블록들을 재사용한다 — 나·Codex 독립 수렴안.
/// 규약: [Serializable] class + 필드 초기화 디폴트(신규 에셋이 바로 유효값) — 구 "0이면 폴백" struct 규약을 대체.
/// 단 기존 직렬화 에셋의 필드 누락 시 초기화값이 그대로 남으므로, 소비 측의 0-가드(예: scale 0→1)는 유지한다.
/// ★타이밍(타격/종료)은 여기 없다 — 클립 AnimationEvent가 소유(애니가 진실).
/// </summary>
public enum VfxBasis { Player, Weapon }

/// <summary>판정(히트박스) — 부채꼴 반경/각/원점 전진/데미지/넉백. KatanaWeapon.DoHit의 입력 어휘.</summary>
[System.Serializable]
public class WeaponHitData
{
    [Tooltip("사거리(m).")] public float range = 3f;
    [Tooltip("부채꼴 半각(deg) — 전체 폭의 절반.")] public float arcHalfAngle = 60f;
    [Tooltip("히트존 원점을 조준 방향으로 전진(m). 클수록 체감 좌우 폭이 좁아짐.")] public float forwardOffset = 0.5f;
    [Tooltip("데미지.")] public int damage = 10;
    [Tooltip("넉백(m/s). 0=무넉백(유효값).")] public float knockback = 6f;
    [Tooltip("★켜면 실효 사거리 = range × (짝지어진 VFX scale). 판정을 보이는 슬래시 크기에 연동(콤보 규약).")]
    public bool rangeFromVfxScale;
}

/// <summary>VFX 스폰(슬래시/불렛 공용) — 기준·프리팹·정합 오프셋·스케일·재생속도·수명.
/// 스폰 실행은 <see cref="WeaponVfxSpawner"/> 단일 경로.</summary>
[System.Serializable]
public class WeaponVfxData
{
    [Tooltip("★기준 — Weapon: 무기(칼) 앵커(슬래시, 휘두름 따라) / Player: 플레이어 위치+조준(불렛/전방 발사, z=앞).")]
    public VfxBasis basis = VfxBasis.Weapon;
    [Tooltip("VFX 프리팹 — 비우면 없음.")] public GameObject prefab;
    [Tooltip("기준(basis) 회전 기준 미세 정합(deg) — 모션마다 스윙 평면이 달라 각자 맞춘다.")]
    public Vector3 eulerOffset;
    [Tooltip("기준-로컬 위치 오프셋(m). Weapon=칼 로컬 / Player=조준 프레임(z=앞·x=우·y=위).")]
    public Vector3 posOffset;
    [Tooltip("스케일 배수(0이면 1).")] public float scale = 1f;
    [Tooltip("재생 속도 배수 — 파티클 simulationSpeed에 곱(0이면 1).")] public float playbackSpeed = 1f;
    [Tooltip("자동 소멸(초, 0이면 1.5).")] public float lifetime = 1.5f;
    [Tooltip("켜면 스폰 후 무기 앵커에 부모로 붙여 스윙 따라 쓸리게(끄면 월드 고정).")]
    public bool parentToWeapon;
}

/// <summary>사운드 1발(2D, 거리감쇠 없음 — 손맛 사운드 정책).</summary>
[System.Serializable]
public class WeaponSfxData
{
    [Tooltip("사운드 클립 — 비우면 무음.")] public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.7f;
}

/// <summary>타이밍(쿨다운·워치독) — 타격/종료 '시점'이 아니다(그건 AnimationEvent 소유). 안전망과 재사용 간격만.</summary>
[System.Serializable]
public class WeaponTimingData
{
    [Tooltip("쿨다운(초). 0이면 애니가 허용하는 한 즉시 재사용(테스트용) — 밸런스 시 올린다.")]
    public float cooldown = 0f;
    [Tooltip("안전 워치독(초) — 클립 길이+여유. OnComboEnd 누락/하드컷 시 진행 플래그 고착(반 소프트락) 방지.")]
    public float maxDuration = 3.5f;
}

/// <summary>Animator 진입 정의 — 트리거 '이름'만 데이터가 소유(애니가 진실 유지 — SO는 어느 상태로 들어갈지만 명명).
/// 해시는 런타임에 <see cref="Resolve"/>로 1회 캐시(Codex 리스크: 문자열 오타 — 빈 이름/미해결은 소비 측이 에러 로그).</summary>
[System.Serializable]
public class WeaponAnimatorData
{
    [Tooltip("★진입 트리거 이름(컨트롤러 AnyState 전이) — 예: Skill01, Counter, DashAttack. 필수.")]
    public string triggerName;
    [Tooltip("차징 윈드업 트리거 이름(차징 액션만) — 예: SkillCharge. 비우면 차징 진입 없음.")]
    public string chargeTriggerName;
    [Tooltip("취소 트리거 이름(차징 불발/하드컷 복귀) — 예: SkillCancel. 비우면 취소 전이 없음.")]
    public string cancelTriggerName;

    [System.NonSerialized] public int TriggerHash;
    [System.NonSerialized] public int ChargeTriggerHash;
    [System.NonSerialized] public int CancelTriggerHash;

    /// <summary>트리거 이름 → 해시 1회 캐시. 빈 이름은 0(소비 측 무동작 가드).</summary>
    public void Resolve()
    {
        TriggerHash = string.IsNullOrEmpty(triggerName) ? 0 : Animator.StringToHash(triggerName);
        ChargeTriggerHash = string.IsNullOrEmpty(chargeTriggerName) ? 0 : Animator.StringToHash(chargeTriggerName);
        CancelTriggerHash = string.IsNullOrEmpty(cancelTriggerName) ? 0 : Animator.StringToHash(cancelTriggerName);
    }
}

/// <summary>★런지(발동 순간 전진) — "확확—딱" 리듬의 '확'(2026-07-05 유저 게임감 방향). 조준 방향으로만 변위
/// (뒤로 갈 수 없는 파워 버튼 = 카이팅 답). PlayerMotor.AddGlide 재사용 — 등속(거리/시간) 후 급정지라
/// duration이 짧을수록 대시급 스냅. distance 0 = 전진 없음(반격/차징스킬 등 기존 액션 기본).</summary>
[System.Serializable]
public class WeaponLungeData
{
    [Tooltip("발동 순간 전진 거리(m). 0=없음. 선풍참 2.5~3 권장.")]
    [Min(0f)] public float distance = 0f;
    [Tooltip("전진 시간(초) — 등속 후 급정지. 짧을수록 '확' 스냅(0.08~0.12 권장).")]
    [Min(0.02f)] public float duration = 0.1f;
}

/// <summary>★밀도 보상 — 한 방에 N마리 이상 맞히면 쿨다운 일부 환류("호드 속으로 파고들수록 이득" —
/// 카이팅을 손해로 만드는 물리 인센티브, 2026-07-05 관용/전직 리서치 수렴). refundHits 0 = 끔.</summary>
[System.Serializable]
public class WeaponDensityData
{
    [Tooltip("환류 발동 최소 적중 수. 0=밀도 보상 없음.")]
    [Min(0)] public int refundHits = 0;
    [Tooltip("환류 비율 — 남은 쿨다운에 곱해 깎는 몫(0.3=쿨 30% 감면).")]
    [Range(0f, 1f)] public float cooldownRefund = 0.3f;
}

/// <summary>차징(홀드→릴리스 발동) 설정 — RMB 차징스킬 문법. enabled=false면 즉발 액션.</summary>
[System.Serializable]
public class WeaponChargeData
{
    [Tooltip("켜면 차징식 — 누르는 동안 기 모으고 놓으면 발동. 끄면 즉발.")]
    public bool enabled;
    [Tooltip("최대 차징 시간(초). 0.83 ≈ 50프레임 @60fps.")]
    [Min(0.05f)] public float chargeMax = 0.83f;
    [Tooltip("발동 최소 차징(초). 미만이면 불발(빈 탭 가드) — 쿨다운도 소비 안 함. chargeMax보다 크면 사용처에서 클램프(영구 불발 방지).")]
    [Min(0f)] public float minChargeToFire = 0.12f;
}
