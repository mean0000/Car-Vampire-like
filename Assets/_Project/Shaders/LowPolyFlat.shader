// LowPolyFlat.shader
// Purpose : URP 17.0.3 (Unity 6) flat-shading shader for low-poly meshes.
//           Uses ddx/ddy to reconstruct a per-face (flat) normal in the
//           fragment stage so each polygon face renders as a single solid tone.
//           Responds to the main Directional Light via a simple Lambert term.
//           SRP Batcher-compatible (all per-material data in CBUFFER).
// Inputs  : _BaseColor (RGBA), scene Directional Light (automatic URP binding)
// Platform: URP 17.0.3 / Unity 6, Shader Model 3.5+

Shader "ZombieCrush/LowPolyFlat"
{
    Properties
    {
        // ---- Surface ----
        [MainColor] _BaseColor ("Color", Color) = (0.5, 0.55, 0.45, 1.0)

        // ---- Shadow / lighting tweaks ----
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.6

        // ---- Internal URP blending state (do not expose in UI) ----
        [HideInInspector] _Cull       ("__cull",       Float) = 2.0
        [HideInInspector] _ZWrite     ("__zwrite",     Float) = 1.0
        [HideInInspector] _SrcBlend   ("__src",        Float) = 1.0
        [HideInInspector] _DstBlend   ("__dst",        Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        // ------------------------------------------------------------------ //
        // Pass 1 – ForwardLit                                                 //
        // Performs flat shading with a single Lambert Directional Light term. //
        // ------------------------------------------------------------------ //
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend   [_SrcBlend] [_DstBlend]
            ZWrite  [_ZWrite]
            Cull    [_Cull]

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            // Required URP shader feature keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            // SRP Batcher requirement: SM 3.5+ for derivatives
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ---- SRP Batcher CBUFFER ------------------------------------ //
            // All per-material properties MUST live inside this block.
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _ShadowStrength;
            CBUFFER_END

            // ---- Vertex input / output structs -------------------------- //
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;   // world-space pos for ddx/ddy
                float4 shadowCoord : TEXCOORD1;
                float  fogFactor   : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---- Vertex shader ----------------------------------------- //
            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.shadowCoord = GetShadowCoord(posInputs);
                OUT.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);

                return OUT;
            }

            // ---- Fragment shader --------------------------------------- //
            half4 Frag(Varyings IN) : SV_Target
            {
                // --- Reconstruct flat face normal via screen-space derivatives ---
                // ddx/ddy give the rate of change of world position across a 2x2
                // pixel quad. Their cross product is perpendicular to the polygon
                // surface — one constant value per triangle (flat shading).
                float3 dx = ddx(IN.positionWS);
                float3 dy = ddy(IN.positionWS);
                float3 flatNormalWS = normalize(cross(dy, dx)); // CCW winding

                // --- Main directional light --------------------------------- //
                Light mainLight = GetMainLight(IN.shadowCoord);

                // Lambert diffuse: saturate(dot(N, L))
                // _ShadowStrength blends between fully lit (1.0) and shadowed.
                half NdotL = saturate(dot(flatNormalWS, mainLight.direction));

                // Shadow attenuation from URP's cascade shadow maps
                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);

                // Final light contribution: diffuse + a small ambient floor so
                // back-faces are never completely black
                half ambientFloor = 0.15h;
                half lighting = max(ambientFloor, NdotL) * shadow;
                half3 litColor = _BaseColor.rgb * mainLight.color * lighting;

                // --- Fog --------------------------------------------------- //
                half3 finalColor = MixFog(litColor, IN.fogFactor);

                return half4(finalColor, _BaseColor.a);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ //
        // Pass 2 – ShadowCaster                                               //
        // Allows this object to cast shadows onto other surfaces.             //
        // ------------------------------------------------------------------ //
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest  LEqual
            Cull   [_Cull]
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _ShadowStrength;
            CBUFFER_END

            // Unity 6 / URP 17 exposes _LightDirection and _LightPosition
            // through the Shadows.hlsl include automatically.
            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionHCS : SV_POSITION;
            };

            ShadowVaryings ShadowVert(ShadowAttributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                ShadowVaryings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);

                // Bias the shadow position along the light direction to reduce
                // self-shadowing artifacts (shadow acne).
                OUT.positionHCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));

                return OUT;
            }

            half4 ShadowFrag(ShadowVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ //
        // Pass 3 – DepthOnly                                                  //
        // Used by URP's depth pre-pass (SSAO, depth-of-field, etc.)          //
        // ------------------------------------------------------------------ //
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull   [_Cull]

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _ShadowStrength;
            CBUFFER_END

            struct DepthAttributes { float4 positionOS : POSITION; };
            struct DepthVaryings   { float4 positionHCS : SV_POSITION; };

            DepthVaryings DepthVert(DepthAttributes IN)
            {
                DepthVaryings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half DepthFrag(DepthVaryings IN) : SV_Target
            {
                return IN.positionHCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
