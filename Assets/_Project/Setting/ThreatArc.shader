// 후방 위협 청각 힌트 아크 — 플레이어 발치에 뜨는 60° 방향성 파문 펄스.
// 세계관: 엘의 나노봇 패시브 스캔이 지면 진동(발소리)을 감지 — "들린다"의 시각화.
//
// ★철학(2026-06-12 동결 스펙): 정답 UI 금지. 깨끗한 UI 도형이 아니라 노이즈로
// 침식된 얼룩진 파문 — 의식하면 보이고, 안 하면 묻히는 수준이 목표.
// _Color    회백(평시) / 앰버(TTC≤0.8s 강펄스만). 빨강·시안·마젠타 금지(색 캐넌).
// _Alpha    단계 알파 × 제곱 페이드 × 조준 배율 (드라이버 RearThreatHint가 구동)
// _Progress 0→1 — 링 반경 확장 + 폭 수축(파문이 퍼지며 얇아진다)
// _Seed     펄스마다 다른 침식 패턴
//
// ── 스타일 3~6: 적 공격 텔레그래프 장판 (2026-06-13 동결, 권위: topdown-attack-grammar §3·§5) ──
// 문법: 모양=피해영역, 채움=타이밍. 외곽선 등장(예고)→안쪽 차오름(카운트다운)→가득참=발동.
// 후방힌트(0~2)의 "묻히는 노이즈" 철학과 반대 — 장판은 "비켜라" 명령이라 가독성 우선,
// 침식은 질감 수준으로만. _Color는 레드-오렌지 단색(시안/앰버/마젠타 금지 — 색 캐넌).
// SDF·외곽선 두께·노이즈 전부 월드 미터 공간(_SizeWorld) — r1.2~r7 어느 크기든 두께 일정.
// 전제: 쿼드 localScale == _SizeWorld.xy. 3=원, 4=레인(직사각), 5=부채꼴(+y 전방), 6=링.
Shader "ZombieCrush/ThreatArc"
{
    Properties
    {
        _Color    ("Color", Color) = (0.75, 0.75, 0.75, 1)
        _Alpha    ("Alpha", Range(0, 1)) = 0.2
        _Progress ("Progress", Range(0, 1)) = 0
        _Seed     ("Seed", Float) = 0
        _Style    ("Style (0 침식아크 1 음파아크 2 발자국블롯 3 원 4 레인 5 부채꼴 6 링)", Float) = 0
        // ── 텔레그래프(스타일 3~6) 전용 ──
        _SizeWorld ("Size World (m) x=폭/지름 y=길이", Vector) = (1, 1, 0, 0)
        _EdgeWorld ("Edge Width (m)", Float) = 0.12
        _AngleDeg  ("Sector Angle (deg)", Float) = 90
        _InnerR01  ("Ring Inner (0-1)", Float) = 0.35
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.35
        _EdgeAlpha ("Edge Alpha", Range(0, 1)) = 0.9
        // 기본 8(Always)=기존 후방힌트 머티리얼 동작 불변. 장판은 4(LEqual) — 장판은 바닥, 몬스터/벽이 가린다.
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 8
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
            ZTest [_ZTest]
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
            float4 _SizeWorld;
            float  _EdgeWorld;
            float  _AngleDeg;
            float  _InnerR01;
            float  _FillAlpha;
            float  _EdgeAlpha;
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
                // ── 텔레그래프 장판(3~6) — 외곽선=예고(상시), 채움=카운트다운 ──
                if (_Style > 2.5)
                {
                    float2 pm = (i.uv - 0.5) * _SizeWorld.xy;   // 쿼드 중심 원점, 월드 미터
                    float sd;      // 도형 SDF — 내부 < 0
                    float fillD;   // 채움 전선 부호거리 — 채워진 쪽 < 0

                    if (_Style < 3.5)        // 3: 원 — 중심→밖으로 차오름
                    {
                        float R = _SizeWorld.x * 0.5;
                        float rr = length(pm);
                        sd = rr - R;
                        fillD = rr - _Progress * R;
                    }
                    else if (_Style < 4.5)   // 4: 레인 — 시전자측(uv.y=0)→끝으로 전진
                    {
                        float2 hf = _SizeWorld.xy * 0.5;
                        float2 q = abs(pm) - hf;
                        sd = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
                        fillD = (pm.y + hf.y) - _Progress * _SizeWorld.y;
                    }
                    else if (_Style < 5.5)   // 5: 부채꼴 — apex=중심, 전방=+y(UV +y)
                    {
                        float R = _SizeWorld.x * 0.5;
                        float aDeg = clamp(_AngleDeg, 1.0, 180.0);   // iq pie SDF는 반각>90°에서 부호 반전 — 문법 최대 180°
                        float2 c = float2(sin(radians(aDeg) * 0.5),
                                          cos(radians(aDeg) * 0.5));
                        float2 q = float2(abs(pm.x), pm.y);   // 좌우 대칭
                        float l = length(q) - R;              // 호
                        float m = length(q - c * clamp(dot(q, c), 0.0, R));   // 직선변
                        sd = max(l, m * sign(c.y * q.x - c.x * q.y));
                        fillD = length(pm) - _Progress * R;   // 각도 마스크 안에서 중심→밖
                    }
                    else                     // 6: 링 — 내경→외경으로 차오름
                    {
                        float Rout = _SizeWorld.x * 0.5;
                        float Rin = Rout * _InnerR01;
                        float rr = length(pm);
                        sd = max(rr - Rout, Rin - rr);
                        fillD = rr - lerp(Rin, Rout, _Progress);
                    }

                    // 외곽선 — _Progress 무관 상시 표시(예고). 침식 없음(명령은 깨끗하게).
                    float edge = (1.0 - smoothstep(0.0, _EdgeWorld, abs(sd))) * _EdgeAlpha;

                    // 채움 — 내부 마스크(소프트 0.03m) × 전선 마스크(소프트 0.07m)
                    float inside = 1.0 - smoothstep(-0.03, 0.0, sd);
                    float fillMask = 1.0 - smoothstep(-0.07, 0.07, fillD);
                    // 침식 — 월드 공간 2옥타브, 채움에서만 약하게(질감 수준)
                    float2 npT = pm * 2.0 + _Seed;
                    float nT = VNoise(npT) * 0.65 + VNoise(npT * 2.3 + 17.0) * 0.35;
                    float fill = saturate(inside * fillMask - nT * 0.18) * _FillAlpha;

                    // 채움 전진 경계 — 살짝 밝게, 차오르는 선이 읽히도록
                    float front = (1.0 - smoothstep(0.0, 0.14, abs(fillD))) * inside;

                    float aT = saturate(edge + fill + front * 0.3);
                    return half4(_Color.rgb, aT * _Alpha);
                }

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
