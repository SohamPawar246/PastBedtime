using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Act = Bindings.Act;

/// <summary>
/// "Best with a mouse and keyboard": once per launch, after the epilepsy warning and before the title. The game's
/// one big ask is both hands at once, so before anything else it shows where they go: the keyboard (left hand) is
/// Max, the mouse (right hand) is Roshan's torch. Two comic cards, with the player's own controls (Settings >
/// Controls); the mouse's buttons light up one by one with what they do. Any key or click goes on (after a moment
/// to read it), and it goes on by itself if nobody's there.
/// </summary>
public class MouseKeyboardScreen : MonoBehaviour
{
    [SerializeField] private float minSeconds = 1.2f, autoSeconds = 14f;
    private const float TourStep = 1.3f;                   // how long each part of the mouse stays lit

    private CanvasGroup _prompt;
    private readonly System.Collections.Generic.List<(RectTransform rt, CanvasGroup group)> _stamps = new();
    private (Image part, TMP_Text key, RectTransform box)[] _tour;
    private Image[] _parts;
    private float _t;
    private bool _advancing;

    private void Start()
    {
        if (App.MouseKeyboardShown) { Advance(); return; }
        App.MouseKeyboardShown = true;

        UIKit.EnsureEventSystem();
        var canvas = UIKit.Canvas("MouseKeyboardCanvas", 0);
        var root = canvas.transform;
        var bg = UIKit.Image(root, "Night", Palette.Night);
        UIKit.Stretch(bg.rectTransform);

        var title = UIKit.Lettering(root, "Title", "BEST WITH A MOUSE AND KEYBOARD", 80f, Palette.Yellow,
            new Vector2(0f, 400f), new Vector2(1760f, 104f), tilt: -1f);
        Stamped(title.transform.parent as RectTransform);
        var sub = UIKit.Text(root, "Sub", "Both hands at once: one plays Max, the other holds Roshan's torch.",
            UIKit.Body, 33f, Palette.Paper);
        sub.fontStyle = FontStyles.Bold;
        UIKit.Place(sub.rectTransform, new Vector2(0f, 318f), new Vector2(1600f, 50f));
        Stamped(sub.rectTransform);

        Stamped(KeyboardCard(root, new Vector2(-448f, -58f)));
        Stamped(MouseCard(root, new Vector2(448f, -58f)));

        var promptGo = UIKit.Rect(root, "Prompt");
        UIKit.Place(promptGo, new Vector2(0f, -446f), new Vector2(1000f, 50f));
        _prompt = promptGo.gameObject.AddComponent<CanvasGroup>();
        _prompt.alpha = 0f;
        var promptText = UIKit.Text(promptGo, "Text", "CLICK OR PRESS ANY KEY", UIKit.Mono, 26f, Palette.PaperDim);
        UIKit.Stretch(promptText.rectTransform);
        promptText.characterSpacing = 8f;

        StartCoroutine(StampIn());
    }

    // ---- the keyboard: Max, on the left hand ------------------------------------------------------------------

