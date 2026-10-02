using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Roshan's hand and torch on the title (a Blender render, bottom-right), tied to
/// the menu's <see cref="TorchBeam"/>: the hand turns a few degrees toward wherever
/// the beam is, a soft shaft of light runs from the lens to the pool, and dust
/// motes drift through the lit air. All positions are in the 1920 × 1080 layer the
/// room render lives in.
/// </summary>
public class TorchHand : MonoBehaviour
{
    public TorchBeam beam;
    public RectTransform layer;          // the cover-scaled 1920 × 1080 layer
    public Vector2 lensRest;             // lens position in layer units, as rendered
    public Vector2 aimRest;              // the point the render aims at
    public Vector2 pivot;                // wrist, in layer units
    public float maxTurn = 9f;

    private RectTransform _hand;
    private LightCone _cone;
    private Image _lensGlow;
    private RectTransform[] _motes;
    private Image[] _moteImages;
    private Vector2[] _moteSeeds;
    private float _turn;

    private const int MoteCount = 28;

    public static TorchHand Create(Transform layerParent, RectTransform layer, TorchBeam beam, Texture handTexture,
        Rect handRect, Vector2 lensRest, Vector2 aimRest, Vector2 wrist)
    {
        var root = UIKit.Rect(layerParent, "TorchHand");
        UIKit.Stretch(root);
        var th = root.gameObject.AddComponent<TorchHand>();
        th.beam = beam; th.layer = layer; th.lensRest = lensRest; th.aimRest = aimRest; th.pivot = wrist;

        th._cone = LightCone.Create(root, "LightShaft", Palette.LensClear.Alpha(0.16f));

        // Dust motes: little specks that only show inside the light.
        th._motes = new RectTransform[MoteCount];
        th._moteImages = new Image[MoteCount];
        th._moteSeeds = new Vector2[MoteCount];
        for (int i = 0; i < MoteCount; i++)
        {
            var img = UIKit.Image(root, "Mote" + i, Palette.LensClear.Alpha(0f), UIKit.Disc);
            float s = Random.Range(2.5f, 5.5f);
            UIKit.Place(img.rectTransform, Vector2.zero, new Vector2(s, s));
            th._motes[i] = img.rectTransform;
            th._moteImages[i] = img;
            th._moteSeeds[i] = new Vector2(Random.value * 100f, Random.value * 100f);
        }

        // The hand, pivoting at the wrist.
        var raw = UIKit.Raw(root, "Hand");
        raw.texture = handTexture;
        var hrt = raw.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
        hrt.sizeDelta = handRect.size;
        hrt.pivot = new Vector2((wrist.x - handRect.xMin) / handRect.width, (wrist.y - handRect.yMin) / handRect.height);
        hrt.anchoredPosition = wrist;
        th._hand = hrt;

        th._lensGlow = UIKit.Image(root, "LensGlow", Palette.LensClear.Alpha(0f), UIKit.Glow);
        UIKit.Place(th._lensGlow.rectTransform, lensRest, new Vector2(120f, 120f));
        return th;
    }

    private void LateUpdate()
    {
        if (beam == null || layer == null) return;
        float power = Mathf.Clamp01(beam.Power);

        // Where the beam is, in layer units.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, beam.ScreenPosition, UIKit.CanvasCamera(layer), out var target);
        float radius = beam.CurrentRadius * 1080f;

        // Turn the hand toward the beam, a little and smoothly.
        float rest = Mathf.Atan2(aimRest.y - lensRest.y, aimRest.x - lensRest.x) * Mathf.Rad2Deg;
        float now = Mathf.Atan2(target.y - lensRest.y, target.x - lensRest.x) * Mathf.Rad2Deg;
        float want = Mathf.Clamp(Mathf.DeltaAngle(rest, now), -maxTurn, maxTurn);
        _turn = Mathf.Lerp(_turn, want, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f));
        _hand.localRotation = Quaternion.Euler(0f, 0f, _turn);
        Vector2 lens = pivot + (Vector2)(Quaternion.Euler(0f, 0f, _turn) * (lensRest - pivot));

        // The shaft of light and the bulb's glow.
        _cone.color = Palette.LensClear.Alpha(0.08f * power);
        _cone.Set(lens, target, 34f, Mathf.Max(60f, radius * 1.5f));
        _lensGlow.rectTransform.anchoredPosition = lens;
        _lensGlow.color = Palette.LensClear.Alpha(0.55f * power);

        // Dust drifting slowly through the pool, visible only where it is lit.
        float t = Time.unscaledTime;
        for (int i = 0; i < MoteCount; i++)
        {
            var seed = _moteSeeds[i];
            var off = new Vector2(Mathf.PerlinNoise(seed.x, t * 0.05f) - 0.5f, Mathf.PerlinNoise(t * 0.05f, seed.y) - 0.5f) * 2.2f;
            Vector2 p = target + off * radius;
            _motes[i].anchoredPosition = p;
            float d = off.magnitude * 0.5f;                         // 0 centre .. ~0.55 edge
            float inLight = Mathf.Clamp01(1f - d / 0.5f);
            float twinkle = 0.5f + 0.5f * Mathf.Sin(t * (0.8f + seed.x % 1.3f) + seed.y);
            _moteImages[i].color = Palette.LensClear.Alpha(0.55f * inLight * twinkle * power);
        }
    }

    /// <summary>Fades the hand, shaft and dust (used when leaving the title).</summary>
    public void SetAlpha(float a)
    {
        var g = GetComponent<CanvasGroup>();
        if (g == null) g = gameObject.AddComponent<CanvasGroup>();
        g.alpha = a;
    }
}
