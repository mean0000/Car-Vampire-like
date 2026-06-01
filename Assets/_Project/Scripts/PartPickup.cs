using UnityEngine;
using DG.Tweening;

/// <summary>
/// 좀비가 죽은 자리에 떨어지는 물리 부품 픽업. 팝(아치) → 정착(스핀+수명) → 자석(흡입) 3상태.
/// 추상 카운터를 "죽은 자리까지 가서 줍는" 위치 결정으로 바꿔 루프에 깊이를 준다.
/// 수집은 무음·무비용 — 긴장은 순전히 "그 위험한 자리로 가야 하는가"라는 위치에서 온다.
/// </summary>
public class PartPickup : MonoBehaviour
{
    [SerializeField] int value = 1;

    [Header("Pop (스폰 시 시체에서 튀어나오는 아치)")]
    [SerializeField] float popScatter = 0.8f;    // 착지 수평 분산 — 더블드롭이 둘로 갈라지게
    [SerializeField] float popJumpPower = 1.2f;
    [SerializeField] float popDuration = 0.45f;
    [SerializeField] float restHeight = 0.25f;   // 지면 위 떠 있는 높이 — 보이게

    [Header("Collect (자석)")]
    [SerializeField] float magnetRadius = 2.5f;  // 이 안에 들면 흡입 시작 — 맵 전체 진공 아님(가야 함)
    [SerializeField] float collectRadius = 0.6f;
    [SerializeField] float magnetAccel = 28f;    // 흡입 가속 — 빨려드는 손맛
    [SerializeField] float magnetMaxSpeed = 16f;

    [Header("Life")]
    [SerializeField] float lifetime = 25f;       // 이 시간 뒤 소멸 — 월드 정리 + 약한 긴급
    [SerializeField] float blinkLast = 4f;       // 소멸 직전 깜빡임 경고
    [SerializeField] float spinSpeed = 120f;

    [SerializeField] LayerMask groundMask = 1 << 6;

    enum State { Pop, Idle, Magnet }
    State _state;
    Transform _player;
    float _age;
    float _magnetSpeed;
    Tween _popTween;
    Renderer[] _renderers;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _state = State.Pop;
    }

    void Start()
    {
        _player = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
    }

    void OnDestroy()
    {
        _popTween?.Kill();
        transform.DOKill();
    }

    /// <summary>스폰 직후 NotifyKill에서 호출. horizontalDir은 흩어질 XZ 방향.</summary>
    public void Init(Vector3 horizontalDir)
    {
        Vector3 landing = transform.position + horizontalDir.normalized * popScatter;
        landing.y = SampleGround(landing) + restHeight;
        _popTween = transform.DOJump(landing, popJumpPower, 1, popDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => { if (_state == State.Pop) _state = State.Idle; });
    }

    void Update()
    {
        _age += Time.deltaTime;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        // 수명/깜빡임 — 자석 단계에선 무시(빨려드는 중엔 안 사라짐)
        if (_state != State.Magnet)
        {
            if (_age >= lifetime) { Destroy(gameObject); return; }
            float remain = lifetime - _age;
            if (remain <= blinkLast) SetVisible(Mathf.FloorToInt(remain * 8f) % 2 == 0);
        }

        if (_player == null) return;

        Vector3 to = _player.position - transform.position;
        to.y = 0f;

        if (_state != State.Magnet)
        {
            float r = magnetRadius * PlayerStats.PickupRadiusMult;   // 자력 채집 카드
            if (to.sqrMagnitude <= r * r)
            {
                _popTween?.Kill();           // 팝 도중이어도 자석이 가로챈다
                _state = State.Magnet;
                _magnetSpeed = 0f;
                SetVisible(true);            // 깜빡임 중이었어도 흡입 시작하면 보이게
            }
            return;
        }

        // Magnet: 가속하며 플레이어로 빨려든다
        _magnetSpeed = Mathf.Min(magnetMaxSpeed, _magnetSpeed + magnetAccel * Time.deltaTime);
        transform.position = Vector3.MoveTowards(transform.position, _player.position, _magnetSpeed * Time.deltaTime);

        if ((_player.position - transform.position).sqrMagnitude <= collectRadius * collectRadius)
            Collect();
    }

    void Collect()
    {
        CraftingSystem.Instance?.AddParts(value);
        Destroy(gameObject);
    }

    void SetVisible(bool v)
    {
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].enabled = v;
    }

    float SampleGround(Vector3 pos)
    {
        Vector3 origin = new Vector3(pos.x, 200f, pos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return transform.position.y;
    }
}