    private RectTransform KeyboardCard(Transform root, Vector2 at)
    {
        const float w = 800f, h = 560f;
        var card = UIKit.Panel(root, "Keyboard", at, new Vector2(w, h), Palette.Paper, shadow: 12f, tilt: -1.2f).transform.parent;
        Plate(card, "THE KEYBOARD IS MAX", new Vector2(-w * 0.5f + 265f, h * 0.5f - 4f), new Vector2(480f, 72f), -3f);
        Hand(card, "LEFT HAND", new Vector2(w * 0.5f - 130f, h * 0.5f - 58f));

        // the left hand's corner of a keyboard, staggered like the real rows; the fighting keys in yellow
        Cap(card, new Vector2(-170f, 128f), 98f, Act.Kick, "KICK", Palette.Yellow);
        Cap(card, new Vector2(-66f, 128f), 98f, Act.Up, "UP", Palette.Paper);
        Cap(card, new Vector2(38f, 128f), 98f, Act.Punch, "PUNCH", Palette.Yellow);
        Cap(card, new Vector2(-144f, 22f), 98f, Act.Left, "LEFT", Palette.Paper);
        Cap(card, new Vector2(-40f, 22f), 98f, Act.Down, "DOWN", Palette.Paper);
        Cap(card, new Vector2(64f, 22f), 98f, Act.Right, "RIGHT", Palette.Paper);
        Cap(card, new Vector2(168f, 22f), 98f, Act.Splash, "SPLASH", Palette.Yellow);
        Cap(card, new Vector2(-150f, -84f), 176f, Act.Dodge, "DODGE", Palette.Paper);
        Cap(card, new Vector2(118f, -84f), 336f, Act.Jump, "JUMP (HOLD = HIGHER)", Palette.Paper);

        var combos = UIKit.Text(card, "Combos",
            $"<b>{Bindings.Name(Act.Punch)} {Bindings.Name(Act.Punch)} {Bindings.Name(Act.Punch)}</b> a 3-hit combo   " +
            $"<b>{Bindings.Name(Act.Up)} + {Bindings.Name(Act.Kick)}</b> launcher",
            UIKit.Body, 23f, Palette.Ink);
        UIKit.Place(combos.rectTransform, new Vector2(0f, -178f), new Vector2(w - 70f, 34f));
        var note = UIKit.Text(card, "Note", "Arrow keys run too.  Change any key: SETTINGS > CONTROLS",
            UIKit.Mono, 20f, Palette.Ink.Alpha(0.6f));
        UIKit.Place(note.rectTransform, new Vector2(0f, -h * 0.5f + 48f), new Vector2(w - 70f, 30f));
        return card as RectTransform;
    }

    private static void Cap(Transform card, Vector2 at, float width, Act act, string what, Color face)
    {
        var cap = UIKit.Panel(card, "Key " + act, at, new Vector2(width, 98f), face, shadow: 5f);
        var key = UIKit.Text(cap.transform, "Key", Bindings.Name(act), UIKit.Display, 42f, Palette.Ink);
        key.textWrappingMode = TextWrappingModes.NoWrap;
        key.enableAutoSizing = true;
        key.fontSizeMin = 18f;
        key.fontSizeMax = 42f;
        UIKit.Place(key.rectTransform, new Vector2(0f, 13f), new Vector2(width - 16f, 52f));
        var label = UIKit.Text(cap.transform, "What", what, UIKit.Body, 17f, Palette.Ink.Alpha(0.8f));
        label.fontStyle = FontStyles.Bold;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        UIKit.Place(label.rectTransform, new Vector2(0f, -29f), new Vector2(width - 8f, 24f));
    }

    // ---- the mouse: Roshan's torch, on the right hand -----------------------------------------------------------

