// 텔레그래프 장판 랩 부트스트랩 — ThreatArc 스타일 3~6(원/레인/부채꼴/링) 시각 검증 전용.
// 장판 문법(권위: docs/00_authority/2026-06-13-topdown-attack-grammar.md §3·§5):
//   모양=피해영역, 채움=타이밍. 외곽선(예고)→차오름(카운트다운)→가득참(발동).
// 렌더 경로(동결): 레이어 PickupInfo(13) → PickupInfoOverlay RenderObjects(이벤트 600, 투명 큐)
//   → TiltShift 콘 어둠 면제(공정성 캐넌). StrainHarvestFX와 같은 길.
// 씬: Greybox_TelegraphLab (빌더: Editor/TelegraphLabCapture.BuildScene)
using UnityEngine;

public class TelegraphLab : MonoBehaviour
{
    [Tooltip("-1 = 1.2초 자동 루프, 0~1 = 고정(캡처용)")]
    public float debugProgress = -1f;

    const float LoopDur = 1.2f;   // 공용 채움 루프(초)

    // 레드-오렌지 단색 — 색 캐넌(시안/앰버/마젠타 금지). 최종 톤은 유저 노브.
    static readonly Color TelegraphRed = new Color(1f, 0.30f, 0.08f, 1f);

    static readonly int StyleId     = Shader.PropertyToID("_Style");
    static readonly int SizeWorldId = Shader.PropertyToID("_SizeWorld");
    static readonly int ColorId     = Shader.PropertyToID("_Color");
    static readonly int AlphaId     = Shader.PropertyToID("_Alpha");
    static readonly int ProgressId  = Shader.PropertyToID("_Progress");
    static readonly int SeedId      = Shader.PropertyToID("_Seed");
    static readonly int ZTestId     = Shader.PropertyToID("_ZTest");
    static readonly int AngleDegId  = Shader.PropertyToID("_AngleDeg");
    static readonly int InnerR01Id  = Shader.PropertyToID("_InnerR01");

    Material[] _mats;
    GameObject[] _quads;
    Material _groundMat;

    void Awake()
    {
        // 지면을 어두운 회색으로 — 레드-오렌지 가독 판정용 바닥톤(런타임 생성, 씬 오염 없음)
        var ground = GameObject.Find("Ground");
        if (ground != null)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                _groundMat = new Material(lit);
                _groundMat.SetColor("_BaseColor", new Color(0.16f, 0.16f, 0.17f, 1f));
                ground.GetComponent<MeshRenderer>().sharedMaterial = _groundMat;
            }
        }

        var shader = Shader.Find("ZombieCrush/ThreatArc");
        if (shader == null)
        {
            Debug.LogError("[TelegraphLab] ZombieCrush/ThreatArc 셰이더 미발견 — 빌드 스트립 의심");
            enabled = false;
            return;
        }

        // PickupInfo 레이어 — 미존재 시 경고 + Default 폴백(StrainHarvestFX 패턴)
        int layer = LayerMask.NameToLayer("PickupInfo");
        if (layer < 0)
        {
            layer = 0;
            Debug.LogWarning("[TelegraphLab] 'PickupInfo' 레이어 미존재 — 콘 어둠 면제 경로 무효(Default 폴백)");
        }

        _mats = new Material[4];
        _quads = new GameObject[4];
        _mats[0] = MakeQuad(shader, layer, 0, 3f, new Vector2(5f, 5f),   new Vector3(-8.5f, 0f, 0f));   // 원 r2.5
        _mats[1] = MakeQuad(shader, layer, 1, 4f, new Vector2(1.5f, 8f), new Vector3(-4.5f, 0f, 0f));   // 레인 1.5×8m
        _mats[2] = MakeQuad(shader, layer, 2, 5f, new Vector2(6f, 6f),   new Vector3(-0.5f, 0f, 0f));   // 부채꼴 r3
        _mats[2].SetFloat(AngleDegId, 100f);                                                             // 전각 100°
        _mats[3] = MakeQuad(shader, layer, 3, 6f, new Vector2(8f, 8f),   new Vector3(6f, 0f, 0f));      // 링 외경 r4
        _mats[3].SetFloat(InnerR01Id, 0.3f);

        ApplyNow();
    }

    Material MakeQuad(Shader shader, int layer, int index, float style, Vector2 size, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _quads[index] = go;
        go.name = $"Telegraph_Style{(int)style}";
        Destroy(go.GetComponent<Collider>());
        go.layer = layer;
        // X+90°: 쿼드 로컬 +y → 월드 +z (레인 채움이 시전자측 -z → +z로 전진), y=0.05 z파이팅 회피
        go.transform.SetPositionAndRotation(pos + Vector3.up * 0.05f, Quaternion.Euler(90f, 0f, 0f));
        // 셰이더 월드 미터 계산의 전제 — localScale == _SizeWorld
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var mat = new Material(shader);
        mat.SetFloat(StyleId, style);
        mat.SetVector(SizeWorldId, new Vector4(size.x, size.y, 0f, 0f));
        mat.SetColor(ColorId, TelegraphRed);
        mat.SetFloat(AlphaId, 0.85f);
        mat.SetFloat(SeedId, style * 7.3f);   // 도형마다 다른 침식 패턴
        mat.SetFloat(ZTestId, 4f);            // LEqual — 장판은 바닥, 몬스터/벽이 가린다
        mat.renderQueue = 3000;               // PickupInfoOverlay 필터=투명 큐 — Transparent대 유지
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return mat;
    }

    void Update()
    {
        ApplyNow();
    }

    void OnDestroy()
    {
        // 런타임 생성분 정리 — 머티리얼은 GC 대상이 아니라 명시 Destroy 필요.
        // null 가드: 씬 언로드 시 쿼드 GO가 이미 파괴됐을 수 있음.
        if (_mats != null)
            for (int i = 0; i < _mats.Length; i++)
                if (_mats[i] != null) Destroy(_mats[i]);
        if (_groundMat != null) Destroy(_groundMat);
        if (_quads != null)
            for (int i = 0; i < _quads.Length; i++)
                if (_quads[i] != null) Destroy(_quads[i]);
    }

    /// <summary>현재 debugProgress(또는 자동 루프)를 즉시 머티리얼에 반영 — 캡처 유틸이 호출.</summary>
    public void ApplyNow()
    {
        if (_mats == null) return;
        float p = debugProgress >= 0f ? Mathf.Clamp01(debugProgress)
                                      : (Time.time % LoopDur) / LoopDur;
        for (int i = 0; i < _mats.Length; i++)
            if (_mats[i] != null) _mats[i].SetFloat(ProgressId, p);
    }
}
