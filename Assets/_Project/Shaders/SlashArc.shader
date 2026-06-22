// 슬래시 궤적 VFX — 극좌표 호 SDF + 스윕 + 가장자리 노이즈 + 빨간 스크래치.
// namutree 슬래시(푸른 궤적+빨간 스크래치) 재현 첫 패스. Additive 발광, 빌보드 쿼드에 적용.
Shader "ZombieCrush/SlashArc"
{
    Properties
    {
        [HDR]_CoreColor ("Core Color", Color) = (3.5,5.5,7.0,1)
        [HDR]_EdgeColor ("Edge Color", Color) = (0.1,1.8,3.2,1)
        [HDR]_ScratchColor ("Scratch Color", Color) = (5.0,0.25,1.1,1)
        _Radius ("Arc Radius", Range(0.1,0.5)) = 0.38
        _Thickness ("Arc Thickness", Range(0.01,0.3)) = 0.11
        _ArcDeg ("Arc Degrees", Range(20,300)) = 150
        _Sweep ("Sweep (0..1.2)", Range(0,1.2)) = 0.75
        _SweepSoft ("Sweep Softness", Range(0.01,0.6)) = 0.22
        _TrailFade ("Trail Fade", Range(0.1,3)) = 1.3
        _NoiseAmp ("Edge Noise", Range(0,0.15)) = 0.045
        _NoiseScale ("Noise Scale", Range(2,40)) = 15
        _ScratchAmt ("Scratch Amount", Range(0,1.5)) = 0.8
        _Intensity ("Intensity", Range(0,4)) = 1.7
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 pos:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            float4 _CoreColor,_EdgeColor,_ScratchColor;
            float _Radius,_Thickness,_ArcDeg,_Sweep,_SweepSoft,_TrailFade,_NoiseAmp,_NoiseScale,_ScratchAmt,_Intensity;

            V vert(A i){ V o; o.pos=TransformObjectToHClip(i.pos.xyz); o.uv=i.uv; return o; }

            float hash(float2 p){ return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float vnoise(float2 p){
                float2 i=floor(p), f=frac(p); f=f*f*(3.0-2.0*f);
                float a=hash(i),b=hash(i+float2(1,0)),c=hash(i+float2(0,1)),d=hash(i+float2(1,1));
                return lerp(lerp(a,b,f.x),lerp(c,d,f.x),f.y);
            }
            #define PI 3.14159265

            float4 frag(V IN):SV_Target
            {
                float2 uv = IN.uv - 0.5;
                float r = length(uv);
                float ang = atan2(uv.y, uv.x);

                float arcHalf = radians(_ArcDeg)*0.5;
                float t = (ang + arcHalf) / (arcHalf*2.0);   // 0..1 along arc
                float inArc = step(0.0, t) * step(t, 1.0);

                // edge noise wobble on radius
                float n = vnoise(float2(t*_NoiseScale, 8.0)) - 0.5;
                float rr = _Radius + n*_NoiseAmp;

                // thickness: thin at ends, fat mid (blade shape)
                float prof = sin(saturate(t)*PI);
                float th = _Thickness * (0.22 + 0.78*prof);

                // radial band (cross-section of the blade trail)
                float band = 1.0 - smoothstep(0.0, th, abs(r - rr));
                band = pow(saturate(band), 1.5);

                // sweep: part behind the head is visible, head is hottest, tail fades
                float head = _Sweep;
                float visible = smoothstep(head, head - _SweepSoft, t);
                float trail = saturate(t / max(head,0.001));
                trail = pow(trail, _TrailFade);
                float headGlow = smoothstep(head - 0.18, head, t) * inArc;

                float a = band * visible * inArc;
                float bodyI = a * (0.35 + trail);

                float core = pow(band, 2.0);
                float3 col = lerp(_EdgeColor.rgb, _CoreColor.rgb, core);
                col += _CoreColor.rgb * headGlow * 1.5;
                float3 outc = col * bodyI;

                // red/magenta scratch streaks
                float scr = 0.0;
                [unroll] for(int k=0;k<3;k++){
                    float off = (k-1)*0.05 + (hash(float2(k,3.0))-0.5)*0.02;
                    float sr = _Radius + off;
                    float sn = vnoise(float2(t*22.0 + k*5.0, 1.3))*0.013;
                    float ln = 1.0 - smoothstep(0.0, 0.013, abs(r - sr + sn));
                    float svis = smoothstep(head+0.05, head-0.1, t)*step(0.0,t)*step(t,1.0);
                    scr += ln * svis;
                }
                scr = saturate(scr) * _ScratchAmt;
                outc += _ScratchColor.rgb * scr;

                float alpha = saturate(bodyI + scr);
                return float4(outc * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
