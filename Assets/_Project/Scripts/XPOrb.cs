using UnityEngine;

/// <summary>
/// 좀비 킬 시 드랍되는 XP 오브.
/// Burst → Float → Attract 3단계로 나노봇 채취 느낌을 구현.
/// </summary>
public class XPOrb : MonoBehaviour
{
    [SerializeField] float burstSpeed = 10f;
    [SerializeField] float burstDuration = 0.3f;
    [SerializeField] float floatHeight = 0.6f;
    [SerializeField] float floatDuration = 1.5f;
    [SerializeField] float attractRadius = 8f;
    [SerializeField] float arcHeight = 8f;
    [SerializeField] float fleeDistance = 1.5f;  // P1: 차 반대 방향으로 얼마나 도망
    [SerializeField] float windUpLift = 2f;       // P1: 위로 얼마나 뜨는지
    [SerializeField] float wanderSpeed = 0.3f;
    [SerializeField][Min(0.01f)] float attractDuration = 0.7f;
    [SerializeField] float absorbRadius = 0.8f;
    [SerializeField] int xpValue = 1;

    enum Phase { Burst, Float, Attract }
    Phase _phase;
    Vector3 _burstDir;
    Vector3 _floatOrigin;
    float _phaseTimer;
    Transform _carTransform;
    Vector3 _attractOrigin;
    Vector3 _attractP1; // 도망 제어점 (Attract 진입 시 고정)
    Vector3 _attractP2; // 아치 제어점 (Attract 진입 시 고정)
    float _attractElapsed;
    Vector3 _wanderDir;
    float _wanderTimer;

    /// <summary>
    /// 스폰 직후 ZombieController에서 호출. burstDir은 XZ 평면 방향.
    /// </summary>
    public void Init(Vector3 burstDir, Transform carTransform)
    {
        _burstDir = burstDir.normalized;
        _carTransform = carTransform;
        _phase = Phase.Burst;
        _phaseTimer = 0f;
        _wanderDir = Random.insideUnitSphere;
        _wanderDir.y = 0f;
        if (_wanderDir.sqrMagnitude < 0.001f) _wanderDir = Vector3.right;
        else _wanderDir.Normalize();
        _wanderTimer = 0f;
    }

    void Start()
    {
        // Init 없이 씬에 직접 배치된 경우 fallback
        if (_carTransform == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null) _carTransform = player.transform;
        }
    }

    void Update()
    {
        switch (_phase)
        {
            case Phase.Burst:   UpdateBurst();   break;
            case Phase.Float:   UpdateFloat();   break;
            case Phase.Attract: UpdateAttract(); break;
        }
    }

    void UpdateBurst()
    {
        if (burstDuration <= 0f) { _floatOrigin = transform.position; _phase = Phase.Float; return; }

        _phaseTimer += Time.deltaTime;

        float t = _phaseTimer / burstDuration;
        float speed = burstSpeed * Mathf.Max(0f, 1f - t); // 점점 감속, 음수 방지
        transform.position += _burstDir * speed * Time.deltaTime;

        if (_phaseTimer >= burstDuration)
        {
            _floatOrigin = transform.position;
            _phaseTimer = 0f;
            _phase = Phase.Float;
        }
    }

    void UpdateFloat()
    {
        _phaseTimer += Time.deltaTime;

        // y를 0.3초에 걸쳐 부드럽게 올림
        float targetY = _floatOrigin.y + floatHeight * Mathf.Clamp01(_phaseTimer / 0.3f);
        transform.position = new Vector3(transform.position.x, targetY, transform.position.z);

        // 방랑: 0.6초마다 랜덤 방향 전환
        _wanderTimer += Time.deltaTime;
        if (_wanderTimer >= 0.6f)
        {
            _wanderTimer = 0f;
            _wanderDir = Random.insideUnitSphere;
            _wanderDir.y = 0f;
            if (_wanderDir.sqrMagnitude < 0.001f) _wanderDir = Vector3.right;
            else _wanderDir.Normalize();
        }
        Vector3 xzPos = new Vector3(transform.position.x, 0f, transform.position.z);
        xzPos += _wanderDir * wanderSpeed * Time.deltaTime;
        transform.position = new Vector3(xzPos.x, transform.position.y, xzPos.z);

        // 차량이 attractRadius 내에 들어오면 즉시 흡수 단계로 전환
        if (_carTransform != null)
        {
            Vector3 tocar = _carTransform.position - transform.position;
            tocar.y = 0f;
            if (tocar.magnitude < attractRadius * PlayerStats.PickupRadiusMult)   // 자력 채집 카드
            {
                _attractOrigin = transform.position;
                Vector3 tocarDir = tocar;
                tocarDir.y = 0f;
                Vector3 awayDir = tocarDir.sqrMagnitude > 0.001f ? -tocarDir.normalized : Vector3.back;
                _attractP1 = _attractOrigin + awayDir * fleeDistance + Vector3.up * windUpLift;
                _attractP2 = (_attractOrigin + _carTransform.position) * 0.5f + Vector3.up * arcHeight;
                _attractElapsed = 0f;
                _phase = Phase.Attract;
                return;
            }
        }

        if (_phaseTimer >= floatDuration)
        {
            _attractOrigin = transform.position;
            Vector3 tocar2 = _carTransform != null ? (_carTransform.position - transform.position) : Vector3.back;
            tocar2.y = 0f;
            Vector3 awayDir2 = tocar2.sqrMagnitude > 0.001f ? -tocar2.normalized : Vector3.back;
            _attractP1 = _attractOrigin + awayDir2 * fleeDistance + Vector3.up * windUpLift;
            _attractP2 = _carTransform != null
                ? (_attractOrigin + _carTransform.position) * 0.5f + Vector3.up * arcHeight
                : _attractOrigin + Vector3.up * arcHeight;
            _attractElapsed = 0f;
            _phase = Phase.Attract;
        }
    }

    void UpdateAttract()
    {
        if (_carTransform == null) { Destroy(gameObject); return; }

        _attractElapsed += Time.deltaTime;
        float rawT = Mathf.Clamp01(_attractElapsed / Mathf.Max(attractDuration, 0.01f));
        float t = rawT * rawT * rawT; // cubic ease-in

        Vector3 carPos = _carTransform.position;

        // Cubic Bezier: P0 → P1(도망) → P2(아치) → P3(차)
        float u = 1f - t;
        transform.position = u * u * u * _attractOrigin
                           + 3f * u * u * t * _attractP1
                           + 3f * u * t * t * _attractP2
                           + t * t * t * carPos;

        if (rawT >= 0.98f)
        {
            if (XPManager.Instance != null) XPManager.Instance.AddXP(xpValue);
            Destroy(gameObject);
        }
    }
}
