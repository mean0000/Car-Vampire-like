using UnityEngine;

/// <summary>
/// XP 오브 위습 발광 효과.
/// 하늘색 자체 Emission만 제어 — 주변 물체에 빛 영향 없음.
/// 부드러운 맥동 + 이중 사인파로 신비로운 불규칙성 구현.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class NanobotBlink : MonoBehaviour
{
    [SerializeField] Color  wispColor      = new Color(0.4f, 0.85f, 1f); // 하늘색 기본값
    [SerializeField] float pulseFrequency  = 1.8f;   // 느린 숨쉬기 맥동 (Hz)
    [SerializeField] float shimmerFreq     = 5.3f;   // 빠른 잔물결 (불규칙함 부여)
    [SerializeField] float shimmerAmount   = 0.18f;  // 잔물결 강도 (0~1)
    [SerializeField] float minIntensity    = 3f;     // Bloom threshold(1.0) 이상 유지
    [SerializeField] float maxIntensity    = 10f;    // 피크 시 강하게 번짐
    [SerializeField] float flashChance     = 0.004f; // 가끔 밝게 번쩍일 확률
    [SerializeField] float flashMultiplier = 2f;     // 번쩍일 때 강도 배수

    Material _mat;
    float _timeOffset;

    static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        _mat = GetComponent<Renderer>().material;
        _timeOffset = Random.value * 100f; // 오브마다 위상 다르게

        // 프리팹에 Point Light가 붙어 있다면 비활성화
        Light existingLight = GetComponent<Light>();
        if (existingLight != null) existingLight.enabled = false;
    }

    void Update()
    {
        float t = Time.time + _timeOffset;

        // 주 맥동: 느린 숨쉬기
        float pulse = (Mathf.Sin(t * pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;

        // 잔물결: 빠른 미세 떨림으로 불규칙한 위습 느낌
        float shimmer = (Mathf.Sin(t * shimmerFreq * Mathf.PI * 2f) + 1f) * 0.5f;

        float intensity = Mathf.Lerp(minIntensity, maxIntensity, pulse + shimmer * shimmerAmount);

        // 드물게 강하게 번쩍 — 위습의 갑작스러운 빛
        if (Random.value < flashChance)
            intensity = maxIntensity * flashMultiplier;

        // Emission만 업데이트 (주변 조명 영향 없이 자체 발광)
        _mat.SetColor(EmissionColorID, wispColor * intensity);
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }
}
