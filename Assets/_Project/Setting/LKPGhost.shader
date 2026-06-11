// LKP(Last Known Position) 실루엣 — 시야 콘에서 사라진 "움직이던" 좀비의 마지막 위치 잔상.
// 베이크된 메시를 반투명 단색으로 그리고 _Fade(0~1)로 사라진다. 라이팅 무관(언릿).
Shader "ZombieCrush/LKPGhost"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.85, 0.92, 1.0, 0.45)
        _Fade      ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }

        Pass
        {
            Name "LKPGhost"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float  _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                return half4(_BaseColor.rgb, _BaseColor.a * _Fade);
            }
            ENDHLSL
        }
    }
}
