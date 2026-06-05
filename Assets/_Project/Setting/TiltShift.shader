Shader "Hidden/Style/TiltShift"
{
    Properties
    {
        _FocusCenter   ("Focus Center (uv.y)", Float) = 0.5
        _FocusHalfBand ("Focus Half Band",     Float) = 0.27
        _Falloff       ("Falloff Softness",    Float) = 0.15
        _MaxBlurRadius ("Max Blur Radius (uv)",Float) = 0.0055
        _Mode          ("Mode 0=Vert 1=2D",    Float) = 0
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
            float _Mode;

            #define TS(o) SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + (o))

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // blur strength by vertical distance from the focus band (0 inside band)
                float dist = abs(uv.y - _FocusCenter) - _FocusHalfBand;
                float b = saturate(dist / max(_Falloff, 1e-4));
                b = b * b * (3.0 - 2.0 * b);            // smoothstep ramp
                float r = b * _MaxBlurRadius;

                if (_Mode < 0.5)
                {
                    // ---- Mode 0: VERTICAL-only 9-tap gaussian (original approach) ----
                    const float w0=0.227027, w1=0.1945946, w2=0.1216216, w3=0.054054, w4=0.016216;
                    half4 col = TS(float2(0,0)) * w0;
                    float o1=r*0.25, o2=r*0.5, o3=r*0.75, o4=r;
                    col += TS(float2(0, o1))*w1; col += TS(float2(0,-o1))*w1;
                    col += TS(float2(0, o2))*w2; col += TS(float2(0,-o2))*w2;
                    col += TS(float2(0, o3))*w3; col += TS(float2(0,-o3))*w3;
                    col += TS(float2(0, o4))*w4; col += TS(float2(0,-o4))*w4;
                    return col;
                }
                else
                {
                    // ---- Mode 1: 2D isotropic disc (aspect-corrected), 17 taps ----
                    float aspect = _ScreenParams.y / max(_ScreenParams.x, 1.0); // height/width
                    half4 col = TS(float2(0,0)) * 0.20;
                    // 8 directions (45deg). x scaled by aspect so the disc is round in PIXELS.
                    const float s = 0.70710678;
                    float2 dirs[8] = {
                        float2(1,0), float2(s,s), float2(0,1), float2(-s,s),
                        float2(-1,0), float2(-s,-s), float2(0,-1), float2(s,-s)
                    };
                    [unroll] for (int i=0;i<8;i++)
                    {
                        float2 d = float2(dirs[i].x * aspect, dirs[i].y);
                        col += TS(d * (r*0.6)) * 0.06;   // inner ring
                        col += TS(d * (r*1.0)) * 0.04;   // outer ring
                    }
                    return col;  // weights: 0.20 + 8*0.06 + 8*0.04 = 1.0
                }
            }
            ENDHLSL
        }
    }
}
