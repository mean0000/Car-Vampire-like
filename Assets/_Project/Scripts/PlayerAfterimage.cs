using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 대시 중 잔상(afterimage)을 남기는 비주얼 컴포넌트. CharacterVisual(스킨드 메시 루트)에 붙인다.
///
/// 원리: 플레이어는 여러 SkinnedMeshRenderer로 구성된다(몸·옷·머리카락 등). 매 interval마다
/// 각 SMR의 현재 포즈를 BakeMesh로 굽고, 월드 변환을 적용해 하나의 Mesh로 합쳐(CombineMeshes)
/// 반투명 고스트로 그 자리에 정지 스냅샷을 띄운다. 고스트는 lifetime에 걸쳐 알파가 빠지며 사라진다.
///
/// ★ 성능: 매 스폰마다 Mesh/GameObject/Material을 새로 만들고 버리면 GC 스파이크로 대시 중 프레임이
///   끊긴다. 그래서 (1) BakeMesh 버퍼·CombineInstance 배열을 영속 재사용하고 (2) 고스트는 풀링해
///   메시·머티리얼·오브젝트를 재활용한다. 워밍업 이후 정상상태 힙 할당 ≈ 0.
///
/// PlayerController.IsDashing을 폴링해 대시 중에만 스폰 — 대시의 트리거/타이밍은 컨트롤러가 소유.
/// </summary>
public class PlayerAfterimage : MonoBehaviour
{
    [Header("Spawn")]
    [Tooltip("잔상 1장을 남기는 간격(초). 작을수록 촘촘한 모션블러 느낌, 클수록 띄엄띄엄.")]
    [SerializeField] float interval = 0.04f;

    [Header("Ghost")]
    [Tooltip("잔상 1장이 떠 있다 사라지기까지 시간(초).")]
    [SerializeField] float lifetime = 0.28f;
    [Tooltip("잔상 색/시작 알파. 청록빛 잔광이 스캔펄스 무드와 맞물린다.")]
    [SerializeField] Color ghostColor = new Color(0.4f, 0.9f, 1f, 0.55f);

    // --- 재사용 버퍼(영속) ---
    SkinnedMeshRenderer[] _renderers;     // 활성 파트 스냅샷(Awake 1회 수집)
    Mesh[] _bakeBuffers;                  // SMR별 BakeMesh 대상 — CombineMeshes가 호출 내에서 즉시 복사하므로 공유 안전
    CombineInstance[] _combineBuffer;     // mesh/subMeshIndex는 고정, transform만 매 스폰 갱신
    int[] _combineStart;                  // renderer i가 _combineBuffer에서 차지하는 시작 인덱스
    int[] _combineCount;                  // renderer i의 서브메시 수

    Material _ghostMatTemplate;
    int _baseColorId = -1;                // URP/Unlit이면 _BaseColor, 폴백이면 -1(_Color)

    readonly List<Ghost> _pool = new List<Ghost>();
    float _spawnTimer;

    // 풀 슬롯 1개 — 자기 Mesh/Material/오브젝트를 영속 보유하고 재활용된다.
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
        interval = Mathf.Max(0.01f, interval);   // 0/음수면 매 프레임 스폰 — 방지

        // 활성·유효 파트만 영속 수집. 비활성/disabled(예: 안 쓰는 머리카락 변종)는 제외 —
        // 안 그러면 잔상에 겹쳐 찍히고 BakeMesh도 낭비된다. 런타임에 옷 파트가 토글되지 않는다는 전제(솔로 MVP).
        var all = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var live = new List<SkinnedMeshRenderer>(all.Length);
        foreach (var s in all)
            if (s != null && s.sharedMesh != null && s.enabled && s.gameObject.activeInHierarchy) live.Add(s);
        _renderers = live.ToArray();

        _bakeBuffers = new Mesh[_renderers.Length];
        _combineStart = new int[_renderers.Length];
        _combineCount = new int[_renderers.Length];

        int total = 0;
        for (int i = 0; i < _renderers.Length; i++)
        {
            _bakeBuffers[i] = new Mesh();
            int sub = _renderers[i].sharedMesh.subMeshCount;
            _combineStart[i] = total;
            _combineCount[i] = sub;
            total += sub;
        }

        // mesh/subMeshIndex를 미리 고정 — 매 스폰엔 transform만 갱신해 배열 재할당을 없앤다.
        _combineBuffer = new CombineInstance[total];
        for (int i = 0; i < _renderers.Length; i++)
            for (int s = 0; s < _combineCount[i]; s++)
                _combineBuffer[_combineStart[i] + s] =
                    new CombineInstance { mesh = _bakeBuffers[i], subMeshIndex = s };

