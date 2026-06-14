// 카타나 슬래시 궤적 VFX 공유 풀 — SlashVfxFX 인스턴스를 풀링·재활용. SmashImpactPool/TelegraphPool의 슬래시 버전.
//   ★(C)하이브리드 슬래시 궤적: 기성 Vefects 슬래시(화려한 궤적) + ThreatArc SDF(정확한 범위).
//   KatanaController가 자작 LineRenderer 대신 이 풀을 Acquire().Play*()로 부른다.
//
// 설계(SmashImpactPool 동형):
//   - MonoBehaviour(씬/플레이어에 1개) — 스포너/PlayerCombat이 만들어 KatanaController에 참조 주입.
//   - Prewarm: poolSize만큼 SlashVfxFX 인스턴스를 미리 생성(런타임 alloc 0 — 콤보 연타 안전).
//   - Acquire().PlayFan/PlayPierce/PlayWave(...): 여분 1개 발동. 수명 끝나면 SlashVfxFX가 Return.
//
// ★머티리얼 조달([[killburst-fx]] 함정 회피):
//   시안/마젠타 리컬러 슬래시 머티리얼 = Resources/VFX/Slash/Materials/ 디스크 author(Vefects URP 네이티브 셰이더 + 시안 색).
//   범위 SDF = ZombieCrush/ThreatArc(장판과 공유). 스파크 = 검증된 KillBurst 가산 머티리얼(흰폴백 회피).
//   코드생성 new Material(Shader.Find)는 텍스처 흰폴백 → 반드시 검증된 디스크 머티리얼 복제.
using System.Collections.Generic;
using UnityEngine;

public class SlashVfxPool : MonoBehaviour
{
    [Header("풀")]
    [Tooltip("미리 생성할 슬래시 FX 수. 동시 활성 슬래시가 이보다 많으면 가장 오래된 것을 회수. "
           + "★진행형 궤적(발도 ~0.7s·참격파 ~0.4s)이 평타 연타와 겹쳐도 강제종료 안 나게 여유(10). 콤보 연타 고려.")]
    [SerializeField] int poolSize = 10;

    // Resources 경로(디스크 author 머티리얼).
    const string ArcMatPath    = "VFX/Slash/Materials/M_SlashArc_Cyan";          // 시안 부채꼴 호(평타)
    const string PierceMatPath = "VFX/Slash/Materials/M_SlashPierce_Cyan";       // 시안 직선 찌르기(발도)
    const string WaveMatPath   = "VFX/Slash/Materials/M_SlashWave_CyanMagenta";  // 시안+마젠타 확장(참격파)
    const string SparkMatPath  = "VFX/Materials/M_KillBurst_Spark";              // 검증된 가산 스파크

    readonly Queue<SlashVfxFX> _idle = new Queue<SlashVfxFX>();
    readonly List<SlashVfxFX> _live = new List<SlashVfxFX>();
    Mesh _quadMesh;
    Shader _rangeShader;
    Material _arcMat, _pierceMat, _waveMat, _sparkMat;
    int _layer;
    bool _built;

    // ── 자가 부트스트랩 싱글톤 ── 플레이어용 슬래시 풀은 스포너 주입 대상이 아니라(몬스터와 달리),
    //   KatanaController가 첫 공격에 1회 참조한다. 씬에 없으면 자동 생성(NoiseManager 식 self-bootstrap).
    static SlashVfxPool _instance;
    public static SlashVfxPool Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindObjectOfType<SlashVfxPool>();
            if (_instance == null)
            {
                var go = new GameObject("SlashVfxPool (auto)");
                _instance = go.AddComponent<SlashVfxPool>();   // Awake → Build()
            }
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // (InitPoolSize 제거 — 슬래시 풀은 self-bootstrap 싱글톤이라 inactive-GO 주입 창이 없다. poolSize는 인스펙터 노브.)

    void Build()
    {
        if (_built) return;
        _built = true;

        _rangeShader = Shader.Find("ZombieCrush/ThreatArc");
        if (_rangeShader == null)
            Debug.LogWarning("[SlashVfxPool] ZombieCrush/ThreatArc 미발견 — 범위 SDF 비활성(슬래시 궤적은 동작). Always Included 등록 확인.");

        _arcMat    = Resources.Load<Material>(ArcMatPath);
        _pierceMat = Resources.Load<Material>(PierceMatPath);
        _waveMat   = Resources.Load<Material>(WaveMatPath);
        _sparkMat  = Resources.Load<Material>(SparkMatPath);

        if (_arcMat == null || _pierceMat == null || _waveMat == null)
        {
            Debug.LogError($"[SlashVfxPool] 시안 슬래시 머티리얼 로드 실패 (arc={_arcMat != null} pierce={_pierceMat != null} wave={_waveMat != null}) — 슬래시 궤적 비활성. Resources/VFX/Slash/Materials/ 확인.");
            enabled = false;
            return;
        }
        if (_sparkMat == null)
            Debug.LogWarning("[SlashVfxPool] 스파크 머티리얼 로드 실패 — 스파크 생략(슬래시·범위는 동작).");

        // PickupInfo 레이어 — 시야콘 어둠 면제(장판/임팩트와 같은 가시 경로). 미존재 시 Default 폴백.
        _layer = LayerMask.NameToLayer("PickupInfo");
        if (_layer < 0) { _layer = 0; Debug.LogWarning("[SlashVfxPool] 'PickupInfo' 레이어 미존재 — 콘 어둠 면제 무효(Default 폴백)"); }

        var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _quadMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(tmp);

        for (int i = 0; i < poolSize; i++)
            _idle.Enqueue(CreateFX(i));
    }

    SlashVfxFX CreateFX(int idx)
    {
        var go = new GameObject($"SlashVfxFX_{idx}");
        go.transform.SetParent(transform, false);
        var fx = go.AddComponent<SlashVfxFX>();
        fx.Build(this, _quadMesh, _rangeShader, _arcMat, _pierceMat, _waveMat, _sparkMat, _layer);
        return fx;
    }

    /// <summary>여분 슬래시 FX 1개 획득. 드라이버가 PlayFan/PlayPierce/PlayWave 호출. 없으면 가장 오래된 것 회수.</summary>
    public SlashVfxFX Acquire()
    {
        if (!_built) Build();
        if (!enabled) return null;
        SlashVfxFX fx = _idle.Count > 0 ? _idle.Dequeue() : RecycleOldest();
        if (fx == null) return null;
        _live.Add(fx);
        return fx;
    }

    SlashVfxFX RecycleOldest()
    {
        if (_live.Count == 0) return null;
        var oldest = _live[0];
        _live.RemoveAt(0);
        oldest.ForceDeactivate();
        return oldest;
    }

    /// <summary>FX가 수명 종료 시 호출 — 대기열로 반납(누수 0).</summary>
    public void Return(SlashVfxFX fx)
    {
        _live.Remove(fx);
        if (!_idle.Contains(fx)) _idle.Enqueue(fx);
    }
}
