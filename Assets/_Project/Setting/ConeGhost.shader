// 시야 콘 밖 좀비 탈색 고스트 — RenderObjects(레이어 7=Zombie)가 좀비 지오메트리를
// 이 머티리얼로 한 번 더 그린다(ZTest LEqual·ZWrite Off — 보이는 표면 픽셀에만 틴트).
// 콘 판정은 TiltShift 포스트와 같은 전역 유니폼(_TS_Cone*)을 픽셀 단위로 읽는다 —
// 콘 안=무틴트(컬러 좀비=즉시 위협), 콘 밖=잿빛 반투명(주변 압박). 드라이버 부재/blend 0이면 자동 무효.
Shader "ZombieCrush/ConeGhost"
{
    Properties
    {
        _GhostColor    ("Ghost Tint", Color) = (0.42, 0.46, 0.50, 1)
        _GhostStrength ("Max Opacity", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ConeGhost"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual   // Equal은 디퍼드 GBuffer 깊이와 정밀도 불일치로 전탈락 가능 — 동일 지오메트리 재드로우라 LEqual이 안전
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _TS_ConePos;      // xy = 플레이어 스크린 uv, zw = 조준 방향(aspect 보정 공간)
            float4 _TS_ConeParams;   // x = cos(내각), y = cos(외각), z = 근접 선명 반경, w = 블렌드

            float4 _GhostColor;
            float  _GhostStrength;

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
                float blend = _TS_ConeParams.w;
                clip(blend - 0.001);   // 콘 꺼짐 → 픽셀 폐기(완전 무효)

                // SV_Position(픽셀 좌표) → 뷰포트 uv(하단 원점, WorldToViewportPoint 규약).
                // D3D 계열(UV_STARTS_AT_TOP)에서 비플립 투영(_ProjectionParams.x>0)일 때만 y 반전 —
                // 에디터 RT 캡처로 검증(2026-06-11). GL 계열은 frag 좌표가 이미 하단 원점이라 무반전(리뷰 H-1).
                float2 uv = i.positionCS.xy / _ScreenParams.xy;
                #if UNITY_UV_STARTS_AT_TOP
                if (_ProjectionParams.x > 0.0) uv.y = 1.0 - uv.y;
                #endif

                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float2 toPix = (uv - _TS_ConePos.xy) * float2(aspect, 1.0);
                float d = length(toPix);
                float2 dirP = toPix / max(d, 1e-5);
                float ca = dot(dirP, _TS_ConePos.zw);
                float coneW = 1.0 - smoothstep(_TS_ConeParams.y, _TS_ConeParams.x, ca);
                float prox = 1.0 - smoothstep(_TS_ConeParams.z * 0.5, _TS_ConeParams.z, d);
                coneW *= 1.0 - prox;

                return half4(_GhostColor.rgb, coneW * blend * _GhostStrength);
            }
            ENDHLSL
        }
    }
}
