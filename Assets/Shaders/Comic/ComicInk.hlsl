// Shared "printing" maths for everything drawn inside the comic.
//
// The comic is black-and-white print: a lighting tone L (0 = black, 1 = white)
// becomes ink on paper in four bands, the way a 1950s page was shaded:
//     L > PaperCut      bare paper
//     mid-tones         Ben-Day dots on a 45-degree screen; darker = bigger dots
//     deep shadow       dots plus diagonal hatching
//     deepest shadow    cross-hatched (the manga inker's last step before solid black)
//     L < SolidCut      solid ink
// Screen-space patterns keep the dots the same printed size however far away
// an object is. Sizes are authored in pixels at 1080p and scale with the target.
#ifndef PB_COMIC_INK_INCLUDED
#define PB_COMIC_INK_INCLUDED

#define PB_PAPER_CUT 0.62
#define PB_HATCH_CUT 0.30
#define PB_CROSS_CUT 0.20
#define PB_SOLID_CUT 0.10

// Global comic key light, set by ComicLight.cs. Independent of URP lights so the
// room's lamps never touch the comic and the comic's light never touches the room.
float4 _ComicLightDir;   // xyz = direction the light travels FROM (world), w unused
float _ComicAmbient;

float3 PB_ComicLightDirection()
{
    float3 d = _ComicLightDir.xyz;
    return dot(d, d) > 1e-4 ? normalize(d) : normalize(float3(-0.45, 0.8, -0.4));
}

float PB_ComicAmbient()
{
    return _ComicAmbient > 0.0 ? _ComicAmbient : 0.35;
}

float PB_Hash(float2 c, float2 k) { return frac(sin(dot(c, k)) * 43758.5453); }

// 0..1 ink coverage for tone L at pixel position px.
float PB_InkCoverage(float L, float2 px, float dotCell, float hatchSpacing)
{
    float scale = _ScreenParams.y / 1080.0;

    // Ben-Day dots: 45-degree screen, radius grows as the tone darkens.
    float cell = max(2.0, dotCell * scale);
    float2 g = float2(px.x + px.y, px.x - px.y) * (0.70710678 / cell);
    float r = length(frac(g) - 0.5);
    float darkness = saturate((PB_PAPER_CUT - L) / PB_PAPER_CUT);
    float dotR = sqrt(darkness) * 0.56;
    float aa = max(fwidth(r), 1e-4);
    float dots = 1.0 - smoothstep(dotR - aa, dotR + aa, r);
    dots *= step(1e-3, darkness);

    // Hatching: thin diagonal strokes that only appear in deep shadow.
    float hs = max(2.0, hatchSpacing * scale);
    float h = abs(frac((px.x - px.y) / hs) - 0.5) * 2.0;           // 0 on a stroke
    float width = lerp(0.0, 0.42, saturate((PB_HATCH_CUT - L) / (PB_HATCH_CUT - PB_SOLID_CUT)));
    float hatch = 1.0 - smoothstep(width, width + max(fwidth(h), 1e-4) * 1.5, h);
    hatch *= step(L, PB_HATCH_CUT);

    // Cross-hatching: a second set of strokes the other way, in the deepest shadow only.
    float h2 = abs(frac((px.x + px.y) / hs) - 0.5) * 2.0;
    float width2 = lerp(0.0, 0.38, saturate((PB_CROSS_CUT - L) / (PB_CROSS_CUT - PB_SOLID_CUT)));
    float cross = 1.0 - smoothstep(width2, width2 + max(fwidth(h2), 1e-4) * 1.5, h2);
    cross *= step(L, PB_CROSS_CUT);

    float ink = max(max(dots, hatch), cross);
    return L < PB_SOLID_CUT ? 1.0 : ink;
}

// Stars pricked out of a night sky: tiny paper specks scattered in world space (so they stay put
// as the page camera moves), only where the sky is dark.
float PB_Stars(float3 posWS, float L, float amount)
{
    if (amount <= 0.0) return 0.0;
    const float cellSize = 0.42;
    float2 p = posWS.xy / cellSize;
    float2 c = floor(p);
    float present = step(PB_Hash(c, float2(12.9898, 78.233)), 0.09 * amount);
    float2 off = (float2(PB_Hash(c, float2(39.3, 11.1)), PB_Hash(c, float2(73.1, 27.7))) - 0.5) * 0.6;
    float big = PB_Hash(c, float2(5.3, 91.7));
    float r = lerp(0.06, 0.13, big * big * big);
    float d = length(frac(p) - 0.5 - off);
    float aa = max(fwidth(d), 1e-4);
    return present * (1.0 - smoothstep(r - aa, r + aa, d)) * (1.0 - smoothstep(0.3, 0.45, L));
}

