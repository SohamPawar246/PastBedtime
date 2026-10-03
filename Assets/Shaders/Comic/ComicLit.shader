// Everything solid inside the comic: characters, props, platforms.
// Lit by the comic key light only (see ComicInk.hlsl), printed as ink on paper,
// outlined with an inverted hull so silhouettes read at a glance.
Shader "PastBedtime/Comic/Lit"
{
    Properties
    {
        _Tone ("Tone (0 ink .. 1 paper)", Range(0, 1)) = 0.95
        _ToneBottom ("Gradation: tone at the bottom edge (-1 off)", Range(-1, 1)) = -1
        _Stars ("Stars (paper specks where it's dark)", Range(0, 1)) = 0
        _Rim ("Rim light", Range(0, 1)) = 0.35
        _Gloss ("Wet-ink gloss (white highlight shapes on black)", Range(0, 1)) = 0
        _Scuffs ("Hand-inked scuffs (short thick strokes on the lit side)", Range(0, 1)) = 0
        [Toggle] _Flat ("Flat (ignore light)", Float) = 0
        _PaperColor ("Paper", Color) = (0.957, 0.945, 0.918, 1)
        _InkColor ("Ink", Color) = (0.071, 0.071, 0.071, 1)
        _DotCell ("Dot cell (px @1080)", Float) = 6
        _HatchSpacing ("Hatch spacing (px @1080)", Float) = 5
        _OutlineWidth ("Outline (px @1080)", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "ComicInk.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _Tone;
            float _ToneBottom;
            float _Stars;
            float _Rim;
            float _Gloss;
            float _Scuffs;
            float _Flat;
            half4 _PaperColor;
            half4 _InkColor;
            float _DotCell;
            float _HatchSpacing;
            float _OutlineWidth;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };
        ENDHLSL

        Pass
        {
            Name "ComicForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float heightOS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.heightOS = v.positionOS.y;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetCameraPositionWS() - i.positionWS);
                // gradation tone: a manga sky darkening overhead, haze at the foot of distant buildings
                float tone = _ToneBottom >= 0.0 ? lerp(_ToneBottom, _Tone, saturate(i.heightOS + 0.5)) : _Tone;
                float ndl = saturate(dot(n, PB_ComicLightDirection()));
                // Two-step "cel" light: a soft shoulder keeps the terminator inky, not blurry.
                float lit = smoothstep(0.08, 0.55, ndl);
                float ambient = PB_ComicAmbient();
                float L = tone * (ambient + (1.0 - ambient) * lit);
                // Rim: a paper-white edge separates figures from dark backgrounds.
                float rim = smoothstep(0.55, 0.85, 1.0 - saturate(dot(n, v))) * _Rim;
                L = max(L, rim * step(0.2, tone));
                // Wet ink: hard-edged white highlight shapes and a lit-side edge, the way
                // comics ink a glossy black figure (off unless _Gloss > 0).
                float3 h = normalize(PB_ComicLightDirection() + v);
                float shine = smoothstep(0.935, 0.95, saturate(dot(n, h)));
                float edge = smoothstep(0.7, 0.74, 1.0 - saturate(dot(n, v))) * step(0.3, ndl);
                L = max(L, max(shine, edge) * _Gloss);
                L = _Flat > 0.5 ? tone : L;

                float ink = PB_InkCoverage(L, i.positionCS.xy, _DotCell, _HatchSpacing);
                ink = max(ink, PB_Scuffs(i.positionWS, n, L, _Scuffs));
                ink = min(ink, 1.0 - PB_Stars(i.positionWS, L, _Stars));
                half3 c = lerp(_PaperColor, _InkColor, ink).rgb;
                return half4(PB_Grade(c, i.positionWS), 1.0);
            }
            ENDHLSL
        }

        // Inverted-hull outline, constant width in screen pixels.
        Pass
        {
            Name "ComicOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct OutlineVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            OutlineVaryings vert(Attributes v)
            {
                OutlineVaryings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float4 cs = TransformObjectToHClip(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                float2 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                float len = length(nCS);
                if (len > 1e-5)
                {
                    float px = _OutlineWidth * (_ScreenParams.y / 1080.0);
                    cs.xy += (nCS / len) * (px * 2.0 / _ScreenParams.xy) * cs.w;
                }
                o.positionCS = cs;
                return o;
            }

            half4 frag(OutlineVaryings i) : SV_Target { return half4(PB_Grade(_InkColor.rgb, i.positionWS), 1.0); }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(Attributes v) : SV_POSITION { return TransformObjectToHClip(v.positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct V2F { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V2F vert(Attributes v)
            {
                V2F o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(V2F i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
