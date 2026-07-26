Shader "ZombieCrush/AfterimageGhost"
{
    // 대시 잔상 고스트 — 베이크된 스킨드 메시 스냅샷용. ★R14(07-13 유저): 바디 필 제거,
    // 테두리(프레넬 림)만 — _BodyFloor 0, 림은 파워 올려 조이고 부스트로 가독 확보.
    // 알파 블렌드라 촘촘히 겹쳐도 시안 유지(가산 흰색 번짐 없음). 알파는 _Alpha로 고스트별 페이드.
    Properties
    {
        _BaseColor ("Color", Color) = (0.2, 0.9, 1, 1)
        _Alpha ("Alpha", Range(0,1)) = 1
        _FresnelPower ("Fresnel Power", Float) = 4
        _RimBoost ("Rim Boost", Float) = 2
        _BodyFloor ("Body Floor", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha   // 알파 — 시안 솔리드 필(겹쳐도 흰색 안 됨)
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
                float fill = saturate(_BodyFloor + fres * _RimBoost);   // 림만(_BodyFloor 0) — 필 복구는 노브로
                float a = fill * _Alpha;
                return half4(_BaseColor.rgb, a);   // rgb=HDR 시안(블룸), a=필 — 알파 블렌드로 솔리드 색 유지
            }
            ENDHLSL
        }
    }
    Fallback Off
}
