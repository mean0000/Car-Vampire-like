using System;
using UnityEngine;

/// <summary>
/// 차량 내구도(HULL) 관리 싱글톤.
/// 0 = 파괴(게임오버), maxHull = 완전한 상태.
/// </summary>
[DefaultExecutionOrder(-100)]
public class HullManager : MonoBehaviour
{
    public static HullManager Instance { get; private set; }

    [SerializeField] float maxHull = 100f;
    [SerializeField] float[] zoneMultipliers = { 0.5f, 1.5f, 1.0f, 1.0f }; // Front, Rear, Left, Right

    float _currentHull;
    float[] _zoneDamage = new float[4];

    /// <summary>현재 내구도 비율 (0~1)</summary>
    public float HullRatio => maxHull > 0f ? _currentHull / maxHull : 0f;

    public event Action<float> OnHullChanged;
    public event Action OnHullDepleted;
    public event Action<CarZone, float> OnZoneDamaged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _currentHull = maxHull;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>내구도 감소. 이미 0이면 무시.</summary>
    public void TakeDamage(float amount)
    {
        if (_currentHull <= 0f) return;

        _currentHull = Mathf.Clamp(_currentHull - amount, 0f, maxHull);
        OnHullChanged?.Invoke(HullRatio);

        if (_currentHull <= 0f)
            OnHullDepleted?.Invoke();
    }

    public void TakeDamage(float amount, CarZone zone)
    {
        int idx = (int)zone;
        float multiplier = idx < zoneMultipliers.Length ? zoneMultipliers[idx] : 1f;
        float finalDamage = amount * multiplier;
        _zoneDamage[idx] += finalDamage;
        OnZoneDamaged?.Invoke(zone, finalDamage);
        TakeDamage(finalDamage);
    }

    public float GetZoneDamage(CarZone zone) => _zoneDamage[(int)zone];

    public void Heal(float amount)
    {
        _currentHull = Mathf.Clamp(_currentHull + amount, 0f, maxHull);
        OnHullChanged?.Invoke(HullRatio);
    }
}
