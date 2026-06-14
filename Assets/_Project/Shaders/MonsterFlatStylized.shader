// MonsterFlatStylized.shader
// Purpose: Synty-style flat / low-poly look on high-poly Protofactor meshes.
//          Each triangle face reads as a single flat shading tone with a
//          subtle specular sheen — no outlines, no cel banding.
// Platform: Unity 6 URP (Forward / Forward+).
// Based on: ZombieCrush/MonsterToon skeleton (same URP includes, GI/APV, shadow passes).
//
// KEY DIFFERENCES from MonsterToon:
//   - Face normal (ddx/ddy in frag) replaces interpolated vertex normal for lighting.
//     This makes every polygon read as a distinct flat plane — the Synty silhouette.
//   - Smooth Lambert (NdotL, no PosterizeLight) + ShadeFloor / ShadeTint.
//   - Blinn-Phong specular (_Smoothness, _SpecIntensity) for the "polished plastic" sheen.
//   - Inverted-hull outline pass REMOVED.
//   - Normal map: not sampled (same as MonsterToon).
//   - _SatFlatten / _DetailFlatten albedo levers kept (user confirmed 0.6 default).
//
// KNOBS:
//   _SatFlatten    : 0 = full texture saturation, 1 = push to luminance gray
//   _DetailFlatten : 0 = keep albedo texture detail, 1 = push toward flat _BaseColor
//   _ShadeFloor    : minimum light contribution (lifts shadow, avoids pitch-black)
//   _ShadeTint     : color tint multiplied into shadow band
//   _Smoothness    : Blinn-Phong specular shininess (0 = none, 1 = mirror-ish)
//   _SpecIntensity : specular brightness scalar (keep low ~0.3 for Synty feel)
//   _RimIntensity  : optional silhouette fresnel (0 to disable)

