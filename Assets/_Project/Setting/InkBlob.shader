// 처리 스냅샷(Purge Snapshot) 잉크 실루엣 — 임팩트 프레임 화이트아웃 위에 찍히는 검은 잉크 얼룩.
// 세계관: 파트너 AI "엘"이 처리 기록을 캡처하는 순간의 1컷(애니메이션식 임팩트 프레임의 절차 생성판).
//
// UI Image 머티리얼용(캔버스 호환): ZTest Always / ZWrite Off / 알파 블렌드 / 버텍스 컬러 곱.
// 절차 생성: fbm 노이즈 도메인 워프 + 다단 스레숄드 → "찢어진 잉크" 윤곽,
//            바깥 링의 높은 스레숄드에서 위성 얼룩(스플래터 도트) 2~3개가 자연히 섬으로 분리.
// _Seed       매번 다른 얼룩 모양 (노이즈 도메인 오프셋)
// _StretchDir shotDir의 스크린 투영(정규화 2D) — 잉크가 탄 방향으로 튄 비대칭 늘림
// _Stretch    늘림 강도 (탄 방향 쪽만 길게, 반대쪽은 1/4)
// _Growth     0→1 성장 — 윤곽 반경 확대 + 위성 얼룩 등장 (드라이버가 4프레임 동안 구동)
Shader "ZombieCrush/InkBlob"
{
    Properties
    {
        _InkColor   ("Ink Color", Color) = (0.039, 0.039, 0.039, 1)   // #0A0A0A — 거의 검정
        _Seed       ("Seed", Float) = 0
        _StretchDir ("Stretch Dir (xy)", Vector) = (1, 0, 0, 0)
        _Stretch    ("Stretch", Range(0, 2)) = 0.6
        _Growth     ("Growth", Range(0, 1)) = 1
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}   // UI 캔버스가 바인딩 — 샘플하지 않음
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent" "RenderType" = "Transparent"
            "IgnoreProjector" = "True" "PreviewType" = "Plane"
        }

        Pass
        {
            Name "InkBlob"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _InkColor;
            float  _Seed;
            float4 _StretchDir;
            float  _Stretch;
            float  _Growth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;      // UI Image 틴트(버텍스 컬러)
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                o.color = i.color;
                return o;
            }

            // ── 절차 노이즈: 해시 → 밸류 노이즈(퀸틱 보간) → fbm 4옥타브 ──
            float Hash(float2 p)
            {
                // _Seed를 도메인에 섞어 매 발사마다 다른 얼룩
                return frac(sin(dot(p, float2(127.1, 311.7)) + _Seed * 17.17) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                float2 u = fp * fp * fp * (fp * (fp * 6.0 - 15.0) + 10.0);   // 퀸틱 — 미분 연속(fwidth AA에 유리)
                float a = Hash(ip);
                float b = Hash(ip + float2(1, 0));
                float c = Hash(ip + float2(0, 1));
                float d = Hash(ip + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                // 4옥타브 — 큰 덩어리(윤곽) + 중간 찢김 + 잔 거칠기
                for (int k = 0; k < 4; k++)
                {
                    v += amp * ValueNoise(p);
                    p = p * 2.03 + 11.7;   // 격자 정렬 깨기
                    amp *= 0.5;
                }
                return v;   // ≈ 0~1
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;   // 중심 0, 가장자리 ±1

                // ── 방향성 늘림: 탄 방향(+along)은 길게, 반대쪽은 짧게 — 잉크가 탄 방향으로 튄 비대칭 ──
                float2 sd = _StretchDir.xy;
                float sdLen = max(length(sd), 1e-4);          // 0벡터 가드(NaN 방지)
                float2 dir = sd / sdLen;
                float along = dot(p, dir);
                float2 perp = p - dir * along;
                float stretchAmt = _Stretch * lerp(0.25, 1.0, smoothstep(-0.4, 0.4, along));
                float2 q = dir * (along / (1.0 + stretchAmt)) + perp;   // along축 압축 = 그 방향으로 모양이 늘어남

                // ── 도메인 워프 — 원이 "찢어진 잉크"로 ──
                float2 w;
                w.x = Fbm(q * 3.0 + float2(_Seed * 0.31, 2.7));
                w.y = Fbm(q * 3.0 + float2(7.3, _Seed * 0.31));
                float2 pw = q + (w - 0.5) * 0.9;   // 워프 강도 0.9 — 윤곽 파괴와 형태 유지의 균형점

                float n = Fbm(pw * 2.2);
                float r = length(pw);

                // ── 메인 얼룩: 방사 감쇠 + 노이즈, _Growth로 반경 성장 ──
                float radius = lerp(0.40, 0.48, _Growth);
                float field = (radius - r) * 2.2 + (n - 0.5) * 1.4;
                float aa = fwidth(field) + 1e-4;               // 스레숄드 AA
                float ink = smoothstep(-aa, aa, field);

                // ── 위성 얼룩(스플래터): 본체 바깥 링에서만, 더 높은 스레숄드 ──
                // 노이즈 봉우리만 섬으로 분리 — fbm 주파수 2.2 기준 링 안에 봉우리가 통상 2~3개.
                // _Growth가 차며 스레숄드가 내려가 위성이 "튀어나오듯" 등장.
                float satRing = smoothstep(radius * 0.9, radius * 1.5, r)
                              * (1.0 - smoothstep(radius * 1.9, radius * 2.4, r));
                float satField = (n - lerp(0.80, 0.68, _Growth)) * 6.0;
                float aaS = fwidth(satField) + 1e-4;
                float sat = smoothstep(-aaS, aaS, satField) * satRing;

                float a = saturate(ink + sat);
                half4 col = half4(_InkColor.rgb, _InkColor.a * a);
                return col * i.color;   // 버텍스 컬러 곱(UI 틴트/페이드 호환)
            }
            ENDHLSL
        }
    }
}
