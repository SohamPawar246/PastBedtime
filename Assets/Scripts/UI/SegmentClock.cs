using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A seven-segment LED readout (the bedside alarm clock). Drawn from rounded
/// segment bars rather than a font, so the digits are evenly spaced and the
/// unlit segments glow faintly, the way a real cheap clock looks at night.
/// </summary>
public static class SegmentClock
{
    //                                  a      b      c      d      e      f      g
    private static readonly bool[][] Digits =
    {
        new[] { true,  true,  true,  true,  true,  true,  false }, // 0
        new[] { false, true,  true,  false, false, false, false }, // 1
        new[] { true,  true,  false, true,  true,  false, true  }, // 2
        new[] { true,  true,  true,  true,  false, false, true  }, // 3
        new[] { false, true,  true,  false, false, true,  true  }, // 4
        new[] { true,  false, true,  true,  false, true,  true  }, // 5
        new[] { true,  false, true,  true,  true,  true,  true  }, // 6
        new[] { true,  true,  true,  false, false, false, false }, // 7
        new[] { true,  true,  true,  true,  true,  true,  true  }, // 8
        new[] { true,  true,  true,  true,  false, true,  true  }, // 9
    };

    /// <summary>Builds the readout centred in <paramref name="parent"/>. Supports digits and ':'.</summary>
    public static RectTransform Build(Transform parent, string text, float digitHeight, Color lit, float ghostAlpha = 0.08f)
    {
        float h = digitHeight, w = h * 0.56f, t = h * 0.13f, gap = h * 0.16f, colonW = t * 1.8f;

        float total = 0f;
        foreach (char ch in text) total += (ch == ':' ? colonW : w) + gap;
        total -= gap;

        var root = UIKit.Rect(parent, "Readout " + text);
        UIKit.Place(root, Vector2.zero, new Vector2(total, h));

        float x = -total * 0.5f;
        foreach (char ch in text)
        {
            if (ch == ':')
            {
                for (int i = -1; i <= 1; i += 2)
                {
                    var dot = UIKit.Image(root, "Colon", lit, UIKit.Disc);
                    UIKit.Place(dot.rectTransform, new Vector2(x + colonW * 0.5f, i * h * 0.2f), new Vector2(t * 1.1f, t * 1.1f));
                }
                x += colonW + gap;
                continue;
            }
            int d = ch - '0';
            if (d < 0 || d > 9) { x += w + gap; continue; }
            Digit(root, Digits[d], new Vector2(x + w * 0.5f, 0f), w, h, t, lit, ghostAlpha);
            x += w + gap;
        }
        return root;
    }

    private static void Digit(Transform parent, bool[] on, Vector2 c, float w, float h, float t, Color lit, float ghost)
    {
        float inset = t * 0.35f;                     // the hairline gaps between segments
        float horiz = w - t - inset, vert = h * 0.5f - t * 0.5f - inset;
        Vector2[] pos =
        {
            new(0f, h * 0.5f - t * 0.5f),             // a
            new(w * 0.5f - t * 0.5f, h * 0.25f),      // b
            new(w * 0.5f - t * 0.5f, -h * 0.25f),     // c
            new(0f, -h * 0.5f + t * 0.5f),            // d
            new(-w * 0.5f + t * 0.5f, -h * 0.25f),    // e
            new(-w * 0.5f + t * 0.5f, h * 0.25f),     // f
            new(0f, 0f),                              // g
        };
        for (int i = 0; i < 7; i++)
        {
            bool vertical = i == 1 || i == 2 || i == 4 || i == 5;
            var seg = UIKit.Image(parent, "Seg", on[i] ? lit : lit.Alpha(ghost), UIKit.Soft, sliced: true);
            seg.pixelsPerUnitMultiplier = 22f / Mathf.Max(1f, t * 0.5f);
            UIKit.Place(seg.rectTransform, c + pos[i], vertical ? new Vector2(t, vert) : new Vector2(horiz, t));
        }
    }
}
