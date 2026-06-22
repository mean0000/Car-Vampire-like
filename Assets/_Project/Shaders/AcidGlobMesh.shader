// 저폴리 메시 발사체 셰이더 — 프레넬 림 발광(비교군 A).
//
// ════════ 왜 균일 가산이 아닌 프레넬 (메시 vs 빌보드 공정 비교) ════════
//   Codex 권고 = "물리형 불렛은 저폴리 메시 — 방향·실루엣이 읽힘". 단 균일 가산 발광(기존 ProjectilePool)은
//   노멀 무시라 각진 면이 안 보여 그냥 빛나는 공이 된다(블렌더서 실증한 함정).
//   → 프레넬(가장자리=시선에 수직인 면일수록 핫)로 실루엣을 띄우고, flat 노멀이라 면마다 프레넬이
//     일정 → 각진 면이 띠처럼 읽힌다. 중심은 어두운 코어 → 저폴리 입체감 보존 + HDR Bloom.
//   색 = 적 위협 캐넌 레드오렌지. 가산 + 씬 Bloom.
Shader "ZombieCrush/AcidGlobMesh"
{
    Properties
    {
        [HDR] _CoreColor ("Core Glow (HDR)", Color) = (0.9, 0.18, 0.04, 1)
        [HDR] _RimColor  ("Rim Color (HDR)", Color) = (2.4, 0.7, 0.18, 1)
        _FresnelPow ("Fresnel Power", Range(0.5, 6.0)) = 2.2
        _RimStrength ("Rim Strength", Range(0.0, 4.0)) = 1.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "MeshFresnelGlow"
            Blend One One          // 가산
            ZWrite Off
            Cull Back
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };

            float4 _CoreColor;
            float4 _RimColor;
            float  _FresnelPow;
            float  _RimStrength;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewWS = GetWorldSpaceViewDir(pos.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewWS);
                // 프레넬: 시선에 수직인 면(가장자리)일수록 1. flat 노멀이라 면마다 일정 → 각진 띠.
                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPow);
                float3 col = _CoreColor.rgb + _RimColor.rgb * fres * _RimStrength;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
