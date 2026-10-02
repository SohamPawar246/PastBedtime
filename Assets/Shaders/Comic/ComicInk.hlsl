// Shared "printing" maths for everything drawn inside the comic.
//
// The comic is black-and-white print: a lighting tone L (0 = black, 1 = white)
// becomes ink on paper in four bands, the way a 1950s page was shaded:
//     L > PaperCut      bare paper
//     mid-tones         Ben-Day dots on a 45-degree screen; darker = bigger dots
//     deep shadow       dots plus diagonal hatching
//     L < SolidCut      solid ink
// Screen-space patterns keep the dots the same printed size however far away
// an object is. Sizes are authored in pixels at 1080p and scale with the target.
#ifndef PB_COMIC_INK_INCLUDED
#define PB_COMIC_INK_INCLUDED

#define PB_PAPER_CUT 0.62
#define PB_HATCH_CUT 0.30
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

    float ink = max(dots, hatch);
    return L < PB_SOLID_CUT ? 1.0 : ink;
}

#endif
