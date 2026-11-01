using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Comic sound-effect lettering (CLICK!, CREEEAK..., THUD!) and speech balloons that
/// pop onto the screen: a quick overshoot "stamp", a short hold, then a fade. The
/// game's audio cues get these as their visual twins (GDD section 12). Each is a copy of
/// UI/ComicSfxWord or UI/ComicSfxBalloon, lettered and sized for what it says.
/// </summary>
public static class ComicSfx
{
    private static GameObject _word, _balloon;
    private static GameObject WordPrefab => _word != null ? _word : _word = Resources.Load<GameObject>("UI/ComicSfxWord");
    private static GameObject BalloonPrefab => _balloon != null ? _balloon : _balloon = Resources.Load<GameObject>("UI/ComicSfxBalloon");

    /// <summary>Pops a lettered word. Start the returned routine on any MonoBehaviour.</summary>
    public static IEnumerator Word(Transform parent, string text, Vector2 pos, float size, Color fill,
        float tilt, float hold = 0.9f, bool burst = false)
    {
        var root = (RectTransform)Object.Instantiate(WordPrefab, parent, false).transform;
        root.name = "SFX " + text;
        var box = new Vector2(size * text.Length * 0.62f + 60f, size * 1.6f);
        UIKit.Place(root, pos, box);
        root.localRotation = Quaternion.Euler(0f, 0f, tilt);
        var b = (RectTransform)root.Find("Burst");
        b.gameObject.SetActive(burst);
        if (burst) UIKit.Place(b, Vector2.zero, new Vector2(size * text.Length * 0.62f + 90f, size * 2.3f));
        var word = (RectTransform)root.Find("Word");
        UIKit.Place(word, Vector2.zero, box);
        var shadow = word.Find("Shadow").GetComponent<TMP_Text>();
        shadow.text = text;
        shadow.fontSize = size;
        shadow.rectTransform.anchoredPosition = new Vector2(size * 0.07f, -size * 0.07f);   // a hard ink drop shadow
        var face = word.Find("Face").GetComponent<TMP_Text>();
        face.text = text;
        face.fontSize = size;
        face.color = fill;
        return Animate(root, root.GetComponent<CanvasGroup>(), hold);
    }

    /// <summary>Pops a speech balloon with a tail pointing down-left at the speaker.</summary>
    public static IEnumerator Balloon(Transform parent, string text, Vector2 pos, float hold = 1.6f)
    {
        var root = (RectTransform)Object.Instantiate(BalloonPrefab, parent, false).transform;
        root.name = "Balloon";
        UIKit.Place(root, pos, new Vector2(220f, 92f));
        root.Find("Text").GetComponent<TMP_Text>().text = text;
        return Animate(root, root.GetComponent<CanvasGroup>(), hold);
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
