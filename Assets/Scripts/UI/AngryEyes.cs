using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Two angry eyes glaring out of Mom's silhouette: almond whites with a soft glow,
/// small pupils, and brows slanted down toward the middle. Exaggerated on purpose,
/// cartoon-style, so they read at a glance. <see cref="Visible"/> fades them in;
/// <see cref="Blink"/> and <see cref="Look"/> give them life.
/// </summary>
public class AngryEyes : MonoBehaviour
{
    [SerializeField] private RectTransform[] _whites = new RectTransform[2];
    [SerializeField] private RectTransform[] _pupils = new RectTransform[2];
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _root;
    [SerializeField] private float _visible = -1f;
    private float _blink, _look;

    private static readonly Color Glow = new Color(1f, 0.93f, 0.7f);
    private static readonly Color White = new Color(1f, 0.98f, 0.9f);
    private static readonly Color Pupil = new Color(0.08f, 0.04f, 0.03f);
    private static readonly Color Brow = new Color(0.03f, 0.02f, 0.035f);

    public static AngryEyes Create(Transform parent, Vector2 centre, float spacing)
    {
        var root = UIKit.Rect(parent, "MomEyes");
        UIKit.Place(root, centre, new Vector2(spacing * 3f, spacing * 2f));
        var eyes = root.gameObject.AddComponent<AngryEyes>();
        eyes._root = root;
        eyes._group = root.gameObject.AddComponent<CanvasGroup>();
        eyes._group.blocksRaycasts = false;

        var glow = UIKit.Image(root, "Glow", Glow.Alpha(0.28f), UIKit.Glow);
        UIKit.Place(glow.rectTransform, Vector2.zero, new Vector2(spacing * 3.4f, spacing * 1.7f));

        float w = spacing * 0.9f, h = spacing * 0.52f;
        for (int side = 0; side < 2; side++)
        {
            float s = side == 0 ? -1f : 1f;
            var eye = UIKit.Rect(root, side == 0 ? "EyeL" : "EyeR");
            UIKit.Place(eye, new Vector2(s * spacing * 0.5f, 0f), new Vector2(w, h));
            var white = UIKit.Image(eye, "White", White, UIKit.Disc);
            UIKit.Stretch(white.rectTransform);
            var pupil = UIKit.Image(eye, "Pupil", Pupil, UIKit.Disc);
            UIKit.Place(pupil.rectTransform, Vector2.zero, new Vector2(h * 0.62f, h * 0.62f));
            // The brow: a dark bar dipping toward the nose, biting into the top of the eye.
            var brow = UIKit.Image(eye, "Brow", Brow, UIKit.Disc);
            UIKit.Place(brow.rectTransform, new Vector2(s * -w * 0.06f, h * 0.55f), new Vector2(w * 1.45f, h * 0.78f));
            brow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, s * 21f);
            eyes._whites[side] = eye;
            eyes._pupils[side] = pupil.rectTransform;
        }
        eyes.Visible = 0f;
        return eyes;
    }

    /// <summary>0 hidden .. 1 fully there (they also swell slightly as they appear).</summary>
    public float Visible
    {
        get => _visible;
        set
        {
            if (Mathf.Approximately(value, _visible)) return;
            _visible = Mathf.Clamp01(value);
            _group.alpha = _visible;
            _root.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, Mathf.SmoothStep(0f, 1f, _visible));
        }
    }

    /// <summary>0 open .. 1 shut.</summary>
    public float Blink
    {
        get => _blink;
        set
        {
            _blink = Mathf.Clamp01(value);
            foreach (var e in _whites) e.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.08f, _blink), 1f);
        }
    }

    /// <summary>-1 .. 1: where the pupils point, left to right (0 = straight at Roshan).</summary>
    public float Look
    {
        get => _look;
        set
        {
            _look = Mathf.Clamp(value, -1f, 1f);
            for (int i = 0; i < 2; i++)
            {
                var parent = (RectTransform)_pupils[i].parent;
                _pupils[i].anchoredPosition = new Vector2(_look * parent.sizeDelta.x * 0.22f, -parent.sizeDelta.y * 0.06f);
            }
        }
    }
}
