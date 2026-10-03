using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mom's door on the title, opening and closing over the pre-rendered room.
///
/// The door was rendered in Blender at a handful of angles. For each one there is
/// a crop of the door region (door, hallway, Mom's silhouette) and the light the
/// open door adds to the rest of the room (a soft, low-resolution "spill" layer,
/// drawn additively). Setting <see cref="Angle"/> picks and crossfades the two
/// nearest renders. Mom's angry eyes are drawn on top when she is in view.
/// Everything lives in the 1920 × 1080 layer the room render uses.
/// </summary>
public class DoorVisit : MonoBehaviour
{
    private static readonly int[] Angles = { 0, 6, 12, 19, 26, 34, 42, 50 };
    public static float MaxAngle => Angles[Angles.Length - 1];

    private Texture2D[] _crops, _spills;
    private RawImage _cropA, _cropB, _spillA, _spillB;
    private AngryEyes _eyes;
    private float _angle = -1f;

    /// <summary>Door angle in degrees (0 = closed).</summary>
    public float Angle
    {
        get => Mathf.Max(0f, _angle);
        set { if (!Mathf.Approximately(value, _angle)) { _angle = value; Apply(); } }
    }

    /// <summary>0..1, how much of Mom the open door reveals (drives her eyes).</summary>
    public float Reveal => Mathf.InverseLerp(24f, 40f, Angle);

    public AngryEyes Eyes => _eyes;

    /// <param name="spillSet">"spill_" for the title's room; "spill_open_" for the game's (no comic standing in it).</param>
    public static DoorVisit Create(Transform layer, Rect cropRect, Vector2 eyesCentre, float eyeSpacing, string spillSet = "spill_")
    {
        var root = UIKit.Rect(layer, "DoorVisit");
        UIKit.Stretch(root);
        var dv = root.gameObject.AddComponent<DoorVisit>();

        int n = Angles.Length;
        dv._crops = new Texture2D[n];
        dv._spills = new Texture2D[n];
        for (int i = 1; i < n; i++)
        {
            dv._crops[i] = Resources.Load<Texture2D>($"Room/Door/door_{Angles[i]:00}");
            dv._spills[i] = Resources.Load<Texture2D>($"Room/Door/{spillSet}{Angles[i]:00}");
        }

        var additive = Shader.Find("PastBedtime/UI/Additive");
        Material addMat = additive != null ? new Material(additive) : null;
        dv._spillA = SpillImage(root, "SpillA", addMat);
        dv._spillB = SpillImage(root, "SpillB", addMat);

        dv._cropA = CropImage(root, "DoorA", cropRect);
        dv._cropB = CropImage(root, "DoorB", cropRect);

        dv._eyes = AngryEyes.Create(root, eyesCentre, eyeSpacing);
        dv.Angle = 0f;
        return dv;
    }

    private static RawImage SpillImage(Transform parent, string name, Material mat)
    {
        var raw = UIKit.Raw(parent, name);
        UIKit.Stretch(raw.rectTransform);
        raw.material = mat;
        raw.color = new Color(1f, 1f, 1f, 0f);
        return raw;
    }

    private static RawImage CropImage(Transform parent, string name, Rect r)
    {
        var raw = UIKit.Raw(parent, name);
        var rt = raw.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = r.size;
        rt.anchoredPosition = r.min;
        raw.color = new Color(1f, 1f, 1f, 0f);
        return raw;
    }

    private void Apply()
    {
        float a = Mathf.Clamp(Angle, 0f, MaxAngle);
        int i = 0;
        while (i < Angles.Length - 2 && a > Angles[i + 1]) i++;
        float k = Mathf.InverseLerp(Angles[i], Angles[i + 1], a);

        // Door region: the lower render fully, the upper one faded in over it.
        Show(_cropA, _crops[i], i == 0 ? 0f : 1f);
        Show(_cropB, _crops[i + 1], i == 0 ? Mathf.SmoothStep(0f, 1f, k) : k);

        // Spilled light: weights add up to one (the closed door spills nothing).
        Show(_spillA, _spills[i], i == 0 ? 0f : 1f - k);
        Show(_spillB, _spills[i + 1], k);

        _eyes.Visible = Reveal;
    }

    private static void Show(RawImage img, Texture tex, float alpha)
    {
        img.texture = tex;
        img.enabled = tex != null && alpha > 0.001f;
        img.color = new Color(1f, 1f, 1f, alpha);
    }
}

/// <summary>
/// Two angry eyes glaring out of Mom's silhouette: almond whites with a soft glow,
/// small pupils, and brows slanted down toward the middle. Exaggerated on purpose,
/// cartoon-style, so they read at a glance. <see cref="Visible"/> fades them in;
/// <see cref="Blink"/> and <see cref="Look"/> give them life.
/// </summary>
public class AngryEyes : MonoBehaviour
{
    private RectTransform[] _whites = new RectTransform[2];
    private RectTransform[] _pupils = new RectTransform[2];
    private CanvasGroup _group;
    private RectTransform _root;
    private float _visible = -1f, _blink, _look;

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
