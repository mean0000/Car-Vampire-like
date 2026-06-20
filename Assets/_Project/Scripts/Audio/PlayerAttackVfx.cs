using UnityEngine;

/// <summary>
/// 플레이어 공격 슬래시 VFX — ★데이터 주도. 현재 활성 <see cref="ComboAttackSet"/>(무기-스타일)을 읽어,
/// 콤보 단별로 슬래시를 무기(칼) 방향에 정합해 스폰한다. 무기/스타일이 늘면 SO만 추가(코드 무수정),
/// 런 중 무기 스왑 시 <see cref="SetComboSet"/>·<see cref="SetWeaponAnchor"/>로 교체.
/// ★KatanaWeapon(판정)과 같은 ComboAttackSet 에셋을 참조한다 — 비주얼·판정 단일 진실.
///
/// 트리거 = <see cref="PlayerAnimatorDriver.AttackHit"/>(블레이드 정점, comboStep 전달). 슬래시 방향은
/// "프레임 추종"이 아니라 *그 순간 무기 방향으로 오리엔트해 띄우고 페이드"(단별 eulerOffset로 정합).
/// </summary>
public class PlayerAttackVfx : MonoBehaviour
{
    [Tooltip("현재 활성 무기-스타일 콤보 세트(판정+슬래시 통합). KatanaWeapon과 같은 에셋. 스왑 시 SetComboSet으로 교체.")]
    [SerializeField] ComboAttackSet comboSet;
    [Tooltip("★무기 기준 transform(칼/칼끝). 스윙 방향 정합 기준. 비우면 비주얼 facing.")]
    [SerializeField] Transform weaponAnchor;
    [Tooltip("스폰 후 무기에 부모로 붙여 스윙 따라 쓸리게(끄면 월드 고정 플래시).")]
    [SerializeField] bool parentToWeapon = false;

    PlayerAnimatorDriver _driver;

    void Awake()
    {
        _driver = GetComponentInChildren<PlayerAnimatorDriver>();
        if (_driver == null)
            Debug.LogWarning("[PlayerAttackVfx] PlayerAnimatorDriver 미발견 — 슬래시가 안 뜬다.", this);
    }

    void OnEnable() { if (_driver != null) _driver.AttackHit += OnAttack; }
    void OnDisable() { if (_driver != null) _driver.AttackHit -= OnAttack; }

    /// <summary>무기/스타일 스왑 — 활성 콤보 세트 교체.</summary>
    public void SetComboSet(ComboAttackSet set) => comboSet = set;
    /// <summary>무기 스왑 — 정합 기준 칼 transform 교체.</summary>
    public void SetWeaponAnchor(Transform anchor) => weaponAnchor = anchor;

    void OnAttack(int comboStep)
    {
        // ★콤보(intParameter 1/2/3)만 슬래시. 스킬/반격(0)은 자기 전용 VFX를 따로 띄운다(KatanaWeapon.skillVfxPrefab 등)
        //   — 여기서 막지 않으면 step[0]으로 클램프돼 콤보1 슬래시가 스킬/반격에 잘못 뜬다.
        if (comboStep < 1) return;
        if (comboSet == null) return;
        if (!comboSet.TryGetStep(comboStep, out var step)) return;

        GameObject prefab = step.slashPrefab != null ? step.slashPrefab : comboSet.fallbackSlashPrefab;
        if (prefab == null) return;

        Transform a = weaponAnchor != null ? weaponAnchor : (_driver != null ? _driver.transform : transform);
        Vector3 pos = a.TransformPoint(step.posOffset);
        Quaternion rot = a.rotation * Quaternion.Euler(step.eulerOffset);

        var go = Instantiate(prefab, pos, rot);
        if (parentToWeapon) go.transform.SetParent(a, true);
        float s = step.scale > 0f ? step.scale : 1f;
        if (!Mathf.Approximately(s, 1f)) go.transform.localScale *= s;

        // ★슬래시 재생 속도 + 강제 재생.
        // 속도: 파티클 simulationSpeed에 곱(0이면 1로 폴백; 미설정 직렬화값 0 함정 가드).
        // 재생: 일부 슬래시 프리팹은 PlayOnAwake=false라 Instantiate만으론 안 뜸
        //       → 스폰 시 모든 파티클을 명시적으로 Play(어떤 슬래시든 동작, 데이터 주도 스왑 빈틈 메움).
        float spd = step.playbackSpeed > 0f ? step.playbackSpeed : 1f;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
        {
            if (!Mathf.Approximately(spd, 1f))
            {
                var main = ps.main;
                main.simulationSpeed *= spd;
            }
            ps.Play(false);
        }

        Destroy(go, step.lifetime > 0f ? step.lifetime : 1.5f);
    }
}
