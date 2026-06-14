// MonsterToon.shader  (TEST ASSET — tone-gate re-judgment of §6 "괴수 셀셰이드 금지")
// Purpose: Posterized/banded toon shading + inverted-hull outline + albedo-flatten knobs,
//          to pull Protofactor semi-real PBR monsters toward a stylized (Borderlands-ish)
//          look so they sit on the Synty low-poly (Toon City) stage.
// Platform: Unity 6 URP (Forward / Forward+).
// Built on the proven ZombieCrush/ActorRimLit skeleton (same includes, GI/APV, multi-pass)
//          so it compiles in this project. Lighting is hand-rolled (ramp), not UniversalFragmentPBR.
//
// KNOBS (the test levers):
//   _RampSteps      : number of lighting bands (2 = hard cel, 3 = soft cel, 4+ = near-smooth)
//   _RampSmoothness : edge softness between bands (0 = razor terminator, high = washed)
//   _ShadeTint      : color multiplier in shadow (cooler shadow = more stylized depth)
//   _SatFlatten     : 0 keeps texture saturation, 1 pushes albedo toward gray luminance
//   _DetailFlatten  : 0 keeps texture detail, 1 pushes albedo toward flat _BaseColor (kills high-freq)
//   _OutlineWidth   : inverted-hull thickness in view space (0 = off)
//   _OutlineColor   : outline ink color
//   _RimIntensity   : silhouette fresnel pop (kept from ActorRimLit, secondary)
//
// NOTE on "normal-map ignore": this shader never samples a normal map (normalTS stays flat),
//   so bump detail is already gone by construction. The remaining high-freq comes from MESH
//   geometry + albedo texture; _DetailFlatten / _SatFlatten are the real flattening levers here.

