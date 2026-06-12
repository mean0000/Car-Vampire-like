using UnityEngine;

/// <summary>
/// 머즐 플래시 포인트 라이트 — 발사 시 0.05초 동안 intensity 8→0 페이드.
/// PlayerCombat.Fire() 안에서 Trigger() 호출로 활성화.
/// 색: 따뜻한 앰버 (1, 0.68, 0.28) / range 6.
/// </summary>
public class GunFlashLight : MonoBehaviour
{
    [Header("Flash Settings")]
    [SerializeField] float peakIntensity = 8f;
    [SerializeField] float duration = 0.05f;
    [SerializeField] float range = 6f;
    [SerializeField] Color flashColor = new Color(1f, 0.68f, 0.28f, 1f);

    Light _light;
    float _timer;
    bool _active;
    float _peak;   // 이번 점멸의 피크 — B-004 C2 발당 스트로브 지터가 배수로 변조

    void Awake()
    {
        _light = GetComponent<Light>();
        if (_light == null)
            _light = gameObject.AddComponent<Light>();

        _light.type = LightType.Point;
        _light.color = flashColor;
        _light.range = range;
        _light.intensity = 0f;
        _light.shadows = LightShadows.None;   // 머즐플래시는 그림자 불필요 — 성능
        _light.enabled = false;
    }

    /// <summary>발사 순간 호출 — 플래시 시작.</summary>
    public void Trigger() => Trigger(1f);

    /// <summary>강도 배수 변형 — B-004 C2 발당 스트로브 지터·강조발 배수용(PlayerCombat이 전달).</summary>
    public void Trigger(float intensityMult)
    {
        _timer = duration;
        _active = true;
        _peak = peakIntensity * intensityMult;
        _light.enabled = true;
        _light.intensity = _peak;
    }

    void Update()
    {
        if (!_active) return;

        _timer -= Time.deltaTime;
        float t = Mathf.Clamp01(_timer / Mathf.Max(0.0001f, duration));
        _light.intensity = _peak * t;

        if (_timer <= 0f)
        {
            _active = false;
            _light.intensity = 0f;
            _light.enabled = false;
        }
    }
}