Shader "ZombieCrush/MonsterFlatStylized"
{
    Properties
    {
        // --- Surface ---
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base / Flat Color", Color) = (1,1,1,1)

        // --- Albedo flatten (same as MonsterToon) ---
        _SatFlatten("Saturation Flatten", Range(0,1)) = 0.6
        _DetailFlatten("Detail Flatten (toward flat color)", Range(0,1)) = 0.0

        // --- Lighting ---
        _ShadeFloor("Shadow Floor (min light)", Range(0,1)) = 0.35
        [HDR] _ShadeTint("Shadow Tint (mul)", Color) = (0.62, 0.66, 0.82, 1)

        // --- Specular ---
        _Smoothness("Smoothness (Blinn-Phong)", Range(0,1)) = 0.4
        _SpecIntensity("Specular Intensity", Range(0,2)) = 0.3

        // --- Fresnel rim (optional, secondary) ---
        [HDR] _RimColor("Rim Color", Color) = (0.85, 0.92, 1.0, 1.0)
        _RimPower("Rim Power", Range(0.5, 8)) = 3.5
        _RimIntensity("Rim Intensity", Range(0, 4)) = 0.0

        // --- Required URP keywords / hidden ---
        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [HideInInspector] _Cull("__cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }
        LOD 300

        // ===============================================================
        // Pass 0: FlatForward — main light + additional lights + shadows
        // No outline pass (Synty has none).
        // Face normal computed in fragment via ddx/ddy of positionWS.
        // ===============================================================
        Pass
        {
            Name "FlatForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            // URP lighting keywords — identical set to MonsterToon
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fog
            #pragma multi_compile _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _SatFlatten;
                half   _DetailFlatten;
                half   _ShadeFloor;
                half4  _ShadeTint;
                half   _Smoothness;
                half   _SpecIntensity;
                half4  _RimColor;
                half   _RimPower;
                half   _RimIntensity;
                half   _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS        : POSITION;
                float3 normalOS          : NORMAL;
                float2 texcoord          : TEXCOORD0;
                float2 staticLightmapUV  : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                // normalWS still passed for GI / shadow sampling;
                // lighting uses face normal computed in frag instead.
                half3  normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);
                #ifdef DYNAMICLIGHTMAP_ON
                float2 dynamicLightmapUV : TEXCOORD5;
                #endif
                #ifdef USE_APV_PROBE_OCCLUSION
                float4 probeOcclusion : TEXCOORD6;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs  = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = normInputs.normalWS;   // used for GI/APV only
                OUT.uv         = TRANSFORM_TEX(IN.texcoord, _BaseMap);
                OUT.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);

                OUTPUT_LIGHTMAP_UV(IN.staticLightmapUV, unity_LightmapST, OUT.staticLightmapUV);
                #ifdef DYNAMICLIGHTMAP_ON
                OUT.dynamicLightmapUV = IN.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
                OUTPUT_SH4(posInputs.positionWS, OUT.normalWS, GetWorldSpaceNormalizeViewDir(posInputs.positionWS), OUT.vertexSH, OUT.probeOcclusion);
                return OUT;
            }

            // --- GI helper (identical to MonsterToon) ---
            half3 GetBakedGI(Varyings IN, half3 normalWS, half3 viewDirWS, float2 positionCS, inout half4 shadowMask)
            {
                #if defined(_SCREEN_SPACE_IRRADIANCE)
                    return SAMPLE_GI(_ScreenSpaceIrradiance, positionCS);
                #elif defined(DYNAMICLIGHTMAP_ON)
                    shadowMask = SAMPLE_SHADOWMASK(IN.staticLightmapUV);
                    return SAMPLE_GI(IN.staticLightmapUV, IN.dynamicLightmapUV, IN.vertexSH, normalWS);
                #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
                    return SAMPLE_GI(IN.vertexSH, GetAbsolutePositionWS(IN.positionWS), normalWS, viewDirWS, positionCS, IN.probeOcclusion, shadowMask);
                #else
                    shadowMask = SAMPLE_SHADOWMASK(IN.staticLightmapUV);
                    return SAMPLE_GI(IN.staticLightmapUV, IN.vertexSH, normalWS);
                #endif
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // ---- Face normal (the Synty flat-shading trick) ----
                // ddx/ddy of positionWS gives the per-triangle tangent/bitangent direction.
                // cross(ddx, ddy) gives the face normal in DX/Metal (left-hand screen space
                // used by Unity on all platforms). normalize guards against degenerate faces.
                float3 faceNormalWS = normalize(cross(ddx(IN.positionWS), ddy(IN.positionWS)));

                // ---- Albedo + flatten (same as MonsterToon) ----
                half4 tex    = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;
                albedo = lerp(albedo, _BaseColor.rgb, _DetailFlatten);
                half  luma   = dot(albedo, half3(0.299, 0.587, 0.114));
                albedo = lerp(albedo, luma.xxx, _SatFlatten);

                // normalWS from vertex stream: used for GI/APV baked irradiance.
                // We still normalise it — the vertex-interpolated direction is fine for
                // low-frequency baked light; only direct lighting uses face normal.
                half3 vertexNormalWS = normalize(IN.normalWS);
                half3 viewDirWS      = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half4 shadowMask     = half4(1,1,1,1);
                half3 bakedGI        = GetBakedGI(IN, vertexNormalWS, viewDirWS, IN.positionCS.xy, shadowMask);

                // ---- Main light — smooth Lambert on face normal ----
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight    = GetMainLight(shadowCoord, IN.positionWS, shadowMask);

                half NdotL_face = saturate(dot(faceNormalWS, mainLight.direction));
                half shadowAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                // Lifted Lambert: never below ShadeFloor, shadow side tinted.
                half lit = NdotL_face * shadowAtten;
                half3 shadeMul = lerp(_ShadeTint.rgb, half3(1,1,1), _ShadeFloor);
                half3 color = albedo * lerp(shadeMul, mainLight.color, saturate(lit + _ShadeFloor));

                // ---- GI / ambient (uses vertex normal — baked irradiance is smooth) ----
                color += albedo * bakedGI;

                // ---- Blinn-Phong specular on face normal ----
                half3 halfDir    = normalize(mainLight.direction + viewDirWS);
                half  NdotH      = saturate(dot(faceNormalWS, halfDir));
                // remap _Smoothness 0..1 → shininess exponent 2..256
                half  shininess  = exp2(_Smoothness * 7.0h + 1.0h);
                half  specFactor = pow(NdotH, shininess) * shadowAtten * NdotL_face;
                color += mainLight.color * (specFactor * _SpecIntensity);

                // ---- Additional lights ----
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS              = IN.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light l   = GetAdditionalLight(lightIndex, IN.positionWS, shadowMask);
                    half  nl  = saturate(dot(faceNormalWS, l.direction));
                    half  att = l.shadowAttenuation * l.distanceAttenuation;
                    color += albedo * l.color * saturate(nl * att + _ShadeFloor);
                    // specular from additional lights
                    half3 h2      = normalize(l.direction + viewDirWS);
                    half  nh2     = saturate(dot(faceNormalWS, h2));
                    half  spec2   = pow(nh2, shininess) * att * nl;
                    color += l.color * (spec2 * _SpecIntensity * 0.5h); // halved for fill lights
                LIGHT_LOOP_END
                #endif

                // ---- Fresnel rim (optional, _RimIntensity=0 to skip) ----
                half NdotV = saturate(dot(faceNormalWS, viewDirWS));
                half rim   = pow(1.0h - NdotV, _RimPower);
                color += _RimColor.rgb * (rim * _RimIntensity);

                // ---- Fog ----
                float fogCoord = InitializeInputDataFog(float4(IN.positionWS, 1.0), IN.fogFactor);
                color = MixFog(color, fogCoord);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ===============================================================
        // Pass 1: ShadowCaster
        // ===============================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // ===============================================================
        // Pass 2: DepthOnly
        // ===============================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