        _ghostMatTemplate = CreateGhostMaterial();
        _baseColorId = _ghostMatTemplate.HasProperty("_BaseColor") ? Shader.PropertyToID("_BaseColor") : -1;

        // 풀 프리워밍: 동시 생존 고스트 수(≈lifetime/interval)만큼 미리 만들어, 첫 대시 중
        // 점진 생성 스파이크를 게임 시작 1회 비용으로 옮긴다.
        int warm = Mathf.CeilToInt(Mathf.Max(0.0001f, lifetime) / interval) + 1;
        for (int i = 0; i < warm; i++) CreateGhost();
    }

    void OnDestroy()
    {
        if (_ghostMatTemplate != null) Destroy(_ghostMatTemplate);
        if (_bakeBuffers != null)
            foreach (var m in _bakeBuffers) if (m != null) Destroy(m);

        // 고스트는 부모 없는 독립 오브젝트라 자동 파괴되지 않는다 — 명시적으로 정리.
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

        // 1) 살아있는 고스트 페이드(메시는 안 건드리고 알파만) — 가벼움.
        for (int i = 0; i < _pool.Count; i++)
        {
            var g = _pool[i];
            if (!g.active) continue;
            g.age += dt;
            float t = g.age / life;
            if (t >= 1f) { g.active = false; if (g.go != null) g.go.SetActive(false); continue; }
            Color c = ghostColor; c.a = ghostColor.a * (1f - t);
            if (_baseColorId >= 0) g.mat.SetColor(_baseColorId, c);
            else g.mat.color = c;
        }

        // 2) 대시 중이면 스폰.
        var pc = PlayerController.Instance;
        if (pc == null || !pc.IsDashing) { _spawnTimer = 0f; return; }

        _spawnTimer -= dt;
        if (_spawnTimer > 0f) return;
        _spawnTimer = interval;
        SpawnGhost();
    }

    void SpawnGhost()
    {
        if (_renderers.Length == 0) return;

        // 현재 포즈를 재사용 버퍼에 굽고 월드 변환만 갱신(메시·배열 신규 할당 없음).
        for (int i = 0; i < _renderers.Length; i++)
        {
            var smr = _renderers[i];
            smr.BakeMesh(_bakeBuffers[i]);
            Matrix4x4 toWorld = smr.transform.localToWorldMatrix;
            int start = _combineStart[i], cnt = _combineCount[i];
            for (int s = 0; s < cnt; s++)
                _combineBuffer[start + s].transform = toWorld;
        }

        var ghost = GetGhost();
        ghost.mesh.Clear();
        ghost.mesh.indexFormat = IndexFormat.UInt32;   // Clear가 UInt16로 되돌릴 수 있어 재확정(65k+ 버텍스 안전)
        ghost.mesh.CombineMeshes(_combineBuffer, true, true);   // 슬롯 메시에 직접 빌드 → 할당 0
        ghost.mf.sharedMesh = ghost.mesh;
        ghost.age = 0f;
        ghost.active = true;

        Color c = ghostColor;   // 첫 프레임부터 풀 알파로 보이게 즉시 색 적용
        if (_baseColorId >= 0) ghost.mat.SetColor(_baseColorId, c);
        else ghost.mat.color = c;

        if (ghost.go != null) ghost.go.SetActive(true);
    }

    Ghost GetGhost()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (!_pool[i].active) return _pool[i];
        return CreateGhost();   // 풀 소진 시에만 1회 확장(워밍업 이후 재호출 안 됨)
    }

    Ghost CreateGhost()
    {
        var go = new GameObject("DashGhost");
        // 부모 없음: 메시에 월드 좌표를 구워 넣으므로 CharacterVisual 회전에 끌려가면 안 된다.
        go.transform.SetParent(null, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var mat = new Material(_ghostMatTemplate);
        mr.sharedMaterial = mat;

        var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };   // 합산 버텍스 65k 초과 대비
        mesh.MarkDynamic();   // 매 스폰 CombineMeshes로 덮어쓰므로 동적 힌트

        var g = new Ghost { go = go, mf = mf, mat = mat, mesh = mesh, active = false };
        go.SetActive(false);
        _pool.Add(g);
        return g;
    }

    Material CreateGhostMaterial()
    {
        // URP/Unlit 투명. 키워드·블렌드 상태를 명시해 런타임 생성에서도 확실히 반투명.
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        Material m;
        if (sh != null)
        {
            m = new Material(sh);
            m.SetFloat("_Surface", 1f);   // Transparent
            m.SetFloat("_Blend", 0f);     // Alpha
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", ghostColor);
        }
        else
        {
            // 폴백: Sprites/Default(항상 존재, 알파 블렌드).
            m = new Material(Shader.Find("Sprites/Default"));
            m.color = ghostColor;
        }
        return m;
    }
}
