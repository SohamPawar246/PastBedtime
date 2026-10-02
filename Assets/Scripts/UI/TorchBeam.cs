using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The menu's torch: a full-screen darkness with one warm beam in it. The beam
/// follows the mouse; when the keyboard or a gamepad is in charge it can glide
/// to whatever is selected, and otherwise rests on a "home" point (the comic).
/// A small hand tremor keeps it alive. Real-world light has a soft edge; the
/// halftone edge is reserved for light inside the comic.
/// </summary>
public class TorchBeam : MonoBehaviour
{
    public float radius = 0.27f;             // overlay heights
    public float softness = 0.11f;
    public float darkness = 0.95f;           // linear-space blend: 0.95 leaves the room at roughly 20% brightness
    public Color darkColor = Palette.Night;
    public Color litColor = Palette.LensClear;
    public float litAlpha = 0.025f;
    [Range(0f, 1f)] public float halftone = 0f; // 0 = soft real-world falloff, 1 = comic dot edge
    public RectTransform home;               // where the beam rests with no pointer and no selection
    public bool followSelection = true;
    public bool followPointer = true;        // false: the beam ignores the mouse (e.g. during the opening)

    private RawImage _overlay;
    private Material _mat;
    private Vector2 _pos, _vel;              // screen pixels
    private Vector2 _lastMouse;
    private float _mouseActiveUntil;
    private float _power;                    // 0 = off, 1 = on (animated)
    private float _pulse;                    // brief radius kick on selection change
    private GameObject _lastSelected;
    private readonly Vector3[] _corners = new Vector3[4];

    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int SoftId = Shader.PropertyToID("_Softness");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int HeightId = Shader.PropertyToID("_HeightPx");
    private static readonly int DotId = Shader.PropertyToID("_DotSize");
    private static readonly int DarkId = Shader.PropertyToID("_DarkColor");
    private static readonly int LitId = Shader.PropertyToID("_LitColor");
    private static readonly int HalftoneId = Shader.PropertyToID("_Halftone");

    /// <summary>Where the beam is now, in screen pixels (for handing over to SceneFlow).</summary>
    public Vector2 ScreenPosition => _pos;
    /// <summary>The beam's current radius in overlay heights.</summary>
    public float CurrentRadius => radius * _power;
    /// <summary>How switched-on the torch is (0..1, animated by SwitchOn).</summary>
    public float Power => _power;
    /// <summary>The overlay's size in screen pixels (it can be larger than the screen).</summary>
    public Vector2 OverlayPixels { get; private set; } = new Vector2(1920f, 1080f);

    /// <summary>The beam's current radius in screen heights (what SceneFlow expects).</summary>
    public float ScreenRadius => CurrentRadius * OverlayPixels.y / Mathf.Max(1, Screen.height);

    /// <summary>Creates the overlay as the next sibling under <paramref name="parent"/>.
    /// With <paramref name="darkTexture"/>, the dark side shows that picture instead of a flat colour.</summary>
    public static TorchBeam Create(Transform parent, RectTransform home, Texture darkTexture = null)
    {
        var raw = UIKit.Raw(parent, "TorchBeam");
        raw.texture = darkTexture;
        UIKit.Stretch(raw.rectTransform);
        var beam = raw.gameObject.AddComponent<TorchBeam>();
        beam._overlay = raw;
        beam.home = home;
        beam._mat = UIKit.NewLightMaskMaterial();
        raw.material = beam._mat;
        if (beam._mat == null) raw.color = Palette.Night.Alpha(0.6f); // shader missing: plain dim
        return beam;
    }

    private void Start()
    {
        _pos = home != null ? UIKit.ScreenCenter(home) : new Vector2(Screen.width, Screen.height) * 0.5f;
        var m = ShellInput.PointerPosition();
        if (m.HasValue) _lastMouse = m.Value;
        Apply();
    }

