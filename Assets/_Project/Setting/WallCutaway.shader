// 벽 높이 컷어웨이 — 일정 월드 높이 위를 디더로 걷어내 부감 시야를 연다 (2026-06-11 유저 콜).
// 게임플레이(콜라이더 h5·좀비 LOS·총알 차단)는 그대로 — 렌더만 자른다.
// ShadowCaster는 디더 없이 풀 높이 유지(그림자/은닉 무드 보존), DepthOnly는 컬러와 동일 디더.
Shader "ZombieCrush/WallCutaway"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.85, 0.82, 0.8, 1)
        _CutStart("Cutaway Start Height (world Y)", Float) = 999
        _CutEnd("Cutaway End Height (world Y)", Float) = 1000
        _Dissolve("Occlusion Dissolve (0=solid, 1=ghost)", Range(0, 1)) = 0
        _DissolveFloor("Dissolve Max Removal (잔존 실루엣 보장)", Range(0.3, 1)) = 0.65
        _SolidBase("Dissolve-Free Base Height (m) — 발치는 항상 남김", Float) = 0.5
        _SolidBaseFade("Base → Ghost 전환 폭 (m)", Float) = 0.4
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _CutStart;
            float _CutEnd;
            float _Dissolve;
            float _DissolveFloor;
            float _SolidBase;
            float _SolidBaseFade;
        CBUFFER_END

        // 4x4 Bayer — 높이 그라데이션을 픽셀 디더로 변환 (반투명 정렬 문제 없음)
        float Bayer4(float2 pixel)
        {
            const float4x4 B = float4x4(
                 0.0,  8.0,  2.0, 10.0,
                12.0,  4.0, 14.0,  6.0,
                 3.0, 11.0,  1.0,  9.0,
                15.0,  7.0, 13.0,  5.0);
            int2 p = int2(fmod(pixel, 4.0));
            return (B[p.x][p.y] + 0.5) / 16.0;
        }

        void HeightDitherClip(float worldY, float2 pixelPos)
        {
            // 높이 컷(옵션, 기본 비활성) + 차단 디졸브(드라이버 구동) 중 큰 쪽.
            // 디졸브는 _DissolveFloor까지만(잔존 픽셀이 면으로 읽힘) + 발치 _SolidBase는 면제 —
            // 풀 고스트여도 벽의 발치와 실루엣이 남아 "벽이 서 있다"는 질량 신호 유지.
            float heightT = saturate((worldY - _CutStart) / max(_CutEnd - _CutStart, 0.001));
            float baseKeep = saturate((worldY - _SolidBase) / max(_SolidBaseFade, 0.001));
            float t = max(heightT, _Dissolve * _DissolveFloor * baseKeep);
            clip(Bayer4(pixelPos) - t);   // t=0 전부 유지, t=1 전부 제거
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                HeightDitherClip(IN.positionWS.y, IN.positionCS.xy);

                float3 n = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 diffuse = saturate(dot(n, mainLight.direction)) * mainLight.color * mainLight.shadowAttenuation;
                half3 ambient = SampleSH(n);
                return half4(_BaseColor.rgb * (diffuse + ambient), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings shadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return OUT;
            }

            half4 shadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings depthVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 depthFrag(Varyings IN) : SV_Target
            {
                HeightDitherClip(IN.positionWS.y, IN.positionCS.xy);
                return 0;
            }
            ENDHLSL
        }

        // DepthNormals — SSAO/HTraceSSGI가 노멀 버퍼를 읽는다(리뷰 Stab H-3/Codex 1: 부재 시 벽 주변 GI 오염).
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex dnVert
            #pragma fragment dnFrag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            Varyings dnVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 dnFrag(Varyings IN) : SV_Target
            {
                HeightDitherClip(IN.positionWS.y, IN.positionCS.xy);
                return half4(normalize(IN.normalWS), 0);
            }
            ENDHLSL
        }
    }
}
