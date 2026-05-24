using UnityEngine;

/// <summary>
/// XP 오브 내부 붉은 코어의 레이저 깜빡임 효과.
/// Emission + Point Light 강도를 함께 제어해 실제 주변을 붉게 밝힘.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class NanobotBlink : MonoBehaviour
{
    [SerializeField] float blinkFrequency  = 9f;    // 초당 깜빡임 횟수
    [SerializeField] float minIntensity    = 0.3f;  // Emission 최소 강도
    [SerializeField] float maxIntensity    = 4f;    // Emission 최대 강도
    [SerializeField] float flashChance     = 0.015f;// 매 프레임 랜덤 플래시 확률
    [SerializeField] float lightMinRange   = 1.2f;  // Point Light 최소 범위
    [SerializeField] float lightMaxRange   = 2.5f;  // Point Light 최대 범위
    [SerializeField] float lightMaxIntensity = 2.5f;// Point Light 최대 강도

    Material _mat;
    Light _light;

    static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        _mat = GetComponent<Renderer>().material;
        _light = GetComponent<Light>();
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * blinkFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);

        if (Random.value < flashChance)
            intensity = maxIntensity * 1.5f;

        // Emission 업데이트
        _mat.SetColor(EmissionColorID, Color.red * intensity);

        // Point Light 강도 + 범위도 동기화
        if (_light != null)
        {
            float normalized = Mathf.InverseLerp(minIntensity, maxIntensity, intensity);
            _light.intensity = normalized * lightMaxIntensity;
            _light.range     = Mathf.Lerp(lightMinRange, lightMaxRange, normalized);
        }
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }
}
