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

    [SerializeField] private Texture2D[] _crops, _spills;
    [SerializeField] private RawImage _cropA, _cropB, _spillA, _spillB;
    [SerializeField] private AngryEyes _eyes;
    [SerializeField] private float _angle = -1f;

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

        dv._spillA = SpillImage(root, "SpillA");
        dv._spillB = SpillImage(root, "SpillB");
        dv.Additive();

        dv._cropA = CropImage(root, "DoorA", cropRect);
        dv._cropB = CropImage(root, "DoorB", cropRect);

        dv._eyes = AngryEyes.Create(root, eyesCentre, eyeSpacing);
        dv.Angle = 0f;
        return dv;
    }

    private static RawImage SpillImage(Transform parent, string name)
    {
        var raw = UIKit.Raw(parent, name);
        UIKit.Stretch(raw.rectTransform);
        raw.color = new Color(1f, 1f, 1f, 0f);
        return raw;
    }

    // a door drawn in a scene: its spilled light gets its additive material as it wakes
    private void Awake()
    {
        if (_spillA != null) Additive();
    }

    /// <summary>The spilled light adds to the room (one material for both renders).</summary>
    private void Additive()
    {
        if (!Application.isPlaying) return;                     // a scene saves no copy of it
        var additive = Resources.Load<Material>("Materials/UIAdditive");
        Material addMat = additive != null ? new Material(additive) : null;
        _spillA.material = addMat;
        _spillB.material = addMat;
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
