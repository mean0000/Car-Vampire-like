using UnityEngine;

/// <summary>
/// 플레이어 공격 스윙 사운드 — ★임시 A/B 비교용(단일 클립). 음색 방향 확정 후 Sound 에이전트가
/// 정식 변주 시스템(콤보 단별 풀·직전제외 랜덤·피치지터 + 적 타격 임팩트 계층)으로 대체한다.
///
/// 트리거 = <see cref="PlayerAnimatorDriver.AttackHit"/>(블레이드 정점, 공격마다 1회). 2D 재생
/// (좀비 그르렁 3D와 다른 채널 — 내 손맛 사운드는 카메라 거리감쇠되면 안 됨). 발소리와 동일 정책.
/// </summary>
public class PlayerAttackSfx : MonoBehaviour
{
    [Tooltip("스윙음 클립(비교용 — 이 칸만 갈아끼우며 A/B).")]
    [SerializeField] AudioClip swingClip;
    [SerializeField, Range(0f, 1f)] float volume = 0.6f;
    [SerializeField, Range(0f, 1f)] float spatialBlend = 0f;
    [Tooltip("비우면 런타임에 2D 소스 생성.")]
    [SerializeField] AudioSource source;

    PlayerAnimatorDriver _driver;

    void Awake()
    {
        _driver = GetComponentInChildren<PlayerAnimatorDriver>();
        if (_driver == null)
            Debug.LogWarning("[PlayerAttackSfx] PlayerAnimatorDriver 미발견 — 스윙음이 안 울린다.", this);
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }
        source.spatialBlend = spatialBlend;
    }

    void OnEnable() { if (_driver != null) _driver.AttackHit += OnAttack; }
    void OnDisable() { if (_driver != null) _driver.AttackHit -= OnAttack; }

    void OnAttack(int comboStep)
    {
        // ★콤보(intParameter 1/2/3)만 스윙음. 스킬/반격(0)은 자기 사운드를 따로 낸다(KatanaWeapon.skillSfx 등)
        //   — 안 막으면 스킬/반격에 콤보 스윙음이 잘못 울린다(VFX와 동일 누수).
        if (comboStep < 1) return;
        if (swingClip == null || source == null) return;
        source.PlayOneShot(swingClip, volume);
    }
}
