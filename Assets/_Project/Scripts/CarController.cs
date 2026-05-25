using UnityEngine;

/// <summary>
/// 차량 컴포넌트 coordinator.
/// 외부(UI, 업그레이드, 좀비 충돌)에서 접근하는 단일 진입점.
/// </summary>
[RequireComponent(typeof(CarGroundSensor))]
[RequireComponent(typeof(CarMovement))]
[RequireComponent(typeof(CarBoost))]
[RequireComponent(typeof(CarHitFeedback))]
public class CarController : MonoBehaviour
{
    private CarMovement    _movement;
    private CarBoost       _boost;
    private CarHitFeedback _feedback;

    private void Awake()
    {
        _movement = GetComponent<CarMovement>();
        _boost    = GetComponent<CarBoost>();
        _feedback = GetComponent<CarHitFeedback>();
    }

    // === 외부 호출 API ===
    public void ApplySpeedPenalty(float amount)        => _feedback.ApplySpeedPenalty(amount);
    public void UpgradeMaxSpeed(float multiplier)      => _movement.UpgradeMaxSpeed(multiplier);
    public void UpgradeBoostCapacity(float multiplier) => _boost.UpgradeBoostCapacity(multiplier);
    public void UpgradeDriftFuelRate(float multiplier) => _boost.UpgradeDriftFuelRate(multiplier);
    public void AddKillFuel(float amount)              => _boost.AddFuel(amount, "킬");
    public void AddBossFuel(float amount)              => _boost.AddFuel(amount, "보스");

    // === 읽기 전용 프로퍼티 (UI·카메라용) ===
    public float   CurrentSpeed   => _movement.CurrentSpeed;
    public float   MaxSpeed       => _movement.MaxSpeed;
    public Vector3 FlatVelocity   => _movement.FlatVelocity;
    public float   LateralSpeed   => _movement.LateralSpeed;
    public float   BoostFuelRatio => _boost.BoostFuelRatio;
    public bool    IsBoostActive  => _boost.IsBoostActive;
    public bool    IsDrifting     => _movement.IsDrifting;
    public bool    IsGrounded     => _movement.IsGrounded;
    public int     DriftTier      => _boost.DriftTier;
    public float   DriftTimer     => _boost.DriftTimer;
    public bool    DriftTierUp    => _boost.DriftTierUp;
}
