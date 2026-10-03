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

    public static void Spawn(string word, Vector2 lanePos, Color fill, float scale = 1f, bool burst = false)
    {
        if (string.IsNullOrEmpty(word)) return;
        if (_i == null) _i = new GameObject("SfxLettering").AddComponent<SfxLettering>();
        // one word per spot at a time: a crowd hit at once reads as one SPLASH!, not a pile of them;
        // different words at the same spot stack upward instead of printing over each other
        float now = Time.unscaledTime;
        _recent.RemoveAll(r => now - r.time > 0.6f);
        int stack = 0;
        foreach (var r in _recent)
        {
            if ((r.pos - lanePos).sqrMagnitude >= 2.25f) continue;
            if (r.word == word && now - r.time < 0.35f) return;
            stack++;
        }
        _recent.Add((word, lanePos, now));
        lanePos += new Vector2(stack % 2 == 0 ? 0.3f : -0.3f, 0.85f) * stack;
        _i.StartCoroutine(_i.Pop(word, lanePos, fill, scale, burst));
    }

    private static readonly System.Collections.Generic.List<(string word, Vector2 pos, float time)> _recent = new();

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

    private IEnumerator Pop(string word, Vector2 pos, Color fill, float scale, bool burst)
    {
        var root = new GameObject("SFX " + word);
        root.layer = _layer;
        root.transform.position = new Vector3(pos.x, pos.y, -3f);
        float tilt = Random.Range(-14f, 14f);
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
        float life = 0.7f;
        for (float a = 0f; a < life; a += Time.unscaledDeltaTime)
        {
            float k = a / 0.14f;
            float s = a < 0.14f ? Mathf.LerpUnclamped(0.3f, 1f, 1f + 2.4f * Mathf.Pow(k - 1f, 3f) + 1.4f * Mathf.Pow(k - 1f, 2f)) : 1f + 0.06f * (a - 0.14f);
            root.transform.localScale = Vector3.one * s;
            float fade = a > life - 0.18f ? (life - a) / 0.18f : 1f;
            t.alpha = fade;
            yield return null;
        }
        Destroy(root);
    }
}
