using UnityEngine;

public static class BiomeResolver
{
    public enum BiomeType { RuinedCity, DryPlains, Desert, Polar, Forest }

    // 각 바이옴의 시작 거리
    private static readonly float[] BiomeStarts = { 0f, 150f, 800f, 1200f, 1600f };
    private const float BlendWidth = 80f;  // 바이옴 경계 블렌딩 폭

    // 해당 좌표의 주 바이옴 반환
    public static BiomeType GetBiome(float worldX, float worldZ)
    {
        float dist = Mathf.Sqrt(worldX * worldX + worldZ * worldZ);
        for (int i = BiomeStarts.Length - 1; i >= 0; i--)
            if (dist >= BiomeStarts[i]) return (BiomeType)i;
        return BiomeType.RuinedCity;
    }

    // 인접 바이옴으로 블렌딩 비율 (0=현재 바이옴 순수, 1=다음 바이옴 순수)
    public static float GetBlendT(float worldX, float worldZ)
    {
        float dist = Mathf.Sqrt(worldX * worldX + worldZ * worldZ);
        for (int i = 0; i < BiomeStarts.Length - 1; i++)
        {
            float nextStart = BiomeStarts[i + 1];
            float blendStart = nextStart - BlendWidth;
            if (dist >= blendStart && dist < nextStart)
                return (dist - blendStart) / BlendWidth;
        }
        return 0f;
    }
}
