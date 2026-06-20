using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ★디버그 히트박스 시각화 매니저 — 플레이어 공격 아크(콤보 단별)와 적 콜라이더를 런타임 GL로 그린다.
/// Game/Scene 뷰 양쪽에 보이며, Inspector 불린 또는 핫키로 각각 토글한다.
/// 공격 아크: 평시엔 모든 단 프리뷰(라이브 조준), 공격 중엔 활성 단을 밝게(잠근 방향).
/// 적 히트박스: enemyMask 레이어의 콜라이더(스피어/캡슐=풋프린트 원+높이, 박스=와이어, 그외=AABB).
/// throwaway 디버그 — 정식 빌드 전 제거/비활성.
/// </summary>
[ExecuteAlways]   // 에디트 Scene 뷰 + Play Game 뷰 양쪽에서 그리게(OnRenderObject는 ExecuteAlways 필요).
public class HitboxDebugManager : MonoBehaviour
{
    [Header("토글")]
    public bool showAttack = true;
    public bool showEnemy = true;
    public bool showPlayerHurtbox = true;
    [SerializeField] KeyCode toggleAttackKey = KeyCode.F1;
    [SerializeField] KeyCode toggleEnemyKey = KeyCode.F2;
    [SerializeField] KeyCode togglePlayerKey = KeyCode.F3;

    [Header("적 탐색")]
    [SerializeField] LayerMask enemyMask = 1 << 7;
    [Tooltip("적 콜라이더 캐시 갱신 간격(초).")]
    [SerializeField] float enemyRefresh = 0.4f;