    private RectTransform MouseCard(Transform root, Vector2 at)
    {
        const float w = 800f, h = 560f;
        var card = UIKit.Panel(root, "Mouse", at, new Vector2(w, h), Palette.Paper, shadow: 12f, tilt: 1.2f).transform.parent;
        Plate(card, "THE MOUSE IS THE TORCH", new Vector2(-w * 0.5f + 295f, h * 0.5f - 4f), new Vector2(540f, 72f), 2.5f);
        Hand(card, "RIGHT HAND", new Vector2(w * 0.5f - 130f, h * 0.5f - 58f));

        // the mouse, drawn, with a highlight for each part that lights up in its turn
        Vector2 m = new(0f, -22f);
        var mouse = UIKit.Rect(card, "Mouse");
        UIKit.Place(mouse, m, new Vector2(MouseArt.W, MouseArt.H));
        var body = UIKit.Image(mouse, "Body", Palette.Paper, MouseArt.Body);
        UIKit.Stretch(body.rectTransform);
        var left = Part(mouse, "Left button", MouseArt.LeftButton);
        var right = Part(mouse, "Right button", MouseArt.RightButton);
        var wheel = Part(mouse, "Wheel", MouseArt.Wheel);
        _parts = new[] { left, right, wheel };

        // what each part does, with the player's own controls; a pointer only where the control really is that part
        var follow = Callout(card, new Vector2(-265f, 152f), true, "HOLD " + Bindings.Name(Act.Follow), "the light follows Max");
        var aim = Callout(card, new Vector2(-265f, -60f), true, "MOVE THE MOUSE", "aim the beam");
        var torch = Callout(card, new Vector2(265f, 150f), false, Bindings.Name(Act.Torch), "torch on / off");
        var crank = Callout(card, new Vector2(265f, 46f), false, Bindings.Name(Act.Crank), "twist the crank");
        var lens = Callout(card, new Vector2(265f, -58f), false, Bindings.Name(Act.Lens), "the purple ghost lens");
        var M = Bindings.Kind.Mouse;
        if (Bindings.Uses(Act.Follow, new Bindings.Control(M, 0))) Leader(card, new Vector2(-132f, 152f), m + MouseArt.LeftAt);
        Leader(card, new Vector2(-132f, -60f), m + MouseArt.SideAt);
        if (Bindings.Uses(Act.Torch, new Bindings.Control(M, 1))) Leader(card, new Vector2(132f, 150f), m + MouseArt.RightAt);
        if (Bindings.UsesWheel(Act.Crank)) Leader(card, new Vector2(132f, 46f), m + MouseArt.WheelAt + new Vector2(12f, 10f));
        if (Bindings.Uses(Act.Lens, new Bindings.Control(M, 2))) Leader(card, new Vector2(132f, -58f), m + MouseArt.WheelAt + new Vector2(10f, -20f));

        var pause = UIKit.Text(card, "Pause", "ESC or P: the Bookmark (pause)", UIKit.Mono, 20f, Palette.Ink.Alpha(0.6f));
        UIKit.Place(pause.rectTransform, new Vector2(0f, -h * 0.5f + 48f), new Vector2(w - 70f, 30f));

        _tour = new[]
        {
            (left, follow.key, follow.box),
            (right, torch.key, torch.box),
            (wheel, crank.key, crank.box),
            (wheel, lens.key, lens.box),
            ((Image)null, aim.key, aim.box),
        };
        return card as RectTransform;
    }

    private static Image Part(RectTransform mouse, string name, Sprite mask)
    {
        var part = UIKit.Image(mouse, name, Palette.Yellow.Alpha(0f), mask);
        UIKit.Stretch(part.rectTransform);
        return part;
    }

    private static (TMP_Text key, RectTransform box) Callout(Transform card, Vector2 at, bool leftOfMouse, string key, string what)
    {
        const float w = 250f;
        var box = UIKit.Rect(card, "Callout " + key);
        UIKit.Place(box, at, new Vector2(w, 84f));
        var align = leftOfMouse ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        var k = UIKit.Text(box, "Key", key, UIKit.Display, 34f, Palette.Ink, align);
        k.textWrappingMode = TextWrappingModes.NoWrap;
        k.enableAutoSizing = true;
        k.fontSizeMin = 18f;
        k.fontSizeMax = 34f;
        UIKit.Place(k.rectTransform, new Vector2(0f, 17f), new Vector2(w, 42f));
        var d = UIKit.Text(box, "What", what, UIKit.Body, 22f, Palette.Ink.Alpha(0.8f), align);
        d.fontStyle = FontStyles.Bold;
        UIKit.Place(d.rectTransform, new Vector2(0f, -21f), new Vector2(w, 34f));
        box.pivot = new Vector2(leftOfMouse ? 1f : 0f, 0.5f);                  // it grows away from the mouse when lit
        box.anchoredPosition += new Vector2(leftOfMouse ? w * 0.5f : -w * 0.5f, 0f);
        return (k, box);
    }

    /// <summary>A thin ink pointer from a callout to a part of the mouse, with a dot on the part.</summary>
    private static void Leader(Transform card, Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        var line = UIKit.Image(card, "Leader", Palette.Ink, UIKit.Solid);
        var rt = line.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(d.magnitude, 3f);
        rt.anchoredPosition = from;
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        var dot = UIKit.Image(card, "Dot", Palette.Ink, UIKit.Disc);
        UIKit.Place(dot.rectTransform, to, new Vector2(13f, 13f));
    }