// Hand-inked scuffs, the way a comic inker roughs up a clean shape: a few short, thick strokes
// flicked over the lit side, tapered at both ends, mostly on a falling diagonal, never dense.
// Placed in world space on the surface's main plane, so they stay put on the scenery as the page
// camera moves. Only on light, lit surfaces (shadow is already ink).
float PB_Scuffs(float3 posWS, float3 nWS, float L, float amount)
{
    if (amount <= 0.0) return 0.0;
    float3 an = abs(nWS);
    float2 p = an.z >= an.x && an.z >= an.y ? posWS.xy : (an.y >= an.x ? posWS.xz : posWS.zy);
    const float cellSize = 0.55;
    float2 cell = floor(p / cellSize);
    float aa = max(length(fwidth(p)), 1e-4) * 0.7;
    float ink = 0.0;
    [unroll] for (int dy = -1; dy <= 1; dy++)
    {
        [unroll] for (int dx = -1; dx <= 1; dx++)
        {
            float2 c = cell + float2(dx, dy);
            float present = step(PB_Hash(c, float2(127.1, 311.7)), 0.2);   // about one cell in five
            float h2 = PB_Hash(c, float2(269.5, 183.3));
            float h3 = PB_Hash(c, float2(419.2, 371.9));
            float2 centre = (c + float2(h2, h3)) * cellSize;
            float ang = radians(-38.0 + (h2 - 0.5) * 46.0) + (h3 > 0.82 ? 1.25 : 0.0);
            float2 dir = float2(cos(ang), sin(ang));
            float len = lerp(0.11, 0.24, h3);
            float2 d = p - centre;
            float t = clamp(dot(d, dir), -len, len);
            float dist = length(d - dir * t);
            float taper = 1.0 - abs(t) / len;
            float w = 0.03 * (0.3 + 0.7 * sqrt(taper));
            ink = max(ink, present * (1.0 - smoothstep(w - aa, w + aa, dist)));
        }
    }
    return ink * amount * smoothstep(0.28, 0.6, L);
}

// The torch (GDD section 11). Inside the beam the page is printed; outside it reads like a
// page in a dark room: blue-black, paper at about 18% brightness, ink faint but readable.
// The beam's edge is a soft 0.4-unit falloff with a slightly warm rim; a coloured lens tints
// the paper (not the ink). Set by LightField.cs. With _PB_GameLight = 0 (the title's cover)
// colour passes through untouched.
float4 _PB_Beam;      // xy centre on the lane, z radius (0 when off), w feather
float4 _PB_Lens;      // rgb lens colour, a tint strength
float _PB_Wake;       // 1 = the whole page is lit (a flare, the room light)
float _PB_GameLight;
float4 _PB_WedgeA;    // Mom's door wedge on the page: xy p0, zw p1
float4 _PB_WedgeB;    //                                 xy p2, zw p3
float _PB_WedgeOn;
float _PB_Impact;     // 1 for an impact frame: what's lit prints as a negative for a moment (ComicFx.cs)

float PB_EdgeSide(float2 a, float2 b, float2 p)
{
    float2 e = b - a, v = p - a;
    return e.x * v.y - e.y * v.x;
}

// 1 inside the (convex) wedge quad, 0 outside
float PB_InWedge(float2 p)
{
    if (_PB_WedgeOn < 0.5) return 0.0;
    float s1 = PB_EdgeSide(_PB_WedgeA.xy, _PB_WedgeA.zw, p);
    float s2 = PB_EdgeSide(_PB_WedgeA.zw, _PB_WedgeB.xy, p);
    float s3 = PB_EdgeSide(_PB_WedgeB.xy, _PB_WedgeB.zw, p);
    float s4 = PB_EdgeSide(_PB_WedgeB.zw, _PB_WedgeA.xy, p);
    bool pos = s1 >= 0.0 && s2 >= 0.0 && s3 >= 0.0 && s4 >= 0.0;
    bool neg = s1 <= 0.0 && s2 <= 0.0 && s3 <= 0.0 && s4 <= 0.0;
    return (pos || neg) ? 1.0 : 0.0;
}

half3 PB_Grade(half3 col, float3 posWS)
{
    if (_PB_GameLight < 0.5) return col;
    float d = distance(posWS.xy, _PB_Beam.xy);
    float r = _PB_Beam.z;
    float f = max(_PB_Beam.w, 1e-3);
    float beam = r > 0.001 ? 1.0 - smoothstep(r - f, r, d) : 0.0;
    float wedge = PB_InWedge(posWS.xy);
    float lit = max(max(beam, _PB_Wake), wedge);
    float lum = dot(col, half3(0.299, 0.587, 0.114));
    half3 night = lum * 0.18 * half3(0.55, 0.66, 1.0) + half3(0.012, 0.016, 0.035);
    half3 tint = lerp(half3(1, 1, 1), _PB_Lens.rgb, _PB_Lens.a * beam);   // a lens colours only its own beam
    half3 read = lerp(col, col * tint, saturate((lum - 0.35) / 0.4));
    float rim = r > 0.001 ? smoothstep(r - f * 1.8, r - f * 0.6, d) * (1.0 - smoothstep(r - f * 0.6, r, d)) : 0.0;
    read += rim * (1.0 - _PB_Wake) * half3(0.2, 0.11, 0.0) * lum;
    read = lerp(read, read * half3(1.0, 0.8, 0.5), wedge * (1.0 - beam * 0.6) * 0.75);   // the hallway's amber
    // the impact frame: the lit page flips to a negative (paper goes to ink, ink to paper), the way manga
    // punctuates a blow; the dark around it stays dark, so the frame darkens rather than flashes
    float tone = dot(read, half3(0.299, 0.587, 0.114));
    half3 negative = lerp(half3(0.957, 0.945, 0.918), half3(0.035, 0.035, 0.045), saturate((tone - 0.06) / 0.86));
    read = lerp(read, negative, _PB_Impact);
    return lerp(night, read, lit);
}

#endif
