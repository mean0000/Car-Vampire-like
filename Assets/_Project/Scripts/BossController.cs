using UnityEngine;

/// <summary>
/// 보스 몬스터 — 차량이 접촉 중인 동안 연료를 지속 충전.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BossController : MonoBehaviour
{
    [SerializeField] float fuelPerSecond = 40f;

    private CarController _car;

    void OnTriggerEnter(Collider other)
    {
        if (_car != null) return;
        _car = other.GetComponentInParent<CarController>();
    }

    void OnTriggerExit(Collider other)
    {
        if (_car == null) return;
        if (other.GetComponentInParent<CarController>() == _car)
            _car = null;
    }

    void FixedUpdate()
    {
        if (_car == null) return;
        _car.AddBossFuel(fuelPerSecond * Time.fixedDeltaTime);
    }
}
