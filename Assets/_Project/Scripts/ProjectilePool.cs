// 투사체 공유 풀 — 직사 글롭 발사·재활용 시스템. ★나머지 원거리 종(스핏·부채탄·링탄·유도탄)의 재사용 토대.
//
// ════════ 왜 자작 발광구인가 (조달 결정) ════════
//   Vefects Projectile 3팩은 BIRP surface 셰이더라 URP에서 마젠타(ShaderHasError=False도 거짓신호 —
//   톤게이트에서 실측). 그래서 투사체는 코드로 만든 단순 메시(구) + URP/Unlit 가산 발광으로.
//   색 = 레드오렌지(적 위협색 캐넌, 공격 문법 §5). 어두운 Frozen Golden Hour에서 발광구가 또렷이 읽힌다.
//
// ════════ 설계 ════════
//   - MonoBehaviour(씬에 1개) — 스포너가 만들어 모든 사수에 같은 참조 주입(글롭 풀 공유).
//   - Prewarm: 풀 크기만큼 AcidGlob을 미리 생성(런타임 alloc 0 — 사격 빈발해도 GC 안 침).
//   - Fire(origin, dir, speed, range): 여분 글롭 1개를 꺼내 직사 발사. 여분 없으면 가장 오래된 것 회수(soft cap).
//   - 비주얼은 코드로: 구 메시 + Unlit 가산 머티리얼 + TrailRenderer(꼬리). 종마다 색/크기만 바꿔 재사용.
using System.Collections.Generic;
using UnityEngine;

public class ProjectilePool : MonoBehaviour
{
    [Header("글롭 비주얼 (종별 재사용 노브)")]
    [Tooltip("글롭 구 반지름(m). 산성 글롭 시작값 0.18.")]
    [SerializeField] float globRadius = 0.18f;
    [Tooltip("글롭 발광색(HDR) — 적 위협색 캐넌=레드오렌지. 어두운 씬에서 또렷이 읽히게 가산 발광.")]
    [ColorUsage(true, true)] [SerializeField] Color globColor = new Color(1.7f, 0.45f, 0.12f, 1f); // 레드오렌지 HDR
    [Tooltip("꼬리 길이(초). 0이면 꼬리 없음. 직사 글롭이 '날아오는 선'으로 읽히게 짧게.")]
    [SerializeField] float trailTime = 0.12f;

    [Header("풀")]
    [Tooltip("미리 생성할 글롭 수. 동시 비행 글롭이 이보다 많아지면 가장 오래된 것을 회수해 재사용.")]
    [SerializeField] int poolSize = 24;

    readonly Queue<AcidGlob> _idle = new Queue<AcidGlob>();   // 대기 글롭
    readonly List<AcidGlob> _live = new List<AcidGlob>();     // 비행 중(soft cap 회수용 FIFO)
    Material _sharedGlobMat;
    Mesh _sphereMesh;
    bool _built;

    void Awake() => Build();

    void Build()
    {
        if (_built) return;
        _built = true;

        // 구 메시 1회 추출(프리미티브 생성 후 메시만 떼고 본체 폐기).
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _sphereMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(tmp);

        // URP Unlit 가산 발광 머티리얼(전 글롭 공유 — 드로우콜·메모리 절약).
        //   ★Unlit이라 _EmissionColor 없음 — 발광은 HDR _BaseColor(>1)가 직접 낸다. 가산 블렌딩 + 씬 블룸이 글로우를 만든다.
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        _sharedGlobMat = new Material(unlit);
        _sharedGlobMat.SetColor("_BaseColor", globColor);   // HDR 레드오렌지(>1) — 어두운 배경 위 또렷한 발광
        // 가산 블렌딩(Src=One, Dst=One) — 어두운 배경 위 발광. URP Unlit Surface=Transparent 전환.
        _sharedGlobMat.SetFloat("_Surface", 1f);     // Transparent
        _sharedGlobMat.SetFloat("_Blend", 1f);       // Additive
        _sharedGlobMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        _sharedGlobMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        _sharedGlobMat.SetFloat("_ZWrite", 0f);
        _sharedGlobMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        _sharedGlobMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _sharedGlobMat.EnableKeyword("_BLEND_ADD");

        for (int i = 0; i < poolSize; i++)
            _idle.Enqueue(CreateGlob(i));
    }

    AcidGlob CreateGlob(int idx)
    {
        var go = new GameObject($"AcidGlob_{idx}");
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * (globRadius * 2f);

        var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = _sphereMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _sharedGlobMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        TrailRenderer tr = null;
        if (trailTime > 0f)
        {
            tr = go.AddComponent<TrailRenderer>();
            tr.time = trailTime;
            tr.startWidth = globRadius * 1.4f;
            tr.endWidth = 0f;
            tr.material = _sharedGlobMat;
            tr.numCapVertices = 2;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
        }

        var glob = go.AddComponent<AcidGlob>();
        glob.Bind(this, mr, tr);
        return glob;
    }

    /// <summary>직사 글롭 1발 발사. origin에서 dir 방향으로 speed(m/s), range(m)까지 직선 비행.</summary>
    public void Fire(Vector3 origin, Vector3 dir, float speed, float range, float lifetime)
    {
        if (!_built) Build();
        AcidGlob glob = _idle.Count > 0 ? _idle.Dequeue() : RecycleOldest();
        if (glob == null) return;
        _live.Add(glob);
        glob.Launch(origin, dir, speed, range, lifetime);
    }

    // soft cap 초과 시 가장 오래된 비행 글롭을 강제 회수(시각적 끊김보다 일관 성능 우선).
    AcidGlob RecycleOldest()
    {
        if (_live.Count == 0) return null;
        var oldest = _live[0];
        _live.RemoveAt(0);
        return oldest;
    }

    // AcidGlob이 소멸(사거리/수명) 시 호출 — 대기열로 반납.
    public void Return(AcidGlob glob)
    {
        _live.Remove(glob);
        if (!_idle.Contains(glob)) _idle.Enqueue(glob);
    }
}
