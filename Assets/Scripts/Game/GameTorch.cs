using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Roshan's hand and the wind-up torch, bottom-right (GDD section 13): the torch is the HUD.
/// The same Blender render as the title's hand, in layers: the crank turns as you scroll (the bulb
/// flickering a little with it, like a real dynamo torch), the needle on the charge dial reads the
/// charge and swings into the red as you overwind, the lens glows the current lens's colour and
/// its window on the wheel lights up. A soft shaft of light runs from the lens to where the beam
/// lands on the comic. Turns a few degrees toward the beam, like the title's hand.
/// </summary>
public class GameTorch : MonoBehaviour
{
    public static GameTorch I { get; private set; }

    // the hand in the 1920 x 1080 front layer: where the title draws it, and where the game holds it
    private static readonly Rect HandRect = Rect.MinMaxRect(416f, -702f, 1016f, -254f);
    private static readonly Vector2 Wrist = new(852f, -548f);
    private static readonly Vector2 GameOffset = new(96f, -78f);
    private const float GameScale = 1f;
    // points in the 600 x 448 render (y down), projected from Blender (torch_layer_points.json)
    private static readonly Vector2 WristPx = new(436f, 294f);
    private static readonly Vector2 LensPx = new(29f, 68f), AimPx = new(-383f, -25f);   // the aim: the title's comic centre
    private static readonly Vector2 DialPx = new(188.63f, 68.49f), DialXPx = new(183.99f, 58.56f), DialYPx = new(175.39f, 65.5f);
    private static readonly Vector2[] WindowPx = { new(33.45f, 126.04f), new(16.97f, 67.98f), new(45.33f, 16.08f), new(64.28f, 73.01f) };
    private static readonly Rect CrankPx = new(50f, 85f, 172f, 128f);
    private static readonly Vector2 SwitchPx = new(232f, 152f);
    private const int CrankFrames = 16;
    private const float MaxTurn = 9f;

    private RectTransform _hand;
    private RawImage _crank;
    private Texture2D[] _crankTex;
    private RectTransform _needle;
    private Image _needleImg;
    private Image _lensGlow, _window;
    private LightCone _cone;
    private TextMeshProUGUI _hint, _lensName;
    private float _turn, _crankAngle, _crankTarget, _flicker, _nameFor;
    private Lens _lastLens;

    public static GameTorch Build(RectTransform front)
    {
        var root = UIKit.Rect(front, "Torch");
        UIKit.Stretch(root);
        var gt = root.gameObject.AddComponent<GameTorch>();
        gt.BuildUI(root);
        return gt;
    }

    private void Awake() => I = this;
    private void OnDestroy() { if (I == this) I = null; }

    private static Vector2 Local(Vector2 px) => new(px.x - WristPx.x, WristPx.y - px.y);

    private void BuildUI(RectTransform root)
    {
        _cone = LightCone.Create(root, "LightShaft", Palette.LensClear.Alpha(0f));

        var hand = UIKit.Raw(root, "Hand");
        hand.texture = Resources.Load<Texture2D>("Room/torch_base");
        _hand = hand.rectTransform;
        _hand.anchorMin = _hand.anchorMax = new Vector2(0.5f, 0.5f);
        _hand.sizeDelta = HandRect.size;
        _hand.pivot = new Vector2(WristPx.x / HandRect.width, 1f - WristPx.y / HandRect.height);
        _hand.anchoredPosition = Wrist;
        hand.raycastTarget = false;

        _crankTex = new Texture2D[CrankFrames];
        for (int i = 0; i < CrankFrames; i++) _crankTex[i] = Resources.Load<Texture2D>($"Room/Crank/crank_{i:00}");
        _crank = UIKit.Raw(_hand, "Crank");
        _crank.texture = _crankTex[0];
        _crank.raycastTarget = false;
        PlaceOnHand(_crank.rectTransform, CrankPx.center, CrankPx.size);

        // the charge dial's needle: a hair of black ink pivoting on the dial's centre
        var needle = UIKit.Image(_hand, "Needle", Palette.Ink);
        _needleImg = needle;
        _needle = needle.rectTransform;
        PlaceOnHand(_needle, DialPx, new Vector2(2.2f, 12f));
        _needle.pivot = new Vector2(0.5f, 0f);
        var hub = UIKit.Image(_hand, "NeedleHub", Palette.HeroRed, UIKit.Disc);
        PlaceOnHand(hub.rectTransform, DialPx, new Vector2(4.5f, 4f));

        _window = UIKit.Image(_hand, "LensWindow", Palette.LensClear.Alpha(0f), UIKit.Glow);
        PlaceOnHand(_window.rectTransform, WindowPx[0], new Vector2(34f, 34f));
        _lensGlow = UIKit.Image(_hand, "LensGlow", Palette.LensClear.Alpha(0f), UIKit.Glow);
        PlaceOnHand(_lensGlow.rectTransform, LensPx, new Vector2(130f, 130f));

        _hint = UIKit.Text(root, "TwistHint", "", UIKit.Display, 34f, Palette.Yellow);
        _hint.rectTransform.anchorMin = _hint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _hint.rectTransform.sizeDelta = new Vector2(320f, 44f);
        _hint.raycastTarget = false;
        _lensName = UIKit.Text(root, "LensName", "", UIKit.Display, 30f, Palette.Paper);
        _lensName.rectTransform.anchorMin = _lensName.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _lensName.rectTransform.sizeDelta = new Vector2(260f, 40f);
        _lensName.raycastTarget = false;

        var torch = TorchController.I;
        if (torch != null)
        {
            torch.Cranked += n => _crankTarget += 75f * n;
            torch.Toggled += on => Pop(on ? "CLICK!" : "CLICK!", SwitchPx, Palette.Yellow);
            _lastLens = torch.Lenses.Current;
        }
    }

