using UnityEngine;

/// <summary>
/// ★무기-스타일 1개의 콤보 공격 데이터(통합 SO) — 단별로 판정(히트박스) + 비주얼(슬래시 VFX)을 한 줄에 모은다.
/// (나·Codex 독립 수렴: per-weapon 단일 진실. 비주얼+판정을 한 스텝에 묶어 "범위를 슬래시 크기에 연동"이 공짜.)
///
/// 무기/스타일 추가 = 이 SO를 하나 더 만들어 채우면 끝. KatanaWeapon(판정)·PlayerAttackVfx(비주얼)가
/// 같은 에셋을 참조해 같은 steps[]를 읽는다. 타이밍은 여기 없다 — AnimationEvent가 소유(애니가 진실).
///
/// ⚠️ 런타임에 steps[] 필드를 쓰지 말 것(공유 에셋 오염). 임시 버프 등 가변값은 별도 런타임 상태로.
/// </summary>
[CreateAssetMenu(fileName = "ComboAttackSet", menuName = "ZombieCrush/Combo Attack Set")]
public class ComboAttackSet : ScriptableObject
{
    [Tooltip("식별/문서용 이름 — 예: Katana_참격, Katana_발도, Greatsword_미정1.")]
    public string styleName = "Katana_참격";

    [System.Serializable]
    public struct ComboAttackStep
    {
        [Header("판정(히트박스)")]
        [Tooltip("기준 사거리(m) — 직접 조정. rangeFromSlashScale면 scale을 추가로 곱한다.")]
        public float range;
        [Tooltip("부채꼴 半각(deg) — 전체 폭의 절반.")]
        public float arcHalfAngle;
        [Tooltip("히트존 원점을 조준 방향으로 전진(m). 클수록 체감 좌우 폭이 좁아짐.")]
        public float forwardOffset;
        [Tooltip("데미지.")]
        public int damage;
        [Tooltip("넉백(m/s).")]
        public float knockback;
        [Tooltip("★켜면 실효 사거리 = range × scale(아래 슬래시 스케일). 판정을 보이는 슬래시 크기에 연동.")]
        public bool rangeFromSlashScale;

        [Header("비주얼(슬래시 VFX)")]
        [Tooltip("이 단의 슬래시 프리팹. 비면 fallbackSlashPrefab 사용.")]
        public GameObject slashPrefab;
        [Tooltip("스윙 평면 정합 각도(deg) — 단마다 스윙이 달라 각자 맞춘다.")]
        public Vector3 eulerOffset;
        [Tooltip("무기(앵커) 기준 로컬 위치 오프셋.")]
        public Vector3 posOffset;
        [Tooltip("슬래시 스케일 배수(0이면 1). rangeFromSlashScale가 이 값을 판정에도 쓴다.")]
        public float scale;
        [Tooltip("슬래시 자동 소멸(초, 0이면 기본 1.5).")]
        public float lifetime;
        [Tooltip("슬래시 재생 속도 배수 — 파티클 simulationSpeed에 곱(0이면 1).")]
        public float playbackSpeed;
    }

    [Tooltip("단에 슬래시 프리팹이 비었을 때 쓰는 폴백.")]
    public GameObject fallbackSlashPrefab;
    [Tooltip("콤보 단별 설정. 인덱스 0=1타. 비거나 범위 밖이면 마지막 단으로 클램프.")]
    public ComboAttackStep[] steps;

    public int StepCount => steps != null ? steps.Length : 0;

    /// <summary>콤보 단(1-based)의 설정 반환. 범위 밖이면 마지막 단으로 클램프.</summary>
    public bool TryGetStep(int comboStep, out ComboAttackStep step)
    {
        step = default;
        if (steps == null || steps.Length == 0) return false;
        step = steps[Mathf.Clamp(comboStep - 1, 0, steps.Length - 1)];
        return true;
    }

    /// <summary>콤보 단(1-based)의 슬래시 스케일(0이면 1 폴백).</summary>
    public float GetScale(int comboStep)
    {
        if (steps == null || steps.Length == 0) return 1f;
        float s = steps[Mathf.Clamp(comboStep - 1, 0, steps.Length - 1)].scale;
        return s > 0f ? s : 1f;
    }
}
