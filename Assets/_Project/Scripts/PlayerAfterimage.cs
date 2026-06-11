using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 대시 중 "위상 분리 잔상"을 남기는 비주얼 컴포넌트. CharacterVisual(스킨드 메시 루트)에 붙인다.
///
/// 원리: 플레이어는 여러 SkinnedMeshRenderer로 구성된다(몸·옷·머리카락 등). 매 interval마다
/// 각 SMR의 현재 포즈를 BakeMesh로 굽고(1회), 같은 버퍼로 CombineMeshes를 2회 호출해
/// 고스트 "페어"(마젠타 1 + 시안 1)를 그 자리에 띄운다 — CombineMeshes는 호출 시점에 메시를
/// 복사하므로 버퍼 공유가 안전하다. 갓 생긴 페어는 같은 자리에 겹쳐 가산 블렌드로 화이트 코어가
/// 자연 발생하고, 나이가 들수록 대시 진행방향의 수평 수직축을 따라 좌우(+/-)로 찢어지며
/// 마젠타 고스트와 시안 고스트로 분리되어 어두워지다 사라진다.
///
/// ★ 디제틱: 나노봇 신호가 캐릭터의 이동 속도를 못 따라와 채널이 갈라진다 — "위상 잔류".
///   시안(0.35,0.75,0.85)=정상 신호(기존 캐넌), 마젠타(1.0,0.17,0.84)=신호 붕괴/규정 외(신규 캐넌).
///   다크 무드 월드라 가산 블렌드 + HDR 색으로 블룸을 통과시킨다(킬 링·머즐 플래시와 같은 문법).
///
/// ★ 성능: 매 스폰마다 Mesh/GameObject/Material을 새로 만들고 버리면 GC 스파이크로 대시 중 프레임이
///   끊긴다. 그래서 (1) BakeMesh 버퍼·CombineInstance 배열을 영속 재사용하고 (2) 고스트는 풀링해
///   메시·머티리얼·오브젝트를 재활용한다(페어라 동시 생존이 2배 — 워밍업도 ×2). 스트릭 파티클은
///   ParticleSystem 내부 풀 + EmitParams 재사용으로 할당 0. 워밍업 이후 정상상태 힙 할당 ≈ 0.
///
/// PlayerController.IsDashing을 폴링해 대시 중에만 스폰 — 대시의 트리거/타이밍은 컨트롤러가 소유.
/// 진행방향은 컨트롤러가 공개하지 않으므로(_dashDir은 private) 위치 델타로 추정한다(수정 금지 영역).
/// </summary>
public class PlayerAfterimage : MonoBehaviour
{
    [Header("Spawn")]
    [Tooltip("잔상 페어 1쌍을 남기는 간격(초). 작을수록 촘촘한 모션블러 느낌, 클수록 띄엄띄엄.")]
    [SerializeField] float interval = 0.04f;

    [Header("Ghost")]
    [Tooltip("잔상 1장이 떠 있다 사라지기까지 시간(초).")]
    [SerializeField] float lifetime = 0.28f;
    [Tooltip("마젠타 고스트 색/시작 알파 — 신호 붕괴 채널(규정 외).")]
    [SerializeField] Color ghostColorMagenta = new Color(1f, 0.17f, 0.84f, 0.55f);
    [Tooltip("시안 고스트 색/시작 알파 — 정상 신호 채널(스캔펄스 캐넌).")]
    [SerializeField] Color ghostColorCyan = new Color(0.35f, 0.75f, 0.85f, 0.55f);

    // --- 위상 분리 튜닝 상수 ---
    const float SplitStart = 0.02f;   // 갓 생긴 페어의 좌우 오프셋(m) — 거의 겹쳐 화이트 코어
    const float SplitEnd = 0.14f;     // 수명 끝의 좌우 오프셋(m) — 완전히 찢어진 두 채널
    const float GhostHDR = 2.5f;      // HDR 색 배율 — 블룸 임계 통과(×2~3 권장 범위)

    // --- 스피드 스트릭 튜닝 상수 ---
    const float StreakRate = 40f;     // 대시 중 초당 방출 수
    static readonly Color StreakColor = new Color(0.7f, 0.95f, 1f, 0.8f);   // 시안~화이트

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

    // --- 진행방향 추정(컨트롤러 비공개라 위치 델타 폴백) ---
    Vector3 _prevPlayerPos;
    bool _hasPrevPos;
    Vector3 _moveDir = Vector3.forward;   // 마지막 유효 이동방향(수평) — 정지 프레임에도 유지

    // --- 스피드 스트릭 ---
    ParticleSystem _streakPS;
    Material _streakMat;
    float _streakTimer;
    ParticleSystem.EmitParams _streakEmit;   // 구조체 재사용 — 할당 0

