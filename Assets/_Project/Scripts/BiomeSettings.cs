using UnityEngine;

[CreateAssetMenu(fileName = "BiomeSettings", menuName = "ZombieCrush/Biome Settings")]
public class BiomeSettings : ScriptableObject
{
    [System.Serializable]
    public struct PropEntry
    {
        public GameObject prefab;
        [Range(0f, 1f)] public float density;     // 노이즈 밀도 임계값 (높을수록 희귀)
        public float minSlope;                     // 배치 가능 최소 경사각 (도)
        public float maxSlope;                     // 배치 가능 최대 경사각 (도)
        public float minHeight;                    // 배치 가능 최소 높이
        public float maxHeight;                    // 배치 가능 최대 높이
        public float minScale;                     // 스케일 랜덤 범위
        public float maxScale;
        public float minSpacing;                   // 포아송 최소 간격
    }

    public string biomeName;
    public PropEntry[] props;

    [Header("Noise Density")]
    public float densityNoiseScale = 0.04f;
    public Vector2 densityNoiseOffset;
}
