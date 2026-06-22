// 빌보드 SDF 발사체 셰이더 — 카메라를 향하는 쿼드 + 절차적 코어+글로우.
//
// ════════ 왜 이 셰이더 (메시 vs 빌보드 비교군 B) ════════
//   웹 리서치 권고 = "탑다운 거리에선 작은 메시보다 빌보드 코어+글로우가 더 읽힘".
//   이건 그 빌보드 후보 — 텍스처 0, SDF로 코어(흰-핫)+글로우(레드오렌지)를 절차 생성.
//   쿼드 메시(Unity 기본 Quad, XY평면 -0.5..0.5)를 정점 셰이더에서 카메라 정면으로 세움 →
//   45° 탑다운에서도 항상 원형(타원 왜곡 없음). 가산 블렌딩 + 씬 Bloom이 글로우 헤일로를 만든다.
//
//   색 = 적 위협 캐넌 레드오렌지(글로우) + 흰-핫 코어. HDR(>1)이라 Bloom이 문다.
Shader "ZombieCrush/AcidGlobBillboard"
{
    Properties
    {
        [HDR] _CoreColor ("Core Color (HDR)", Color) = (3.0, 3.0, 2.4, 1)
        [HDR] _GlowColor ("Glow Color (HDR)", Color) = (2.2, 0.6, 0.15, 1)
        _CoreSize  ("Core Size", Range(0.0, 0.5)) = 0.12
        _GlowSize  ("Glow Size", Range(0.05, 0.71)) = 0.5
        _GlowPow   ("Glow Falloff", Range(1.0, 6.0)) = 2.4
        _Softness  ("Core Edge Softness", Range(0.001, 0.3)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "BillboardGlow"
            Blend One One          // 가산(Additive) — 어두운 배경 위 발광
            ZWrite Off
            Cull Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            float4 _CoreColor;
            float4 _GlowColor;
            float  _CoreSize;
            float  _GlowSize;
            float  _GlowPow;
            float  _Softness;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                // 오브젝트 월드 스케일 추출(빌보드가 transform.scale을 따르게).
                float3 scale = float3(
                    length(unity_ObjectToWorld._m00_m10_m20),
                    length(unity_ObjectToWorld._m01_m11_m21),
                    length(unity_ObjectToWorld._m02_m12_m22));
                // 오브젝트 중심을 뷰 공간으로 → 쿼드 xy를 뷰 평면 오프셋으로 더함(항상 카메라 정면).
                float3 centerVS = mul(UNITY_MATRIX_MV, float4(0,0,0,1)).xyz;
                centerVS.xy += IN.positionOS.xy * scale.xy;
                OUT.positionCS = mul(UNITY_MATRIX_P, float4(centerVS, 1.0));
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // UV 중심(0.5,0.5)에서의 거리 = 원형 SDF (0=중심 .. ~0.707=모서리).
                float d = distance(IN.uv, float2(0.5, 0.5));

                // 코어: 중심의 흰-핫 디스크(부드러운 가장자리).
                float core = 1.0 - smoothstep(_CoreSize, _CoreSize + _Softness, d);

                // 글로우: 바깥으로 멀어질수록 감쇠(거듭제곱으로 핫코어 집중).
                float glow = saturate(1.0 - d / _GlowSize);
                glow = pow(glow, _GlowPow);

                float3 col = _CoreColor.rgb * core + _GlowColor.rgb * glow;
                // 가산이라 알파 불요 — rgb가 가장자리에서 0으로 떨어져 자연 페이드.
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
