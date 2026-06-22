using UnityEngine;

/// <summary>
/// 무기-스타일 1개의 공격 슬래시 VFX 데이터(예: 카타나_참격 / 카타나_발도 / 대검_미정1).
/// 콤보 단별로 슬래시 프리팹·스윙 정합 각도·위치·스케일을 가진다. 무기/스타일 추가 = 이 SO를
/// 하나 더 만들어 채우면 끝(코드 무수정). PlayerAttackVfx가 현재 활성 세트를 읽어 스폰한다.
/// </summary>
[CreateAssetMenu(fileName = "WeaponSlashSet", menuName = "ZombieCrush/Weapon Slash Set")]
public class WeaponSlashSet : ScriptableObject
{
    [Tooltip("식별/문서용 이름 — 예: Katana_참격, Katana_발도, Greatsword_미정1.")]
    public string styleName = "Katana_참격";

    [System.Serializable]
    public struct SlashStep
    {
        [Tooltip("이 콤보 단의 슬래시 프리팹. 비면 fallbackPrefab 사용.")]
        public GameObject slashPrefab;
        [Tooltip("스윙 평면 정합 각도(deg) — 단마다 스윙(하향/내려치기/상향)이 달라 각자 맞춘다.")]
        public Vector3 eulerOffset;
        [Tooltip("무기(앵커) 기준 로컬 위치 오프셋.")]
        public Vector3 posOffset;
        [Tooltip("스케일 배수(0이면 1).")]
        public float scale;
        [Tooltip("★슬래시 재생 속도 배수 — 파티클 simulationSpeed에 곱(0이면 1). 클수록 휙 빠르게 지나감.")]
        public float playbackSpeed;
        [Tooltip("자동 소멸(초, 0이면 기본 1.5).")]
        public float lifetime;
    }

    [Tooltip("단에 프리팹이 비었을 때 쓰는 폴백 슬래시.")]
    public GameObject fallbackPrefab;
    [Tooltip("콤보 단별 설정. 인덱스 0=1타, 1=2타, ...")]
    public SlashStep[] steps;

    /// <summary>콤보 단(1-based)의 슬래시 스케일(0이면 1 폴백). 히트박스 범위 연동(KatanaWeapon)용.</summary>
    public float GetScale(int comboStep)
    {
        if (steps == null || steps.Length == 0) return 1f;
        var s = steps[Mathf.Clamp(comboStep - 1, 0, steps.Length - 1)];
        return s.scale > 0f ? s.scale : 1f;
    }

    /// <summary>콤보 단(1-based)의 설정 반환. 범위 밖이면 마지막 단으로 클램프.</summary>
    public bool TryGetStep(int comboStep, out SlashStep step)
    {
        step = default;
        if (steps == null || steps.Length == 0) return false;
        int idx = Mathf.Clamp(comboStep - 1, 0, steps.Length - 1);
        step = steps[idx];
        return true;
    }
}
