using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI 동료 "엘"의 스캔 펄스. 플레이어를 중심으로 링이 주기적으로 확장되며,
/// 확장하는 전선(front)이 "현재 어그로된" 좀비를 쓸고 지나가면 그 좀비에 위협 태그
/// (시안 아웃라인)를 잠시 켠다. Idle/배회 좀비는 절대 태깅되지 않는다(디제틱 위협 표시).
/// </summary>
public class ScanPulseController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] ZombieSpawner spawner;

    [Header("Pulse")]
    [SerializeField] float pulseInterval = 3f;     // 펄스 간 대기(초)
    [SerializeField] float pulseSpeed = 28f;        // 링 확장 속도(units/sec)
    [SerializeField] float pulseMaxRadius = 32f;    // 이 반경에 도달하면 펄스 종료
    [SerializeField] float ringBand = 2.5f;         // "전선이 쓸고 지나간" 판정 밴드 두께
    [SerializeField] float tagDuration = 2.5f;      // 태그 유지 시간(초)

    [Header("Ring VFX")]
    [SerializeField] bool showRing = true;

    Transform _player;

    bool _pulsing;
    float _intervalTimer;
    float _radius;

    // 태그된 좀비별 남은 유지 시간.
    readonly Dictionary<ZombieController, float> _tagTimers = new Dictionary<ZombieController, float>();
    // dict 순회 중 변형을 피하기 위한 재사용 만료 목록.
    readonly List<ZombieController> _expired = new List<ZombieController>();
    // dict 키 스냅샷용 재사용 버퍼 — 매 프레임 new 할당 방지(GC).
    readonly List<ZombieController> _tagKeys = new List<ZombieController>();

    LineRenderer _ring;
    const int RingSegments = 64;

    void Start()
    {
        if (PlayerController.Instance != null)
            _player = PlayerController.Instance.transform;

        if (spawner == null)
            spawner = FindFirstObjectByType<ZombieSpawner>();
        if (spawner == null)
            Debug.LogWarning("[ScanPulseController] ZombieSpawner를 찾지 못함 — 스캔 펄스가 비활성 상태로 동작합니다.", this);

        BuildRing();
    }

    void OnDestroy()
    {
        // BuildRing에서 만든 런타임 머티리얼 인스턴스 정리(에디터 경고/빌드 누수 방지).
        if (_ring != null && _ring.material != null)
            Destroy(_ring.material);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        UpdatePulse(dt);
        UpdateTagTimers(dt);
        UpdateRingVisual();
    }

    // ──────────── Pulse ────────────

    void UpdatePulse(float dt)
    {
        if (_player == null || spawner == null) return;

        if (!_pulsing)
        {
            _intervalTimer += dt;
            if (_intervalTimer >= pulseInterval)
            {
                _intervalTimer = 0f;
                _pulsing = true;
                _radius = 0f;
            }
            return;
        }

        float prevRadius = _radius;
        _radius += pulseSpeed * dt;

        Vector3 playerPos = _player.position;
        var zombies = spawner.ActiveZombies;
        for (int i = 0; i < zombies.Count; i++)
        {
            var z = zombies[i];
            if (z == null || z.IsDead || !z.IsAggro) continue;

            float d = FlatDistance(z.transform.position, playerPos);

            // 이 프레임에 전선이 좀비를 쓸고 지나갔는가:
            // 직전 반경(밴드 안쪽 경계) 너머에 있었고, 현재 반경(밴드 바깥 경계) 안에 들어왔으면 적중.
            if (d > prevRadius - ringBand && d <= _radius + ringBand)
            {
                _tagTimers[z] = tagDuration;
                z.SetScanTagged(true);
            }
        }

        if (_radius >= pulseMaxRadius)
            _pulsing = false;
    }

    // ──────────── Tag Lifetime ────────────

    void UpdateTagTimers(float dt)
    {
        if (_tagTimers.Count == 0) return;

        _expired.Clear();
        // 키 변형 없이 값만 감소 — 만료/소멸 키는 따로 모았다가 나중에 제거.
        _tagKeys.Clear();
        _tagKeys.AddRange(_tagTimers.Keys);
        for (int i = 0; i < _tagKeys.Count; i++)
        {
            var z = _tagKeys[i];
            if (z == null)
            {
                _expired.Add(z);
                continue;
            }

            float t = _tagTimers[z] - dt;
            if (t <= 0f)
                _expired.Add(z);
            else
                _tagTimers[z] = t;
        }

        for (int i = 0; i < _expired.Count; i++)
        {
            var z = _expired[i];
            if (z != null) z.SetScanTagged(false);
            _tagTimers.Remove(z);
        }
    }

    // ──────────── Ring VFX ────────────

    void BuildRing()
    {
        var go = new GameObject("ScanPulseRing");
        go.transform.SetParent(transform, false);

        _ring = go.AddComponent<LineRenderer>();
        _ring.loop = true;
        _ring.positionCount = RingSegments;
        _ring.widthMultiplier = 0.12f;
        _ring.useWorldSpace = true;
        _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _ring.receiveShadows = false;
        _ring.alignment = LineAlignment.View;

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit != null)
        {
            var mat = new Material(unlit);
            Color cyan = new Color(0.05f, 0.9f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cyan);
            else mat.color = cyan;
            _ring.material = mat;
        }

        _ring.enabled = false;
    }

    void UpdateRingVisual()
    {
        if (_ring == null) return;

        bool visible = showRing && _pulsing && _player != null;
        if (_ring.enabled != visible)
            _ring.enabled = visible;
        if (!visible) return;

        Vector3 c = _player.position;
        float y = c.y + 0.06f;
        for (int i = 0; i < RingSegments; i++)
        {
            float a = (i / (float)RingSegments) * Mathf.PI * 2f;
            _ring.SetPosition(i, new Vector3(
                c.x + Mathf.Cos(a) * _radius,
                y,
                c.z + Mathf.Sin(a) * _radius));
        }
    }

    // ──────────── Utility ────────────

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
