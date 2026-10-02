using TMPro;
using UnityEngine;

/// <summary>
/// Credits crawl, read by torchlight. The text lives in Resources/Credits.txt
/// (it mirrors CREDITS.md) with a tiny markup:
///     # Heading      → a yellow caption box
///     ## Name        → big lettering
///     plain line     → body text (TMP rich text allowed)
///     blank line     → a gap
/// Any key (after a short debounce) or reaching the end returns to the title.
/// </summary>
public class CreditsScreen : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 75f;
    [SerializeField] private float debounceSeconds = 0.6f;
    private const float Width = 1100f;

    private RectTransform _crawl;
    private float _endY, _elapsed;
    private bool _leaving;

    private void Start()
    {
        UIKit.EnsureEventSystem();
        var canvas = UIKit.Canvas("CreditsCanvas", 0);
        var root = canvas.transform;

        var bg = UIKit.Image(root, "Page", Palette.Night);
        UIKit.Stretch(bg.rectTransform);

        var beam = TorchBeam.Create(root, null);
        beam.darkness = 0.7f;
        beam.radius = 0.36f;
        beam.followSelection = false;

        var viewport = UIKit.Rect(root, "Viewport");
        UIKit.Stretch(viewport);
        _crawl = UIKit.Rect(viewport, "Crawl");
        _crawl.anchorMin = _crawl.anchorMax = new Vector2(0.5f, 0.5f);
        _crawl.pivot = new Vector2(0.5f, 1f);
        _crawl.sizeDelta = new Vector2(Width, 10f);

        float height = Fill(_crawl, LoadText());
        float half = UIKit.RefHeight * 0.5f;
        _crawl.anchoredPosition = new Vector2(0f, -half);   // top of the crawl starts just below the screen
        _endY = half + height;

        var hint = UIKit.Text(root, "Hint", "ANY KEY  BACK TO THE MENU", UIKit.Mono, 20f, Palette.PaperDim);
        UIKit.Pin(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(800f, 30f));
    }

    private static string LoadText()
    {
        var asset = Resources.Load<TextAsset>("Credits");
        return asset != null ? asset.text : "# CREDITS\nResources/Credits.txt is missing.";
    }

    /// <summary>Lays the markup out top-down; returns the total height.</summary>
    private static float Fill(RectTransform parent, string text)
    {
        float y = 0f;
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            string line = raw.TrimEnd();
            if (line.Length == 0) { y -= 30f; continue; }

            if (line.StartsWith("## "))
            {
                var t = UIKit.Lettering(parent, "Name", line.Substring(3), 58f, Palette.Paper,
                    Vector2.zero, new Vector2(Width, 74f), outline: 0.2f, shadow: 5f);
                Top((RectTransform)t.transform.parent, ref y, 74f, 6f);
            }
            else if (line.StartsWith("# "))
            {
                var plate = UIKit.Panel(parent, "Heading", Vector2.zero, new Vector2(520f, 66f), Palette.Yellow,
                    shadow: 6f, tilt: Random.Range(-2.5f, 2.5f));
                var t = UIKit.Text(plate.transform, "Text", line.Substring(2), UIKit.Display, 40f, Palette.Ink);
                UIKit.Stretch(t.rectTransform);
                t.characterSpacing = 6f;
                Top((RectTransform)plate.transform.parent, ref y, 66f, 22f);
            }
            else
            {
                var t = UIKit.Text(parent, "Line", line, UIKit.Body, 30f, Palette.Paper);
                t.richText = true;
                float h = t.GetPreferredValues(line, Width, 0f).y + 6f;
                t.rectTransform.sizeDelta = new Vector2(Width, h);
                Top(t.rectTransform, ref y, h, 0f);
            }
        }
        return -y;
    }

    /// <summary>Stacks an element under the previous one inside a top-pivoted crawl.</summary>
    private static void Top(RectTransform rt, ref float y, float height, float gap)
    {
        y -= gap;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        y -= height;
    }

    private void Update()
    {
        if (_leaving) return;
        _elapsed += Time.unscaledDeltaTime;
        _crawl.anchoredPosition += Vector2.up * (scrollSpeed * Time.unscaledDeltaTime);

        bool finished = _crawl.anchoredPosition.y >= _endY;
        bool skipped = _elapsed > debounceSeconds && ShellInput.AnyPress();
        if (finished || skipped)
        {
            _leaving = true;
            App.I.Flow.Load(Scenes.Title);
        }
    }
}
