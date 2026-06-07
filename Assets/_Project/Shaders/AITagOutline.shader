Shader "ZombieCrush/AITagOutline"
{
    // Inverted-hull silhouette outline, drawn by a URP Render Objects feature
    // over the "Zombie" GameObject layer. The zombie body still renders with its
    // original material in the opaque pass; this pass adds only the cyan threat-
    // tag edge on top (diegetic = 엘's AI scan). View-space expansion keeps a
    // near-constant screen-space width regardless of distance.
    //
    // PER-OBJECT GATING: the pass runs for every zombie, but each fragment is
    // discarded unless the renderer's Rendering Layer bit (_TagRenderingLayer)
    // is set. ScanController toggles renderer.renderingLayerMask per zombie so
    // only currently-tagged (scan-swept, aggroed) zombies show the outline. This
    // avoids touching the physics layer (layer 7 is locked to weapon targeting).
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (0, 1.7, 2.0, 1)
        _OutlineWidth ("Outline Width (screen)", Float) = 2.5
        _TagRenderingLayer ("Tag Rendering Layer Bit (mask)", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "AITagOutline"
            Cull Front
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _TagRenderingLayer;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                float3 positionVS = TransformWorldToView(positionWS);
                float3 normalVS   = TransformWorldToViewDir(normalWS);

                // Offset scaled by linear view depth (-z) => constant screen width.
                float2 dir = normalize(normalVS.xy + 1e-5);
                positionVS.xy += dir * (_OutlineWidth * 0.001) * (-positionVS.z);

                OUT.positionCS = mul(UNITY_MATRIX_P, float4(positionVS, 1.0));
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Per-object gate: only draw the outline if this renderer's
                // Rendering Layer carries the tag bit (set by ScanController).
                // unity_RenderingLayer.x stores the mask as a reinterpreted float,
                // so it must be read with asuint(), not a numeric cast.
                uint renderingLayer = asuint(unity_RenderingLayer.x);
                uint tagBit = (uint)_TagRenderingLayer;
                clip((renderingLayer & tagBit) != 0u ? 1.0 : -1.0);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
