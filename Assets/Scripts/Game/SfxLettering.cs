using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// SFX lettering in the comic world (GDD section 11): KAPOW, KRAK, WHIRR, SPRONG pop on
/// impacts with a random tilt, a coloured fill and a black outline, sometimes on a burst.
/// They live on the lane just in front of the actors, so the page camera prints them onto
/// the page with everything else.
/// </summary>
public class SfxLettering : MonoBehaviour
{
    private static SfxLettering _i;
    private TMP_FontAsset _font;
    private Material _mat;
    private int _layer;

    /// <summary>How long a word stays up (real time).</summary>
    private const float Life = 0.7f;

    /// <param name="after">Seconds (real time) to wait first: a scream after the blow that caused it.</param>
    public static void Spawn(string word, Vector2 lanePos, Color fill, float scale = 1f, bool burst = false, float after = 0f)
    {
        if (string.IsNullOrEmpty(word)) return;
        if (_i == null) _i = new GameObject("SfxLettering").AddComponent<SfxLettering>();
        if (after > 0f)
        {
            _i.StartCoroutine(_i.Later(after, word, lanePos, fill, scale, burst));
            return;
        }
        float tilt = Random.Range(-14f, 14f);
        Vector2 half = Half(word, scale, burst, tilt);
        // one word per spot at a time: a crowd hit at once reads as one SPLASH!, not a pile of them
        float now = Time.unscaledTime;
        _live.RemoveAll(r => now - r.time > Life);
        foreach (var r in _live)
            if (r.word == word && now - r.time < 0.35f && Overlaps(lanePos, half, r.pos, r.half)) return;
        lanePos = Place(lanePos, half);
        _live.Add((word, lanePos, half, now));
        _i.StartCoroutine(_i.Pop(word, lanePos, fill, scale, burst, tilt));
    }

    /// <summary>A different word never prints over one that's still up, nor over Max's speech balloon: it goes
    /// where it was meant to if that's free, else to the nearest free spot just above, beside or below whatever
    /// is in the way (rising is preferred: a scream goes over the blow that caused it). Sizes are each word's
    /// real size, so a big burst moves a word clear of all of it.</summary>
    private static Vector2 Place(Vector2 at, Vector2 half)
    {
        Rect balloon = default;
        bool hasBalloon = GameHUD.I != null && GameHUD.I.BubbleLane(out balloon);
        bool Free(Vector2 p)
        {
            foreach (var r in _live) if (Overlaps(p, half, r.pos, r.half)) return false;
            return !hasBalloon || !Overlaps(p, half, balloon.center, balloon.size * 0.5f);
        }
        if (Free(at)) return at;
        _spots.Clear();
        void Around(Vector2 c, Vector2 h)
        {
            _spots.Add(new Vector2(at.x, c.y + h.y + half.y + 0.05f));
            _spots.Add(new Vector2(at.x, c.y - h.y - half.y - 0.05f));
            _spots.Add(new Vector2(c.x - h.x - half.x - 0.05f, at.y));
            _spots.Add(new Vector2(c.x + h.x + half.x + 0.05f, at.y));
        }
        foreach (var r in _live) if (Overlaps(at, half, r.pos, r.half)) Around(r.pos, r.half);
        if (hasBalloon && Overlaps(at, half, balloon.center, balloon.size * 0.5f)) Around(balloon.center, balloon.size * 0.5f);
        Vector2 best = at;
        float bestCost = float.MaxValue;
        for (int round = 0; round < 2 && bestCost == float.MaxValue; round++)
        {
            foreach (var p in _spots)
            {
                if (!Free(p)) continue;
                float cost = (p - at).sqrMagnitude * (p.y >= at.y ? 1f : 1.5f);
                if (cost < bestCost) { bestCost = cost; best = p; }
            }
            if (bestCost < float.MaxValue) break;
            // every spot next to them is taken too: try round the things in the way of those spots
            int n = _spots.Count;
            for (int i = 0; i < n; i++)
                foreach (var r in _live)
                    if (Overlaps(_spots[i], half, r.pos, r.half)) Around(r.pos, r.half);
        }
        if (bestCost < float.MaxValue) return best;
        // boxed in on every side (a pile-up): over the top of everything in the way
        float top = at.y;
        foreach (var r in _live) if (Mathf.Abs(r.pos.x - at.x) < r.half.x + half.x) top = Mathf.Max(top, r.pos.y + r.half.y + half.y + 0.05f);
        return new Vector2(at.x, top);
    }

