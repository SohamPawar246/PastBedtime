using System.Collections;
using UnityEngine;

/// <summary>
/// Comic sound-effect lettering (CLICK!, CREEEAK..., THUD!) and speech balloons that
/// pop onto the screen: a quick overshoot "stamp", a short hold, then a fade. The
/// game's audio cues get these as their visual twins (GDD section 12).
/// </summary>
public static class ComicSfx
{
    /// <summary>Pops a lettered word. Start the returned routine on any MonoBehaviour.</summary>
    public static IEnumerator Word(Transform parent, string text, Vector2 pos, float size, Color fill,
        float tilt, float hold = 0.9f, bool burst = false)
    {
        var root = UIKit.Rect(parent, "SFX " + text);
        UIKit.Place(root, pos, new Vector2(size * text.Length * 0.62f + 60f, size * 1.6f));
        root.localRotation = Quaternion.Euler(0f, 0f, tilt);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        if (burst)
        {
            var b = UIKit.Image(root, "Burst", Palette.Yellow, UIKit.Burst);
            UIKit.Place(b.rectTransform, Vector2.zero, new Vector2(size * text.Length * 0.62f + 90f, size * 2.3f));
        }
        UIKit.Lettering(root, "Word", text, size, fill, Vector2.zero, root.sizeDelta, outline: 0.24f, shadow: size * 0.07f);
        return Animate(root, group, hold);
    }

    /// <summary>Pops a speech balloon with a tail pointing down-left at the speaker.</summary>
    public static IEnumerator Balloon(Transform parent, string text, Vector2 pos, float hold = 1.6f)
    {
        var root = UIKit.Rect(parent, "Balloon");
        UIKit.Place(root, pos, new Vector2(220f, 92f));
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        // tail: an ink-outlined triangle under the balloon
        var tailInk = UIKit.Image(root, "TailInk", Palette.Ink, UIKit.Triangle);
        UIKit.Place(tailInk.rectTransform, new Vector2(-62f, -50f), new Vector2(40f, 46f));
        tailInk.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 160f);
        var shadow = UIKit.Image(root, "Shadow", Palette.Ink, UIKit.Disc);
        UIKit.Place(shadow.rectTransform, new Vector2(5f, -5f), new Vector2(220f, 92f));
        var ink = UIKit.Image(root, "Ink", Palette.Ink, UIKit.Disc);
        UIKit.Place(ink.rectTransform, Vector2.zero, new Vector2(220f, 92f));
        var face = UIKit.Image(root, "Face", Color.white, UIKit.Disc);
        UIKit.Place(face.rectTransform, Vector2.zero, new Vector2(208f, 80f));
        var tail = UIKit.Image(root, "Tail", Color.white, UIKit.Triangle);
        UIKit.Place(tail.rectTransform, new Vector2(-60f, -42f), new Vector2(28f, 34f));
        tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 160f);
        var t = UIKit.Text(root, "Text", text, UIKit.Body, 34f, Palette.Ink);
        t.fontStyle = TMPro.FontStyles.Bold;
        UIKit.Place(t.rectTransform, new Vector2(0f, 2f), new Vector2(200f, 70f));
        return Animate(root, group, hold);
    }

    private static IEnumerator Animate(RectTransform root, CanvasGroup group, float hold)
    {
        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.18f;
            float back = 1f + 2.6f * Mathf.Pow(k - 1f, 3f) + 1.6f * Mathf.Pow(k - 1f, 2f);
            root.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, back);
            group.alpha = Mathf.Clamp01(k * 3f);
            yield return null;
        }
        root.localScale = Vector3.one;
        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(hold);
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / 0.25f;
            root.localScale = Vector3.one * (1f + 0.08f * t / 0.25f);
            yield return null;
        }
        Object.Destroy(root.gameObject);
    }
}
