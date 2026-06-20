using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ★대시 잔상(afterimage) — 하이브리드: 기존 성능 골격(풀링·버퍼 재사용·GC≈0·속도선) +
/// 시안 프레넬 단색 비주얼(ZombieCrush/AfterimageGhost). 스킨드 메시 루트(CharacterVisual)에 붙인다.
///
/// 원리: 플레이어는 여러 SkinnedMeshRenderer로 구성된다. 매 interval마다 각 SMR의 현재 포즈를
/// BakeMesh로 굽고(재사용 버퍼), CombineMeshes로 한 메시로 합쳐 그 자리에 고스트 1장을 띄운다.
/// 몸은 대시로 빠져나가고 고스트는 월드 고정으로 남아 페이드 → 촘촘한 "스슥" 잔상.
///
/// ★성능(기존 보존): BakeMesh 버퍼·CombineInstance 배열을 영속 재사용하고 고스트를 풀링한다.
///   프리워밍으로 첫 대시 생성 스파이크를 시작 1회로 옮긴다. 스트릭은 ParticleSystem 내부 풀 +
///   EmitParams 재사용으로 할당 0. 정상상태 힙 할당 ≈ 0.
/// ★비주얼(신규): 마젠타 위상분리 폐기 → 시안 단색 프레넬 림 고스트(실루엣 살리고 바디 옅게, 가산).
///   색 팔레트 규약 시안=액션. 알파는 머티리얼 _Alpha로 페이드.
///
/// PlayerMotor.IsDashing을 폴링해 대시 중에만 스폰 — 트리거/타이밍은 모터가 소유. 진행방향은
/// 위치 델타로 추정(스트릭 방향용).
/// </summary>
public class PlayerAfterimage : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("대시 상태 소스. 비우면 부모/씬에서 탐색.")]
    [SerializeField] PlayerMotor motor;
    [Tooltip("고스트 셰이더(비우면 ZombieCrush/AfterimageGhost 자동 탐색).")]
    [SerializeField] Shader ghostShader;

    [Header("Spawn")]
    [Tooltip("잔상 1장 간격(초). 작을수록 촘촘. 대시 0.42s에 0.03이면 ~14장.")]
    [SerializeField] float interval = 0.03f;

    [Header("Ghost")]
    [Tooltip("잔상 1장 페이드 시간(초).")]
    [SerializeField] float lifetime = 0.28f;
    [Tooltip("고스트 색(시안=액션 규약). HDR — 다크월드 블룸 통과.")]
    [ColorUsage(true, true)]
    [SerializeField] Color ghostColor = new Color(0.3f, 1.4f, 1.7f, 1f);
    [Tooltip("고스트 시작 알파(가산이라 1 미만 권장).")]
    [SerializeField, Range(0f, 1f)] float startAlpha = 0.9f;

    [Header("Streaks (속도선)")]
    [Tooltip("대시 방향 속도선 켜기.")]
    [SerializeField] bool enableStreaks = true;

    const float StreakRate = 40f;     // 대시 중 초당 방출 수
    static readonly Color StreakColor = new Color(0.7f, 0.95f, 1f, 0.8f);

    // --- 재사용 버퍼(영속) ---
    SkinnedMeshRenderer[] _renderers;
    Mesh[] _bakeBuffers;
    int[] _combineCount;          // SMR별 서브메시 수(고정, sharedMesh 기준)
    CombineInstance[] _combine;   // 활성 SMR만 — 멤버십 변할 때만 재구성(GC-제로 유지)
    bool[] _prevActive;           // 직전 스폰의 SMR별 활성 상태(변화 감지)
    bool _layoutDirty = true;

    Material _ghostMatTemplate;
    static readonly int AlphaID = Shader.PropertyToID("_Alpha");
    static readonly int ColorID = Shader.PropertyToID("_BaseColor");

    readonly List<Ghost> _pool = new List<Ghost>();
    float _spawnTimer;

    // --- 진행방향 추정(스트릭용) ---
    Vector3 _prevPos;
    bool _hasPrevPos;
    Vector3 _moveDir = Vector3.forward;

    // --- 스피드 스트릭 ---
    ParticleSystem _streakPS;
    Material _streakMat;
    float _streakTimer;
    ParticleSystem.EmitParams _streakEmit;

    class Ghost
    {
        public GameObject go;
        public MeshFilter mf;
        public Material mat;
        public Mesh mesh;
        public float age;
        public bool active;
    }

    void Awake()
    {
        interval = Mathf.Max(0.01f, interval);
        if (motor == null) motor = GetComponentInParent<PlayerMotor>();
        if (motor == null) motor = FindObjectOfType<PlayerMotor>();

        var all = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var live = new List<SkinnedMeshRenderer>(all.Length);
        foreach (var s in all)
            if (s != null && s.sharedMesh != null) live.Add(s);   // 활성 여부는 스폰 시 판단(런타임 토글 대응, Stab 권고-4)
        _renderers = live.ToArray();

        if (_renderers.Length == 0) { Debug.LogWarning("[PlayerAfterimage] SkinnedMeshRenderer 미발견.", this); }

        _bakeBuffers = new Mesh[_renderers.Length];
        _combineCount = new int[_renderers.Length];
        _prevActive = new bool[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            _bakeBuffers[i] = new Mesh();
            _combineCount[i] = _renderers[i].sharedMesh.subMeshCount;
        }
        // _combine은 첫 스폰 시 활성 멤버십에 맞춰 구성(_layoutDirty=true).

        _ghostMatTemplate = CreateGhostMaterial();
        if (_ghostMatTemplate == null) { enabled = false; return; }   // 셰이더 전멸 — 안전 무효화

        // 프리워밍: 동시 생존 고스트 수(≈lifetime/interval)만큼 미리 생성.
        int warm = Mathf.CeilToInt(Mathf.Max(0.0001f, lifetime) / interval) + 1;
        for (int i = 0; i < warm; i++) CreateGhost();

        if (enableStreaks)
        {
            _streakMat = CreateStreakMaterial();
            _streakPS = CreateStreakPS();
            _streakPS.Play();
        }
    }

    void OnDisable()
    {
        foreach (var g in _pool) { g.active = false; if (g.go != null) g.go.SetActive(false); }
        if (_streakPS != null) _streakPS.Clear();
        _spawnTimer = 0f;
        _streakTimer = 0f;
    }

    void OnDestroy()
    {
        if (_ghostMatTemplate != null) Destroy(_ghostMatTemplate);
        if (_streakMat != null) Destroy(_streakMat);
        if (_bakeBuffers != null)
            foreach (var m in _bakeBuffers) if (m != null) Destroy(m);
        foreach (var g in _pool)
        {
            if (g.mesh != null) Destroy(g.mesh);
            if (g.mat != null) Destroy(g.mat);
            if (g.go != null) Destroy(g.go);
        }
        _pool.Clear();
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        float life = Mathf.Max(0.0001f, lifetime);

        // 1) 살아있는 고스트 페이드(_Alpha만 — 메시 안 건드림).
        for (int i = 0; i < _pool.Count; i++)
        {
            var g = _pool[i];
            if (!g.active) continue;
            g.age += dt;
            float t = g.age / life;
            if (t >= 1f) { g.active = false; if (g.go != null) g.go.SetActive(false); continue; }
            g.mat.SetFloat(AlphaID, startAlpha * (1f - t));
        }

        // 2) 진행방향 추정(스트릭 방향).
        if (motor != null)
        {
            Vector3 pos = motor.transform.position;
            if (_hasPrevPos)
            {
                Vector3 d = pos - _prevPos; d.y = 0f;
                if (d.sqrMagnitude > 1e-6f) _moveDir = d.normalized;
            }
            _prevPos = pos;
            _hasPrevPos = true;
        }

        // 3) 대시 중이면 고스트 스폰 + 스트릭.
        if (motor == null || !motor.IsDashing) { _spawnTimer = 0f; _streakTimer = 0f; return; }

        if (enableStreaks && _streakPS != null) EmitStreaks(dt);

        _spawnTimer -= dt;
        if (_spawnTimer > 0f) return;
        _spawnTimer = interval;
        SpawnGhost();
    }

    static bool IsActive(SkinnedMeshRenderer s) => s != null && s.enabled && s.gameObject.activeInHierarchy;

    /// <summary>활성 SMR 멤버십이 바뀌었을 때만 _combine 레이아웃 재구성(GC-제로 유지, Stab 권고-4).</summary>
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
        _combine = new CombineInstance[total];   // 멤버십 변화 시에만 할당(평시 0)
        int w = 0;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (!_prevActive[i]) continue;
            for (int s = 0; s < _combineCount[i]; s++)
                _combine[w++] = new CombineInstance { mesh = _bakeBuffers[i], subMeshIndex = s };
        }
        _layoutDirty = false;
    }

    void SpawnGhost()
    {
        if (_renderers.Length == 0) return;
        RebuildCombineIfNeeded();
        if (_combine == null || _combine.Length == 0) return;   // 활성 SMR 없음

        // 활성 SMR만 베이크 + 월드 변환 갱신(_combine 레이아웃과 동일 순서).
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

        var ghost = GetGhost();
        ghost.mesh.Clear();
        ghost.mesh.indexFormat = IndexFormat.UInt32;
        ghost.mesh.CombineMeshes(_combine, true, true);   // 호출 시점 복사 → 버퍼 재사용 안전, 할당 0
        ghost.mf.sharedMesh = ghost.mesh;
        ghost.age = 0f;
        ghost.active = true;
        ghost.go.transform.position = Vector3.zero;   // 메시에 월드 좌표가 구워짐 → GO는 원점

        ghost.mat.SetColor(ColorID, ghostColor);
        ghost.mat.SetFloat(AlphaID, startAlpha);
        if (ghost.go != null) ghost.go.SetActive(true);
    }

    Ghost GetGhost()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (!_pool[i].active) return _pool[i];
        return CreateGhost();
    }

    Ghost CreateGhost()
    {
        var go = new GameObject("DashGhost");
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

        var g = new Ghost { go = go, mf = mf, mat = mat, mesh = mesh, active = false };
        go.SetActive(false);
        _pool.Add(g);
        return g;
    }

    Material CreateGhostMaterial()
    {
        var sh = ghostShader != null ? ghostShader : Shader.Find("ZombieCrush/AfterimageGhost");
        if (sh == null)
        {
            Debug.LogWarning("[PlayerAfterimage] ZombieCrush/AfterimageGhost 셰이더 미발견 — 잔상 비활성. 빌드 스트립 의심.");
            return null;
        }
        var m = new Material(sh);
        m.SetColor(ColorID, ghostColor);
        m.SetFloat(AlphaID, startAlpha);
        return m;
    }

    // ──────────── 스피드 스트릭 ────────────

    void EmitStreaks(float dt)
    {
        _streakTimer -= dt;
        int safety = 8;
        while (_streakTimer <= 0f && safety-- > 0)
        {
            _streakTimer += 1f / StreakRate;
            _streakEmit.position = motor.transform.position + Vector3.up * 0.9f + Random.insideUnitSphere * 0.35f;
            _streakEmit.velocity = -_moveDir * Random.Range(1.5f, 3f);
            _streakPS.Emit(_streakEmit, 1);
        }
    }

    ParticleSystem CreateStreakPS()
    {
        var go = new GameObject("DashStreakPS");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = StreakColor;
        main.maxParticles = 64;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.enabled = false;
        var shp = ps.shape; shp.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Stretch;
        psr.lengthScale = 4f;
        psr.velocityScale = 0.08f;
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows = false;
        psr.sharedMaterial = _streakMat;
        return ps;
    }

    Material CreateStreakMaterial()
    {
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var mat = new Material(sh);
        Color hdr = StreakColor * 2.5f; hdr.a = StreakColor.a;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hdr);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", hdr);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        return mat;
    }
}