    // ---- shared pieces ----------------------------------------------------------------------------------------

    private static void Plate(Transform card, string text, Vector2 at, Vector2 size, float tilt)
    {
        var plate = UIKit.Panel(card, "TitlePlate", at, size, Palette.Yellow, shadow: 6f, tilt: tilt);
        var t = UIKit.Text(plate.transform, "Title", text, UIKit.Display, 42f, Palette.Ink);
        t.characterSpacing = 4f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true;
        t.fontSizeMin = 26f;
        t.fontSizeMax = 42f;
        UIKit.Stretch(t.rectTransform, 12f);
    }

    private static void Hand(Transform card, string text, Vector2 at)
    {
        var t = UIKit.Text(card, "Hand", text, UIKit.Mono, 21f, Palette.Ink.Alpha(0.5f));
        t.characterSpacing = 4f;
        UIKit.Place(t.rectTransform, at, new Vector2(220f, 30f));
    }

    private void Stamped(RectTransform rt)
    {
        var group = rt.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        _stamps.Add((rt, group));
    }

    /// <summary>Each piece lands like a rubber stamp, one after another.</summary>
    private IEnumerator StampIn()
    {
        foreach (var (rt, group) in _stamps)
        {
            StartCoroutine(Stamp(rt, group));
            yield return new WaitForSecondsRealtime(0.12f);
        }
    }

    private static IEnumerator Stamp(RectTransform rt, CanvasGroup group)
    {
        Vector3 rest = rt.localScale;
        for (float t = 0f; t < 0.24f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.24f;
            float s = k < 0.65f ? Mathf.Lerp(1.18f, 0.97f, k / 0.65f) : Mathf.Lerp(0.97f, 1f, (k - 0.65f) / 0.35f);
            rt.localScale = rest * s;
            group.alpha = Mathf.Clamp01(k * 3f);
            yield return null;
        }
        rt.localScale = rest;
        group.alpha = 1f;
    }

    private void Update()
    {
        if (_advancing || _prompt == null) return;
        _t += Time.unscaledDeltaTime;

        // the mouse's tour: one part lit at a time, and what it does in red
        if (_tour != null && _t > 0.8f)
        {
            int on = (int)((_t - 0.8f) / TourStep) % _tour.Length;
            foreach (var p in _parts)
            {
                bool lit = _tour[on].part == p;
                p.color = Palette.Yellow.Alpha(Mathf.MoveTowards(p.color.a, lit ? 0.95f : 0f, Time.unscaledDeltaTime * 6f));
            }
            for (int i = 0; i < _tour.Length; i++)
            {
                bool lit = i == on;
                _tour[i].key.color = lit ? Palette.HeroRed : Palette.Ink;
                float s = Mathf.MoveTowards(_tour[i].box.localScale.x, lit ? 1.08f : 1f, Time.unscaledDeltaTime * 1.5f);
                _tour[i].box.localScale = new Vector3(s, s, 1f);
            }
        }

        bool ready = _t >= minSeconds;
        if (ready) _prompt.alpha = Mathf.MoveTowards(_prompt.alpha, 0.65f + 0.35f * Mathf.Sin((_t - minSeconds) * 3f), Time.unscaledDeltaTime * 2f);
        if ((ready && ShellInput.AnyPress()) || _t >= autoSeconds) Advance();
    }

    private void Advance()
    {
        if (_advancing) return;
        _advancing = true;
        App.MouseKeyboardShown = true;
        App.I.Flow.Load(Scenes.Title);
    }
}

