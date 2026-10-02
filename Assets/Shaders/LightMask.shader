// A full-screen UI overlay that is dark everywhere except inside one circular
// "beam". The beam's edge dissolves through a 45-degree Ben-Day dot screen, so
// light falls off the way a printed comic shades: dots that grow into ink.
//
// Used by the menu torch (TorchBeam) and the scene transition (SceneFlow).
// The dark side may be textured: on the title it is the moonlit room render,
// and a torch-lit render sits underneath, so the beam "lights" the real room.
// Geometry comes from the image's own UVs (0..1 across the overlay), so it is
// independent of canvas mode and graphics API.
Shader "PastBedtime/UI/LightMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _DarkColor ("Dark", Color) = (0.043, 0.063, 0.149, 0.86)
        _LitColor ("Lit", Color) = (1, 0.89, 0.64, 0.05)
        _Center ("Beam centre (uv)", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Beam radius (overlay heights)", Float) = 0.3
        _Softness ("Edge width (overlay heights)", Float) = 0.1
        _Aspect ("Overlay aspect (w/h)", Float) = 1.7777
        _HeightPx ("Overlay height in pixels", Float) = 1080
        _DotSize ("Dot cell (pixels)", Float) = 12
        _Halftone ("Halftone edge", Range(0, 1)) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

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

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _DarkColor;
            fixed4 _LitColor;
            float4 _Center;
            float _Radius;
            float _Softness;
            float _Aspect;
            float _HeightPx;
            float _DotSize;
            float _Halftone;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Distance from the beam centre, in overlay heights.
                float2 d = i.uv - _Center.xy;
                d.x *= _Aspect;
                float dist = length(d);

                // t: 0 inside the beam, 1 in the dark.
                float soft = max(_Softness, 1e-4);
                float t = saturate((dist - (_Radius - soft)) / soft);

                // Ben-Day screen at 45 degrees, in pixels so dots stay round.
                float2 px = float2(i.uv.x * _Aspect, i.uv.y) * _HeightPx;
                float2 g = float2(px.x + px.y, px.x - px.y) * (0.70710678 / max(_DotSize, 1.0));
                float r = length(frac(g) - 0.5);
                float dotR = sqrt(t) * 0.75;                 // 0.75 > 0.707: full cover at t = 1
                float aa = max(fwidth(r), 1e-4);
                float dots = 1.0 - smoothstep(dotR - aa, dotR + aa, r);

                float cover = lerp(t, dots, _Halftone);
                cover = t <= 0.001 ? 0.0 : (t >= 0.999 ? 1.0 : cover);

                // Blend in premultiplied space: lerping colour and alpha separately
                // makes a bright halo where the soft edge is half-transparent.
                // The dark side can carry a picture (the moonlit room); with no texture
                // the RawImage supplies white, so it is a flat colour as before.
                fixed4 dark = _DarkColor * tex2D(_MainTex, i.uv);
                float alpha = lerp(_LitColor.a, dark.a, cover);
                float3 rgb = lerp(_LitColor.rgb * _LitColor.a, dark.rgb * dark.a, cover) / max(alpha, 1e-4);
                return fixed4(rgb, alpha) * i.color;
            }
            ENDCG
        }
    }
}
