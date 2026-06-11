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
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            // 시야 마스크 월드좌표 재구성용 씬 깊이(_CameraDepthTexture) — URP 에셋 Depth Texture 필수(현재 켜짐).
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _FocusCenter;
            float _FocusHalfBand;
            float _Falloff;
            float _MaxBlurRadius;
            float _Mode;

            // ---- View Cone (전역 유니폼 — TiltShiftConeDriver가 매 프레임 주입) ----
            // 미설정(드라이버 부재) 시 전부 0 → blend 0 → 기존 Y 밴드와 완전 동일.
            float4 _TS_ConePos;      // xy = 플레이어 스크린 uv, zw = 조준 방향(aspect 보정 공간, 정규화)
            float4 _TS_ConeParams;   // x = cos(내각), y = cos(외각), z = 근접 선명 반경(uv.y 단위), w = 블렌드 0~1
            float4 _TS_ConeExtra;    // x = 콘 밖 블러 바닥, y = 콘 밖 채도 감소, z = 콘 밖 어둠, w = AimBlend
            float4 _TS_ConeFx;       // x = 수렴 진행(0~1), y = 수렴 완료 플래시(지수 감쇠)

            // ★시야 콘 마스크(Blindspot 문법 — VisionConeRenderer 주입): r=1 가시 영역.
            //   레이캐스트 팬 메시를 오쏘 수직 하방 카메라가 찍은 "월드 XZ 평면" 텍스처(벽 시야 그림자 포함).
            //   스크린 UV가 아니라 깊이로 재구성한 월드 XZ로 샘플한다(탑다운 fog-of-war 장르 표준).
            //   플래그 0이면 무시(구 동작).
            TEXTURE2D(_TS_VisionMask);
            float  _TS_VisionMaskOn;
            float4 _TS_VisionMaskCenter;   // xy = 마스크 카메라(=플레이어) 월드 XZ
            float  _TS_VisionMaskHalfSize; // 오쏘 half-size(m) — UV 매핑 스케일

            #define TS(o) SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + (o))

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // 조준 포커스 풀(_TS_ConeExtra.w = AimBlend): 밴드가 조여지고 블러가 강해져
                // "렌즈가 초점을 당기는" 느낌 — FOV 줌이 금지(투영 한계)라 지각적 줌을 대신한다.
                float aimF = _TS_ConeExtra.w;
                float halfBand = _FocusHalfBand * (1.0 - 0.3 * aimF);

                // blur strength by vertical distance from the focus band (0 inside band)
                float dist = abs(uv.y - _FocusCenter) - halfBand;
                float b = saturate(dist / max(_Falloff, 1e-4));
                b = b * b * (3.0 - 2.0 * b);            // smoothstep ramp
                b *= aimF;   // ★평시 틸트시프트 폐지(2026-06-11 유저 콜) — 밴드는 조준 중에만(포커스 풀)

                // ---- View Cone: 콘 안은 밴드를 열고(선명), 콘 밖은 블러 바닥 보장 + 탈색 ----
                float coneW = 0.0;                      // 0=콘 안(선명) ~ 1=콘 밖
                float blend = _TS_ConeParams.w;
                if (blend > 0.001)
                {
                    float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                    float2 toPix = (uv - _TS_ConePos.xy) * float2(aspect, 1.0);   // uv.y 단위 등방 공간
                    float d = length(toPix);
                    float2 dirP = toPix / max(d, 1e-5);
                    float ca = dot(dirP, _TS_ConePos.zw);
                    // cos는 단조감소 — 내각 안(ca≥x)에서 0, 외각 밖(ca≤y)에서 1
                    coneW = 1.0 - smoothstep(_TS_ConeParams.y, _TS_ConeParams.x, ca);
                    // 캐릭터 근접 반경은 방향 무관 무조건 선명(밀착 위협 보호)
                    float prox = 1.0 - smoothstep(_TS_ConeParams.z * 0.5, _TS_ConeParams.z, d);
                    coneW *= 1.0 - prox;
                    // 콘 적용 블러: 콘 안(coneW=0)=0(밴드 열림) ~ 콘 밖(coneW=1)=max(밴드, 바닥)
                    float bCone = lerp(0.0, max(b, _TS_ConeExtra.x), coneW);
                    b = lerp(b, bCone, blend);          // blend 0=현행 밴드 그대로 ~ 1=풀 콘
                    coneW *= blend;                     // 이후 탈색 가중에 사용
                }

                // ---- 시야 콘 마스크 합성: 마스크 밖 = 시야 밖. blend에 희석되지 않는다 —
                //      벽 그림자·콘 밖은 "확실히" 어두워야 화면=정보가 성립(Blindspot).
                //      _TS_VisionMaskOn 값 자체가 어둠 강도(0=끔, ~0.7=권장).
                float coneWScr = coneW;   // 수렴 시안 경계는 화면 콘 전용(마스크 경계에 번지면 "시안 덮기"가 됨)
                float maskHidden = 0.0;
                if (_TS_VisionMaskOn > 0.001)
                {
                    // 깊이→월드좌표 재구성 후 XZ를 오쏘 마스크 UV로 매핑(스크린 직샘플 폐지).
                    // 좀비/플레이어 몸 픽셀도 자기 발밑 XZ로 샘플돼 몸 잘림이 원천 소멸하고,
                    // 경계는 마스크 평면의 한 줄 그대로(시도1 스크린 3탭·시도2 높이 적층의 다겹 복제 종결).
                    float depth = SampleSceneDepth(uv);
                    float3 wpos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                    float2 maskUV = (wpos.xz - _TS_VisionMaskCenter.xy)
                                    / max(2.0 * _TS_VisionMaskHalfSize, 1e-4) + 0.5;
                    // 영역 밖은 clamp 샘플 + 마스크 RT 가장자리 검정(orthoSize = range+2 여유)으로 자연히 어둠.
                    half vis = SAMPLE_TEXTURE2D(_TS_VisionMask, sampler_LinearClamp, maskUV).r;
                    maskHidden = (1.0 - vis) * _TS_VisionMaskOn;
                    coneW = max(coneW, maskHidden);          // 탈색 경로 공유
                    b = max(b, maskHidden * _TS_ConeExtra.x);   // 시야 밖 블러 바닥 보장
                }
                float r = b * _MaxBlurRadius * (1.0 + 0.3 * aimF);   // 조준 시 블러 강화(포커스 풀)
                half4 col;

                if (_Mode < 0.5)
                {
                    // ---- Mode 0: VERTICAL-only 9-tap gaussian (original approach) ----
                    const float w0=0.227027, w1=0.1945946, w2=0.1216216, w3=0.054054, w4=0.016216;
                    col = TS(float2(0,0)) * w0;
                    float o1=r*0.25, o2=r*0.5, o3=r*0.75, o4=r;
                    col += TS(float2(0, o1))*w1; col += TS(float2(0,-o1))*w1;
                    col += TS(float2(0, o2))*w2; col += TS(float2(0,-o2))*w2;
                    col += TS(float2(0, o3))*w3; col += TS(float2(0,-o3))*w3;
                    col += TS(float2(0, o4))*w4; col += TS(float2(0,-o4))*w4;
                }
                else
                {
                    // ---- Mode 1: 2D isotropic disc (aspect-corrected), 17 taps ----
                    float aspect = _ScreenParams.y / max(_ScreenParams.x, 1.0); // height/width
                    col = TS(float2(0,0)) * 0.20;
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
                    // weights: 0.20 + 8*0.06 + 8*0.04 = 1.0
                }

                // ---- View Cone 보조 채널: 콘 밖 채도 감소 — 경계가 "색이 바래는" 그라데이션으로 읽혀
                //      블러 경계 이동(조준 회전)이 덜 거슬린다. coneW에 blend 기가산.
                float desat = coneW * _TS_ConeExtra.y;
                if (desat > 0.001)
                {
                    float luma = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                    col.rgb = lerp(col.rgb, luma.xxx, desat);
                }

                // ---- 조준 시 콘 밖 어두워짐(z = darken×AimBlend): 터널비전 = 지각적 줌이자
                //      주변 시야 상실(조준의 비용). 일시 상태라 평시 가독성 헌법과 양립.
                col.rgb *= 1.0 - coneW * _TS_ConeExtra.z;
                // 시야 마스크 밖 전용 어둠 — 콘 z 채널과 독립(시야 밖은 항상 깊은 어둠).
                col.rgb *= 1.0 - maskHidden;

                // ---- 수렴 신호: 콘 경계가 시안으로 점화 — 진행 중 은은(×0.15), 완료 순간 플래시(×0.55).
                //      "지금이다"의 시각 절반(사운드 틱은 B-005). coneW에 blend 기가산이라 평시 0.
                float edge = coneWScr * (1.0 - coneWScr) * 4.0;   // 경계 밴드에서 피크 — 화면 콘 전용(마스크 경계 제외)
                col.rgb += float3(0.0, 1.0, 0.93) * edge * (_TS_ConeFx.x * 0.15 + _TS_ConeFx.y * 0.55);
                return col;
            }
            ENDHLSL
        }
    }
}
