using System;
using TMPro;
using UnityEngine;

/// <summary>
/// "How to read": the controls from GDD section 3 as a two-column comic page.
/// Left hand plays Max, right hand plays the reader. The keys are the player's own
/// (<see cref="Bindings"/>), re-read whenever the page opens or the controls change.
/// </summary>
public static class HowToReadPanel
{
    private static string N(Bindings.Act a) => Bindings.Name(a);

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
        var live = root.gameObject.AddComponent<HowToReadLive>();
        live.Add(Column(root, new Vector2(-w * 0.25f + 10f, 110f), "THE KEYBOARD IS MAX", Palette.HeroRed, 7), () => new[]
        {
            (N(Bindings.Act.Left) + " / " + N(Bindings.Act.Right), "run"),
            (N(Bindings.Act.Jump), "jump (hold = higher)"),
            (N(Bindings.Act.Punch), "punch, x3 = haymaker"),
            (N(Bindings.Act.Kick), $"kick ({N(Bindings.Act.Up)}+{N(Bindings.Act.Kick)} launches)"),
            (N(Bindings.Act.Dodge), "dodge"),
            (N(Bindings.Act.Up) + " / " + N(Bindings.Act.Down), "up / slam (with kick)"),
            (N(Bindings.Act.Splash), "splash page (meter full)"),
        });
        live.Add(Column(root, new Vector2(w * 0.25f - 10f, 110f), "THE MOUSE IS THE TORCH", Palette.Cyan, 6), () => new[]
        {
            ("MOVE", "aim the beam"),
            ("HOLD " + N(Bindings.Act.Follow), "beam follows Max"),
            (N(Bindings.Act.Torch), "torch on / off"),
            (N(Bindings.Act.Crank), "twist the crank"),
            (N(Bindings.Act.Lens), "twist the lens (1-4 pick)"),
            ("ESC", "bookmark (pause)"),
        });
        var change = UIKit.Text(root, "Rebind", "Change the keys: SETTINGS > CONTROLS", UIKit.Mono, 22f, Palette.Ink.Alpha(0.6f));
        UIKit.Place(change.rectTransform, new Vector2(0f, -h * 0.5f + 245f), new Vector2(w - 120f, 30f));

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

    private static TMP_Text[] Column(Transform root, Vector2 center, string heading, Color accent, int count)
    {
        const float colW = 560f;
        var head = UIKit.Text(root, "Head " + heading, heading, UIKit.Display, 44f, accent);
        UIKit.Place(head.rectTransform, center + new Vector2(0f, 170f), new Vector2(colW, 60f));
        head.rectTransform.localRotation = Quaternion.Euler(0, 0, -1.5f);
        head.characterSpacing = 3f;

        var lines = new TMP_Text[count];
        float y = center.y + 100f;
        for (int i = 0; i < count; i++)
        {
            var line = UIKit.Text(root, "Line " + i, "", UIKit.Mono, 26f, Palette.Ink, TextAlignmentOptions.Left);
            line.textWrappingMode = TextWrappingModes.NoWrap;
            UIKit.Place(line.rectTransform, new Vector2(center.x, y), new Vector2(colW, 40f));
            lines[i] = line;
            y -= 44f;
        }
        return lines;
    }

    /// <summary>"KEY .......... what it does": the dots run out to a column (and shrink for long names).</summary>
    public static string Line(string key, string what) =>
        "<b>" + key + " " + new string('.', Mathf.Max(2, 14 - key.Length)) + "</b> " + what;
}

/// <summary>Re-letters the How to Read lines from the bindings whenever the page is shown or they change.</summary>
public class HowToReadLive : MonoBehaviour
{
    private readonly System.Collections.Generic.List<(TMP_Text[] lines, Func<(string key, string what)[]> rows)> _columns = new();

    public void Add(TMP_Text[] lines, Func<(string key, string what)[]> rows)
    {
        _columns.Add((lines, rows));
        Refresh();
    }

    private void OnEnable()
    {
        Bindings.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() => Bindings.Changed -= Refresh;

    private void Refresh()
    {
        foreach (var (lines, rows) in _columns)
        {
            var r = rows();
            for (int i = 0; i < lines.Length && i < r.Length; i++) lines[i].text = HowToReadPanel.Line(r[i].key, r[i].what);
        }
    }
}