/// <summary>A computer mouse drawn in code, comic style (paper, ink outline, the buttons' seams, the wheel), and
/// the masks that light up its parts. 200 x 320 design units, centre origin.</summary>
internal static class MouseArt
{
    public const int W = 200, H = 320;
    private const float A = 94f, B = 152f, Pow = 2.5f;              // the body: a rounded superellipse
    private const float SplitY = 30f;                               // the buttons are above this seam
    private const float Outline = 6f, Seam = 4f;
    private static readonly Vector2 WheelHalf = new(13f, 30f);
    public static readonly Vector2 WheelAt = new(0f, 84f);
    public static readonly Vector2 LeftAt = new(-50f, 92f), RightAt = new(50f, 92f), SideAt = new(-90f, -46f);

    private enum Shade { Body, Left, Right, Wheel }
    private static Sprite _body, _left, _right, _wheel;
    public static Sprite Body => _body != null ? _body : _body = Make(Shade.Body);
    public static Sprite LeftButton => _left != null ? _left : _left = Make(Shade.Left);
    public static Sprite RightButton => _right != null ? _right : _right = Make(Shade.Right);
    public static Sprite Wheel => _wheel != null ? _wheel : _wheel = Make(Shade.Wheel);

    private static Sprite Make(Shade what)
    {
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, true)
        {
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[W * H];
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
                px[j * W + i] = Pixel(what, i + 0.5f - W * 0.5f, j + 0.5f - H * 0.5f);
        tex.SetPixels32(px);
        tex.Apply(true, true);
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>How far inside the body's edge a point is (px, negative outside), measured along its ray from the centre.</summary>
    private static float Inside(float x, float y)
    {
        float q = Mathf.Pow(Mathf.Pow(Mathf.Abs(x) / A, Pow) + Mathf.Pow(Mathf.Abs(y) / B, Pow), 1f / Pow);
        if (q < 1e-4f) return A;
        return (1f - q) * Mathf.Sqrt(x * x + y * y) / q;
    }

    /// <summary>Signed distance to the wheel's rounded box (negative inside).</summary>
    private static float WheelDist(float x, float y)
    {
        const float r = 11f;
        float qx = Mathf.Abs(x - WheelAt.x) - (WheelHalf.x - r), qy = Mathf.Abs(y - WheelAt.y) - (WheelHalf.y - r);
        return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    private static float Band(float distance, float width) => Mathf.Clamp01(width * 0.5f - distance + 0.5f);

    private static Color32 Pixel(Shade what, float x, float y)
    {
        float d = Inside(x, y), wd = WheelDist(x, y);
        float cover = Mathf.Clamp01(d + 0.5f);
        if (cover <= 0f) return new Color32(255, 255, 255, 0);
        switch (what)
        {
            case Shade.Body:
            {
                // ink: the outline, the seam under the buttons, the split between them, the wheel's rim and ridges
                float ink = Mathf.Clamp01(Outline - d + 0.5f);
                ink = Mathf.Max(ink, Band(Mathf.Abs(y - SplitY), Seam));
                if (y > SplitY && wd > 0f) ink = Mathf.Max(ink, Band(Mathf.Abs(x), Seam));
                ink = Mathf.Max(ink, Band(Mathf.Abs(wd + 2.5f), 5f));
                if (wd < -5f && Mathf.Repeat(y - WheelAt.y, 9f) < 2.5f) ink = Mathf.Max(ink, 0.7f);
                // a flat comic shade down the right-hand side and along the bottom, so it reads as round
                float paper = x * 0.8f - y * 0.35f > 34f && d < 24f ? 0.86f : 1f;
                byte v = (byte)(Mathf.Lerp(255f * paper, 22f, ink));
                return new Color32(v, v, v, (byte)(255f * cover));
            }
            case Shade.Left:
            case Shade.Right:
            {
                float side = what == Shade.Left ? -x : x;
                float a = Mathf.Clamp01(d - (Outline + 2f)) * Mathf.Clamp01(y - (SplitY + Seam)) *
                          Mathf.Clamp01(side - Seam) * Mathf.Clamp01(wd - 2f);
                return new Color32(255, 255, 255, (byte)(255f * a));
            }
            default:
                return new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01(-wd - 5f)));
        }
    }
}
