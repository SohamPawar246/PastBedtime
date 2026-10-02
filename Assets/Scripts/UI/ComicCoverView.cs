using TMPro;
using UnityEngine;

/// <summary>
/// The printed cover of Max Voltage #1, as it appears in the room: black-and-white
/// art (rendered from the comic set by <see cref="ComicCover"/>), an ink masthead,
/// the issue number, the story strap, and one spot of colour, the SFX burst.
/// One builder, so the title and the Bookmark pause show the same object.
/// </summary>
public static class ComicCoverView
{
    /// <summary>Builds the cover filling <paramref name="parent"/> (a rect of any size; 600 × 820 is the design size).</summary>
    public static void Build(RectTransform parent, float width, float height)
    {
        float k = width / 600f;
        float border = 16f * k;

        var page = UIKit.Image(parent, "Paper", Palette.ComicPaper);
        UIKit.Stretch(page.rectTransform);

        var art = UIKit.Raw(parent, "Art");
        UIKit.Stretch(art.rectTransform, border);
        var tex = ComicCover.Shared;
        if (tex != null) art.texture = tex;
        else art.color = Palette.ComicPaper;

        // Masthead: black ink letters with a paper keyline, tipped like a real logo.
        var mast = UIKit.Text(parent, "Masthead", "MAX VOLTAGE", UIKit.Display, 112f * k, Palette.ComicInk);
        mast.outlineWidth = 0.2f;
        mast.outlineColor = Palette.ComicPaper;
        mast.characterSpacing = 2f;
        mast.textWrappingMode = TextWrappingModes.NoWrap;
        UIKit.Place(mast.rectTransform, new Vector2(0f, height * 0.5f - 96f * k), new Vector2(width - 40f * k, 120f * k));
        mast.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2f);

        // Issue number and story strap along the bottom edge.
        float box = 68f * k;
        float by = -height * 0.5f + border + box * 0.5f;
        var issue = UIKit.Image(parent, "Issue", Palette.ComicInk);
        UIKit.Place(issue.rectTransform, new Vector2(-width * 0.5f + border + box * 0.5f, by), new Vector2(box, box));
        var issueText = UIKit.Text(issue.transform, "No", "#1", UIKit.Display, 40f * k, Palette.ComicPaper);
        UIKit.Stretch(issueText.rectTransform);

        float strapW = width - 2f * border - box - 8f * k;
        var strap = UIKit.Image(parent, "Strap", Palette.ComicInk);
        UIKit.Place(strap.rectTransform, new Vector2(-width * 0.5f + border + box + 8f * k + strapW * 0.5f, by), new Vector2(strapW, box));
        var strapText = UIKit.Text(strap.transform, "Title", "NIGHT OF THE BLOT", UIKit.Display, 46f * k, Palette.ComicPaper);
        strapText.characterSpacing = 6f;
        strapText.textWrappingMode = TextWrappingModes.NoWrap;
        UIKit.Stretch(strapText.rectTransform);

        // The one spot of colour a comic is allowed: the SFX.
        var burst = UIKit.Image(parent, "Burst", Palette.Yellow, UIKit.Burst);
        UIKit.Place(burst.rectTransform, new Vector2(96f * k, 104f * k), new Vector2(230f * k, 190f * k));
        burst.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
        var sfx = UIKit.Text(burst.transform, "Kapow", "KAPOW!", UIKit.Display, 60f * k, Palette.HeroRed);
        sfx.outlineWidth = 0.22f;
        sfx.outlineColor = Palette.Ink;
        sfx.textWrappingMode = TextWrappingModes.NoWrap;
        UIKit.Stretch(sfx.rectTransform);
        sfx.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -6f);
    }
}
