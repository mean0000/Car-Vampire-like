using UnityEngine;

/// <summary>
/// 플레이어 상태(소음)를 월드 공간에서 시각화 + 피격 시 아바타 히트 플래시.
/// - 소음 링: 좀비가 들을 수 있는 실제 반경(HearingRadius)을 바닥에 원으로 표시.
/// - 아바타 히트 플래시: 피격 시 캐릭터 바디 SkinnedMeshRenderer들의 _EmissionColor를
///   MaterialPropertyBlock으로 순간 밝힌 뒤 원래값으로 페이드(언스케일드).
/// 그레이박스 기준 평지(바닥 y≈0)를 가정한다.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerVisualFeedback : MonoBehaviour
{
    [Header("Noise Ring")]
    [SerializeField] int ringSegments = 48;
    [SerializeField] float ringWorldY = 0.05f;
    [SerializeField] float ringWidth = 0.18f;
    [SerializeField] Color ringQuietColor = new Color(0.3f, 1f, 0.4f, 0.6f);
    [SerializeField] Color ringLoudColor = new Color(1f, 0.2f, 0.1f, 0.8f);

    [Header("Hit Flash (REAL — PlayerController.OnPlayerDamaged) — 아바타 발광")]
    [Tooltip("피격 순간 바디 _EmissionColor 피크값(HDR). 블룸과 함께 톡 튀도록 화이트+따뜻함.")]
    [SerializeField] Color hitFlashEmission = new Color(2.2f, 2.0f, 1.7f);
    [Tooltip("피크 → 원래 발광으로 되돌아오는 페이드 시간(초). 즉시 켜고 ease-out으로 감쇠.")]
    [SerializeField] float hitFlashDuration = 0.12f;

    static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    LineRenderer _ring;

    // 아바타 히트 플래시 — 바디 SkinnedMeshRenderer별 MPB로 _EmissionColor 구동.
    SkinnedMeshRenderer[] _bodyRenderers;
    MaterialPropertyBlock[] _bodyMpb;
    Color[] _bodyBaseEmission;     // 각 렌더러의 원래 발광색(피크에서 되돌아갈 목표)
    float _flashTimer;             // >0이면 페이드 진행 중. 새 피격 시 duration으로 리프레시(디바운스)

    void Awake()
    {
        CreateRing();
        CacheBodyRenderers();
    }

    // 캐릭터 바디 SkinnedMeshRenderer들을 캐시하고 렌더러별 MPB/원래 발광을 준비.
    void CacheBodyRenderers()
    {
        _bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        int n = _bodyRenderers.Length;
        _bodyMpb = new MaterialPropertyBlock[n];
        _bodyBaseEmission = new Color[n];
        for (int i = 0; i < n; i++)
        {
            _bodyMpb[i] = new MaterialPropertyBlock();
            // 공유 머티리얼의 현재 발광을 기준값으로 캡처(되돌아갈 목표). 키워드 게이팅과 무관하게 값은 읽힘.
            var mat = _bodyRenderers[i].sharedMaterial;
            _bodyBaseEmission[i] = (mat != null && mat.HasProperty(EmissionColorID))
                ? mat.GetColor(EmissionColorID)
                : Color.black;
        }
    }

    void OnEnable()
    {
        PlayerController.OnPlayerDamaged += HandlePlayerDamaged;
    }

    void OnDisable()
    {
        PlayerController.OnPlayerDamaged -= HandlePlayerDamaged;
    }

    // 피격 — 발광을 피크로 즉시 올리고 타이머를 리프레시(디바운스: 새 피격이 피크로 되돌림, 스택 없음).
    void HandlePlayerDamaged(float amount)
    {
        _flashTimer = hitFlashDuration;
        ApplyEmission(1f);   // instant-on 피크
    }

    // t(0~1): 0=원래 발광, 1=피크. 각 렌더러 MPB로 _EmissionColor 세팅(공유 머티리얼 불변).
    void ApplyEmission(float t)
    {
        if (_bodyRenderers == null) return;
        for (int i = 0; i < _bodyRenderers.Length; i++)
        {
            var r = _bodyRenderers[i];
            if (r == null) continue;
            Color c = Color.Lerp(_bodyBaseEmission[i], hitFlashEmission, t);
            r.GetPropertyBlock(_bodyMpb[i]);
            _bodyMpb[i].SetColor(EmissionColorID, c);
            r.SetPropertyBlock(_bodyMpb[i]);
        }
    }

    void CreateRing()
    {
        var go = new GameObject("NoiseRing");
        go.transform.SetParent(transform, false);
        _ring = go.AddComponent<LineRenderer>();
        _ring.useWorldSpace = true;
        _ring.loop = true;
        _ring.positionCount = ringSegments;
        _ring.widthMultiplier = ringWidth;
        _ring.numCornerVertices = 2;
        _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _ring.receiveShadows = false;
        // Sprites/Default가 LineRenderer 정점색을 정상 반영. URP에서 누락 시 폴백.
        var ringShader = Shader.Find("Sprites/Default");
        if (ringShader == null) ringShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (ringShader == null) ringShader = Shader.Find("Universal Render Pipeline/Unlit");
        _ring.material = new Material(ringShader);
        _ring.enabled = false;
    }

    void Update()
    {
        // 아바타 히트 플래시 페이드 — 언스케일드(히트스탑 timeScale≈0.05 중에도 진행). ease-out.
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.unscaledDeltaTime;
            float t = hitFlashDuration > 0f ? Mathf.Clamp01(_flashTimer / hitFlashDuration) : 0f;
            // t는 남은비율(1→0). ease-out 감쇠를 위해 제곱(꼬리가 빠르게 잦아듦).
            ApplyEmission(t * t);
            if (_flashTimer <= 0f) ApplyEmission(0f);   // 정확히 원래 발광으로 정착
        }

        // 소음 링
        var nm = NoiseManager.Instance;
        float noise = nm != null ? nm.CurrentNoise : 0f;
        float radius = nm != null ? nm.HearingRadius : 0f;
        bool show = radius > 0.1f;
        _ring.enabled = show;
        if (!show) return;

        Vector3 center = transform.position;
        center.y = ringWorldY;
        for (int i = 0; i < ringSegments; i++)
        {
            float a = (i / (float)ringSegments) * Mathf.PI * 2f;
            _ring.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }

        // 50(추격 임계)에서 완전히 붉게
        Color rc = Color.Lerp(ringQuietColor, ringLoudColor, Mathf.Clamp01(noise / 50f));
        _ring.startColor = rc;
        _ring.endColor = rc;
    }
}
