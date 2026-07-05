using UnityEngine;

/// <summary>
/// ★무기-스타일 1개의 콤보 공격 데이터(통합 SO) — 단별로 판정(hit) + 비주얼(vfx)을 공통 어휘
/// (<see cref="WeaponHitData"/>·<see cref="WeaponVfxData"/>, 2026-07-05 통일)로 모은다.
/// (나·Codex 독립 수렴: per-weapon 단일 진실. 비주얼+판정을 한 스텝에 묶어 "범위를 슬래시 크기에 연동"이 공짜.)
///
/// 무기/스타일 추가 = 이 SO를 하나 더 만들어 채우면 끝. KatanaWeapon(판정)·PlayerAttackVfx(비주얼)가
/// 같은 에셋을 참조해 같은 steps[]를 읽는다. 타이밍은 여기 없다 — AnimationEvent가 소유(애니가 진실).
/// 콤보 외 액션(스킬/반격/대시베기)은 <see cref="WeaponActionSet"/> — steps[] 진행 구조가 필요한 콤보만 이 SO.
///
/// ⚠️ 런타임에 steps[] 필드를 쓰지 말 것(공유 에셋 오염). 임시 버프 등 가변값은 별도 런타임 상태로.
/// </summary>
[CreateAssetMenu(fileName = "ComboAttackSet", menuName = "ZombieCrush/Combo Attack Set")]
public class ComboAttackSet : ScriptableObject
{
    [Tooltip("식별/문서용 이름 — 예: Katana_참격, Katana_발도, Greatsword_미정1.")]
    public string styleName = "Katana_참격";

    /// <summary>콤보 1단 = 판정 + 슬래시 비주얼(공통 어휘 재사용 — 2026-07-05 스키마 통일, 구 평면 필드 대체).</summary>
    [System.Serializable]
    public class ComboAttackStep
    {
        [Tooltip("판정(히트박스). rangeFromVfxScale면 실효 사거리 = range × vfx.scale(판정을 보이는 슬래시 크기에 연동).")]
        public WeaponHitData hit = new WeaponHitData();
        [Tooltip("비주얼(슬래시 VFX) — prefab 비면 fallbackSlashPrefab 사용. 콤보 스폰 기준은 무기 앵커(PlayerAttackVfx)라 basis는 Weapon로 둔다.")]
        public WeaponVfxData vfx = new WeaponVfxData();
    }

    [Tooltip("단에 슬래시 프리팹이 비었을 때 쓰는 폴백.")]
    public GameObject fallbackSlashPrefab;
    [Tooltip("콤보 단별 설정. 인덱스 0=1타. 비거나 범위 밖이면 마지막 단으로 클램프.")]
    public ComboAttackStep[] steps;

    public int StepCount => steps != null ? steps.Length : 0;

    /// <summary>콤보 단(1-based)의 설정 반환. 범위 밖이면 마지막 단으로 클램프.</summary>
    public bool TryGetStep(int comboStep, out ComboAttackStep step)
    {
        step = null;
        if (steps == null || steps.Length == 0) return false;
        step = steps[Mathf.Clamp(comboStep - 1, 0, steps.Length - 1)];
        return step != null;
    }

    /// <summary>콤보 단(1-based)의 슬래시 스케일(0이면 1 폴백).</summary>
    public float GetScale(int comboStep)
    {
        if (!TryGetStep(comboStep, out var s) || s.vfx == null) return 1f;
        return s.vfx.scale > 0f ? s.vfx.scale : 1f;
    }
}