    private static void PlaceOnHand(RectTransform rt, Vector2 px, Vector2 size)
    {
        var hand = (RectTransform)rt.parent;
        rt.anchorMin = rt.anchorMax = hand.pivot;                    // positions measured from the wrist
        rt.sizeDelta = size;
        rt.anchoredPosition = Local(px);
    }

    /// <summary>A front-layer point for a point on the hand render (follows the hand's turn and move).</summary>
    private Vector2 HandToFront(Vector2 px)
    {
        var parent = (RectTransform)_hand.parent;
        Vector3 w = _hand.TransformPoint(Local(px));
        return parent.InverseTransformPoint(w);
    }

    private void LateUpdate()
    {
        var view = PageView.I;
        var field = LightField.I;
        var torch = TorchController.I;
        if (view == null || field == null || torch == null) return;
        float dt = Time.unscaledDeltaTime;
        var parent = (RectTransform)_hand.parent;

        // where the hand is: the title's spot, sliding down to the corner as the view zooms in
        float k = view.Framing;
        _hand.anchoredPosition = Wrist + GameOffset * k;
        float s = Mathf.Lerp(1f, GameScale, k);

        // turn a little toward the beam (in the title's framing, the title's beam on the closed comic)
        Vector2 onPage = parent.InverseTransformPoint(view.LaneToWorld(field.BeamCentre));
        Vector2 target = Vector2.Lerp(PageView.ComicCentre, onPage, k);
        Vector2 lensRest = Wrist + GameOffset * k + Local(LensPx) * s;
        Vector2 aimRest = Wrist + GameOffset * k + Local(AimPx) * s;
        float rest = Mathf.Atan2(aimRest.y - lensRest.y, aimRest.x - lensRest.x) * Mathf.Rad2Deg;
        float now = Mathf.Atan2(target.y - lensRest.y, target.x - lensRest.x) * Mathf.Rad2Deg;
        float want = Mathf.Clamp(Mathf.DeltaAngle(rest, now), -MaxTurn, MaxTurn) * k;
        _turn = Mathf.Lerp(_turn, want, 1f - Mathf.Exp(-dt * 10f));
        _hand.localRotation = Quaternion.Euler(0f, 0f, _turn);
        _hand.localScale = new Vector3(s, s, 1f);

        // the crank: each scroll notch winds it on a quarter-ish turn
        _crankAngle = Mathf.MoveTowards(_crankAngle, _crankTarget, dt * 900f);
        int frame = ((int)Mathf.Round(_crankAngle / (360f / CrankFrames)) % CrankFrames + CrankFrames) % CrankFrames;
        _crank.texture = _crankTex[frame];
        bool cranking = Mathf.Abs(_crankTarget - _crankAngle) > 0.5f || torch.Charge.SinceTwist < 0.15f;

        // a dynamo bulb flickers as it's wound: brief dips in the light, nothing dramatic
        _flicker = cranking ? 0.82f + 0.18f * Mathf.PerlinNoise(Time.unscaledTime * 32f, 0.37f) : Mathf.MoveTowards(_flicker, 1f, dt * 4f);
        torch.CrankFlicker = Settings.ReduceFlashing ? 1f : _flicker;

        // the charge dial: left (empty) round over the top to right (full); overwinding swings it into the red
        var c = torch.Charge;
        float phi = 90f - 180f * Mathf.Clamp01(c.Share);
        if (c.Overwind > 0) phi -= 28f * c.Overwind / Mathf.Max(1, c.OverwindBuffer);
        if (c.Dead) phi = 90f;
        float r = phi * Mathf.Deg2Rad;
        Vector2 ax = Local(DialXPx) - Local(DialPx), ay = Local(DialYPx) - Local(DialPx);
        Vector2 dir = ax * Mathf.Cos(r) + ay * Mathf.Sin(r);
        _needle.sizeDelta = new Vector2(2.2f, dir.magnitude * 1.05f);
        _needle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        _needleImg.color = c.Overwind > 0 ? Palette.HeroRed : Palette.Ink;

        // the lens: its colour, its window lit on the wheel, the bulb's glow and the shaft of light
        Lens lens = torch.Lenses.Current;
        Color lc = lens switch { Lens.Green => Palette.LensGreen, Lens.Red => Palette.LensRed, Lens.Ghost => Palette.LensGhost, _ => Palette.LensClear };
        float power = field.BeamLive ? field.Brightness : 0f;
        if (field.FlareTime > 0f) power = 1.6f;
        _lensGlow.color = lc.Alpha(Mathf.Clamp01(0.6f * power));
        _window.rectTransform.anchoredPosition = Local(WindowPx[(int)lens]);
        _window.color = lc.Alpha(0.85f);
        Vector2 lensAt = HandToFront(LensPx);
        if (k < 1f) power = Mathf.Lerp(1f, power, k);                 // the title's torch is simply on
        _cone.color = lc.Alpha(0.075f * Mathf.Clamp01(power));
        float beamPx = (parent.InverseTransformPoint(view.LaneToWorld(field.BeamCentre + Vector2.right * field.BeamRadius)) - (Vector3)onPage).magnitude;
        _cone.Set(lensAt, target, Mathf.Lerp(34f, 30f * s, k), Mathf.Lerp(680f, Mathf.Max(40f, beamPx * 2f), k));

        if (lens != _lastLens)
        {
            _lastLens = lens;
            _nameFor = 1.2f;
            _lensName.text = LensWheel.Name(lens);
            _lensName.color = lc;
        }
        _nameFor -= dt;
        _lensName.alpha = Mathf.Clamp01(_nameFor / 0.3f) * k;
        _lensName.rectTransform.anchoredPosition = lensAt + new Vector2(-40f, 78f);

        // the hint by the crank: wind me when low, the ratchet when overwound, the bulb cooling
        bool low = c.Share < 0.25f && !c.Dead;
        string hint = c.Dead ? "COOLING..." : c.Overwind > 0 ? "CLICK-CLICK!" : low && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f ? "TWIST! (SCROLL)" : "";
        _hint.text = hint;
        _hint.color = c.Overwind > 0 || c.Dead ? Palette.HeroRed : Palette.Yellow;
        _hint.alpha = k;
        _hint.rectTransform.anchoredPosition = HandToFront(LensPx) + new Vector2(-40f, 120f);   // above the torch head
    }

    private void Pop(string word, Vector2 px, Color color) => StartCoroutine(PopRoutine(word, px, color));

    private System.Collections.IEnumerator PopRoutine(string word, Vector2 px, Color color)
    {
        var parent = (RectTransform)_hand.parent;
        var t = UIKit.Text(parent, "Pop", word, UIKit.Display, 40f, color);
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        t.rectTransform.sizeDelta = new Vector2(200f, 50f);
        t.rectTransform.anchoredPosition = HandToFront(px) + new Vector2(-40f, 50f);
        t.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-12f, 12f));
        t.raycastTarget = false;
        for (float a = 0f; a < 0.5f; a += Time.unscaledDeltaTime)
        {
            t.alpha = 1f - a / 0.5f;
            t.rectTransform.localScale = Vector3.one * (1f + a * 0.4f);
            yield return null;
        }
        Destroy(t.gameObject);
    }
}
