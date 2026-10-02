// Frames the screen as a comic panel for the title's opening: a paper gutter and
// an ink border round the edge, and Ben-Day dots that darken the corners like the
// shading on a printed page. _Amount (0..1) draws the frame in and out.
Shader "PastBedtime/UI/ComicFrame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Amount ("Amount", Range(0, 1)) = 1
        _Gutter ("Gutter (screen heights)", Float) = 0.022
        _Ink ("Border (screen heights)", Float) = 0.009
        _Dots ("Corner dots", Range(0, 1)) = 0.5
        _DotSize ("Dot cell (pixels @1080)", Float) = 14
        _Aspect ("Aspect (w/h)", Float) = 1.7777
        _HeightPx ("Height in pixels", Float) = 1080
        _PaperColor ("Paper", Color) = (0.949, 0.910, 0.812, 1)
        _InkColor ("Ink", Color) = (0.078, 0.078, 0.078, 1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            float _Amount, _Gutter, _Ink, _Dots, _DotSize, _Aspect, _HeightPx;
            fixed4 _PaperColor, _InkColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = float2(i.uv.x * _Aspect, i.uv.y);                  // screen heights
                float edge = min(min(p.x, _Aspect - p.x), min(p.y, 1.0 - p.y));

                float gutter = _Gutter * _Amount, ink = _Ink * _Amount;
                float aa = fwidth(edge);
                float inGutter = 1.0 - smoothstep(gutter - aa, gutter + aa, edge);
                float inInk = 1.0 - smoothstep(gutter + ink - aa, gutter + ink + aa, edge);

                // Ben-Day corner shading: dots grow toward the corners.
                float2 d = (i.uv - 0.5) * float2(_Aspect / 1.7777, 1.0);
                float t = saturate((length(d) * 1.3 - 0.64) / 0.4) * _Dots * _Amount;
                float2 px = p * _HeightPx;
                float cell = max(2.0, _DotSize * _HeightPx / 1080.0);
                float2 g = float2(px.x + px.y, px.x - px.y) * (0.70710678 / cell);
                float r = length(frac(g) - 0.5);
                float dotR = sqrt(t) * 0.62;
                float raa = max(fwidth(r), 1e-4);
                float dots = (1.0 - smoothstep(dotR - raa, dotR + raa, r)) * step(0.001, t);

                fixed4 col = fixed4(_InkColor.rgb, dots * 0.85);
                col = lerp(col, _InkColor, inInk);
                col = lerp(col, _PaperColor, inGutter);
                return col;
            }
            ENDCG
        }
    }
}
