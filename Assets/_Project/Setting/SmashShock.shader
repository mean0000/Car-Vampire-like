// 슬램 임팩트 충격파 — 착탄 순간 바깥으로 퍼지는 가산(additive) 발광 링 + 중심 플래시.
//   ★ThreatArc(장판)와 의도적으로 다르다: 장판 = alpha-blend 바닥 데칼(예고), 이건 = additive 발광(터졌다).
//   장판이 "비켜라"라면 이건 "맞았다/쾅" — 어두운 월드에서 핀다(가산이라 HDR로 블룸 먹음).
//
//   ★월드 미터 공간(_SizeWorld) SDF — 쿼드 localScale == _SizeWorld 전제(ThreatArc/TelegraphPad와 동일 규약).
//   드라이버(SmashImpactFX)가 _Progress 0→1로 구동: 링 반경 0→max 확장 + 폭 수축 + 알파 페이드.
//
//   _Color     충격파 색(HDR 가산 — 위협 레드오렌지 권장, 톤=유저 판정).
//   _Progress  0→1 — 링 확장·수축·페이드의 단일 위상.
//   _SizeWorld x = 최대 지름(m). 쿼드 localScale.xy와 일치해야 SDF가 월드미터.
//   _RingWidth 0(시작) 링 폭(m) — _Progress로 절반까지 수축(퍼지며 얇아짐).
//   _CoreFlash 중심 플래시 강도(임팩트 0.0~0.15 구간 짧은 흰핵).
//   _Intensity 가산 밝기 배율(블룸 먹이 — 1~3).
Shader "ZombieCrush/SmashShock"
{
    Properties
    {
        _Color     ("Color (HDR)", Color) = (1, 0.32, 0.10, 1)
        _Progress  ("Progress", Range(0, 1)) = 0
        _SizeWorld ("Size World (m) x=max diameter", Vector) = (6, 6, 0, 0)
        _RingWidth ("Ring Width (m)", Float) = 0.9
        _CoreFlash ("Core Flash Strength", Range(0, 4)) = 1.6
        _Intensity ("Additive Intensity", Float) = 1.8
        _Seed      ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SmashShock"
            Cull Off
            ZWrite Off
            // ★바닥 깊이 존중(LEqual) — 몬스터/벽이 충격파를 가린다(임팩트는 바닥에서 핀다). 장판과 같은 깊이 규약.
            ZTest LEqual
            Blend SrcAlpha One     // ★가산 — 어두운 월드에서 발광·블룸. (장판은 SrcAlpha OneMinusSrcAlpha)

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float  _Progress;
            float4 _SizeWorld;
            float  _RingWidth;
            float  _CoreFlash;
            float  _Intensity;
            float  _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float VNoise(float2 p)
            {
                float2 i = floor(p); float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 pm = (i.uv - 0.5) * _SizeWorld.xy;   // 쿼드 중심 원점, 월드 미터
                float Rmax = _SizeWorld.x * 0.5;
                float rr = length(pm);

                // ── 확장 링: 반경 = Rmax·smooth(_Progress), 퍼질수록 폭 수축(파문이 얇아진다) ──
                //   ease-out(빠르게 터지고 끝에서 감속) — 1-(1-p)^2.
                float pe = 1.0 - (1.0 - _Progress) * (1.0 - _Progress);
                float ringR = Rmax * pe;
                float width = _RingWidth * (1.0 - 0.6 * _Progress);   // 폭 0.9→수축
                width = max(width, 0.05);

                // 링 밴드 — 가장자리 약간 침식(거친 충격파 질감, 정답UI 회피).
                float2 np = pm * 1.5 + _Seed;
                float n = VNoise(np) * 0.6 + VNoise(np * 2.7 + 11.0) * 0.4;
                float ring = 1.0 - saturate(abs(rr - ringR) / width);
                ring = saturate(ring - n * 0.25);
                ring = ring * ring;   // 코어 집중(밝은 중심선)

                // ── 중심 플래시: 임팩트 초반(_Progress 0~0.25)에만 짧게 핀 흰핵(쾅의 섬광) ──
                float flashLife = saturate(1.0 - _Progress / 0.25);
                float core = (1.0 - smoothstep(0.0, Rmax * 0.28, rr)) * flashLife * flashLife;

                // ── 전체 페이드: 끝으로 갈수록 사라짐(퍼지고 소멸) ──
                float fade = 1.0 - smoothstep(0.55, 1.0, _Progress);

                float a = saturate(ring + core * _CoreFlash) * fade;
                // 가산이라 알파가 밝기 = rgb에 직접 실어 _Intensity로 블룸 먹임.
                float3 rgb = _Color.rgb * _Intensity * a;
                // 중심 플래시는 흰끼 살짝 더(섬광은 색보다 밝음).
                rgb += core * _CoreFlash * flashLife * 0.5;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