    // 풀 슬롯 1개 — 자기 Mesh/Material/오브젝트를 영속 보유하고 재활용된다.
    class Ghost
    {
        public GameObject go;
        public MeshFilter mf;
        public Material mat;
        public Mesh mesh;
        public float age;
        public bool active;
        public Color baseColor;     // 이 고스트의 채널 색(마젠타 or 시안)
        public Vector3 splitDir;    // 분리축(대시 진행방향의 수평 수직 벡터, 정규화)
        public float splitSign;     // 좌우 부호(+1/-1)
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
        if (_ghostMatTemplate == null) return;   // [L-3] 셰이더 전멸 — 안전 무효화(고스트/스트릭 미생성, enabled=false는 내부에서)
        _baseColorId = _ghostMatTemplate.HasProperty("_BaseColor") ? Shader.PropertyToID("_BaseColor") : -1;

        // 풀 프리워밍: 동시 생존 고스트 수(≈lifetime/interval) × 2(페어)만큼 미리 만들어,
        // 첫 대시 중 점진 생성 스파이크를 게임 시작 1회 비용으로 옮긴다.
        int warm = (Mathf.CeilToInt(Mathf.Max(0.0001f, lifetime) / interval) + 1) * 2;
        for (int i = 0; i < warm; i++) CreateGhost();

        _streakMat = CreateStreakMaterial();
        _streakPS = CreateStreakPS();
        _streakPS.Play();   // emission 모듈은 꺼둠 — 수동 Emit만 시뮬레이션되게 가동 상태 유지
    }

    void OnDisable()
    {
        // [L-5] 비활성 시 잔존 정리 — 고스트는 부모 없는 독립 GO라 컴포넌트가 꺼져도 LateUpdate 페이드가
        // 멈춰 가산 HDR 고스트가 월드에 풀 강도로 영구 잔존한다. 풀 전체 끄고 스트릭도 비운다.
        foreach (var g in _pool)
        {
            g.active = false;
            if (g.go != null) g.go.SetActive(false);
        }
        if (_streakPS != null) _streakPS.Clear();
        _spawnTimer = 0f;
        _streakTimer = 0f;
    }

    void OnDestroy()
    {
        if (_ghostMatTemplate != null) Destroy(_ghostMatTemplate);
        if (_streakMat != null) Destroy(_streakMat);   // 스트릭 PS 오브젝트는 자식이라 함께 파괴 — 머티리얼만 명시 정리
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

        // 1) 살아있는 고스트 페이드 + 위상 분리(메시는 안 건드리고 색·위치만) — 가벼움.
        //    가산 블렌드에선 알파만으론 안 빠질 수 있어 색 자체를 (1-t)로 어둡게 한다.
        for (int i = 0; i < _pool.Count; i++)
        {
            var g = _pool[i];
            if (!g.active) continue;
            g.age += dt;
            float t = g.age / life;
            if (t >= 1f) { g.active = false; if (g.go != null) g.go.SetActive(false); continue; }

            // 분리: 메시는 월드 좌표로 구워졌고 GO는 원점 기준이므로 position 오프셋이 곧 월드 분리.
            g.go.transform.position = g.splitDir * (g.splitSign * Mathf.Lerp(SplitStart, SplitEnd, t));

            Color c = GhostTint(g.baseColor, 1f - t);
            if (_baseColorId >= 0) g.mat.SetColor(_baseColorId, c);
            else g.mat.color = c;
        }

        // 2) 진행방향 추정 — 컨트롤러가 방향을 공개하지 않으므로 위치 델타(수평)로 갱신.
        var pc = PlayerController.Instance;
        if (pc != null)
        {
            Vector3 pos = pc.transform.position;
            if (_hasPrevPos)
            {
                Vector3 d = pos - _prevPlayerPos; d.y = 0f;
                if (d.sqrMagnitude > 1e-6f) _moveDir = d.normalized;
            }
            _prevPlayerPos = pos;
            _hasPrevPos = true;
        }

        // 3) 대시 중이면 고스트 페어 스폰 + 스트릭 방출.
        if (pc == null || !pc.IsDashing) { _spawnTimer = 0f; _streakTimer = 0f; return; }

        EmitStreaks(dt);

        _spawnTimer -= dt;
        if (_spawnTimer > 0f) return;
        _spawnTimer = interval;
        SpawnGhostPair();
    }

    // 가산용 채널 색: RGB는 HDR 배율 × 페이드, 알파는 페이드만(블렌드 기여 보조).
    static Color GhostTint(Color baseColor, float fade)
    {
        Color c = baseColor * (GhostHDR * fade);
        c.a = baseColor.a * fade;
        return c;
    }

