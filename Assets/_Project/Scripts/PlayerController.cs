using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("HP")]
    [SerializeField] float maxHP = 100f;
    float _currentHP;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float runMultiplier = 1.7f;    // Shift 달리기 속도 배율
    [SerializeField] float crouchMultiplier = 0.5f; // Ctrl 앉기 속도 배율 (느림)
    [SerializeField] LayerMask groundLayer = 1 << 6;

    [Header("Noise (이동 중 지속 소음 레벨)")]
    [SerializeField] float crouchNoiseLevel = 2f;  // 앉기: 거의 무음(반경 ~0.5) — 암살 접근용. 닿을 듯해야 들림
    [SerializeField] float walkNoiseLevel = 22f;   // 걷기: 근처 2~3마리는 반응(반경 ~5.5) — 기본 이동에 긴장
    [SerializeField] float runNoiseLevel = 70f;    // 달리기: 추격 임계(50) 초과 — 확 커지는 위험한 스파이크

    float _groundOffset;

    public float CurrentHP => _currentHP;
    public float MaxHP => maxHP * PlayerStats.MaxHPMult;   // 강화 골격 카드 반영

    public event System.Action OnPlayerDied;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        PlayerStats.Reset();   // 매 런(씬 1회) 시작 시 카드 보정 초기화
        _currentHP = maxHP;
    }

    void Start()
    {
        // ★ 월드 바운드가 확정된 뒤(Start) 계산 — Awake 시 스케일/바운드 미확정 가능성 회피
        var col = GetComponent<Collider>();
        _groundOffset = col != null ? col.bounds.center.y - col.bounds.min.y : 0f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        UpdateMovement();
    }

    void UpdateMovement()
    {
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 1f) input.Normalize();

        bool moving = input.sqrMagnitude > 0.001f;
        bool crouching = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool running = moving && !crouching && Input.GetKey(KeyCode.LeftShift); // 앉은 채로는 못 달림

        if (moving)
        {
            float mult = crouching ? crouchMultiplier : (running ? runMultiplier : 1f);
            mult *= PlayerStats.MoveSpeedMult;   // 경량화 카드 반영
            Vector3 next = transform.position + input * (moveSpeed * mult * Time.deltaTime);
            next.y = SampleGroundHeight(next) + _groundOffset;
            transform.position = next;
        }

        // 지속 소음을 매 프레임 갱신 — 정지=0, 앉기<걷기<달리기.
        // NoiseManager가 attack/release 엔벨로프로 "확 커졌다 확 줄어드는" 질감을 만든다.
        float noiseLevel;
        if (!moving) noiseLevel = 0f;
        else if (crouching) noiseLevel = crouchNoiseLevel;
        else if (running) noiseLevel = runNoiseLevel;
        else noiseLevel = walkNoiseLevel;
        NoiseManager.Instance?.SetMovementNoise(noiseLevel);
    }

    float SampleGroundHeight(Vector3 pos)
    {
        Vector3 origin = new Vector3(pos.x, 200f, pos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f, groundLayer, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        // ★ M3 fix: 레이 미스 시 현재 지면 높이 유지 (next.y += _groundOffset 후 드리프트 누적 방지)
        return transform.position.y - _groundOffset;
    }

    public void TakeDamage(float amount)
    {
        if (_currentHP <= 0f) return;   // 이미 사망 — 같은 프레임 다중 타격 시 OnPlayerDied 중복 발화 방지
        _currentHP -= amount;
        if (_currentHP <= 0f)
        {
            _currentHP = 0f;
            OnPlayerDied?.Invoke();
        }
    }

    /// <summary>구급상자 제작 등으로 회복. maxHP를 넘지 않도록 클램프.</summary>
    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        _currentHP = Mathf.Min(MaxHP, _currentHP + amount);
    }

    /// <summary>강화 골격 카드로 MaxHPMult가 오를 때 호출. 늘어난 최대치만큼 현재 체력도 채워 즉시 체감되게 한다.</summary>
    public void RaiseMaxHP(float oldMult, float newMult)
    {
        if (_currentHP <= 0f) return;   // 사망 상태에선 최대치만 오르고 부활시키지 않는다
        float added = maxHP * (newMult - oldMult);
        if (added > 0f) _currentHP = Mathf.Min(MaxHP, _currentHP + added);
    }
}