    private static readonly System.Collections.Generic.List<Vector2> _spots = new();

    private static readonly System.Collections.Generic.List<(string word, Vector2 pos, Vector2 half, float time)> _live = new();

    private IEnumerator Later(float after, string word, Vector2 lanePos, Color fill, float scale, bool burst)
    {
        yield return new WaitForSecondsRealtime(after);
        Spawn(word, lanePos, fill, scale, burst);
    }

    /// <summary>Half the size a word prints at on the lane, tilt included (Bangers is about 0.38 units a letter
    /// and 0.96 tall at scale 1; a burst's star sits between 33% and 49% of its box).</summary>
    private static Vector2 Half(string word, float scale, bool burst, float tilt)
    {
        Vector2 h = burst ? new Vector2((0.55f * word.Length + 1.4f) * scale, 2.2f * scale) * 0.42f
                          : new Vector2(0.19f * word.Length * scale + 0.1f, 0.48f * scale + 0.08f);
        float c = Mathf.Cos(tilt * Mathf.Deg2Rad), s = Mathf.Abs(Mathf.Sin(tilt * Mathf.Deg2Rad));
        return new Vector2(h.x * c + h.y * s, h.x * s + h.y * c);
    }

    private static bool Overlaps(Vector2 a, Vector2 aHalf, Vector2 b, Vector2 bHalf) =>
        Mathf.Abs(a.x - b.x) < aHalf.x + bHalf.x && Mathf.Abs(a.y - b.y) < aHalf.y + bHalf.y;

    private void Awake()
    {
        _font = Resources.Load<TMP_FontAsset>("Fonts/Bangers SDF");
        _layer = LayerMask.NameToLayer("Comic");
        if (_font != null)
        {
            _mat = new Material(_font.material);
            _mat.EnableKeyword("OUTLINE_ON");
            _mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
            _mat.SetColor(ShaderUtilities.ID_OutlineColor, Palette.Ink);
            _mat.EnableKeyword("UNDERLAY_ON");
            _mat.SetColor(ShaderUtilities.ID_UnderlayColor, Palette.Ink);
            _mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
            _mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            _mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        }
    }

    private void OnDestroy()
    {
        if (_i == this) _i = null;
        if (_mat != null) Destroy(_mat);
    }

    private IEnumerator Pop(string word, Vector2 pos, Color fill, float scale, bool burst, float tilt)
    {
        var root = new GameObject("SFX " + word);
        root.layer = _layer;
        root.transform.position = new Vector3(pos.x, pos.y, -3f);
        root.transform.rotation = Quaternion.Euler(0f, 0f, tilt);

        if (burst)
        {
            var b = new GameObject("Burst");
            b.layer = _layer;
            b.transform.SetParent(root.transform, false);
            b.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            var sr = b.AddComponent<SpriteRenderer>();
            sr.sprite = UIKit.Burst;
            sr.color = Palette.Yellow;
            float w = 0.55f * word.Length + 1.4f;
            b.transform.localScale = new Vector3(w, 2.2f, 1f) * scale / (sr.sprite.bounds.size.x > 0 ? sr.sprite.bounds.size.x : 1f);
        }

        var t = root.AddComponent<TextMeshPro>();
        if (_font != null)
        {
            t.font = _font;
            t.fontSharedMaterial = _mat;
        }
        t.text = word;
        t.fontSize = 9f * scale;
        t.color = fill;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = new Vector2(20f, 4f);
        t.sortingOrder = 50;

        // pop: overshoot in, hold, puff out (real time: lettering is the reader's eye, not the comic's clock)
        for (float a = 0f; a < Life; a += Time.unscaledDeltaTime)
        {
            float k = a / 0.14f;
            float s = a < 0.14f ? Mathf.LerpUnclamped(0.3f, 1f, 1f + 2.4f * Mathf.Pow(k - 1f, 3f) + 1.4f * Mathf.Pow(k - 1f, 2f)) : 1f + 0.06f * (a - 0.14f);
            root.transform.localScale = Vector3.one * s;
            float fade = a > Life - 0.18f ? (Life - a) / 0.18f : 1f;
            t.alpha = fade;
            yield return null;
        }
        Destroy(root);
    }
}
