// A comic background: radial speed-burst stripes printed as halftone, like the
// sunburst behind a splash panel. Alternating paper and mid-tone wedges; the
// mid-tone wedges come out as Ben-Day dots, fading to bare paper at the centre.
Shader "PastBedtime/Comic/Burst"
{
    Properties
    {
        _Center ("Centre (uv)", Vector) = (0.5, 0.5, 0, 0)
        _Rays ("Rays", Float) = 28
        _Tone ("Stripe tone", Range(0, 1)) = 0.45
        _Hole ("Clear centre radius (uv)", Float) = 0.03
        _PaperColor ("Paper", Color) = (0.957, 0.945, 0.918, 1)
        _InkColor ("Ink", Color) = (0.071, 0.071, 0.071, 1)
        _DotCell ("Dot cell (px @1080)", Float) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ComicBurst"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ComicInk.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Center;
                float _Rays;
                float _Tone;
                float _Hole;
                half4 _PaperColor;
                half4 _InkColor;
                float _DotCell;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 d = i.uv - _Center.xy;
                float ang = atan2(d.y, d.x) / (2.0 * PI) * _Rays;
                float stripe = step(0.5, frac(ang));
                float r = length(d);
                float fade = smoothstep(_Hole, _Hole + 0.18, r);          // calm centre
                float L = lerp(1.0, _Tone, stripe * fade);
                float ink = PB_InkCoverage(L, i.positionCS.xy, _DotCell, 6.0);
                return lerp(_PaperColor, _InkColor, ink);
            }
            ENDHLSL
        }
    }
}