    [Header("색")]
    [SerializeField] Color[] stepColors =
    {
        new Color(0.2f, 0.9f, 1f, 1f),   // C1 시안
        new Color(0.4f, 1f, 0.5f, 1f),   // C2 초록
        new Color(1f, 0.85f, 0.2f, 1f),  // C3 금
    };
    [SerializeField] Color enemyColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] Color originColor = Color.white;
    [Tooltip("플레이어 허트박스 — 피격 가능(취약) 상태 색.")]
    [SerializeField] Color playerHurtColor = new Color(0.4f, 1f, 0.4f, 1f);
    [Tooltip("플레이어 허트박스 — 무적(i-frame) 상태 색. 회피/피격 무적 구간에 이 색으로 바뀐다.")]
    [SerializeField] Color playerInvulnColor = new Color(0.3f, 0.95f, 1f, 1f);

    KatanaWeapon _kw;
    PlayerHealth _ph;
    Material _lineMat;
    readonly List<Collider> _enemies = new List<Collider>();
    float _refreshTimer;

    void OnEnable()
    {
        if (_lineMat == null)
        {
            var sh = Shader.Find("Hidden/Internal-Colored");
            _lineMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            _lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _lineMat.SetInt("_ZWrite", 0);
            _lineMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always); // 항상 보이게(벽 뒤도)
        }
        RefreshEnemies();
    }

    void OnDestroy() { if (_lineMat != null) DestroyImmediate(_lineMat); }

    void Update()
    {
        if (Application.isPlaying)
        {
            if (Input.GetKeyDown(toggleAttackKey)) showAttack = !showAttack;
            if (Input.GetKeyDown(toggleEnemyKey)) showEnemy = !showEnemy;
            if (Input.GetKeyDown(togglePlayerKey)) showPlayerHurtbox = !showPlayerHurtbox;
        }

        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer <= 0f) { RefreshEnemies(); _refreshTimer = enemyRefresh; }
    }

    void RefreshEnemies()
    {
        _enemies.Clear();
        foreach (var c in FindObjectsOfType<Collider>())
            if ((enemyMask.value & (1 << c.gameObject.layer)) != 0) _enemies.Add(c);
    }

    KatanaWeapon Weapon()
    {
        if (_kw == null) _kw = FindObjectOfType<KatanaWeapon>();
        return _kw;
    }

    Transform PlayerT()
    {
        var kw = Weapon();
        if (kw == null) return null;
        if (kw.DebugOwner != null) return kw.DebugOwner;
        var m = FindObjectOfType<PlayerMotor>();
        return m != null ? m.transform : kw.transform.root;
    }

    void OnRenderObject()
    {
        if (_lineMat == null) return;
        // Scene/Game 뷰 외 프리뷰/미니맵 카메라 중복 방지는 생략(디버그).
        _lineMat.SetPass(0);
        GL.PushMatrix();
        GL.Begin(GL.LINES);

        if (showAttack) DrawAttack();
        if (showEnemy) DrawEnemies();
        if (showPlayerHurtbox) DrawPlayerHurtbox();

        GL.End();
        GL.PopMatrix();
    }

    PlayerHealth PlayerHealthRef()
    {
        if (_ph == null) _ph = FindObjectOfType<PlayerHealth>();
        return _ph;
    }

    /// <summary>플레이어 허트박스(피격 콜라이더) — 취약=초록 / 무적(회피·피격 i-frame)=시안. 무적 상태가 한눈에 보인다.</summary>
    void DrawPlayerHurtbox()
    {
        var ph = PlayerHealthRef();
        if (ph == null) return;
        var col = ph.GetComponent<Collider>();
        if (col == null) col = ph.GetComponentInChildren<Collider>();
        if (col == null) return;
        GL.Color(ph.IsInvulnerable ? playerInvulnColor : playerHurtColor);
        DrawColliderShape(col);
    }

    void DrawAttack()
    {
        var kw = Weapon();
        var pt = PlayerT();
        if (kw == null || pt == null) return;

        Vector3 aim = kw.DebugAimDir; aim.y = 0f;
        if (aim.sqrMagnitude < 1e-4f) aim = Vector3.forward; else aim.Normalize();
        int count = Mathf.Max(kw.DebugHitCount, 0);
        int active = kw.DebugStep; // 0=평시

        for (int s = 1; s <= count; s++)
        {
            if (!kw.DebugGetHit(s, out float range, out float arcHalf, out float fwd)) continue;
            Vector3 origin = pt.position + aim * fwd;
            Color c = stepColors[Mathf.Clamp(s - 1, 0, stepColors.Length - 1)];
            // 공격 중이면 활성 단만 불투명, 나머지 흐리게. 평시(active=0)엔 전부 프리뷰.
            float a = (active == 0) ? 0.5f : (s == active ? 1f : 0.12f);
            c.a = a;
            DrawFan(origin, aim, range, arcHalf, c, pt.position.y + 0.05f);
            // 원점 마커
            GL.Color(originColor);
            CrossXZ(origin, 0.12f, pt.position.y + 0.05f);
        }
    }

    void DrawFan(Vector3 origin, Vector3 aim, float range, float arcHalf, Color c, float y)
    {
        origin.y = y;
        float baseAng = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
        int seg = Mathf.Max(8, Mathf.CeilToInt(arcHalf / 6f) * 2);
        GL.Color(c);
        Vector3 Edge(float deg)
        {
            float r = (baseAng + deg) * Mathf.Deg2Rad;
            return origin + new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r)) * range;
        }
        // 두 가장자리
        Line(origin, Edge(-arcHalf));
        Line(origin, Edge(arcHalf));
        // 호 폴리라인
        Vector3 prev = Edge(-arcHalf);
        for (int i = 1; i <= seg; i++)
        {
            float t = Mathf.Lerp(-arcHalf, arcHalf, i / (float)seg);
            Vector3 cur = Edge(t);
            Line(prev, cur);
            prev = cur;
        }
        // 스포크(채움 느낌) — 흐릴 땐 생략
        if (c.a > 0.3f)
        {
            Color sc = c; sc.a *= 0.4f; GL.Color(sc);
            for (int i = 1; i < seg; i++)
            {
                float t = Mathf.Lerp(-arcHalf, arcHalf, i / (float)seg);
                Line(origin, Edge(t));
            }
            GL.Color(c);
        }
    }

    void DrawEnemies()
    {
        GL.Color(enemyColor);
        for (int i = 0; i < _enemies.Count; i++)
        {
            var col = _enemies[i];
            if (col == null) continue;
            DrawColliderShape(col);
        }
    }

    /// <summary>콜라이더 와이어 — 스피어/캡슐=풋프린트 원+높이, 박스=와이어, 그외=AABB. 색은 호출자가 GL.Color로 정함.</summary>
    void DrawColliderShape(Collider col)
    {
        switch (col)
        {
            case SphereCollider sc:
            {
                Vector3 ctr = sc.transform.TransformPoint(sc.center);
                float r = sc.radius * MaxAbsScale(sc.transform.lossyScale);
                RingXZ(ctr, r, ctr.y);
                VLine(ctr, r);
                break;
            }
            case CapsuleCollider cc:
            {
                Vector3 ctr = cc.transform.TransformPoint(cc.center);
                Vector3 ls = cc.transform.lossyScale;
                float r = cc.radius * Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z));
                float h = Mathf.Max(cc.height * Mathf.Abs(ls.y), r * 2f);
                RingXZ(ctr, r, ctr.y);
                RingXZ(ctr, r, ctr.y + h * 0.5f - r);
                RingXZ(ctr, r, ctr.y - h * 0.5f + r);
                VLine(ctr, r, h);
                break;
            }
            case BoxCollider bc:
            {
                DrawWireBox(bc);
                break;
            }
            default:
            {
                var b = col.bounds; // AABB 폴백
                DrawAABB(b.center, b.extents);
                break;
            }
        }
    }

    // ── GL 프리미티브 헬퍼 ──
    static void Line(Vector3 a, Vector3 b) { GL.Vertex(a); GL.Vertex(b); }

    void RingXZ(Vector3 c, float r, float y)
    {
        const int seg = 24;
        Vector3 prev = c + new Vector3(r, 0, 0); prev.y = y;
        for (int i = 1; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            Vector3 cur = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); cur.y = y;
            Line(prev, cur); prev = cur;
        }
    }

    void VLine(Vector3 c, float r, float h = -1f)
    {
        float half = h > 0f ? h * 0.5f : r;
        Line(c + Vector3.up * half + Vector3.right * r, c - Vector3.up * half + Vector3.right * r);
        Line(c + Vector3.up * half - Vector3.right * r, c - Vector3.up * half - Vector3.right * r);
    }

    void CrossXZ(Vector3 c, float s, float y)
    {
        c.y = y;
        Line(c + Vector3.right * s, c - Vector3.right * s);
        Line(c + Vector3.forward * s, c - Vector3.forward * s);
    }

    void DrawAABB(Vector3 c, Vector3 e)
    {
        Vector3[] v =
        {
            c + new Vector3(-e.x,-e.y,-e.z), c + new Vector3(e.x,-e.y,-e.z),
            c + new Vector3(e.x,-e.y,e.z),   c + new Vector3(-e.x,-e.y,e.z),
            c + new Vector3(-e.x,e.y,-e.z),   c + new Vector3(e.x,e.y,-e.z),
            c + new Vector3(e.x,e.y,e.z),     c + new Vector3(-e.x,e.y,e.z),
        };
        for (int i = 0; i < 4; i++) { Line(v[i], v[(i + 1) % 4]); Line(v[4 + i], v[4 + (i + 1) % 4]); Line(v[i], v[4 + i]); }
    }

    void DrawWireBox(BoxCollider bc)
    {
        Vector3 c = bc.center, s = bc.size * 0.5f;
        var t = bc.transform;
        Vector3[] v = new Vector3[8];
        int k = 0;
        for (int xi = -1; xi <= 1; xi += 2)
            for (int yi = -1; yi <= 1; yi += 2)
                for (int zi = -1; zi <= 1; zi += 2)
                    v[k++] = t.TransformPoint(c + new Vector3(xi * s.x, yi * s.y, zi * s.z));
        // 인덱스: bit pattern (x,y,z) → 0..7. 모서리 연결
        int[,] edges = { {0,1},{0,2},{0,4},{1,3},{1,5},{2,3},{2,6},{3,7},{4,5},{4,6},{5,7},{6,7} };
        for (int i = 0; i < edges.GetLength(0); i++) Line(v[edges[i, 0]], v[edges[i, 1]]);
    }

    static float MaxAbsScale(Vector3 s) => Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
}
