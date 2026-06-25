using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ★Skill01 차징 팬텀 — 차징 중(<see cref="KatanaWeapon.IsCharging"/>) 플레이어 포즈를 BakeMesh로 굽고
/// 시안 고스트(ZombieCrush/AfterimageGhost) 1장을 만들어 *전방으로 드리프트 + 산란*시키며 페이드한다.
/// 대시 잔상(<see cref="PlayerAfterimage"/>)이 제자리에 남는 것과 달리, 차징 팬텀은 앞으로 슉 날아간다("슉슉슉").
///
/// 진행: 비트2a = 현재 포즈 베이크(파이프라인 증명). 비트2b = Attack01/02/03 포즈로 교체. 비트2c = 슬래시 VFX 동반.
/// PlayerAfterimage의 GC≈0 골격(영속 베이크 버퍼·CombineInstance 재사용·고스트 풀·프리워밍) 재사용.
/// SkinnedMeshRenderer 루트(Visual)에 붙인다. 방향은 차징 시작 시 잠근 <see cref="KatanaWeapon.ChargeAimDir"/>.
/// </summary>
public class ChargePhantomEmitter : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("차징 상태 소스. 비우면 부모/루트에서 KatanaWeapon 탐색.")]
    [SerializeField] KatanaWeapon weapon;
    [Tooltip("고스트 셰이더(비우면 ZombieCrush/AfterimageGhost 자동 탐색).")]
    [SerializeField] Shader ghostShader;

    [Header("방출")]
    [Tooltip("팬텀 1장 간격(초). 0.08 ≈ 차징 0.83s 동안 ~10장.")]
    [SerializeField] float interval = 0.08f;

    [Header("드리프트 (전방 + 산란)")]
    [Tooltip("전방(ChargeAimDir) 드리프트 속도(m/s).")]
    [SerializeField] float forwardSpeed = 6f;
    [Tooltip("좌우 산란 속도 폭(±m/s) — 슉슉 흩어지는 느낌.")]
    [SerializeField] float scatter = 1.2f;

    [Header("고스트")]
    [Tooltip("팬텀 1장 페이드 시간(초).")]
    [SerializeField] float lifetime = 0.45f;
    [Tooltip("고스트 색(시안=액션 규약, HDR — 블룸 통과).")]
    [ColorUsage(true, true)]
    [SerializeField] Color ghostColor = new Color(0.3f, 1.4f, 1.7f, 1f);
    [Tooltip("고스트 시작 알파.")]
    [SerializeField, Range(0f, 1f)] float startAlpha = 0.7f;

    // --- 재사용 버퍼(영속) ---
    SkinnedMeshRenderer[] _renderers;
    Mesh[] _bakeBuffers;
    int[] _combineCount;
    CombineInstance[] _combine;
    bool[] _prevActive;
    bool _layoutDirty = true;

    Material _ghostMatTemplate;
    static readonly int AlphaID = Shader.PropertyToID("_Alpha");
    static readonly int ColorID = Shader.PropertyToID("_BaseColor");

    readonly List<Phantom> _pool = new List<Phantom>();
    float _spawnTimer;

    class Phantom
    {
        public GameObject go;
        public MeshFilter mf;
        public Material mat;
        public Mesh mesh;
        public float age;
        public bool active;
        public Vector3 pos;   // 누적 드리프트 오프셋(메시에 월드좌표가 구워져 GO 이동으로 전체 평행이동)
        public Vector3 vel;   // 전방 + 산란 속도
    }

    void Awake()
    {
        interval = Mathf.Max(0.01f, interval);
        if (weapon == null) weapon = GetComponentInParent<KatanaWeapon>();
        if (weapon == null && transform.root != null) weapon = transform.root.GetComponentInChildren<KatanaWeapon>(true);
        if (weapon == null) { Debug.LogError("[ChargePhantomEmitter] KatanaWeapon 미연결 — 팬텀 비활성. Inspector/계층 확인.", this); enabled = false; return; }

        var all = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var live = new List<SkinnedMeshRenderer>(all.Length);
        foreach (var s in all) if (s != null && s.sharedMesh != null) live.Add(s);   // 활성 여부는 스폰 시 판단(슬래시 FX 메시는 평시 비활성 → 자연 제외)
        _renderers = live.ToArray();
        if (_renderers.Length == 0) { Debug.LogWarning("[ChargePhantomEmitter] SkinnedMeshRenderer 미발견 — 비활성.", this); enabled = false; return; }

        _bakeBuffers = new Mesh[_renderers.Length];
        _combineCount = new int[_renderers.Length];
        _prevActive = new bool[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            _bakeBuffers[i] = new Mesh();
            _combineCount[i] = _renderers[i].sharedMesh.subMeshCount;
        }

        _ghostMatTemplate = CreateGhostMaterial();
        if (_ghostMatTemplate == null) { enabled = false; return; }

        int warm = Mathf.CeilToInt(Mathf.Max(0.0001f, lifetime) / interval) + 1;
        for (int i = 0; i < warm; i++) CreatePhantom();
    }

    void OnDisable()
    {
        foreach (var p in _pool) { p.active = false; if (p.go != null) p.go.SetActive(false); }
        _spawnTimer = 0f;
    }

    void OnDestroy()
    {
        if (_ghostMatTemplate != null) Destroy(_ghostMatTemplate);
        if (_bakeBuffers != null) foreach (var m in _bakeBuffers) if (m != null) Destroy(m);
        foreach (var p in _pool)
        {
            if (p.mesh != null) Destroy(p.mesh);
            if (p.mat != null) Destroy(p.mat);
            if (p.go != null) Destroy(p.go);
        }
        _pool.Clear();
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        float life = Mathf.Max(0.0001f, lifetime);

        // 1) 살아있는 팬텀 드리프트 + 페이드
        for (int i = 0; i < _pool.Count; i++)
        {
            var p = _pool[i];
            if (!p.active) continue;
            p.age += dt;
            float t = p.age / life;
            if (t >= 1f) { p.active = false; if (p.go != null) p.go.SetActive(false); continue; }
            p.pos += p.vel * dt;
            if (p.go != null) p.go.transform.position = p.pos;
            p.mat.SetFloat(AlphaID, startAlpha * (1f - t));
        }

        // 2) 차징 중이면 방출(IsCharging일 때만 — ChargeAimDir도 이때만 유효).
        if (weapon == null || !weapon.IsCharging) { _spawnTimer = 0f; return; }
        _spawnTimer -= dt;
        if (_spawnTimer > 0f) return;
        _spawnTimer = interval;
        SpawnPhantom();
    }

    static bool IsActive(SkinnedMeshRenderer s) => s != null && s.enabled && s.gameObject.activeInHierarchy;

    void RebuildCombineIfNeeded()
    {
        bool changed = _layoutDirty;
        for (int i = 0; i < _renderers.Length && !changed; i++)
            if (IsActive(_renderers[i]) != _prevActive[i]) changed = true;
        if (!changed) return;

        int total = 0;
        for (int i = 0; i < _renderers.Length; i++)
        {
            _prevActive[i] = IsActive(_renderers[i]);
            if (_prevActive[i]) total += _combineCount[i];
        }
        _combine = new CombineInstance[total];
        int w = 0;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (!_prevActive[i]) continue;
            for (int s = 0; s < _combineCount[i]; s++)
                _combine[w++] = new CombineInstance { mesh = _bakeBuffers[i], subMeshIndex = s };
        }
        _layoutDirty = false;
    }

    void SpawnPhantom()
    {
        RebuildCombineIfNeeded();
        if (_combine == null || _combine.Length == 0) return;

        int w = 0;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (!_prevActive[i]) continue;
            var smr = _renderers[i];
            smr.BakeMesh(_bakeBuffers[i]);
            Matrix4x4 toWorld = smr.transform.localToWorldMatrix;
            for (int s = 0; s < _combineCount[i]; s++)
                _combine[w++].transform = toWorld;
        }

        var p = GetPhantom();
        p.mesh.Clear();
        p.mesh.indexFormat = IndexFormat.UInt32;
        p.mesh.CombineMeshes(_combine, true, true);   // 호출 시점 복사 → 버퍼 재사용 안전
        p.mf.sharedMesh = p.mesh;
        p.age = 0f;
        p.active = true;
        p.pos = Vector3.zero;   // 메시에 월드좌표 구워짐 → GO 원점 시작, drift가 전체 평행이동

        // 전방(ChargeAimDir) + 좌우/상하 산란 속도.
        Vector3 fwd = weapon.ChargeAimDir; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = transform.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        p.vel = fwd * forwardSpeed
              + right * Random.Range(-scatter, scatter)
              + Vector3.up * Random.Range(-scatter * 0.3f, scatter * 0.5f);

        if (p.go != null) p.go.transform.position = p.pos;
        p.mat.SetColor(ColorID, ghostColor);
        p.mat.SetFloat(AlphaID, startAlpha);
        if (p.go != null) p.go.SetActive(true);
    }

    Phantom GetPhantom()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (!_pool[i].active) return _pool[i];
        return CreatePhantom();
    }

    Phantom CreatePhantom()
    {
        var go = new GameObject("ChargePhantom") { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(null, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

        var mat = new Material(_ghostMatTemplate);
        mr.sharedMaterial = mat;

        var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
        mesh.MarkDynamic();

        var p = new Phantom { go = go, mf = mf, mat = mat, mesh = mesh, active = false };
        go.SetActive(false);
        _pool.Add(p);
        return p;
    }

    Material CreateGhostMaterial()
    {
        var sh = ghostShader != null ? ghostShader : Shader.Find("ZombieCrush/AfterimageGhost");
        if (sh == null)
        {
            Debug.LogWarning("[ChargePhantomEmitter] ZombieCrush/AfterimageGhost 셰이더 미발견 — 비활성.");
            return null;
        }
        var m = new Material(sh);
        m.SetColor(ColorID, ghostColor);
        m.SetFloat(AlphaID, startAlpha);
        return m;
    }
}
