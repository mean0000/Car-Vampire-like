using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>부스트 연료 관리·발동·드리프트 충전</summary>
[DefaultExecutionOrder(-10)] // CarMovement보다 먼저 실행해 SetActiveMaxSpeed 반영
[RequireComponent(typeof(Rigidbody))]
public class CarBoost : MonoBehaviour
{
    [Header("Boost")]
    [SerializeField] private float maxBoostFuel    = 100f;
    [SerializeField] private float boostDrainRate  = 30f;
    [SerializeField] private float driftFuelRate   = 20f;
    [SerializeField] private float boostImpulseSpeed = 38f;
    [SerializeField] private float boostForce      = 15f;
    [SerializeField] private float boostMaxSpeed   = 35f;
    [SerializeField] private float syncRatePerSecond = 0.05f;
    [SerializeField] private float boostShake      = 0.4f;
    [SerializeField] private float boostDecayRate  = 8f;    // 부스트 종료 후 속도 감쇠 속도

    [Header("Passive Drain")]
    [SerializeField] private float passiveDrainRate       = 6f;   // 초당 자연 감소량
    [SerializeField] private float emptyFuelSpeedMult     = 0.4f; // 연료 0일 때 최고속 배율

    [Header("Feel")]
    [SerializeField] private MMF_Player fuelEmptyFeedback;
#if MM_URP
    [SerializeField] private MMSpringChromaticAberrationIntensity_URP _springCA;
#endif

    [Header("Drift Tier Reward")]
    [SerializeField] private float[] driftTierThresholds    = { 1f, 3f, 5f };
    [SerializeField] private float[] driftTierFuelMultipliers = { 1.5f, 2f, 3f };
    [SerializeField] private float[] driftTierExitBonus     = { 10f, 25f, 50f };

    private Rigidbody _rb;
    private CarGroundSensor _ground;
    private CarMovement _movement;
    private CameraController _cam;

    private float _boostFuel;
    private bool _isBoosting;
    private bool _wasBoostingLastFrame;
    private bool _impulseActive;
    private float _postBoostSpeed;
    private bool  _wasFuelEmptyLastFrame;
    private float _driftTimer;
    private int   _driftTier;
    private int   _prevDriftTier;
    private bool  _wasDriftingForReward;

    // 순이동 거리 추적 (원형 주행 패널티)
    private const float NET_DISP_WINDOW    = 6f;   // 추적 윈도우 (초)
    private const float NET_DISP_THRESHOLD = 30f;  // 이 거리 미만이면 효율 감소
    private Vector3 _dispCheckPos;
    private float   _dispTimer;
    private float   _driftEfficiency = 1f;

    public bool  IsBoostActive  => _isBoosting;
    public float BoostFuelRatio => maxBoostFuel > 0f ? _boostFuel / maxBoostFuel : 0f;
    public int   DriftTier      => _driftTier;
    public float DriftTimer     => _driftTimer;
    public bool  DriftTierUp    { get; private set; }

    private void Awake()
    {
        _rb       = GetComponent<Rigidbody>();
        _ground   = GetComponent<CarGroundSensor>();
        _movement = GetComponent<CarMovement>();
        _cam      = GetComponentInChildren<CameraController>();
        if (_cam == null) _cam = FindFirstObjectByType<CameraController>();
        _postBoostSpeed = 0f;
        _dispCheckPos = transform.position;
    }

    private void FixedUpdate()
    {
        // 순이동 거리 추적 (원형 주행 패널티)
        _dispTimer += Time.fixedDeltaTime;
        if (_dispTimer >= NET_DISP_WINDOW)
        {
            float netDist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(_dispCheckPos.x, 0f, _dispCheckPos.z));
            _driftEfficiency = Mathf.Clamp01(netDist / NET_DISP_THRESHOLD);
            _dispCheckPos = transform.position;
            _dispTimer = 0f;
        }

        bool grounded = _ground != null && _ground.IsGrounded;
        Vector3 groundNormal = _ground != null ? _ground.GroundNormal : Vector3.up;
        bool isDrifting = _movement != null && _movement.IsDrifting;

        _isBoosting = Input.GetKey(KeyCode.Space) && _boostFuel > 0f && grounded;

        // 최고속 제한을 CarMovement에 전달 (부스트 종료 후 서서히 감속)
        if (_movement != null)
        {
            if (_isBoosting)
            {
                _postBoostSpeed = boostMaxSpeed;
                _movement.SetActiveMaxSpeed(boostMaxSpeed);
            }
            else if (_postBoostSpeed > _movement.BaseMaxSpeed)
            {
                _postBoostSpeed = Mathf.Lerp(_postBoostSpeed, _movement.BaseMaxSpeed,
                    1f - Mathf.Exp(-boostDecayRate * Time.fixedDeltaTime));
                if (_postBoostSpeed - _movement.BaseMaxSpeed < 0.5f)
                    _postBoostSpeed = _movement.BaseMaxSpeed;
                _movement.SetActiveMaxSpeed(_postBoostSpeed);
            }
            else
            {
                _movement.SetActiveMaxSpeed(_movement.BaseMaxSpeed);
            }
        }