Shader "ZombieCrush/MonsterToon"
{
    Properties
    {
        // --- Surface ---
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base / Flat Color", Color) = (1,1,1,1)

        // --- Albedo flatten ---
        _SatFlatten("Saturation Flatten", Range(0,1)) = 0.25
        _DetailFlatten("Detail Flatten (toward flat color)", Range(0,1)) = 0.0

        // --- Toon ramp ---
        _RampSteps("Ramp Steps", Range(1,6)) = 3
        _RampSmoothness("Ramp Smoothness", Range(0.001, 0.5)) = 0.03
        _LitBoost("Lit Band Boost", Range(0.5, 2.5)) = 1.25
        [HDR] _ShadeTint("Shadow Tint (mul)", Color) = (0.62, 0.66, 0.82, 1)
        _ShadeFloor("Shadow Floor (min light)", Range(0,1)) = 0.35

        // --- Outline (inverted hull) ---
        _OutlineColor("Outline Color", Color) = (0.04, 0.04, 0.05, 1)
        _OutlineWidth("Outline Width", Range(0, 6)) = 1.2

        // --- Fresnel rim (secondary) ---
        [HDR] _RimColor("Rim Color", Color) = (0.85, 0.92, 1.0, 1.0)
        _RimPower("Rim Power", Range(0.5, 8)) = 3.5
        _RimIntensity("Rim Intensity", Range(0, 8)) = 0.6

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
        // Pass 0: Outline  (inverted hull — back faces extruded along normal)
        //   Drawn first; depth-written so the lit pass overdraws interiors.
        // ===============================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }   // extra unlit pass, runs in forward

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _SatFlatten;
                half   _DetailFlatten;
                half   _RampSteps;
                half   _RampSmoothness;
                half   _LitBoost;
                half4  _ShadeTint;
                half   _ShadeFloor;
                half4  _OutlineColor;
                half   _OutlineWidth;
                half4  _RimColor;
                half   _RimPower;
                half   _RimIntensity;
                half   _Cutoff;
            CBUFFER_END

            struct AttributesO { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VaryingsO   { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            VaryingsO vertOutline(AttributesO IN)
            {
                VaryingsO OUT = (VaryingsO)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // Extrude along normal in *view* space so thickness is roughly screen-uniform
                // with distance (perspective-correct enough for a 15m top-down framing).
                float3 posVS    = TransformWorldToView(TransformObjectToWorld(IN.positionOS.xyz));
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 normalVS = TransformWorldToViewDir(normalWS, true);

                // width in centimeters-ish of view space, scaled by depth so it doesn't vanish far away
                float depthScale = -posVS.z * 0.012;
                posVS += normalVS * (_OutlineWidth * depthScale);

                OUT.positionCS = TransformWViewToHClip(posVS);
                return OUT;
            }

            half4 fragOutline(VaryingsO IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                return half4(_OutlineColor.rgb, 1);
            }
            ENDHLSL
        }

        // ===============================================================
        // Pass 1: Toon Forward (main light + additional lights + shadows, banded)
        // ===============================================================
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On

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
                half   _RampSteps;
                half   _RampSmoothness;
                half   _LitBoost;
                half4  _ShadeTint;
                half   _ShadeFloor;
                half4  _OutlineColor;
                half   _OutlineWidth;
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
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
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
                OUT.normalWS   = normInputs.normalWS;
                OUT.uv         = TRANSFORM_TEX(IN.texcoord, _BaseMap);
                OUT.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);

                OUTPUT_LIGHTMAP_UV(IN.staticLightmapUV, unity_LightmapST, OUT.staticLightmapUV);
                #ifdef DYNAMICLIGHTMAP_ON
                OUT.dynamicLightmapUV = IN.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
                OUTPUT_SH4(posInputs.positionWS, OUT.normalWS, GetWorldSpaceNormalizeViewDir(posInputs.positionWS), OUT.vertexSH, OUT.probeOcclusion);
                return OUT;
            }

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

            // Quantize a 0..1 light value into N bands with a soft edge between steps.
            // Borderlands-ish hard cel at steps=2, smoother at higher counts.
            half PosterizeLight(half x, half steps, half smooth)
            {
                steps = max(steps, 1.0h);
                half scaled = saturate(x) * steps;          // 0..steps
                half lower  = floor(scaled);                 // band index
                half frac_  = scaled - lower;                // 0..1 within band
                // soft transition only near the band boundary
                half edge   = smoothstep(0.5h - smooth, 0.5h + smooth, frac_);
                return (lower + edge) / steps;               // back to 0..1, banded
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // ---- Albedo + flatten ----
                half4 tex   = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;
                // detail flatten: lerp toward flat base color (kills high-freq texture)
                albedo = lerp(albedo, _BaseColor.rgb, _DetailFlatten);
                // saturation flatten: lerp toward luminance
                half  luma  = dot(albedo, half3(0.299, 0.587, 0.114));
                albedo = lerp(albedo, luma.xxx, _SatFlatten);

                half3 normalWS  = normalize(IN.normalWS);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half4 shadowMask = half4(1,1,1,1);
                half3 bakedGI   = GetBakedGI(IN, normalWS, viewDirWS, IN.positionCS.xy, shadowMask);

                // ---- Main light, banded ----
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord, IN.positionWS, shadowMask);
                half  NdotL = dot(normalWS, mainLight.direction) * 0.5h + 0.5h;   // wrap-ish 0..1
                half  shadowAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half  lightBand = PosterizeLight(NdotL * shadowAtten, _RampSteps, _RampSmoothness);

                // toon diffuse: HUE-PRESERVING. Shadow keeps albedo color but tinted+lifted toward
                // a floor (never crushes to black), lit band is a punchy boosted highlight.
                // This is the key fix: dark-albedo monsters stay readable; saturated accents survive.
                half3 lightCol  = mainLight.color * _LitBoost;
                half3 shadeMul  = lerp(_ShadeTint.rgb, half3(1,1,1), _ShadeFloor); // lifted cool shadow
                half3 litTerm   = albedo * lightCol;
                half3 shadeTerm = albedo * shadeMul;
                half3 color = lerp(shadeTerm, litTerm, lightBand);

                // ---- Ambient / GI (kept smooth, lifts the shadow side) ----
                color += albedo * bakedGI;

                // ---- Additional lights (banded too, kept cheap) ----
                // LIGHT_LOOP_BEGIN (clustered path) reads inputData.normalizedScreenSpaceUV
                // for the tile lookup, so a minimal InputData must be in scope.
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS              = IN.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light l = GetAdditionalLight(lightIndex, IN.positionWS, shadowMask);
                    half  nl = dot(normalWS, l.direction) * 0.5h + 0.5h;
                    half  band = PosterizeLight(nl * l.shadowAttenuation * l.distanceAttenuation, _RampSteps, _RampSmoothness);
                    color += albedo * l.color * band;
                LIGHT_LOOP_END
                #endif

                // ---- Fresnel rim (secondary silhouette pop) ----
                half NdotV = saturate(dot(normalWS, viewDirWS));
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
        // Pass 2: ShadowCaster
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
        // Pass 3: DepthOnly
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
