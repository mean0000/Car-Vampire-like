using UnityEngine;

/// <summary>
/// 탑다운 마우스 조준 전투. 몸통은 회전시키지 않고(카메라가 플레이어 자식이라
/// 회전 시 화면이 같이 돌아 어지러움), 마우스 지면 투영으로 조준 방향만 계산한다.
///
/// 무기 종류(WeaponLoadout.Kind)에 따라 분기한다:
/// - Ranged(총): 좌클릭 홀드 = 쿨마다 히트스캔(스피어캐스트). 벽에 막히고 좀비를 맞히면 데미지.
///   총성은 NoiseManager로 큰 순간 소음 → 호드 유발(핵심 긴장).
/// - Melee(근접): MeleeAttacker가 스윙/판정/넉백/연출을 전담. 좌클릭 홀드 = 쿨마다 자동 스윙.
///   이동이 스윙을 막지 않아 무빙하며 패기(run-and-bash). 소음은 낮다(근접의 정체성).
///
/// 입력은 두 무기 모두 「좌클릭 홀드 = 쿨마다 반복」으로 통일했다.
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

    [Header("Aim Line (greybox)")]
    [SerializeField] float aimLineLength = 3f;
    [SerializeField] float aimLineWidth = 0.04f;
    [SerializeField] Color aimColor = new Color(1f, 0.9f, 0.3f, 0.35f);

    [Header("Tracer (greybox)")]
    [SerializeField] float tracerTime = 0.06f;
    [SerializeField] float tracerWidth = 0.12f;
    [SerializeField] Color tracerColor = new Color(1f, 0.85f, 0.3f, 1f);

    [Header("Muzzle Flash")]
    [SerializeField] GunFlashLight gunFlashLight;

    [Header("Debug")]
    [SerializeField] KeyCode evolveKey = KeyCode.T;   // 데모용: 방망이 → 쇠지렛대 라이브 진화

    Camera _cam;
    float _cooldownTimer;
    Vector3 _aimDir = Vector3.forward;

    WeaponLoadout.Kind _kind = WeaponLoadout.Kind.Ranged;
    MeleeAttacker _melee;   // 근접일 때만 생성(원거리는 null)

    LineRenderer _aimLine;
    LineRenderer _tracer;
    float _tracerTimer;

    void Awake()
    {
        _cam = Camera.main;

        // 시작 무기 선택(WeaponSelect 화면)이 있으면 그 스탯으로 덮어쓴다. 없으면 인스펙터 기본값 유지.
        if (WeaponLoadout.HasSelection)
        {
            var w = WeaponLoadout.Selected;
            _kind = w.kind;
            damage = w.damage;
            fireCooldown = w.fireCooldown;
            range = w.range;
            gunshotNoise = w.gunshotNoise;

            if (_kind == WeaponLoadout.Kind.Melee)
                _melee = new MeleeAttacker(transform, w, muzzleHeight, zombieMask, obstacleMask);
        }

        _aimLine = CreateLine("AimLine", aimLineWidth);
        _aimLine.startColor = aimColor;   // 상수 — 생성 시 1회만
        _aimLine.endColor = aimColor;

        // 트레이서는 원거리 전용. 근접이면 만들지 않는다.
        if (_kind == WeaponLoadout.Kind.Ranged)
        {
            _tracer = CreateLine("Tracer", tracerWidth);
            _tracer.enabled = false;
        }
    }

    void OnDestroy()
    {
        if (_aimLine != null) Destroy(_aimLine.material);
        if (_tracer != null) Destroy(_tracer.material);
        _melee?.Cleanup();
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

        // 제작 채널링 중에는 무방비 — 사격/스윙 모두 잠금(움직임도 잠겨 있음).
        bool crafting = CraftingSystem.Instance != null && CraftingSystem.Instance.IsCrafting;
        bool attackHeld = Input.GetMouseButton(0);

        if (_kind == WeaponLoadout.Kind.Melee)
        {
            // 근접: 입력/조준/잠금만 넘기고 쿨·판정·연출은 MeleeAttacker가 전담.
            _melee.Tick(attackHeld, _aimDir, crafting);

            // 데모: 디버그키로 방망이 → 쇠지렛대 라이브 진화.
            if (Input.GetKeyDown(evolveKey) && _melee != null)
                _melee.Evolve(WeaponLoadout.EvolvedCrowbar);
        }
        else
        {
            // 원거리: 좌클릭 홀드 = 쿨마다 자동 발사(근접과 입력 통일).
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
            if (!crafting && attackHeld && _cooldownTimer <= 0f)
                Fire();

            UpdateTracer();
        }
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

        gunFlashLight?.Trigger();
        ShowTracer(origin, end);
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