        if (_isBoosting)
        {
            Vector3 bv  = _rb.linearVelocity;
            Vector3 dir = transform.forward;

            if (!_wasBoostingLastFrame)
            {
                _impulseActive = true;
                float impulseTarget = Mathf.Max(new Vector3(bv.x, 0f, bv.z).magnitude, boostImpulseSpeed);
                _rb.linearVelocity = new Vector3(dir.x * impulseTarget, bv.y, dir.z * impulseTarget);
                _cam?.TriggerShake(boostShake);
#if MM_URP
                _springCA?.Bump(0.8f);
#endif
                Debug.Log($"[연료] 드레인 시작 -{boostDrainRate:F0}/s | {_boostFuel:F1}/{maxBoostFuel}");
                OnFuelEvent?.Invoke("드레인", -boostDrainRate);
            }
            else
            {
                _impulseActive = false;
                Vector3 slopeDir = Vector3.ProjectOnPlane(dir, groundNormal);
                if (slopeDir.sqrMagnitude < 0.001f) slopeDir = dir;
                else slopeDir.Normalize();
                _rb.AddForce(slopeDir * boostForce, ForceMode.Acceleration);

                Vector3 after = _rb.linearVelocity;
                Vector3 afterFlat = new Vector3(after.x, 0f, after.z);
                if (afterFlat.magnitude > boostMaxSpeed)
                {
                    Vector3 capped = afterFlat.normalized * boostMaxSpeed;
                    _rb.linearVelocity = new Vector3(capped.x, after.y, capped.z);
                }
            }

            _boostFuel = Mathf.Max(0f, _boostFuel - boostDrainRate * Time.fixedDeltaTime);
            SyncRateManager.Instance?.AddSync(syncRatePerSecond * Time.fixedDeltaTime);
            if (_boostFuel <= 0f)
            {
                _isBoosting = false;
                Debug.Log("[연료] 소진");
                OnFuelEvent?.Invoke("소진", 0f);
            }
        }

        if (!_isBoosting) _impulseActive = false;
        _wasBoostingLastFrame = _isBoosting;

        // ── 패시브 드레인 (부스트 미사용 시 항상 감소) ──
        if (!_isBoosting)
        {
            bool wasFuelEmpty = _boostFuel <= 0f;
            _boostFuel = Mathf.Max(0f, _boostFuel - passiveDrainRate * Time.fixedDeltaTime);
            if (!wasFuelEmpty && _boostFuel <= 0f)
            {
                Debug.Log("[연료] 소진 (패시브 드레인)");
                OnFuelEvent?.Invoke("소진", 0f);
                fuelEmptyFeedback?.PlayFeedbacks();
            }
        }

        // ── 드리프트 티어 보상 시스템 ──
        DriftTierUp = false;

        if (isDrifting && !_isBoosting)
        {
            _driftTimer += Time.fixedDeltaTime;

            // 티어 계산
            _prevDriftTier = _driftTier;
            _driftTier = 0;
            for (int i = driftTierThresholds.Length - 1; i >= 0; i--)
            {
                if (_driftTimer >= driftTierThresholds[i])
                {
                    _driftTier = i + 1;
                    break;
                }
            }

            // 티어 상승 감지
            if (_driftTier > _prevDriftTier)
            {
                DriftTierUp = true;
                _cam?.TriggerShake(0.15f * _driftTier);
#if MM_URP
                _springCA?.Bump(0.25f * _driftTier);
#endif
            }

            // 티어 기반 충전률 적용
            float multiplier = _driftTier > 0 && _driftTier <= driftTierFuelMultipliers.Length
                ? driftTierFuelMultipliers[_driftTier - 1] : 1f;
            _boostFuel = Mathf.Min(_boostFuel + driftFuelRate * multiplier * _driftEfficiency * Time.fixedDeltaTime, maxBoostFuel);
        }
        else
        {
            // 드리프트 종료 시 보상
            if (_wasDriftingForReward && _driftTier > 0 && _driftTier <= driftTierExitBonus.Length)
            {
                float bonus = driftTierExitBonus[_driftTier - 1];
                _boostFuel = Mathf.Min(_boostFuel + bonus, maxBoostFuel);
                _cam?.TriggerShake(0.2f * _driftTier);
                Debug.Log($"[연료] +{bonus:F1} (드리프트 T{_driftTier} 보너스) | {_boostFuel:F1}/{maxBoostFuel}");
                OnFuelEvent?.Invoke($"드리프트 T{_driftTier}", bonus);
            }

            _driftTimer = 0f;
            _driftTier = 0;
            _prevDriftTier = 0;
        }

        _wasDriftingForReward = isDrifting && !_isBoosting;
        _wasFuelEmptyLastFrame = _boostFuel <= 0f;
    }

    /// 연료 변경 이벤트 (source, amount) — 양수=충전, 음수=드레인 시작 알림
    public static event System.Action<string, float> OnFuelEvent;

    public void AddFuel(float amount, string source = "")
    {
        float before = _boostFuel;
        _boostFuel = Mathf.Min(_boostFuel + amount, maxBoostFuel);
        float actual = _boostFuel - before;
        if (actual > 0.01f)
        {
            Debug.Log($"[연료] +{actual:F1} ({source}) | {_boostFuel:F1}/{maxBoostFuel}");
            OnFuelEvent?.Invoke(source, actual);
        }
    }

    public void UpgradeBoostCapacity(float multiplier) => maxBoostFuel  *= multiplier;
    public void UpgradeDriftFuelRate(float multiplier) => driftFuelRate *= multiplier;

#if UNITY_EDITOR
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F)) _boostFuel = maxBoostFuel;
    }
#endif
}
