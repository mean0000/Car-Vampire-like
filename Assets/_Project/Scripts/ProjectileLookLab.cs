// 발사체 룩 비교 랩 — 메시(저폴리 프레넬) vs 빌보드(SDF 코어+글로우)를 같은 조건에서 나란히 날린다.
//
// ════════ 왜 (메시 vs 빌보드 발산 — Unity 실측 판정) ════════
//   웹 리서치 = "탑다운 거리엔 빌보드 코어+글로우가 메시보다 읽힘" vs Codex = "저폴리 메시가 방향·아트 일관·정렬문제 없음".
//   두 프로바이더 발산 → 정지 캡처로 못 가리는 거리감/움직임을 실제 45° 카메라 + Bloom에서 유저가 본다.
//   변수 통제: 같은 탄속·색 계열·크기. 좌 레인=메시(AcidGlobMesh 프레넬), 우 레인=빌보드(AcidGlobBillboard SDF).
//   기존 ProjectilePool은 안 건드린다(외과적). 승자를 나중에 풀에 접는다.
//
//   사용: 빈 씬에 빈 GameObject 1개 만들고 이 컴포넌트 부착 → Play. 카메라/바닥/Bloom/글롭 전부 자동 생성.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ProjectileLookLab : MonoBehaviour
{
    [Header("비행 (변수 통제 — 양 레인 동일)")]
    [Tooltip("탄속(m/s). 정지사격 처벌 공정성 노브와 같은 값(7).")]
    [SerializeField] float globSpeed = 7f;
    [Tooltip("레인당 동시 글롭 수(스트림 밀도).")]
    [SerializeField] int perLane = 6;
    [Tooltip("레인 X 간격(좌=메시, 우=빌보드).")]
    [SerializeField] float laneOffsetX = 2.2f;
    [SerializeField] float spawnZ = 8f;      // 먼 쪽(발사 시작)
    [SerializeField] float endZ = -8f;       // 가까운 쪽(소멸·랩)
    [SerializeField] float globHeight = 0.85f;
    [Tooltip("글롭 지름(m). 리서치 최소 가독 0.25~0.3 권고 → 0.36.")]
    [SerializeField] float globDiameter = 0.36f;

    [Header("색 (적 위협 캐넌 = 레드오렌지)")]
    [ColorUsage(true, true)] [SerializeField] Color meshCore = new Color(0.9f, 0.18f, 0.04f, 1f);
    [ColorUsage(true, true)] [SerializeField] Color meshRim  = new Color(2.4f, 0.7f, 0.18f, 1f);
    [ColorUsage(true, true)] [SerializeField] Color billCore = new Color(3.0f, 3.0f, 2.4f, 1f);
    [ColorUsage(true, true)] [SerializeField] Color billGlow = new Color(2.2f, 0.6f, 0.15f, 1f);
    [ColorUsage(true, true)] [SerializeField] Color trailColor = new Color(1.7f, 0.45f, 0.12f, 1f);

    [Header("환경")]
    [SerializeField] Color groundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
    [SerializeField] Color skyColor = new Color(0.02f, 0.025f, 0.035f, 1f);

    readonly List<Transform> _movers = new List<Transform>();
    float _span;

    void Awake()
    {
        _span = spawnZ - endZ;
        BuildCamera();
        BuildLight();
        BuildGround();
        BuildBloom();

        Material meshMat = MakeMat("ZombieCrush/AcidGlobMesh", m => { m.SetColor("_CoreColor", meshCore); m.SetColor("_RimColor", meshRim); });
        Material billMat = MakeMat("ZombieCrush/AcidGlobBillboard", m => { m.SetColor("_CoreColor", billCore); m.SetColor("_GlowColor", billGlow); });
        Material trailMat = MakeTrailMat();

        Mesh icoGlob = BuildIcoGlob();
        Mesh quad = BuildQuad();

        BuildLane("Mesh", -laneOffsetX, icoGlob, meshMat, trailMat, globDiameter);
        BuildLane("Billboard", +laneOffsetX, quad, billMat, trailMat, globDiameter * 1.6f); // 빌보드 글로우가 가장자리로 퍼져 코어가 작아보이므로 보정
    }

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < _movers.Count; i++)
        {
            var t = _movers[i];
            var p = t.position;
            p.z -= globSpeed * dt;                 // 카메라 쪽(-Z)으로 날아옴
            if (p.z < endZ)
            {
                p.z += _span;                      // 먼 쪽으로 랩(연속 스트림)
                var tr = t.GetComponent<TrailRenderer>();
                if (tr != null) tr.Clear();         // 랩 순간 꼬리 잔상 제거
            }
            t.position = p;
        }
    }

    // ════════ 빌드 헬퍼 ════════

    void BuildCamera()
    {
        var go = new GameObject("JudgeCam");
        var cam = go.AddComponent<Camera>();
        const float dist = 15f, pitch = 45f;
        float r = pitch * Mathf.Deg2Rad;
        go.transform.position = new Vector3(0f, dist * Mathf.Sin(r), -dist * Mathf.Cos(r));
        go.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = skyColor;
        cam.fieldOfView = 40f;
        var data = cam.GetUniversalAdditionalCameraData();
        if (data != null) data.renderPostProcessing = true;   // Bloom 적용
    }

    void BuildLight()
    {
        var go = new GameObject("Sun");
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 0.8f;
        l.color = new Color(0.7f, 0.78f, 0.95f);
        go.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.1f);
    }

    void BuildGround()
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
        g.name = "Ground";
        g.transform.localScale = Vector3.one * 5f;   // 50m
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit != null)
        {
            var m = new Material(lit);
            m.SetColor("_BaseColor", groundColor);
            m.SetFloat("_Smoothness", 0.1f);
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
        }
    }

    void BuildBloom()
    {
        var go = new GameObject("Global Volume");
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.sharedProfile = prof;
        var bloom = prof.Add<Bloom>(true);
        bloom.intensity.Override(1.1f);
        bloom.threshold.Override(0.9f);
        bloom.scatter.Override(0.7f);
        var tone = prof.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);
    }

    Material MakeMat(string shaderName, System.Action<Material> cfg)
    {
        var sh = Shader.Find(shaderName);
        if (sh == null) { Debug.LogError($"[ProjectileLookLab] 셰이더 미발견: {shaderName}"); return null; }
        var m = new Material(sh);
        cfg?.Invoke(m);
        return m;
    }

    Material MakeTrailMat()
    {
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(unlit);
        m.SetColor("_BaseColor", trailColor);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 1f);
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.renderQueue = (int)RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_BLEND_ADD");
        return m;
    }

    void BuildLane(string tag, float x, Mesh mesh, Material mat, Material trailMat, float diameter)
    {
        for (int i = 0; i < perLane; i++)
        {
            var go = new GameObject($"Glob_{tag}_{i}");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * diameter;
            float z = spawnZ - (_span / perLane) * i;
            go.transform.position = new Vector3(x, globHeight, z);

            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.12f;
            tr.startWidth = diameter * 0.7f;
            tr.endWidth = 0f;
            tr.sharedMaterial = trailMat;
            tr.numCapVertices = 2;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.alignment = LineAlignment.View;

            _movers.Add(go.transform);
        }
    }

    // 저폴리 이코사면체 글로브(20면, flat 노멀, Y 살짝 눌러 글롭).
    static Mesh BuildIcoGlob()
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] b = {
            new(-1, t, 0), new( 1, t, 0), new(-1,-t, 0), new( 1,-t, 0),
            new( 0,-1, t), new( 0, 1, t), new( 0,-1,-t), new( 0, 1,-t),
            new( t, 0,-1), new( t, 0, 1), new(-t, 0,-1), new(-t, 0, 1)
        };
        for (int i = 0; i < b.Length; i++) b[i] = b[i].normalized;
        int[] f = {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        var verts = new Vector3[f.Length];
        var norms = new Vector3[f.Length];
        var tris = new int[f.Length];
        for (int i = 0; i < f.Length; i += 3)
        {
            Vector3 a = b[f[i]], c = b[f[i + 1]], d = b[f[i + 2]];
            Vector3 n = Vector3.Cross(c - a, d - a).normalized;   // flat 노멀
            verts[i] = a; verts[i + 1] = c; verts[i + 2] = d;
            norms[i] = norms[i + 1] = norms[i + 2] = n;
            tris[i] = i; tris[i + 1] = i + 1; tris[i + 2] = i + 2;
        }
        // 반지름 0.5(지름=scale) + Y 눌러 글롭.
        for (int i = 0; i < verts.Length; i++)
        {
            var v = verts[i] * 0.5f; v.y *= 0.8f; verts[i] = v;
        }
        var m = new Mesh { name = "IcoGlob" };
        m.vertices = verts; m.normals = norms; m.triangles = tris;
        m.RecalculateBounds();
        return m;
    }

    // 빌보드용 쿼드(XY평면 -0.5..0.5, uv 0..1). 정점 셰이더가 카메라 정면으로 세움.
    static Mesh BuildQuad()
    {
        var m = new Mesh { name = "BillboardQuad" };
        m.vertices = new Vector3[] { new(-0.5f,-0.5f,0), new(0.5f,-0.5f,0), new(0.5f,0.5f,0), new(-0.5f,0.5f,0) };
        m.uv = new Vector2[] { new(0,0), new(1,0), new(1,1), new(0,1) };
        m.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        m.RecalculateBounds();
        return m;
    }
}
