// 싱크 글리치(Sync Glitch) — 시그니처 풀스크린 신호 붕괴 이펙트 (URP, 니어플레인 쿼드 + 씬 컬러 재구성).
//
// 세계관: 주인공은 나노봇 싱크로 연결된 존재. 동기화율(SyncRate)이 1.0에 가까워질수록
//   (=군체에 잠식될수록) "나"의 신호가 깨진다. 글리치 = 연결 붕괴의 시각 언어.
// 색상 예산: 시안(0.35,0.75,0.85)=본사/보상(기존), 마젠타=신호 붕괴/규정 외(신규).
//   마젠타/시안은 직접 칠하지 않는다 — RGB 채널 분리에서 자연 발생하는 프린지가 본체
//   (블록 노이즈 팔레트 솔리드만 예외). 빨강 금지.
//
// 구동: GlitchFX.cs가 매 프레임 주입 — _Intensity / _GlitchTime(unscaled) / _Seed(스터터 재롤) / _BurstFlash.
//   ★Time 내장변수 사용 금지 — 사망 시 timeScale=0, 히트스탑 0.05에서도 글리치가 흘러야 한다.
// 씬 컬러: _CameraOpaqueTexture(불투명 패스 후 캡처) → Transparent 큐에서 "실제로 일그러진 픽셀"만
//   알파마스크로 부분 덮어쓰기([C-1]) — 비활성 픽셀은 알파 0으로 프레임버퍼의 투명물(시야 콘·잔상 등) 보존.
// _Intensity 0이면 알파 전역 0 → 원본 무손실 통과.
Shader "ZombieCrush/SyncGlitch"
{
    Properties
    {
        _Intensity("Glitch Intensity", Range(0, 1)) = 0
        _GlitchTime("Glitch Time (unscaled, C# 주입)", Float) = 0
        _Seed("Stutter Seed (C# 재롤)", Float) = 0
        _BurstFlash("Burst White Flash (C# 주입)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SyncGlitch"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha   // [C-1] 부분 덮어쓰기 — 활성 픽셀만 덮고, 알파 0 픽셀은 원본(투명물 포함) 보존

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _GlitchTime;
                float _Seed;
                float _BurstFlash;
            CBUFFER_END

            // ── 튜닝 ──
            #define SLICE_MAX_OFFSET   0.06     // 슬라이스 수평 변위 최대 = 화면폭 6%
            #define SLICE_BANDS_MIN    8.0      // 밴드 총수 하한(시드마다 변동 → 불균등 밴드)
            #define SLICE_BANDS_MAX    16.0     // 밴드 총수 상한
            #define SPLIT_MAX_OFFSET   0.012    // RGB 분리 최대 = 화면폭 1.2%
            #define SPLIT_BAND_BOOST   1.8      // 슬라이스 밴드 안에서 RGB 분리 증폭 배율
            #define BLOCK_START        0.4      // 블록 노이즈 등장 강도
            #define BLOCK_CELL_PROB    0.15     // 풀강도에서 셀 픽 확률
            #define BLOCK_SOLID_RATIO  0.5      // 풀강도에서 팔레트 솔리드 치환 비율(나머지=복사)
            #define SCANLINE_AMP       0.03     // 스캔라인 명암 ±3%(플래시 아님 — 발작 가드)
            #define SCANLINE_FREQ      280.0    // 화면 세로당 가로줄 수

            // 팔레트(블록 노이즈 솔리드 전용) — 시안=본사/보상, 마젠타=신호 붕괴/규정 외. 빨강 금지.
            static const float3 PAL_MAGENTA = float3(1.0, 0.17, 0.84);
            static const float3 PAL_CYAN    = float3(0.35, 0.75, 0.85);
            static const float3 PAL_WHITE   = float3(1.0, 1.0, 1.0);

            // 정수 비트 트릭 없는 표준 float 해시 — 시드가 같으면 프레임 간 결과 동일(스터터의 "유지" 구간)
            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 니어플레인 쿼드가 프러스텀 풀커버 → 픽셀 좌표를 그대로 스크린 UV로 쓴다
                float2 uv = IN.positionCS.xy / _ScaledScreenParams.xy;
                float I = saturate(_Intensity);

                // ── 1. 가로 슬라이스 변위 ──
                // 불균등 밴드: 밴드 총수 자체가 시드마다 8~16으로 출렁여 균일 격자 인상을 깬다.
                // 활성 밴드 기대 수 = lerp(3, 9, I) — 강도에 비례.
                float bandCount = floor(lerp(SLICE_BANDS_MIN, SLICE_BANDS_MAX, hash11(_Seed * 7.13)));
                float band = floor(uv.y * bandCount);
                float activeFrac = lerp(3.0, 9.0, I) / bandCount;
                float bandActive = step(hash21(float2(band, _Seed)), activeFrac);
                float bandShift = (hash21(float2(band, _Seed + 11.0)) * 2.0 - 1.0) * SLICE_MAX_OFFSET * I;
                float2 uvS = uv + float2(bandActive * bandShift, 0.0);

                // ── 2. RGB 채널 분리 — 마젠타/시안 프린지의 원천 ──
                // R/B를 반대 방향으로 미세 오프셋(수평 위주, 시드마다 살짝 기울어짐).
                // 슬라이스 밴드 안에서는 증폭 — 변위된 띠가 가장 심하게 "찢어진" 신호로 읽힌다.
                // [C-1] 베이스라인 분리는 활성 밴드 내부로 한정(밴드 밖 0). 풀스크린 분리는
                // _BurstFlash>0 동안만 — 순간 투명물 소실은 "신호 완전 붕괴" 디제틱으로 수용.
                float burstOn = step(1e-4, _BurstFlash);
                float2 splitDir = normalize(float2(1.0, (hash11(_Seed * 3.71) - 0.5) * 0.25));
                float2 splitOff = splitDir * (SPLIT_MAX_OFFSET * I * (1.0 + bandActive * SPLIT_BAND_BOOST))
                                * max(bandActive, burstOn);
                float3 col;
                col.r = SampleSceneColor(saturate(uvS + splitOff)).r;
                col.g = SampleSceneColor(saturate(uvS)).g;
                col.b = SampleSceneColor(saturate(uvS - splitOff)).b;

                // ── 3. 블록 노이즈 — 강도 0.4+에서 등장 ──
                // 가로로 긴 직사각 셀(12×32) 해시 그리드에서 확률적으로 픽:
                // 픽된 셀은 다른 위치의 씬 컬러 복사 또는 팔레트 솔리드 치환. 둘 다 강도 비례.
                float blockAmt = saturate((I - BLOCK_START) / (1.0 - BLOCK_START));
                float2 cell = floor(uv * float2(12.0, 32.0));
                float picked = step(hash21(cell + _Seed * 17.0), blockAmt * BLOCK_CELL_PROB);
                // 복사원 UV — frac 랩어라운드(화면 밖으로 나가면 반대편에서 가져옴 = 글리치다운 전위)
                float2 copyUV = frac(uv + float2(hash21(cell + _Seed * 41.0), hash21(cell + _Seed * 53.0)) - 0.5);
                float3 copyCol = SampleSceneColor(copyUV);
                float pRnd = hash21(cell + _Seed * 61.0);
                float3 pal = pRnd < 0.4 ? PAL_MAGENTA : (pRnd < 0.8 ? PAL_CYAN : PAL_WHITE);
                float solid = step(hash21(cell + _Seed * 29.0), blockAmt * BLOCK_SOLID_RATIO);
                col = lerp(col, lerp(copyCol, pal, solid), picked);

                // ── 4. 스캔라인 — 아주 미세한 가로줄 명암. _GlitchTime으로 느리게 흐른다 ──
                float scan = sin(uv.y * SCANLINE_FREQ * 6.2831853 + _GlitchTime * 7.0);
                col *= 1.0 + scan * (SCANLINE_AMP * I) * max(bandActive, burstOn);   // [C-1] 밴드/버스트 마스크 안으로 — 알파 마스크와 정합

                // ── 5. 화이트 코어 플래시 — Burst 첫 60ms에만(C#이 발작 가드·빈도 제한) ──
                // 베이스라인에선 _BurstFlash가 항상 0 — 상시 휘도 플래시 없음.
                float centerW = 1.0 - saturate(length(uv - 0.5) * 1.4);
                col += centerW * centerW * _BurstFlash;

                // [C-1] 출력 알파 = "실제로 일그러진 픽셀" 마스크(슬라이스 밴드 활성 + 블록 셀 픽 + 버스트).
                // 비활성 픽셀은 알파 0 → 프레임버퍼 원본(투명물 포함) 보존. _Intensity=0이면 전역 0(완전 통과).
                float mask = saturate(bandActive + picked + burstOn) * step(1e-4, I);
                return half4(col, mask);
            }
            ENDHLSL
        }
    }
}
