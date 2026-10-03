using System;
using TMPro;
using UnityEngine;

/// <summary>
/// "How to read": the controls from GDD section 3 as a two-column comic page.
/// Left hand plays Max, right hand plays the reader.
/// </summary>
public static class HowToReadPanel
{
    public static CaptionButton Build(Transform parent, Action onBack)
    {
        const float w = 1320f, h = 800f;
        var root = UIKit.Rect(parent, "HowToRead");
        UIKit.Place(root, Vector2.zero, new Vector2(w, h));

        UIKit.Panel(root, "Card", Vector2.zero, new Vector2(w, h), Palette.Paper, shadow: 12f);

        var plate = UIKit.Panel(root, "TitlePlate", new Vector2(-w * 0.5f + 200f, h * 0.5f - 6f),
            new Vector2(360f, 70f), Palette.Yellow, shadow: 6f, tilt: -3f);
        var title = UIKit.Text(plate.transform, "Title", "HOW TO READ", UIKit.Display, 46f, Palette.Ink);
        UIKit.Stretch(title.rectTransform);
        title.characterSpacing = 6f;

        // left hand only: everything Max does is in reach of W A S D (the right hand holds the mouse)
        Column(root, new Vector2(-w * 0.25f + 10f, 110f), "THE KEYBOARD IS MAX", Palette.HeroRed, new[]
        {
            ("A / D", "run"),
            ("SPACE", "jump (hold = higher)"),
            ("E", "punch, x3 = haymaker"),
            ("Q", "kick (W+Q launches)"),
            ("SHIFT", "dodge"),
            ("W / S", "look up / drop down"),
            ("F", "splash page (meter full)"),
        });

        Column(root, new Vector2(w * 0.25f - 10f, 110f), "THE MOUSE IS THE TORCH", Palette.Cyan, new[]
        {
            ("MOVE", "aim the beam"),
            ("HOLD LEFT", "beam follows Max"),
            ("RIGHT CLICK", "torch on / off"),
            ("SCROLL", "twist the crank"),
            ("MIDDLE CLICK", "twist the lens"),
            ("ESC", "bookmark (pause)"),
        });

        // The one rule, as a caption strip along the bottom.
        var strip = UIKit.Panel(root, "Rule", new Vector2(0f, -h * 0.5f + 175f), new Vector2(w - 120f, 96f),
            Palette.Yellow, shadow: 7f, tilt: 0.6f);
        var rule = UIKit.Text(strip.transform, "Text",
            "<b>ONLY WHAT'S LIT IS ALIVE.</b>  Out of the light, everything freezes, even Max.\n" +
            "When the hallway light comes on under the door: <b>LIGHTS OUT!</b>",
            UIKit.Body, 27f, Palette.Ink);
        UIKit.Stretch(rule.rectTransform, 14f);

        var back = CaptionButton.Create(root, "BackButton", "BACK", () => onBack?.Invoke(),
            new Vector2(0f, -h * 0.5f + 58f), new Vector2(240f, 64f), 34f, tilt: -1.2f, quiet: true);
        return back;
    }

    private static void Column(Transform root, Vector2 center, string heading, Color accent, (string key, string what)[] rows)
    {
        const float colW = 560f;
        var head = UIKit.Text(root, "Head " + heading, heading, UIKit.Display, 44f, accent);
        UIKit.Place(head.rectTransform, center + new Vector2(0f, 170f), new Vector2(colW, 60f));
        head.rectTransform.localRotation = Quaternion.Euler(0, 0, -1.5f);
        head.characterSpacing = 3f;

        float y = center.y + 100f;
        foreach (var (key, what) in rows)
        {
            string lead = key + " " + new string('.', Mathf.Max(2, 14 - key.Length));
            var line = UIKit.Text(root, "Line " + key, "<b>" + lead + "</b> " + what, UIKit.Mono, 26f, Palette.Ink,
                TextAlignmentOptions.Left);
            line.textWrappingMode = TextWrappingModes.NoWrap;
            UIKit.Place(line.rectTransform, new Vector2(center.x, y), new Vector2(colW, 40f));
            y -= 44f;
        }
    }
}