    /// <summary>The torch comes on: a bulb warming up with a little sputter (skipped with
    /// Reduce Flashing), a soft over-bright pop, then steady. Silent: it plays right as
    /// a scene appears, and scene changes stay quiet.</summary>
    public IEnumerator SwitchOn(float delay = 0.35f)
    {
        _power = 0f;
        yield return new WaitForSecondsRealtime(delay);
        if (!Settings.ReduceFlashing)
        {
            foreach (var (p, s) in new[] { (0.35f, 0.05f), (0.08f, 0.06f), (0.55f, 0.07f), (0.2f, 0.05f) })
            {
                _power = p;
                yield return new WaitForSecondsRealtime(s);
            }
        }
        float pop = Settings.ReduceFlashing ? 1.0f : 1.12f;
        for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.22f;
            _power = Mathf.Lerp(0f, pop, Mathf.Sin(k * Mathf.PI * 0.5f));
            yield return null;
        }
        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
        {
            _power = Mathf.Lerp(pop, 1f, t / 0.18f);
            yield return null;
        }
        _power = 1f;
    }

    /// <summary>Flood the room (used as the beam "reads" the chosen option).</summary>
    public IEnumerator Widen(float toRadius, float seconds)
    {
        float from = radius;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            radius = Mathf.Lerp(from, toRadius, t / seconds);
            yield return null;
        }
        radius = toRadius;
    }

    public void SetPowerInstant(float p) => _power = p;

    /// <summary>Eases the torch's power to <paramref name="target"/> (0 = off).</summary>
    public IEnumerator PowerTo(float target, float seconds)
    {
        float from = _power;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            _power = Mathf.Lerp(from, target, t / seconds);
            yield return null;
        }
        _power = target;
    }

    private void Update()
    {
        var mouse = ShellInput.PointerPosition();
        if (mouse.HasValue && (mouse.Value - _lastMouse).sqrMagnitude > 9f)
        {
            _lastMouse = mouse.Value;
            _mouseActiveUntil = Time.unscaledTime + 2.5f;
        }

        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != _lastSelected)
        {
            _lastSelected = selected;
            if (selected != null) _pulse = 1f;
        }

        Vector2 target;
        if (followPointer && mouse.HasValue && Time.unscaledTime < _mouseActiveUntil) target = mouse.Value;
        else if (followSelection && selected != null && selected.transform is RectTransform srt) target = UIKit.ScreenCenter(srt);
        else if (home != null) target = UIKit.ScreenCenter(home);
        else target = new Vector2(Screen.width, Screen.height) * 0.5f;

        // SmoothDamp divides by dt when it clamps an overshoot, so a zero-length frame
        // would poison the position with NaN for good. Skip those frames.
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f) _pos = Vector2.SmoothDamp(_pos, target, ref _vel, 0.085f, 1e5f, dt);
        if (float.IsNaN(_pos.x) || float.IsNaN(_pos.y)) { _pos = target; _vel = Vector2.zero; }
        _pulse = Mathf.MoveTowards(_pulse, 0f, dt * 5f);
        Apply();
    }

    private void Apply()
    {
        if (_mat == null) return;
        var rt = _overlay.rectTransform;
        // Real pixel size, including any parent scaling (the overlay can cover more than the screen).
        rt.GetWorldCorners(_corners);
        var cam = UIKit.CanvasCamera(rt);
        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, _corners[2]);
        float w = Mathf.Max(1f, Mathf.Abs(b.x - a.x)), h = Mathf.Max(1f, Mathf.Abs(b.y - a.y));
        OverlayPixels = new Vector2(w, h);

        // Hand tremor: a few pixels of slow noise, and a breath in the radius.
        float time = Time.unscaledTime;
        var tremor = new Vector2(Mathf.PerlinNoise(time * 0.7f, 0.3f) - 0.5f,
                                 Mathf.PerlinNoise(0.6f, time * 0.7f) - 0.5f) * (h * 0.012f);
        Vector2 uv = UIKit.ScreenToUV(rt, _pos + tremor);

        float r = radius * (1f + 0.012f * Mathf.Sin(time * 1.7f) + 0.05f * _pulse * _pulse) * _power;
        _mat.SetVector(CenterId, uv);
        _mat.SetFloat(RadiusId, r <= 0.0001f ? -softness : r);
        _mat.SetFloat(SoftId, softness);
        _mat.SetFloat(AspectId, w / h);
        _mat.SetFloat(HeightId, h);
        _mat.SetFloat(DotId, 11f * h / 1080f);
        _mat.SetColor(DarkId, darkColor.Alpha(darkness));
        _mat.SetColor(LitId, litColor.Alpha(litAlpha * Mathf.Clamp01(_power)));
        _mat.SetFloat(HalftoneId, halftone);
    }
}
