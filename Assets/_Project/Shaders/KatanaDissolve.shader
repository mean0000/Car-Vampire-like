// KatanaDissolve.shader
// Purpose: 칼(Katana_Mesh) 전용 나노봇 디머터리얼라이즈 디졸브.
//          _Dissolve(0=솔리드, 1=완전 소멸)를 MPB.SetFloat로 구동한다.
//          평시(_Dissolve=0)엔 URP Lit 룩 그대로 유지. 경계=HDR 시안 발광(블룸).
//          노이즈=월드 공간 FBM + Voronoi 나노입자(텍스처 0 — 절차 전용).
//          y축 sweepBias로 위→아래(or 아래→위) 방향성 부여.
// Platform: Unity 6 URP (Forward / Forward+)
// Inputs:   _Dissolve, _DissolveEdge, _DissolveColor(HDR), _SweepBias, _NoiseScale
// ★MPB 주의: _Dissolve/경계 노브는 UNITY_DEFINE_INSTANCED_PROP 없이 uniform으로 선언
//             → MPB SetFloat/SetColor로 per-renderer 오버라이드 가능.
//             (URP Lit과 달리 이 셰이더는 GPU Instancing을 명시 지원하지 않는다 —
//              칼은 씬에 1개라 문제 없음.)

Shader "ZombieCrush/KatanaDissolve"
{
    Properties
    {
        // --- Surface (기존 칼 룩 보존) ---
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (0.745, 0, 0, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.3
        _Metallic("Metallic", Range(0, 1)) = 0.5

        // --- Dissolve ---
        [Header(Dissolve)]
        [PerRendererData] _Dissolve("Dissolve (0=solid 1=gone)", Range(0, 1)) = 0.0
        _DissolveEdge("Edge Width", Range(0.001, 0.15)) = 0.05
        [HDR] _DissolveColor("Edge Color (HDR Cyan)", Color) = (0, 4, 4, 1)
        _SweepBias("Sweep Bias (0=uniform 1=top-down)", Range(0, 1)) = 0.7
        _NoiseScale("Noise Scale", Range(1, 20)) = 6.0
        _ParticleSharpness("Particle Sharpness", Range(1, 32)) = 8.0

        // --- URP hidden ---
        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [HideInInspector] _Surface("Surface Type", Float) = 0
        [HideInInspector] _Blend("Blend Mode", Float) = 0
        [HideInInspector] _SrcBlend("__src", Float) = 1
        [HideInInspector] _DstBlend("__dst", Float) = 0
        [HideInInspector] _ZWrite("__zw", Float) = 1
        [HideInInspector] _Cull("__cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "TransparentCutout"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "AlphaTest"
            "UniversalMaterialType" = "Lit"
        }
        LOD 300

        // ---------------------------------------------------------------
        // Pass 1 : ForwardLit
        // ---------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend  [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull   [_Cull]

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // URP lighting
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fog
            #pragma multi_compile _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── Constant Buffer (Material 고정값 — SRP Batcher가 배치로 공급) ──
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _Smoothness;
                half   _Metallic;
                half   _DissolveEdge;
                half4  _DissolveColor;
                half   _SweepBias;
                half   _NoiseScale;
                half   _ParticleSharpness;
                half   _Cutoff;
            CBUFFER_END
            // ★_Dissolve는 CBUFFER 밖에 단독 선언 — MPB.SetFloat("_Dissolve", t)가 per-draw로 오버라이드.
            //   CBUFFER 안에 있으면 SRP Batcher가 Material의 고정값을 채워 MPB가 덮지 못한다.
            half _Dissolve;

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS  : POSITION;
                float3 normalOS    : NORMAL;
                float2 texcoord    : TEXCOORD0;
                float2 staticLightmapUV  : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                half3  normalWS     : TEXCOORD2;
                half   fogFactor    : TEXCOORD3;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);
                #ifdef DYNAMICLIGHTMAP_ON
                float2 dynamicLightmapUV : TEXCOORD5;
                #endif
                #ifdef USE_APV_PROBE_OCCLUSION
                float4 probeOcclusion : TEXCOORD6;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── 절차 노이즈 유틸 ──

            // 해시 (Inigo Quilez hash3)
            float3 _hash3(float3 p)
            {
                p = frac(p * float3(443.897f, 441.423f, 437.195f));
                p += dot(p, p.yxz + 19.19f);
                return frac((p.xxy + p.yzz) * p.zyx);
            }

            // 3D Voronoi — 가장 가까운 점까지 거리 반환 (0..~0.9)
            float Voronoi3D(float3 p)
            {
                float3 b = floor(p);
                float3 f = frac(p);
                float minDist = 8.0f;
                for (int z = -1; z <= 1; z++)
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float3 n  = float3(x, y, z);
                    float3 rp = _hash3(b + n);       // 셀 내 점 위치 (0..1)
                    float3 d  = n + rp - f;
                    float  di = dot(d, d);
                    minDist = min(minDist, di);
                }
                return sqrt(minDist);
            }

            // FBM (2 octave — 지시 효율 / 칼은 소형 메시라 충분)
            float FBM(float3 p)
            {
                float v  = 0.0f;
                float a  = 0.5f;
                float3 shift = float3(100.0f, 100.0f, 100.0f);
                for (int i = 0; i < 2; i++)
                {
                    v += a * Voronoi3D(p);
                    p  = p * 2.0f + shift;
                    a *= 0.5f;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs  = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = normInputs.normalWS;
                OUT.uv         = TRANSFORM_TEX(IN.texcoord, _BaseMap);
                OUT.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);

                OUTPUT_LIGHTMAP_UV(IN.staticLightmapUV, unity_LightmapST, OUT.staticLightmapUV);
                #ifdef DYNAMICLIGHTMAP_ON
                OUT.dynamicLightmapUV = IN.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy
                                      + unity_DynamicLightmapST.zw;
                #endif
                OUTPUT_SH4(posInputs.positionWS, OUT.normalWS,
                           GetWorldSpaceNormalizeViewDir(posInputs.positionWS),
                           OUT.vertexSH, OUT.probeOcclusion);
                return OUT;
            }

            // GI helper — ActorRimLit과 동일 패턴
            half3 GetBakedGI(Varyings IN, half3 normalWS, half3 viewDirWS,
                             float2 positionCS, inout half4 shadowMask)
            {
                #if defined(_SCREEN_SPACE_IRRADIANCE)
                    return SAMPLE_GI(_ScreenSpaceIrradiance, positionCS);
                #elif defined(DYNAMICLIGHTMAP_ON)
                    shadowMask = SAMPLE_SHADOWMASK(IN.staticLightmapUV);
                    return SAMPLE_GI(IN.staticLightmapUV, IN.dynamicLightmapUV, IN.vertexSH, normalWS);
                #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
                    return SAMPLE_GI(IN.vertexSH,
                        GetAbsolutePositionWS(IN.positionWS),
                        normalWS, viewDirWS, positionCS,
                        IN.probeOcclusion, shadowMask);
                #else
                    shadowMask = SAMPLE_SHADOWMASK(IN.staticLightmapUV);
                    return SAMPLE_GI(IN.staticLightmapUV, IN.vertexSH, normalWS);
                #endif
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // ── 나노봇 디졸브 마스크 ──────────────────────────────────
                // 1. 월드 공간 노이즈 (나노입자 패턴)
                float3 wp    = IN.positionWS * _NoiseScale;
                float  noise = FBM(wp);                              // 0..~1

                // 2. 방향성 sweep bias — 칼날(Z방향 길이 1.3m)이 tip→guard 순으로 분해.
                //    칼이 손에 붙어 XZ평면에 눕혀있으므로 Z좌표 기반 sweep이 칼날 방향에 정합.
                //    sweepBias=1이면 Z그래디언트만, 0이면 순수 노이즈.
                //    bounds: z=0.17(tip)..1.47(guard). tip 쪽이 먼저 사라짐.
                float zRange = 1.4f;
                float zNorm  = saturate((IN.positionWS.z - 0.17f) / max(zRange, 0.001f)); // 0(tip)..1(guard)
                // tip(zNorm=0)이 먼저 사라지도록 뒤집기
                float swp    = 1.0f - zNorm;
                float mask   = lerp(noise, noise + swp * 0.35f, _SweepBias);
                mask = saturate(mask);

                // 3. Particle sharpness — 높을수록 나노입자 경계가 선명
                mask = pow(saturate(mask), 1.0f / max(_ParticleSharpness * 0.1f, 0.001f));
                mask = saturate(mask);

                // 4. clip + 경계 발광
                //    mask < _Dissolve       → clip (사라짐)
                //    mask < _Dissolve + edge → 경계 발광
                float dissolveThreshold = _Dissolve;
                float edge = _DissolveEdge;

                // _Dissolve=0 → 아무것도 clip 안 됨(솔리드)
                // _Dissolve=1 → 전체 clip(완전 소멸)
                clip(mask - dissolveThreshold);   // mask < threshold → discard

                // 경계 발광: dissolveThreshold ~ dissolveThreshold+edge 사이
                // edgeFactor: 0=경계 바로 안쪽, 1=솔리드 내부
                float edgeFactor = saturate((mask - dissolveThreshold) / max(edge, 0.001f));
                // 1-edgeFactor^2: 경계에서 넓고 부드럽게 빠짐(pow2는 pow3보다 더 넓은 글로우)
                half edgeGlow = (half)((1.0f - edgeFactor) * (1.0f - edgeFactor));

                // ── PBR Lit ─────────────────────────────────────────────
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                half3 normalWS  = normalize(IN.normalWS);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half4 shadowMask = half4(1,1,1,1);

                half3 bakedGI = GetBakedGI(IN, normalWS, viewDirWS, IN.positionCS.xy, shadowMask);

                InputData inputData = (InputData)0;
                inputData.positionWS              = IN.positionWS;
                inputData.positionCS              = IN.positionCS;
                inputData.normalWS                = normalWS;
                inputData.viewDirectionWS         = viewDirWS;
                inputData.shadowCoord             = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord                = InitializeInputDataFog(float4(IN.positionWS, 1.0), IN.fogFactor);
                inputData.vertexLighting           = half3(0,0,0);
                inputData.bakedGI                 = bakedGI;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask              = shadowMask;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo     = albedoAlpha.rgb;
                surfaceData.alpha      = albedoAlpha.a;
                surfaceData.metallic   = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS   = half3(0, 0, 1);
                surfaceData.occlusion  = 1.0;

                half4 litColor = UniversalFragmentPBR(inputData, surfaceData);

                // ── 경계 발광 합성 (emissive 위에 add) ───────────────────
                // HDR 시안 — 블룸이 픽업한다
                litColor.rgb += _DissolveColor.rgb * edgeGlow;

                // ── Fog ───────────────────────────────────────────────────
                litColor.rgb = MixFog(litColor.rgb, inputData.fogCoord);

                return litColor;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // Pass 2 : ShadowCaster — 디졸브 clip 정합
        // ---------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest  LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _Smoothness;
                half   _Metallic;
                half   _DissolveEdge;
                half4  _DissolveColor;
                half   _SweepBias;
                half   _NoiseScale;
                half   _ParticleSharpness;
                half   _Cutoff;
            CBUFFER_END
            half _Dissolve;

            struct ShadowAttributes
            {
                float4 positionOS  : POSITION;
                float3 normalOS    : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 _shash3(float3 p)
            {
                p = frac(p * float3(443.897f, 441.423f, 437.195f));
                p += dot(p, p.yxz + 19.19f);
                return frac((p.xxy + p.yzz) * p.zyx);
            }
            float sVoronoi(float3 p)
            {
                float3 b = floor(p); float3 f = frac(p);
                float md = 8.0f;
                for (int z=-1;z<=1;z++) for (int y=-1;y<=1;y++) for (int x=-1;x<=1;x++)
                { float3 n=float3(x,y,z); float3 d=n+_shash3(b+n)-f; md=min(md,dot(d,d)); }
                return sqrt(md);
            }
            float sFBM(float3 p)
            {
                float v=0; float a=0.5f;
                for (int i=0;i<2;i++) { v+=a*sVoronoi(p); p=p*2+100; a*=0.5f; }
                return v;
            }

            ShadowVaryings shadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(IN.normalOS);

                // shadow bias 적용
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, nrmWS, _MainLightPosition.xyz));
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, posCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, posCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionCS = posCS;
                OUT.positionWS = posWS;
                return OUT;
            }

            half4 shadowFrag(ShadowVaryings IN) : SV_Target
            {
                // ForwardLit과 동일 노이즈·clip — 그림자도 디졸브 정합
                float3 wp    = IN.positionWS * _NoiseScale;
                float  noise = sFBM(wp);
                float  zNorm = saturate((IN.positionWS.z - 0.17f) / 1.4f);
                float  swp   = 1.0f - zNorm;
                float  mask  = saturate(lerp(noise, noise + swp * 0.35f, _SweepBias));
                mask = pow(saturate(mask), 1.0f / max(_ParticleSharpness * 0.1f, 0.001f));
                clip(mask - _Dissolve);
                return 0;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // Pass 3 : DepthOnly
        // ---------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   depthVert
            #pragma fragment depthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _Smoothness;
                half   _Metallic;
                half   _DissolveEdge;
                half4  _DissolveColor;
                half   _SweepBias;
                half   _NoiseScale;
                half   _ParticleSharpness;
                half   _Cutoff;
            CBUFFER_END
            half _Dissolve;

            struct DA { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DV { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float3 _dhash3(float3 p)
            { p=frac(p*float3(443.897f,441.423f,437.195f)); p+=dot(p,p.yxz+19.19f); return frac((p.xxy+p.yzz)*p.zyx); }
            float dVoronoi(float3 p)
            { float3 b=floor(p),f=frac(p); float md=8;
              for(int z=-1;z<=1;z++) for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++)
              { float3 n=float3(x,y,z),d=n+_dhash3(b+n)-f; md=min(md,dot(d,d)); }
              return sqrt(md); }
            float dFBM(float3 p)
            { float v=0,a=0.5f; for(int i=0;i<2;i++){v+=a*dVoronoi(p);p=p*2+100;a*=0.5f;} return v; }

            DV depthVert(DA IN)
            { DV OUT=(DV)0; UNITY_SETUP_INSTANCE_ID(IN); UNITY_TRANSFER_INSTANCE_ID(IN,OUT);
              OUT.positionWS=TransformObjectToWorld(IN.positionOS.xyz);
              OUT.positionCS=TransformWorldToHClip(OUT.positionWS); return OUT; }

            half depthFrag(DV IN) : SV_Target
            { float3 wp=IN.positionWS*_NoiseScale; float noise=dFBM(wp);
              float zNorm=saturate((IN.positionWS.z-0.17f)/1.4f);
              float swp=1.0f-zNorm;
              float mask=saturate(lerp(noise,noise+swp*0.35f,_SweepBias));
              mask=pow(saturate(mask),1.0f/max(_ParticleSharpness*0.1f,0.001f));
              clip(mask-_Dissolve); return 0; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
