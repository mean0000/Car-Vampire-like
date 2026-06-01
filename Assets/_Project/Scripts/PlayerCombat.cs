using UnityEngine;

/// <summary>
/// 탑다운 마우스 조준 + 원거리(총) 전투. 몸통은 회전시키지 않고(카메라가 플레이어 자식이라
/// 회전 시 화면이 같이 돌아 어지러움), 마우스 지면 투영으로 조준 방향만 계산한다.
/// 발사는 히트스캔(스피어캐스트) — 벽(Obstacle)에 막히고, 좀비를 맞히면 데미지.
/// 총성은 NoiseManager.EmitImpulse로 큰 순간 소음을 내 호드를 끌어모은다(핵심 긴장).
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField] int damage = 2;              // General 좀비 HP 3 → 2발
    [SerializeField] float fireCooldown = 0.35f;
    [SerializeField] float range = 18f;
    [SerializeField] float hitRadius = 0.4f;      // 스피어캐스트 조준 관용
    [SerializeField] float muzzleHeight = 1f;

    [Header("Layers")]
    [SerializeField] LayerMask zombieMask = 1 << 7;
    [SerializeField] LayerMask obstacleMask = 1 << 8;   // 총알이 벽에 막힘

    [Header("Noise")]
    [SerializeField] float gunshotNoise = 90f;    // 추격 임계(50) 훨씬 위 — 쏘면 들킨다

    [Header("Assassinate (무음 근접)")]
    [SerializeField] float assassinateRange = 2.4f;   // 좀비 반경0.5 감안 표면~1.4m — 밀착 안 해도 됨
    [SerializeField] float assassinateCooldown = 0.5f;

    [Header("Assassinate Indicator (greybox)")]
    [SerializeField] float markerHeight = 2.2f;        // 좀비 머리 위 높이
    [SerializeField] float markerSize = 0.35f;         // 역삼각형 반폭
    [SerializeField] float markerBobAmp = 0.12f;       // 위아래 흔들림 진폭
    [SerializeField] float markerBobSpeed = 4f;        // 흔들림 속도
    [SerializeField] float markerPulseSpeed = 6f;      // 알파 깜빡임 속도
    [SerializeField] Color markerColor = new Color(0.4f, 1f, 0.5f, 1f);

    [Header("Aim Line (greybox)")]
    [SerializeField] float aimLineLength = 3f;
    [SerializeField] float aimLineWidth = 0.04f;
    [SerializeField] Color aimColor = new Color(1f, 0.9f, 0.3f, 0.35f);

    [Header("Tracer (greybox)")]
    [SerializeField] float tracerTime = 0.06f;
    [SerializeField] float tracerWidth = 0.12f;
    [SerializeField] Color tracerColor = new Color(1f, 0.85f, 0.3f, 1f);

    Camera _cam;
    float _cooldownTimer;
    float _assassinateTimer;
    Vector3 _aimDir = Vector3.forward;

    LineRenderer _aimLine;
    LineRenderer _tracer;
    float _tracerTimer;

    ZombieController _currentTarget;   // 이번 프레임 F가 죽일 좀비(인디케이터와 공유)
    LineRenderer _marker;              // 머리 위 역삼각형(빌보드)

    void Awake()
    {
        _cam = Camera.main;

        // 시작 무기 선택(WeaponSelect 화면)이 있으면 그 스탯으로 덮어쓴다. 없으면 인스펙터 기본값 유지.
        if (WeaponLoadout.HasSelection)
        {
            var w = WeaponLoadout.Selected;
            damage = w.damage;
            fireCooldown = w.fireCooldown;
            range = w.range;
            gunshotNoise = w.gunshotNoise;
        }

        _aimLine = CreateLine("AimLine", aimLineWidth);
        _aimLine.startColor = aimColor;   // 상수 — 생성 시 1회만
        _aimLine.endColor = aimColor;
        _tracer = CreateLine("Tracer", tracerWidth);
        _tracer.enabled = false;

        _marker = CreateLine("AssassinateMarker", 0.06f);
        _marker.positionCount = 3;   // 역삼각형(loop): 좌상→우상→하단꼭짓점
        _marker.loop = true;
        _marker.enabled = false;
    }

    void OnDestroy()
    {
        if (_aimLine != null) Destroy(_aimLine.material);
        if (_tracer != null) Destroy(_tracer.material);
        if (_marker != null) Destroy(_marker.material);
    }

    LineRenderer CreateLine(string name, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.widthMultiplier = width;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        var sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        lr.material = new Material(sh);
        return lr;
    }

    void Update()
    {
        UpdateAim();

        // 제작 채널링 중에는 무방비 — 사격/암살 모두 잠금(움직임도 잠겨 있음).
        bool crafting = CraftingSystem.Instance != null && CraftingSystem.Instance.IsCrafting;

        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
        if (!crafting && Input.GetMouseButtonDown(0) && _cooldownTimer <= 0f)
            Fire();

        // 매 프레임 암살 대상을 한 번만 찾아 F키와 인디케이터가 동일 좀비를 공유.
        _currentTarget = crafting ? null : FindAssassinationTarget();

        if (_assassinateTimer > 0f) _assassinateTimer -= Time.deltaTime;
        if (!crafting && Input.GetKeyDown(KeyCode.F) && _assassinateTimer <= 0f && _currentTarget != null)
        {
            if (_currentTarget.TryAssassinate())
            {
                _assassinateTimer = assassinateCooldown;   // 성공 시에만 쿨다운
                _currentTarget = null;                     // 죽은 좀비에 인디케이터 잔상 방지
            }
        }

        UpdateIndicator();
        UpdateTracer();
    }

    /// <summary>근접 범위 안에서 F가 죽일 수 있는(무경계 + 시야확보) 가장 가까운 좀비. 없으면 null.</summary>
    ZombieController FindAssassinationTarget()
    {
        // 좀비 콜라이더는 트리거(isTrigger)라 Collide로 탐지해야 한다. Ignore면 0개로 잡힘.
        var hits = Physics.OverlapSphere(transform.position, assassinateRange, zombieMask, QueryTriggerInteraction.Collide);
        ZombieController best = null;
        float bestSqr = float.MaxValue;
        Vector3 origin = transform.position + Vector3.up * muzzleHeight;
        foreach (var h in hits)
        {
            var z = h.GetComponentInParent<ZombieController>();
            if (z == null || !z.IsAssassinable) continue;

            // 벽 너머 좀비 제외 — 총의 벽 막힘과 동일하게 obstacleMask 시야 체크.
            Vector3 dir = z.transform.position + Vector3.up * muzzleHeight - origin;
            float dist = dir.magnitude;
            if (dist > 0.001f && Physics.Raycast(origin, dir / dist, dist, obstacleMask, QueryTriggerInteraction.Ignore))
                continue;

            float d = (z.transform.position - transform.position).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = z; }
        }
        return best;
    }

    void UpdateAim()
    {
        if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

        // 마우스 → 플레이어 높이의 수평면 교차점. 카메라 각도와 무관하게 동작.
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        Plane ground = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (ground.Raycast(ray, out float enter))
        {
            Vector3 hit = ray.GetPoint(enter);
            Vector3 dir = hit - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) _aimDir = dir.normalized;
        }

        Vector3 origin = transform.position + Vector3.up * muzzleHeight;
        _aimLine.SetPosition(0, origin);
        _aimLine.SetPosition(1, origin + _aimDir * aimLineLength);
    }

    void Fire()
    {
        // 속사 카드: 연사 속도 배수만큼 쿨다운 단축.
        _cooldownTimer = fireCooldown / Mathf.Max(0.01f, PlayerStats.FireRateMult);

        Vector3 origin = transform.position + Vector3.up * muzzleHeight;

        // 벽에 먼저 막히는 거리 계산. 스피어캐스트 반경만큼 빼서 벽 모서리를 돌아
        // 뒤쪽 좀비를 맞히는 "관통" 방지.
        float maxDist = range;
        if (Physics.Raycast(origin, _aimDir, out RaycastHit wallHit, range, obstacleMask, QueryTriggerInteraction.Ignore))
            maxDist = Mathf.Max(0f, wallHit.distance - hitRadius);

        // 그 거리 안에서 좀비 스피어캐스트
        Vector3 end;
        if (Physics.SphereCast(origin, hitRadius, _aimDir, out RaycastHit zHit, maxDist, zombieMask, QueryTriggerInteraction.Collide))
        {
            var zombie = zHit.collider.GetComponentInParent<ZombieController>();
            if (zombie != null) zombie.TakeDamage(damage + PlayerStats.DamageBonus);   // 강선 카드: 피해 가산
            // 시작부터 겹친 경우(distance 0, point=원점)엔 트레이서가 월드 원점으로 튀므로 폴백.
            end = zHit.distance > 0f ? zHit.point : origin + _aimDir * Mathf.Min(hitRadius, maxDist);
        }
        else
        {
            end = origin + _aimDir * maxDist;
        }

        NoiseManager.Instance?.EmitImpulse(gunshotNoise);

        ShowTracer(origin, end);
    }

    /// <summary>F가 죽일 수 있는 좀비 머리 위에 빌보드 역삼각형 표식을 띄운다. 대상 없으면 숨김.</summary>
    void UpdateIndicator()
    {
        if (_currentTarget == null || !_currentTarget.IsAssassinable)
        {
            if (_marker.enabled) _marker.enabled = false;
            return;
        }
        if (_cam == null) { _cam = Camera.main; if (_cam == null) { _marker.enabled = false; return; } }

        float bob = Mathf.Sin(Time.time * markerBobSpeed) * markerBobAmp;
        Vector3 center = _currentTarget.transform.position + Vector3.up * (markerHeight + bob);

        // 카메라를 향한 빌보드 평면(우/상 축). 탑다운이라 화면 정면으로 보인다.
        Vector3 right = _cam.transform.right;
        Vector3 up = _cam.transform.up;

        Vector3 topL = center - right * markerSize + up * (markerSize * 0.8f);
        Vector3 topR = center + right * markerSize + up * (markerSize * 0.8f);
        Vector3 bottom = center - up * (markerSize * 0.9f);   // 아래로 향한 꼭짓점

        _marker.SetPosition(0, topL);
        _marker.SetPosition(1, topR);
        _marker.SetPosition(2, bottom);

        // 알파 펄스로 "지금 누르면 된다" 신호.
        float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * markerPulseSpeed));
        Color c = markerColor; c.a *= pulse;
        _marker.startColor = c;
        _marker.endColor = c;
        _marker.enabled = true;
    }

    void ShowTracer(Vector3 from, Vector3 to)
    {
        _tracer.SetPosition(0, from);
        _tracer.SetPosition(1, to);
        _tracer.enabled = true;
        _tracerTimer = tracerTime;
    }

    void UpdateTracer()
    {
        if (!_tracer.enabled) return;
        _tracerTimer -= Time.deltaTime;
        float a = Mathf.Clamp01(_tracerTimer / Mathf.Max(0.0001f, tracerTime));
        Color c = tracerColor; c.a *= a;
        _tracer.startColor = c;
        _tracer.endColor = c;
        if (_tracerTimer <= 0f) _tracer.enabled = false;
    }
}
