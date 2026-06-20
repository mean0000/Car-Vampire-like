Shader "ZombieCrush/AfterimageGhost"
{
    // 대시 잔상 고스트 — 베이크된 스킨드 메시 스냅샷용. 프레넬 림으로 실루엣을 살리고(바디는 옅게),
    // 가산 블렌드로 시안 에너지 잔상. 알파는 _Alpha(MaterialPropertyBlock)로 고스트별 페이드.
    Properties
    {
        _BaseColor ("Color", Color) = (0.2, 0.9, 1, 1)
        _Alpha ("Alpha", Range(0,1)) = 1
        _FresnelPower ("Fresnel Power", Float) = 2.5
        _RimBoost ("Rim Boost", Float) = 1.6
        _BodyFloor ("Body Floor", Range(0,1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha One     // 가산
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewDirWS : TEXCOORD1; };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Alpha;
                float _FresnelPower;
                float _RimBoost;
                float _BodyFloor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(p.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float3 v = normalize(IN.viewDirWS);
                float fres = pow(1.0 - saturate(dot(n, v)), _FresnelPower);
                float intensity = (fres * _RimBoost + _BodyFloor) * _Alpha;
                return half4(_BaseColor.rgb * intensity, intensity);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
