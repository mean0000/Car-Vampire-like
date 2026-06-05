Shader "Hidden/Style/TiltShift"
{
    Properties
    {
        _FocusCenter   ("Focus Center (uv.y)", Float) = 0.5
        _FocusHalfBand ("Focus Half Band",     Float) = 0.27
        _Falloff       ("Falloff Softness",    Float) = 0.15
        _MaxBlurRadius ("Max Blur Radius (uv)",Float) = 0.0055
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "TiltShift"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Blit.hlsl"

            float _FocusCenter;
            float _FocusHalfBand;
            float _Falloff;
            float _MaxBlurRadius;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // blur strength by vertical distance from the focus band (0 inside band)
                float dist = abs(uv.y - _FocusCenter) - _FocusHalfBand;
                float b = saturate(dist / max(_Falloff, 1e-4));
                b = b * b * (3.0 - 2.0 * b);            // smoothstep ramp
                float r = b * _MaxBlurRadius;

                // 9-tap vertical gaussian (normalized weights)
                const float w0 = 0.227027;
                const float w1 = 0.1945946;
                const float w2 = 0.1216216;
                const float w3 = 0.054054;
                const float w4 = 0.016216;

                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv) * w0;
                float o1 = r * 0.25, o2 = r * 0.5, o3 = r * 0.75, o4 = r;
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0,  o1)) * w1;
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0, -o1)) * w1;
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0,  o2)) * w2;
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0, -o2)) * w2;
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0,  o3)) * w3;
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0, -o3)) * w3;
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0,  o4)) * w4;
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0, -o4)) * w4;
                return col;
            }
            ENDHLSL
        }
    }
}
