using UnityEngine;

/// <summary>
/// 플레이어 상태(소음)를 월드 공간에서 시각화.
/// - 소음 링: 좀비가 들을 수 있는 실제 반경(HearingRadius)을 바닥에 원으로 표시.
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

    LineRenderer _ring;

    void Awake()
    {
        CreateRing();
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
