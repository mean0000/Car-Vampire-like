using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ZombieController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 2f;
    [SerializeField] float speedPenalty = 2f;
    [SerializeField] float stopDistance = 1.5f;

    [Header("Separation")]
    [SerializeField] float separationRadius = 1.5f;
    [SerializeField] float separationStrength = 1.5f;
    [SerializeField] LayerMask zombieLayer;

    [Header("Car Avoidance")]
    [SerializeField] float carAvoidRadius = 3f;
    [SerializeField] float carAvoidStrength = 8f;

    [Header("Smoothing")]
    [SerializeField] float velocitySmoothing = 8f; // 클수록 반응 빠름, 작을수록 부드러움

    [Header("Kill Feedback")]
    [SerializeField] ParticleSystem killParticlePrefab;
    [SerializeField] AudioClip killSound;

    [Header("XP")]
    [SerializeField] GameObject xpOrbPrefab;
    [SerializeField] int orbCountMin = 3;
    [SerializeField] int orbCountMax = 5;

    [Header("Drift Launch")]
    [SerializeField] int maxChainKills = 3;
    [SerializeField] float chainKillRadius = 1.8f;
    [SerializeField] float homingRadius = 8f;
    [SerializeField] float homingStrength = 3f;

    Transform _target;
    Rigidbody _rb;
    Collider _carCollider;
    bool _dead;
    bool _launched;
    int _chainKillCount;
    Vector3 _velocity;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Start()
    {
        if (_target == null)
        {
            CarController car = FindFirstObjectByType<CarController>();
            if (car != null) _target = car.transform;
        }
        if (_target != null)
            _carCollider = _target.GetComponent<Collider>();
    }

    public void Init(Transform target)
    {
        _target = target;
        _carCollider = target != null ? target.GetComponent<Collider>() : null;
    }

    void FixedUpdate()
    {
        if (_dead || _target == null) return;

        Vector3 toTarget = _target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        Vector3 separation = CalcSeparation();
        Vector3 carAvoid  = CalcCarAvoidance();

        // 목표 방향: chase + 좀비 분리 + 차 회피 혼합
        Vector3 desiredDir = (dist > stopDistance ? toTarget.normalized : Vector3.zero)
                           + separation * separationStrength
                           + carAvoid * carAvoidStrength;
        desiredDir.y = 0f;

        Vector3 desiredVel = desiredDir.sqrMagnitude > 0.001f
            ? desiredDir.normalized * moveSpeed
            : Vector3.zero;

        // 저역통과 필터: 급격한 방향 전환을 막아 진동 제거
        _velocity = Vector3.Lerp(_velocity, desiredVel,
            1f - Mathf.Exp(-velocitySmoothing * Time.fixedDeltaTime));

        if (_velocity.sqrMagnitude > 0.0001f)
            _rb.MovePosition(transform.position + _velocity * Time.fixedDeltaTime);
    }

    // 차 콜라이더 표면에서 밀려나는 벡터 — 좀비가 차를 통과하지 못하게
    Vector3 CalcCarAvoidance()
    {
        if (_carCollider == null) return Vector3.zero;

        Vector3 closest = _carCollider.ClosestPoint(transform.position);
        Vector3 away = transform.position - closest;
        away.y = 0f;
        float d = away.magnitude;

        if (d >= carAvoidRadius) return Vector3.zero;
        if (d > 0.001f)
            return away.normalized * (carAvoidRadius / d - 1f);
        else
            return (transform.position - _target.position).normalized * 10f;
    }

    // 주변 좀비들로부터 밀려나는 벡터 (역거리 — 가까울수록 기하급수적으로 강하게)
    Vector3 CalcSeparation()
    {
        Vector3 sep = Vector3.zero;
        Collider[] nearby = Physics.OverlapSphere(transform.position, separationRadius, zombieLayer);
        foreach (var col in nearby)
        {
            if (col.gameObject == gameObject) continue;
            var z = col.GetComponent<ZombieController>();
            if (z != null && z._launched) continue;
            Vector3 away = transform.position - col.transform.position;
            away.y = 0f;
            float d = away.magnitude;
            if (d > 0.001f)
                sep += away.normalized * (separationRadius / d - 1f); // d가 작을수록 급격히 강해짐
        }
        return sep;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_dead) return;

        CarController car = other.GetComponentInParent<CarController>();
        if (car == null) return;
        if (car.CurrentSpeed < 3f) return;

        Vector3 toZombie = transform.position - car.transform.position;
        toZombie.y = 0f;
        Vector3 carDir = car.FlatVelocity.sqrMagnitude > 0.01f
            ? car.FlatVelocity.normalized
            : car.transform.forward;
        if (toZombie.sqrMagnitude > 0.001f &&
            Vector3.Dot(carDir, toZombie.normalized) < -0.5f)
            return;

        _dead = true;
        car.ApplySpeedPenalty(speedPenalty);

        Vector3 pos = transform.position;

        if (killParticlePrefab != null)
            Instantiate(killParticlePrefab, pos, Quaternion.identity);

        if (killSound != null)
            AudioSource.PlayClipAtPoint(killSound, pos);

        SpawnXPOrbs(pos, car.transform);

        if (car.IsDrifting)
        {
            Vector3 lateralDir = car.transform.right * Mathf.Sign(car.LateralSpeed);
            if (Mathf.Abs(car.LateralSpeed) < 0.1f) lateralDir = car.transform.right;
            float lateralSpeed = Mathf.Clamp(Mathf.Abs(car.LateralSpeed), 8f, 15f);
            Launch(lateralDir * lateralSpeed + Vector3.up * 5f);
            DriftComboUI.Instance?.RegisterDriftKill();
        }
        else if (car.IsBoostActive)
        {
            float boostSpeed = Mathf.Clamp(car.CurrentSpeed, 10f, 20f);
            Launch(car.FlatVelocity.normalized * boostSpeed + Vector3.up * 4f);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LaunchKill()
    {
        if (_dead) return;
        _dead = true;

        Vector3 pos = transform.position;

        if (killParticlePrefab != null)
            Instantiate(killParticlePrefab, pos, Quaternion.identity);

        if (killSound != null)
            AudioSource.PlayClipAtPoint(killSound, pos);

        SpawnXPOrbs(pos, _target);

        Destroy(gameObject);
    }

    void SpawnXPOrbs(Vector3 pos, Transform orbTarget)
    {
        if (xpOrbPrefab == null) return;
        int count = Random.Range(orbCountMin, orbCountMax + 1);
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + Random.Range(-30f, 30f);
            Vector3 burstDir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var obj = Instantiate(xpOrbPrefab, pos, Quaternion.identity);
            var orb = obj.GetComponent<XPOrb>();
            if (orb != null) orb.Init(burstDir, orbTarget);
        }
    }

    public void Launch(Vector3 launchVelocity)
    {
        _dead = true;
        _launched = true;
        _rb.isKinematic = false;
        GetComponent<Collider>().isTrigger = false;
        _rb.linearVelocity = launchVelocity;
        Destroy(gameObject, 3f);
        StartCoroutine(ChainKillRoutine());
    }

    System.Collections.IEnumerator ChainKillRoutine()
    {
        while (_chainKillCount < maxChainKills)
        {
            // 근처 좀비 쪽으로 속도 방향 보정
            ZombieController nearest = FindNearestLivingZombie(homingRadius);
            if (nearest != null)
            {
                Vector3 vel = _rb.linearVelocity;
                Vector3 toTarget = (nearest.transform.position - transform.position).normalized;
                Vector3 newDir = Vector3.Lerp(vel.normalized, toTarget, homingStrength * Time.fixedDeltaTime);
                _rb.linearVelocity = newDir * vel.magnitude;
            }

            // 체인킬 판정
            Collider[] hits = Physics.OverlapSphere(transform.position, chainKillRadius, zombieLayer);
            foreach (var col in hits)
            {
                if (_chainKillCount >= maxChainKills) break;
                if (col.gameObject == gameObject) continue;
                var other = col.GetComponent<ZombieController>();
                if (other != null && !other._launched)
                {
                    _chainKillCount++;
                    other.LaunchKill();
                }
            }
            yield return new WaitForFixedUpdate();
        }
    }

    ZombieController FindNearestLivingZombie(float radius)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, zombieLayer);
        ZombieController nearest = null;
        float minDist = float.MaxValue;
        foreach (var col in hits)
        {
            if (col.gameObject == gameObject) continue;
            var z = col.GetComponent<ZombieController>();
            if (z == null || z._dead) continue;
            float d = (col.transform.position - transform.position).sqrMagnitude;
            if (d < minDist) { minDist = d; nearest = z; }
        }
        return nearest;
    }
}
