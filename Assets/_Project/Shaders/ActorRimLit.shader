// ActorRimLit.shader
// Purpose: URP Lit with view-direction Fresnel rim. Preserves full URP lighting
//          (main light, shadows, APV/GI, additional lights, fog). Adds rim as
//          emissive-style term so actors read against any stage lighting.
// Platform: Unity 6 URP (Forward / Forward+)
// Based on:  com.unity.render-pipelines.universal LitForwardPass.hlsl patterns

Shader "ZombieCrush/ActorRimLit"
{
    Properties
    {
        // --- Surface ---
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.3
        _Metallic("Metallic", Range(0,1)) = 0.0

        // --- Fresnel Rim ---
        [HDR] _RimColor("Rim Color", Color) = (0.8, 0.88, 1.0, 1.0)
        _RimPower("Rim Power", Range(0.5, 8)) = 3.0
        _RimIntensity("Rim Intensity", Range(0, 16)) = 1.2

        // --- Required URP keywords / hidden ---
        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [HideInInspector] _Surface("Surface Type", Float) = 0
        [HideInInspector] _Blend("Blend Mode", Float) = 0
        [HideInInspector] _SrcBlend("__src", Float) = 1
        [HideInInspector] _DstBlend("__dst", Float) = 0
        [HideInInspector] _ZWrite("__zw", Float) = 1
        [HideInInspector] _Cull("__cull", Float) = 2
        [HideInInspector] _AlphaClip("__clip", Float) = 0
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

        // ---------------------------------------------------------------
        // Pass 1: Forward Lit  (main light + additional lights + shadows)
        // ---------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            // URP lighting keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fog

            // APV (Unity 6)
            #pragma multi_compile _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ---- Constant Buffer ----
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _Smoothness;
                half   _Metallic;
                half4  _RimColor;
                half   _RimPower;
                half   _RimIntensity;
                half   _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS       : POSITION;
                float3 normalOS         : NORMAL;
                float2 texcoord         : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV: TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS           : SV_POSITION;
                float2 uv                   : TEXCOORD0;
                float3 positionWS           : TEXCOORD1;
                half3  normalWS             : TEXCOORD2;
                half   fogFactor            : TEXCOORD3;

                // SH / lightmap (slot 4 = staticLightmapUV or vertexSH depending on LIGHTMAP_ON)
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);

                #ifdef DYNAMICLIGHTMAP_ON
                float2 dynamicLightmapUV    : TEXCOORD5;
                #endif

                #ifdef USE_APV_PROBE_OCCLUSION
                float4 probeOcclusion       : TEXCOORD6;
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

                VertexPositionInputs posInputs   = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs  = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS  = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = normInputs.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.texcoord, _BaseMap);
                OUT.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);

                OUTPUT_LIGHTMAP_UV(IN.staticLightmapUV, unity_LightmapST, OUT.staticLightmapUV);
                #ifdef DYNAMICLIGHTMAP_ON
                OUT.dynamicLightmapUV = IN.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif

                OUTPUT_SH4(posInputs.positionWS, OUT.normalWS, GetWorldSpaceNormalizeViewDir(posInputs.positionWS), OUT.vertexSH, OUT.probeOcclusion);

                return OUT;
            }

            // Mirrors InitializeBakedGIData from LitForwardPass — handles all SAMPLE_GI branches
            half3 GetBakedGI(Varyings IN, half3 normalWS, half3 viewDirWS, float2 positionCS, inout half4 shadowMask)
            {
                #if defined(_SCREEN_SPACE_IRRADIANCE)
                    return SAMPLE_GI(_ScreenSpaceIrradiance, positionCS);
                #elif defined(DYNAMICLIGHTMAP_ON)
                    shadowMask = SAMPLE_SHADOWMASK(IN.staticLightmapUV);
                    return SAMPLE_GI(IN.staticLightmapUV, IN.dynamicLightmapUV, IN.vertexSH, normalWS);
                #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
                    return SAMPLE_GI(IN.vertexSH,
                        GetAbsolutePositionWS(IN.positionWS),
                        normalWS,
                        viewDirWS,
                        positionCS,
                        IN.probeOcclusion,
                        shadowMask);
                #else
                    shadowMask = SAMPLE_SHADOWMASK(IN.staticLightmapUV);
                    return SAMPLE_GI(IN.staticLightmapUV, IN.vertexSH, normalWS);
                #endif
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // --- Base albedo ---
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                half3 normalWS   = normalize(IN.normalWS);
                half3 viewDirWS  = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half4 shadowMask = half4(1,1,1,1);

                // --- Baked GI / SH ---
                half3 bakedGI = GetBakedGI(IN, normalWS, viewDirWS, IN.positionCS.xy, shadowMask);

                // --- Build InputData ---
                InputData inputData = (InputData)0;
                inputData.positionWS              = IN.positionWS;
                inputData.positionCS              = IN.positionCS;
                inputData.normalWS                = normalWS;
                inputData.viewDirectionWS         = viewDirWS;
                inputData.shadowCoord             = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord                = InitializeInputDataFog(float4(IN.positionWS, 1.0), IN.fogFactor);
                inputData.vertexLighting           = half3(0,0,0);
                inputData.bakedGI                 = bakedGI;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask              = shadowMask;

                // --- Build SurfaceData ---
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo     = albedoAlpha.rgb;
                surfaceData.alpha      = albedoAlpha.a;
                surfaceData.metallic   = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS   = half3(0, 0, 1);
                surfaceData.occlusion  = 1.0;

                // --- Full URP PBR shading ---
                half4 litColor = UniversalFragmentPBR(inputData, surfaceData);

                // --- View-direction Fresnel rim ---
                // Fresnel = pow(1 - saturate(N.V), rimPower)
                // Added as emissive after PBR so edges glow even in total darkness.
                half NdotV     = saturate(dot(normalWS, viewDirWS));
                half rim       = pow(1.0h - NdotV, _RimPower);
                litColor.rgb  += _RimColor.rgb * (rim * _RimIntensity);

                // --- Fog ---
                litColor.rgb = MixFog(litColor.rgb, inputData.fogCoord);

                return litColor;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // Pass 2: ShadowCaster
        // ---------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // Pass 3: DepthOnly
        // ---------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // Pass 4: Meta (lightmapping)
        // ---------------------------------------------------------------
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.LitShader"
}
