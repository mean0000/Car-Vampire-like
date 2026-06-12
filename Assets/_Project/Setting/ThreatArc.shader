// 후방 위협 청각 힌트 아크 — 플레이어 발치에 뜨는 60° 방향성 파문 펄스.
// 세계관: 엘의 나노봇 패시브 스캔이 지면 진동(발소리)을 감지 — "들린다"의 시각화.
//
// ★철학(2026-06-12 동결 스펙): 정답 UI 금지. 깨끗한 UI 도형이 아니라 노이즈로
// 침식된 얼룩진 파문 — 의식하면 보이고, 안 하면 묻히는 수준이 목표.
// _Color    회백(평시) / 앰버(TTC≤0.8s 강펄스만). 빨강·시안·마젠타 금지(색 캐넌).
// _Alpha    단계 알파 × 제곱 페이드 × 조준 배율 (드라이버 RearThreatHint가 구동)
// _Progress 0→1 — 링 반경 확장 + 폭 수축(파문이 퍼지며 얇아진다)
// _Seed     펄스마다 다른 침식 패턴
Shader "ZombieCrush/ThreatArc"
{
    Properties
    {
        _Color    ("Color", Color) = (0.75, 0.75, 0.75, 1)
        _Alpha    ("Alpha", Range(0, 1)) = 0.2
        _Progress ("Progress", Range(0, 1)) = 0
        _Seed     ("Seed", Float) = 0
        _Style    ("Style (0 침식아크 1 음파아크 2 발자국블롯)", Float) = 0
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}   // UI 캔버스가 바인딩 — 샘플하지 않음
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent" "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ThreatArc"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float  _Alpha;
            float  _Progress;
            float  _Seed;
            float  _Style;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                float r = length(p) * 2.0;   // 0=중심, 1=쿼드 가장자리

                // 노이즈 — 2옥타브 밸류, 시드로 매번 다른 얼룩
                float2 np = p * 9.0 + _Seed;
                float n = VNoise(np) * 0.65 + VNoise(np * 2.3 + 17.0) * 0.35;

                // 스타일 2: 발자국 블롯 — 각도 마스크 없음, 강한 침식 = 유기적 얼룩
                if (_Style > 1.5)
                {
                    float blob = 1.0 - smoothstep(0.35, 0.8, r);
                    float a2 = saturate(blob - n * 0.55) * 1.7;
                    return half4(_Color.rgb, saturate(a2) * _Alpha);
                }

                // 60° 각도 마스크 — 로컬 +Z(=UV +y) 전방, 반각 30°, 18°부터 소프트 감쇠
                float ang = abs(atan2(p.x, p.y));
                float aMask = 1.0 - smoothstep(0.31, 0.5236, ang);
                float radius = lerp(0.30, 0.92, _Progress);

                // 스타일 1: 음파 아크 — 동심원 3겹("소리 난다" 보편 기호), 침식 약하게
                if (_Style > 0.5)
                {
                    float b1 = 1.0 - saturate(abs(r - radius) / 0.09);
                    float b2 = 1.0 - saturate(abs(r - (radius - 0.22)) / 0.07);
                    float b3 = 1.0 - saturate(abs(r - (radius - 0.44)) / 0.06);
                    float band1 = saturate(b1 + b2 * 0.6 + b3 * 0.35);
                    float a1 = saturate(band1 * aMask - n * 0.25) * 1.5;
                    return half4(_Color.rgb, saturate(a1) * _Alpha);
                }

                // 스타일 0: 침식 아크(기준점) — 확장 링, 폭 0.26→0.14
                float width = lerp(0.26, 0.14, _Progress);
                float band = 1.0 - saturate(abs(r - radius) / width);
                float a = saturate(band * aMask - n * 0.5) * 1.6;
                return half4(_Color.rgb, saturate(a) * _Alpha);
            }
            ENDHLSL
        }
    }
}
