// 텔레그래프 장판 공유 풀 — TelegraphPad 인스턴스를 풀링·재활용. ProjectilePool의 장판 버전.
// ★나머지 장판 종(스매시 원·도약 착지·가스 잔류) 재사용 토대. 종마다 SpawnFan/Circle/Ring만 골라 부른다.
//
// 설계(ProjectilePool 동형):
//   - MonoBehaviour(씬에 1개) — 스포너가 만들어 모든 몬스터 드라이버에 같은 참조 주입.
//   - Prewarm: poolSize만큼 PickupInfo 레이어 쿼드를 미리 생성(런타임 alloc 0).
//   - Acquire(): 여분 패드 1개 반환(없으면 가장 오래된 것 회수). 드라이버가 SpawnFan 등 호출.
//   - 렌더 경로(동결): 레이어 PickupInfo(13) → PickupInfoOverlay RenderObjects(이벤트 600) = 시야콘 면제.
//     ⚠️오버레이 캔버스/URP Decal 재발명 금지(시야콘 합성이 월드 투명 VFX를 지운 사고 이력).
using System.Collections.Generic;
using UnityEngine;

public class TelegraphPool : MonoBehaviour
{
    [Header("풀")]
    [Tooltip("미리 생성할 장판 패드 수. 동시 활성 장판이 이보다 많으면 가장 오래된 것을 회수.")]
    [SerializeField] int poolSize = 12;

    readonly Queue<TelegraphPad> _idle = new Queue<TelegraphPad>();
    readonly List<TelegraphPad> _live = new List<TelegraphPad>();
    Mesh _quadMesh;
    Shader _shader;
    int _layer;
    bool _built;

    void Awake() => Build();

    /// <summary>풀 크기를 Build() 전에 주입(스포너가 inactive GO에 AddComponent 후, SetActive 전에 호출).
    /// ★#if UNITY_EDITOR 없이 빌드에서도 동작 — SerializedObject 주입(에디터 전용)의 빌드-무력화 함정을 피한다.</summary>
    public void InitPoolSize(int size)
    {
        if (_built) { Debug.LogWarning("[TelegraphPool] InitPoolSize는 Build() 전에 호출해야 함(이미 prewarm 완료 — 무시)."); return; }
        poolSize = Mathf.Max(1, size);
    }

    void Build()
    {
        if (_built) return;
        _built = true;

        _shader = Shader.Find("ZombieCrush/ThreatArc");
        if (_shader == null)
        {
            Debug.LogError("[TelegraphPool] ZombieCrush/ThreatArc 셰이더 미발견 — 빌드 스트립 의심. 장판 비활성.");
            enabled = false;
            return;
        }

        // PickupInfo 레이어 — 미존재 시 Default 폴백(콘 면제 경로 무효, StrainHarvestFX 패턴 경고).
        _layer = LayerMask.NameToLayer("PickupInfo");
        if (_layer < 0)
        {
            _layer = 0;
            Debug.LogWarning("[TelegraphPool] 'PickupInfo' 레이어 미존재 — 콘 어둠 면제 경로 무효(Default 폴백)");
        }

        // 쿼드 메시 1회 추출(프리미티브 생성 후 메시만 떼고 본체 폐기).
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _quadMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(tmp);

        for (int i = 0; i < poolSize; i++)
            _idle.Enqueue(CreatePad(i));
    }

    TelegraphPad CreatePad(int idx)
    {
        var go = new GameObject($"TelegraphPad_{idx}");
        go.transform.SetParent(transform, false);
        go.layer = _layer;

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = _quadMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        // 인스턴스별 머티리얼(각자 다른 _Progress·도형). 셰이더 직접 인스턴스.
        var mat = new Material(_shader);
        mr.sharedMaterial = mat;

        var pad = go.AddComponent<TelegraphPad>();
        pad.Bind(this, go.transform, mr, mat);
        return pad;
    }

    /// <summary>여분 장판 패드 1개 획득. 드라이버가 SpawnFan/Circle/Ring을 호출해 채움 개시. 없으면 가장 오래된 것 회수.</summary>
    public TelegraphPad Acquire()
    {
        if (!_built) Build();
        TelegraphPad pad = _idle.Count > 0 ? _idle.Dequeue() : RecycleOldest();
        if (pad == null) return null;
        _live.Add(pad);
        return pad;
    }

    TelegraphPad RecycleOldest()
    {
        if (_live.Count == 0) return null;
        var oldest = _live[0];
        _live.RemoveAt(0);
        // ★SmashImpactPool.RecycleOldest() 패턴과 대칭 — 강제 비활성으로
        //   WatchPadReturn 루프가 mr.enabled=false를 감지해 카운트를 감소시킨다(H-1 직결).
        if (oldest != null) oldest.ForceDeactivate();
        return oldest;
    }

    /// <summary>패드가 수명(가득참+홀드) 종료 시 호출 — 대기열로 반납.</summary>
    public void Return(TelegraphPad pad)
    {
        _live.Remove(pad);
        if (!_idle.Contains(pad)) _idle.Enqueue(pad);
    }
}
