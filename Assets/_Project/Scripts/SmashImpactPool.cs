// 슬램 임팩트 VFX 공유 풀 — SmashImpactFX 인스턴스를 풀링·재활용. TelegraphPool의 임팩트 버전.
//   ★29종 근접 몬스터(Caniathrox/Dimax 포함) 임팩트 연출 재사용 토대. 종마다 Play(origin,radius,…)만 부른다.
//
// 설계(TelegraphPool 동형):
//   - MonoBehaviour(씬에 1개) — 스포너가 만들어 모든 몬스터 드라이버에 같은 참조 주입.
//   - Prewarm: poolSize만큼 SmashImpactFX 인스턴스를 미리 생성(런타임 alloc 0 — 브루트 연타 안전).
//   - Acquire().Play(...): 여분 1개 발동(없으면 가장 오래된 것 회수). 수명 끝나면 SmashImpactFX가 Return.
//
// ★머티리얼 조달([[killburst-fx]] 함정 회피):
//   먼지/그을림 베이스 = 검증된 가산 URP 파티클 머티리얼 `Resources/VFX/Materials/M_KillBurst_Body.mat`.
//   코드생성 머티리얼은 텍스처 흰폴백 → 반드시 *검증된 머티리얼 복제*. SmashImpactFX가 복제해 색·블렌드만 교체.
using System.Collections.Generic;
using UnityEngine;

public class SmashImpactPool : MonoBehaviour
{
    [Header("풀")]
    [Tooltip("미리 생성할 임팩트 FX 수. 동시 활성 임팩트가 이보다 많으면 가장 오래된 것을 회수. 브루트 연타 고려.")]
    [SerializeField] int poolSize = 8;

    const string DustBaseMatPath = "VFX/Materials/M_KillBurst_Body";   // Resources 경로(검증된 가산 머티리얼)

    readonly Queue<SmashImpactFX> _idle = new Queue<SmashImpactFX>();
    readonly List<SmashImpactFX> _live = new List<SmashImpactFX>();
    Mesh _quadMesh;
    Shader _shockShader;
    Material _dustBaseMat;
    int _layer;
    bool _built;

    void Awake() => Build();

    /// <summary>풀 크기를 Build() 전 주입(스포너가 inactive GO에 AddComponent 후 SetActive 전 호출).
    /// ★#if 없는 런타임 주입 — 빌드 무력화 함정 회피(TelegraphPool과 동일 규약).</summary>
    public void InitPoolSize(int size)
    {
        if (_built) { Debug.LogWarning("[SmashImpactPool] InitPoolSize는 Build() 전 호출 필요(이미 prewarm — 무시)."); return; }
        poolSize = Mathf.Max(1, size);
    }

    void Build()
    {
        if (_built) return;
        _built = true;

        _shockShader = Shader.Find("ZombieCrush/SmashShock");
        if (_shockShader == null)
        {
            Debug.LogError("[SmashImpactPool] ZombieCrush/SmashShock 셰이더 미발견 — 빌드 스트립 의심(Always Included 등록 필요). 임팩트 충격파 비활성.");
            // 셰이더 없어도 먼지/그을림은 가능 — enabled 유지(부분 동작).
        }

        _dustBaseMat = Resources.Load<Material>(DustBaseMatPath);
        if (_dustBaseMat == null)
        {
            Debug.LogError($"[SmashImpactPool] Resources/{DustBaseMatPath} 로드 실패 — 먼지/그을림 머티리얼 부재. 임팩트 비활성.");
            enabled = false;
            return;
        }

        // PickupInfo 레이어 — 충격파가 시야콘 어둠에 안 묻히게(장판과 같은 가시 경로). 미존재 시 Default 폴백.
        _layer = LayerMask.NameToLayer("PickupInfo");
        if (_layer < 0) { _layer = 0; Debug.LogWarning("[SmashImpactPool] 'PickupInfo' 레이어 미존재 — 콘 어둠 면제 경로 무효(Default 폴백)"); }

        // 쿼드 메시 1회 추출.
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _quadMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(tmp);

        for (int i = 0; i < poolSize; i++)
            _idle.Enqueue(CreateFX(i));
    }

    SmashImpactFX CreateFX(int idx)
    {
        var go = new GameObject($"SmashImpactFX_{idx}");
        go.transform.SetParent(transform, false);
        var fx = go.AddComponent<SmashImpactFX>();
        // 셰이더가 null이면 충격파만 빠지고 먼지/그을림은 동작(Build에서 null 가드 — 여기선 그대로 넘김).
        fx.Build(this, _quadMesh, _shockShader, _dustBaseMat, _layer);
        return fx;
    }

    /// <summary>여분 임팩트 FX 1개 획득. 드라이버가 Play(origin,radius,…) 호출. 없으면 가장 오래된 것 회수.</summary>
    public SmashImpactFX Acquire()
    {
        if (!_built) Build();
        if (!enabled || _dustBaseMat == null) return null;
        SmashImpactFX fx = _idle.Count > 0 ? _idle.Dequeue() : RecycleOldest();
        if (fx == null) return null;
        _live.Add(fx);
        return fx;
    }

    SmashImpactFX RecycleOldest()
    {
        if (_live.Count == 0) return null;
        var oldest = _live[0];
        _live.RemoveAt(0);
        // ★방어(리뷰): 강제 회수 시 즉시 비활성 — Acquire 직후 Play가 어차피 재세팅하지만, 명시적 Deactivate로
        //   "회수됐으나 _active=true로 한 틱 더 도는" 잠재 경로를 닫는다(향후 비동기 리팩터링 안전).
        oldest.ForceDeactivate();
        return oldest;
    }

    /// <summary>FX가 수명 종료 시 호출 — 대기열로 반납(누수 0).</summary>
    public void Return(SmashImpactFX fx)
    {
        _live.Remove(fx);
        if (!_idle.Contains(fx)) _idle.Enqueue(fx);
    }
}