    void SpawnGhostPair()
    {
        if (_renderers.Length == 0) return;

        // 현재 포즈를 재사용 버퍼에 굽고 월드 변환만 갱신 — 페어여도 베이크는 1회뿐(메시·배열 신규 할당 없음).
        for (int i = 0; i < _renderers.Length; i++)
        {
            var smr = _renderers[i];
            smr.BakeMesh(_bakeBuffers[i]);
            Matrix4x4 toWorld = smr.transform.localToWorldMatrix;
            int start = _combineStart[i], cnt = _combineCount[i];
            for (int s = 0; s < cnt; s++)
                _combineBuffer[start + s].transform = toWorld;
        }

        // 분리축 = 진행방향의 수평 수직 벡터. 마젠타=+측, 시안=-측으로 찢어진다.
        Vector3 perp = Vector3.Cross(Vector3.up, _moveDir);
        SpawnGhostFromBuffer(ghostColorMagenta, perp, +1f);
        SpawnGhostFromBuffer(ghostColorCyan, perp, -1f);
    }

    void SpawnGhostFromBuffer(Color channelColor, Vector3 splitDir, float splitSign)
    {
        var ghost = GetGhost();
        ghost.mesh.Clear();
        ghost.mesh.indexFormat = IndexFormat.UInt32;   // Clear가 UInt16로 되돌릴 수 있어 재확정(65k+ 버텍스 안전)
        ghost.mesh.CombineMeshes(_combineBuffer, true, true);   // 호출 시점 복사 → 같은 버퍼로 2회 호출 안전, 할당 0
        ghost.mf.sharedMesh = ghost.mesh;
        ghost.age = 0f;
        ghost.active = true;
        ghost.baseColor = channelColor;
        ghost.splitDir = splitDir;
        ghost.splitSign = splitSign;
        ghost.go.transform.position = splitDir * (splitSign * SplitStart);

        Color c = GhostTint(channelColor, 1f);   // 첫 프레임부터 풀 강도로 보이게 즉시 색 적용
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
        // URP/Unlit 가산(SrcAlpha, One). 다크 월드 위에 채널 색이 쌓여 빛나고, 페어가 겹친
        // 초기엔 마젠타+시안 ≈ 화이트 코어가 자연 발생. ZWrite off라 정렬 무관(가산은 순서 독립).
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        Material m;
        if (sh != null)
        {
            m = new Material(sh);
            m.SetFloat("_Surface", 1f);   // Transparent
            m.SetFloat("_Blend", 2f);     // Additive
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", GhostTint(ghostColorCyan, 1f));
        }
        else
        {
            // 폴백: Sprites/Default(알파 블렌드).
            var fallback = Shader.Find("Sprites/Default");
            if (fallback == null)
            {
                // [L-3] 최종 폴백도 null이면 예외 대신 안전 무효화 — GlitchFX의 셰이더 미발견 패턴과 동일
                Debug.LogWarning("[PlayerAfterimage] 고스트 셰이더 미발견(URP/Unlit·Sprites/Default 모두) — 잔상/스트릭 영구 비활성. 빌드 스트립 의심.");
                enabled = false;
                return null;
            }
            m = new Material(fallback);
            m.color = ghostColorCyan;
        }
        return m;
    }

    // ──────────── 스피드 스트릭(대시 방향 발광 속도선) ────────────

    void EmitStreaks(float dt)
    {
        _streakTimer -= dt;
        int safety = 8;   // 저 FPS 프레임에서 폭주 방지
        while (_streakTimer <= 0f && safety-- > 0)
        {
            _streakTimer += 1f / StreakRate;
            // 몸통 높이 부근에서 흩뿌리고, 대시 반대 방향으로 살짝 흘려보낸다 —
            // 스트레치드 빌보드가 속도 벡터를 따라 늘어나며 속도선이 된다.
            _streakEmit.position = transform.position + Vector3.up * 0.9f + Random.insideUnitSphere * 0.35f;
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
        main.startSpeed = 0f;   // 속도는 EmitParams로 직접 지정(방향 제어)
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = StreakColor;   // [L-4] startColor는 Color32로 클램프돼 HDR 배율 무효 — 실제 블룸은 머티리얼 _BaseColor(CreateStreakMaterial)가 낸다
        main.maxParticles = 64;                 // 40/s × 0.25s ≈ 10 동시 생존 — 여유분
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // 캐릭터를 따라가지 않고 궤적에 잔류
        var em = ps.emission; em.enabled = false;   // 수동 Emit 전용
        var shp = ps.shape; shp.enabled = false;

        // 수명 끝 투명 페이드(버텍스 컬러를 읽는 파티클 셰이더에서 동작, 폴백에선 무해).
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Stretch;   // 속도 벡터 방향으로 늘어나는 속도선
        psr.lengthScale = 4f;
        psr.velocityScale = 0.08f;
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows = false;
        psr.sharedMaterial = _streakMat;
        return ps;
    }

    Material CreateStreakMaterial()
    {
        // ZombieDeathFX.CreateAdditive와 같은 폴백 체인. 머티리얼 색에도 HDR을 직접 박아
        // 버텍스 컬러를 안 읽는 셰이더(URP/Unlit)로 떨어져도 색·블룸은 유지된다.
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
