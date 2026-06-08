using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 분위기용 절차적 그래픽(에셋 0): 은은한 스캔라인 오버레이 + 부서 인장 링.
/// 텍스처를 런타임 생성해 RawImage/Image에 주입한다(스프라이트 에셋 불필요).
/// </summary>
public class TitleFX : MonoBehaviour
{
    [Header("Scanlines")]
    public RawImage scanlines;
    [Range(0f, 1f)] public float scanlineAlpha = 0.18f;
    public int scanlinePeriod = 4;   // px 주기(1px 어두움 + 나머지 투명)

    [Header("Seal Ring")]
    public Image sealRing;
    public Color ringColor = new Color(0.059f, 0.659f, 0.769f, 1f); // #0FA8C4 cyan-d
    public float ringThickness = 4f;  // 128px 텍스처 기준 두께

    void Start()
    {
        if (sealRing != null) BuildRing();
        if (scanlines != null) StartCoroutine(BuildScanlinesNextFrame());
    }

    IEnumerator BuildScanlinesNextFrame()
    {
        yield return null; // 레이아웃 계산 후 rect 크기 확보
        BuildScanlines();
    }

    void BuildScanlines()
    {
        int period = Mathf.Max(2, scanlinePeriod);
        var tex = new Texture2D(1, period, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        var cols = new Color[period];
        cols[0] = new Color(0f, 0f, 0f, scanlineAlpha);
        for (int i = 1; i < period; i++) cols[i] = new Color(0f, 0f, 0f, 0f);
        tex.SetPixels(cols);
        tex.Apply();

        scanlines.texture = tex;
        scanlines.color = Color.white;

        float h = scanlines.rectTransform.rect.height;
        if (h < 1f) h = Screen.height;
        scanlines.uvRect = new Rect(0f, 0f, 1f, h / period);
    }

    void BuildRing()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color[size * size];
        float c = (size - 1) / 2f;
        float outer = c - 1f;
        float inner = outer - Mathf.Max(1f, ringThickness);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = 0f;
                if (d <= outer && d >= inner) a = 1f;                       // 링 본체
                else if (d > outer && d < outer + 1.5f) a = outer + 1.5f - d; // 외곽 AA
                else if (d < inner && d > inner - 1.5f) a = d - (inner - 1.5f); // 내곽 AA
                a = Mathf.Clamp01(a);
                px[y * size + x] = new Color(ringColor.r, ringColor.g, ringColor.b, a * ringColor.a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();

        sealRing.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        sealRing.color = Color.white;
    }
}
