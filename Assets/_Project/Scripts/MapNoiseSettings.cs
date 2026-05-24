using UnityEngine;

[CreateAssetMenu(fileName = "MapNoiseSettings", menuName = "ZombieCrush/Map Noise Settings")]
public class MapNoiseSettings : ScriptableObject
{
    [System.Serializable]
    public struct NoiseOctave
    {
        public enum Mode { Normal, Ridged, Valley }

        public float scale;
        [Tooltip("지형 높이 기여도. 전체 octaves 합계 35 초과 시 차량 물리 불안정 가능")]
        public float amplitude;
        public Vector2 offset;
        public Mode mode;
    }

    private const float AmplitudeWarnThreshold = 35f;

    public NoiseOctave[] octaves = new NoiseOctave[]
    {
        new NoiseOctave { scale = 0.003f, amplitude = 12f,  offset = new Vector2(0f,   0f),   mode = NoiseOctave.Mode.Normal  },  // 기반 지형
        new NoiseOctave { scale = 0.006f, amplitude = 10f,  offset = new Vector2(200f, 400f), mode = NoiseOctave.Mode.Valley  },  // 계곡/협곡
        new NoiseOctave { scale = 0.009f, amplitude = 8f,   offset = new Vector2(600f, 100f), mode = NoiseOctave.Mode.Ridged  },  // 능선/산맥
        new NoiseOctave { scale = 0.025f, amplitude = 2f,   offset = new Vector2(300f, 50f),  mode = NoiseOctave.Mode.Normal  },  // 중간 굴곡
        new NoiseOctave { scale = 0.07f,  amplitude = 0.5f, offset = new Vector2(500f, 400f), mode = NoiseOctave.Mode.Normal  },  // 미세 요철
    };

    [Header("Domain Warping")]
    public bool enableDomainWarp = true;
    [Range(0.0001f, 0.05f)] public float warpScale = 0.006f;
    [Range(0f, 200f)] public float warpStrength = 18f;
    public Vector2 warpOffset = new Vector2(700f, 900f);

    [Header("Terrain Remapping")]
    [Tooltip("0~1 입력 노이즈값을 높이로 변환하는 커브. X축=노이즈값, Y축=높이 배율(0~1)")]
    public AnimationCurve heightRemap = new AnimationCurve(
        new Keyframe(0.00f, 0.00f),
        new Keyframe(0.30f, 0.02f),
        new Keyframe(0.50f, 0.15f),
        new Keyframe(0.65f, 0.50f),
        new Keyframe(0.78f, 0.82f),
        new Keyframe(0.88f, 0.95f),
        new Keyframe(1.00f, 1.00f)
    );
    [Min(0f)] public float remapMaxHeight = 22f;

    public float cityRadius = 120f;
    public float cityFlatTransition = 80f;
    [Range(0f, 1f)] public float smallTownProbability = 0.05f;

    private void OnValidate()
    {
        cityRadius = Mathf.Max(cityRadius, 0f);
        cityFlatTransition = Mathf.Clamp(cityFlatTransition, 0.001f, cityRadius > 0f ? cityRadius : 0.001f);

        if (octaves == null) return;
        float totalAmplitude = 0f;
        foreach (var oct in octaves) totalAmplitude += Mathf.Abs(oct.amplitude);
        if (totalAmplitude > AmplitudeWarnThreshold)
            Debug.LogWarning($"[MapNoiseSettings] 전체 amplitude 합계 {totalAmplitude:F1} > {AmplitudeWarnThreshold}. 차량 물리 불안정 가능.");

        warpStrength = Mathf.Clamp(warpStrength, 0f, 200f);
        warpScale    = Mathf.Max(warpScale, 0.0001f);

        if (heightRemap == null || heightRemap.length == 0)
            heightRemap = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        for (int i = 0; i < octaves.Length; i++)
        {
            if (octaves[i].amplitude < 0f)
            {
                Debug.LogWarning("[MapNoiseSettings] amplitude 음수 → 0으로 교정됨.");
                var oct = octaves[i];
                oct.amplitude = 0f;
                octaves[i] = oct;
            }
        }

        smallTownProbability = Mathf.Clamp01(smallTownProbability);
    }
}
