using UnityEngine;

/// <summary>
/// 도보 플레이어용 탑다운 카메라 리그.
///
/// 두 가지를 더한다:
/// 1) 커서 리드 — 마우스 커서 쪽으로 카메라 초점을 살짝 끌어 "나와 조준의 공간 관계"를 만든다.
///    트윈스틱 장르(Enter the Gungeon/Nuclear Throne)의 연결감 핵심. 멀리 조준하면 그쪽이 더 보임.
/// 2) 지연 추종 — 리지드 락 대신 SmoothDamp로 살짝 늦게 따라붙어 이동에 카메라가 "반응"하게 한다.
///
/// ★ 카메라가 Player의 자식이면 부모 변위가 매 프레임 먼저 적용돼 지연이 상쇄된다(랙이 안 생김).
///   → Awake에서 월드 트랜스폼을 유지한 채 분리(SetParent(null)). 씬 파일은 안 건드리고 런타임만 분리.
/// 회전/높이/각도(프레이밍)는 시작 시 오프셋을 캡처해 그대로 보존 — XZ 추종만 새로 구동한다.
/// </summary>
[DefaultExecutionOrder(-50)]   // MMWiggle(Feel 쉐이크)보다 먼저 실행 → 쉐이크가 추종 위치 위에 얹힘(차량 CameraController와 동일)
[RequireComponent(typeof(Camera))]
public class PlayerCameraRig : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("따라갈 플레이어. 비우면 PlayerController를 자동 검색.")]
    [SerializeField] Transform target;
    [Tooltip("커서 월드 투영용 PlayerCombat. 비우면 target에서 검색. 없으면 커서 리드 비활성(이동 추종만).")]
    [SerializeField] PlayerCombat aimSource;

    [Header("Cursor Lead (멀미 주의 — 0이면 비활성)")]
    [Tooltip("플레이어↔커서 사이 어디에 카메라 초점을 둘지. 0=플레이어 중앙, 0.4≈커서 쪽 40%. 멀미 유발 핵심 노브.")]
    [SerializeField, Range(0f, 0.6f)] float leadFraction = 0.4f;
    [Tooltip("커서 리드가 밀어낼 수 있는 최대 거리(m). 멀리 조준해도 캐릭터가 화면 밖으로 안 나가게 캡.")]
    [SerializeField, Min(0f)] float leadMaxDistance = 5f;

    [Header("Velocity Lead (이동 방향 예측 — 0이면 비활성)")]
    [Tooltip("이동 방향으로 미리 보는 시간(초). 속도감 보강용. 과하면 화면이 흐물거린다(swimmy).")]
    [SerializeField, Min(0f)] float velocityLeadTime = 0f;
    [Tooltip("속도 리드 최대 거리(m).")]
    [SerializeField, Min(0f)] float velocityLeadMax = 2f;

    [Header("Follow Damping")]
    [Tooltip("SmoothDamp 추종 시간(초). 작을수록 즉각적(생존 장르=작게), 클수록 묵직/지연.")]
    [SerializeField, Min(0f)] float followSmoothTime = 1f;

    Camera _cam;
    Vector3 _baseOffset;     // 시작 시 카메라-플레이어 월드 오프셋(높이/각도 프레이밍 보존)
    Vector3 _lastTargetPos;
    float _velX, _velZ;      // SmoothDamp 내부 속도 상태(축별 분리 — Y 변위가 XZ 수렴에 안 섞이게)
    bool _ready;

    void Awake()
    {
        _cam = GetComponent<Camera>();

        // 부모(Player) 리지드 결합이 지연을 상쇄하므로, 월드 트랜스폼을 유지한 채 분리한다.
        if (transform.parent != null) transform.SetParent(null, true);

        if (target == null)
        {
            var pc = FindObjectOfType<PlayerController>();
            if (pc != null) target = pc.transform;
        }
        if (aimSource == null && target != null) aimSource = target.GetComponent<PlayerCombat>();
    }

    void Start()
    {
        if (target == null) return;
        // 분리 직후의 월드 오프셋 = 기존 프레이밍(높이/뒤로 기운 각도)을 그대로 보존.
        _baseOffset = transform.position - target.position;
        _lastTargetPos = target.position;
        _ready = true;
    }

    void LateUpdate()
    {
        if (!_ready || target == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 p = target.position;

        // --- 커서 리드: 플레이어↔커서 지면 투영점 사이를 leadFraction만큼, 최대 거리로 캡 ---
        Vector3 lead = Vector3.zero;
        if (leadFraction > 0f && _cam != null)
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            Plane ground = new Plane(Vector3.up, new Vector3(0f, p.y, 0f));
            if (ground.Raycast(ray, out float enter))
            {
                Vector3 cursor = ray.GetPoint(enter);
                lead = cursor - p; lead.y = 0f;
                lead *= leadFraction;
                float m = lead.magnitude;
                if (m > leadMaxDistance) lead *= leadMaxDistance / m;
            }
        }

        // --- 속도 리드: 직전 프레임 변위로 이동 방향 예측(PlayerController 비침습 — 위치 차분으로 산출) ---
        Vector3 velLead = Vector3.zero;
        if (velocityLeadTime > 0f)
        {
            Vector3 v = (p - _lastTargetPos) / dt; v.y = 0f;
            velLead = v * velocityLeadTime;
            float m = velLead.magnitude;
            if (m > velocityLeadMax) velLead *= velocityLeadMax / m;
        }
        _lastTargetPos = p;

        // 초점 = 플레이어 + 리드. 카메라 목표 = 초점 + 기존 오프셋(높이/각도 보존).
        Vector3 focus = p + lead + velLead;
        Vector3 desired = focus + _baseOffset;
        desired.y = p.y + _baseOffset.y;   // 높이는 지연 없이 지면을 따라가 상하 흔들림 방지

        // XZ만 지연 추종(각 축 독립 SmoothDamp — Y 변위가 속도 상태에 섞여 XZ 수렴을 흔들지 않게), Y는 즉시 고정.
        float smooth = Mathf.Max(0.0001f, followSmoothTime);   // 0이면 Unity가 내부 클램프하지만 의도 명시
        Vector3 cur = transform.position;
        float nx = Mathf.SmoothDamp(cur.x, desired.x, ref _velX, smooth, Mathf.Infinity, dt);
        float nz = Mathf.SmoothDamp(cur.z, desired.z, ref _velZ, smooth, Mathf.Infinity, dt);
        transform.position = new Vector3(nx, desired.y, nz);
    }
}
