using UnityEngine;

/// <summary>
/// 나노봇 투사체. 포물선 비행 → 착탄 지점 범위 데미지.
/// 차징 중 탄착 예고 원(indicatorPrefab)이 지면에 표시된다.
/// </summary>
public class ZombieProjectile : MonoBehaviour
{
    [SerializeField] float arcHeight = 6f;
    [SerializeField] float lifetime = 5f;

    Vector3 _start;
    Vector3 _target;
    float _damage;
    float _blastRadius;
    float _flightDuration;
    float _elapsed;
    bool _landed;
    GameObject _indicator;

    public void Init(Vector3 startPos, Vector3 targetPos, float damage, float flightDuration, float blastRadius)
    {
        _start = startPos;
        _target = targetPos;
        _damage = damage;
        _blastRadius = blastRadius;
        _flightDuration = Mathf.Max(flightDuration, 0.05f);
        _elapsed = 0f;
        transform.position = _start;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _flightDuration);

        // 수평 보간
        Vector3 pos = Vector3.Lerp(_start, _target, t);
        // 포물선 높이 (0→peak→0)
        pos.y += arcHeight * 4f * t * (1f - t);

        transform.position = pos;

        // 진행 방향으로 회전
        if (t < 1f)
        {
            float nextT = Mathf.Clamp01(t + 0.01f);
            Vector3 nextPos = Vector3.Lerp(_start, _target, nextT);
            nextPos.y += arcHeight * 4f * nextT * (1f - nextT);
            Vector3 dir = nextPos - pos;
            if (dir.sqrMagnitude > 0.0001f)
                transform.forward = dir.normalized;
        }

        // 착탄
        if (t >= 1f)
        {
            OnLand();
        }
    }

    void OnLand()
    {
        if (_landed) return;
        _landed = true;

        // 범위 내 차량 검색
        Collider[] hits = Physics.OverlapSphere(_target, _blastRadius);
        foreach (var col in hits)
        {
            CarController car = col.GetComponentInParent<CarController>();
            if (car == null) continue;

            CarZone zone = CarZoneHelper.Resolve(car.transform, _target);
            HullManager.Instance?.TakeDamage(_damage, zone);
            break; // 차량 1회만
        }

        if (_indicator != null) Destroy(_indicator);
        Destroy(gameObject);
    }

    public void SetIndicator(GameObject indicator)
    {
        _indicator = indicator;
    }

    void OnDestroy()
    {
        if (_indicator != null) Destroy(_indicator);
    }
}
