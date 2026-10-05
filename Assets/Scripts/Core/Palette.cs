using UnityEngine;

/// <summary>
/// The colours from the GDD art direction (section 11). Every screen and effect
/// pulls from here, so a re-tint is a one-file change.
///
/// The comic itself is printed in black and white (ComicPaper / ComicInk); the
/// only colour inside it is SFX lettering and the torch lens tint.
/// </summary>
public static class Palette
{
    // ---- Print (lettering, captions, menus) ------------------------------------------
    public static readonly Color Paper = Hex("#F2E8CF");
    public static readonly Color PaperDim = Hex("#CFC4A8");
    public static readonly Color Ink = Hex("#141414");
    public static readonly Color Cyan = Hex("#00A6D6");
    public static readonly Color Magenta = Hex("#E6007E");
    public static readonly Color Yellow = Hex("#FFD400");
    public static readonly Color HeroRed = Hex("#E10600");
    public static readonly Color HeroBlue = Hex("#2156C9");

    // ---- The comic page (black and white) ------------------------------------------------
    public static readonly Color ComicPaper = Hex("#F4F1EA");
    public static readonly Color ComicInk = Hex("#121212");

    // ---- The bedroom (around the comic) -----------------------------------------
    public static readonly Color Night = Hex("#0B1026");
    public static readonly Color Bedroom = Hex("#141A2E");
    public static readonly Color BedroomHi = Hex("#1E2742");
    public static readonly Color Duvet = Hex("#22305A");
    public static readonly Color DuvetHi = Hex("#2E3F72");
    public static readonly Color Door = Hex("#1A2340");
    public static readonly Color Moonlight = Hex("#3A5390");
    public static readonly Color Hallway = Hex("#FFC873");
    public static readonly Color ClockRed = Hex("#FF3B30");

    // ---- Lenses -------------------------------------------------------------------------
    public static readonly Color LensClear = Hex("#FFE3A3");
    public static readonly Color LensGhost = Hex("#B26BFF");

    public static Color Alpha(this Color c, float a) { c.a = a; return c; }

    /// <summary>Parses "#RRGGBB" or "#RRGGBBAA"; magenta means a typo.</summary>
    public static Color Hex(string hex) =>
        ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
}
