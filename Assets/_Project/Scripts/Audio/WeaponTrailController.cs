using UnityEngine;

/// <summary>
/// 무기 트레일 토글 — 공격 커밋(WeaponBehaviour.IsBusy) 동안만 칼날 트레일을 emit한다.
/// 칼끝(BladeTip)의 TrailRenderer가 스윙이 그리는 실제 궤적을 추종 → 하향/상향/대각이 자동으로 따라옴.
/// 짧은 time으로 현재 스윙 호만 보이고 빠르게 페이드. 비공격 시 끄고 Clear(이동 중 잔상 방지).
///
/// 자족 — Player 루트에 붙어 자식 WeaponBehaviour를 읽는다. 코드는 emit on/off만, 궤적은 트레일이 소유.
/// </summary>
public class WeaponTrailController : MonoBehaviour
{
    [Tooltip("칼끝 BladeTip의 TrailRenderer.")]
    [SerializeField] TrailRenderer trail;

    WeaponBehaviour _weapon;
    bool _wasAttacking;

    void Awake()
    {
        _weapon = GetComponentInChildren<WeaponBehaviour>();
        if (_weapon == null)
            Debug.LogWarning("[WeaponTrailController] WeaponBehaviour 미발견 — 트레일이 안 켜진다.", this);
        if (trail != null) { trail.emitting = false; trail.Clear(); }
    }

    void LateUpdate()
    {
        if (trail == null) return;
        bool attacking = _weapon != null && _weapon.IsBusy;
        if (attacking && !_wasAttacking) { trail.Clear(); trail.emitting = true; }   // 스윙 시작 — 이전 잔상 비우고 emit
        else if (!attacking && _wasAttacking) { trail.emitting = false; }            // 스윙 끝 — emit 정지(꼬리는 time만큼 자연 페이드)
        _wasAttacking = attacking;
    }
}
