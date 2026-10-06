using System.Collections;
using UnityEngine;

/// <summary>
/// The page camera (GDD sections 3 and 14): orthographic, it frames one tier of the comic and
/// renders it into a texture that the page view shows (and, in the bedroom, the open comic).
/// It follows Max sideways with a 4-unit dead zone; crossing to the next tier is a reading
/// sweep, down and back to the left edge in 0.6 s. Heavy hits shake only this camera, and the
/// Splash Page punches it in on Max for a beat (<see cref="Punch"/>; neither with screen shake off).
/// </summary>
public class PageCamera : MonoBehaviour
{
    public static PageCamera I { get; private set; }

    public const float ViewWidth = 24f, DeadZone = 4f, Sweep = 0.6f;
    /// <summary>The view sits this much above the tier's middle, so the printed HUD (hearts, stars)
    /// fills the margin above the panels instead of covering their captions.</summary>
    public const float HudRoom = 0.55f;
    public const int TextureWidth = 1600, TextureHeight = 900;

    public Camera Cam { get; private set; }
    public RenderTexture Texture { get; private set; }
    public bool Sweeping { get; private set; }

    private float _minX, _maxX, _y;
    private float _x;
    private float _shake;
    private Vector2 _shakeOffset;
    private float _baseSize;
    // the punch-in: where, how far (the view's size times this), and its timing
    private Vector2 _punchAt;
    private float _punchZoom = 1f, _punchIn, _punchHold, _punchOut, _punchT = -1f, _punch;

    private void Awake()
    {
        I = this;
        Texture = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32)
        {
            name = "PageTexture", antiAliasing = 4, useMipMap = false,
        };
        Cam = gameObject.AddComponent<Camera>();
        Cam.orthographic = true;
        Cam.orthographicSize = _baseSize = ViewWidth * TextureHeight / TextureWidth / 2f;
        Cam.nearClipPlane = 0.1f;
        Cam.farClipPlane = 80f;
        Cam.clearFlags = CameraClearFlags.SolidColor;
        Cam.backgroundColor = Palette.ComicPaper * 0.18f;
        Cam.cullingMask = 1 << LayerMask.NameToLayer("Comic");
        Cam.targetTexture = Texture;
        Cam.allowMSAA = true;
        transform.rotation = Quaternion.Euler(4f, 0f, 0f);          // a hair above the lane: rooftops show a top edge
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
        if (Texture != null) Texture.Release();
    }

    public float HalfWidth => ViewWidth / 2f;

    public void Frame(TierLayout tier, float x, bool snap)
    {
        _minX = tier.minX - PageDef.Gutter;
        _maxX = tier.maxX + PageDef.Gutter;
        _y = tier.CentreY + HudRoom;
        _x = ClampX(x);
        if (snap) Apply();
    }

    private float ClampX(float x)
    {
        float lo = _minX + HalfWidth, hi = _maxX - HalfWidth;
        return lo > hi ? (_minX + _maxX) / 2f : Mathf.Clamp(x, lo, hi);
    }

    /// <summary>The reading sweep to the next tier: pans down and back to the left edge.</summary>
    public IEnumerator SweepTo(TierLayout tier, float x)
    {
        Sweeping = true;
        float fromX = _x, fromY = _y;
        _minX = tier.minX - PageDef.Gutter;
        _maxX = tier.maxX + PageDef.Gutter;
        float toX = ClampX(x), toY = tier.CentreY + HudRoom;
        for (float t = 0f; t < Sweep; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / Sweep);
            _x = Mathf.Lerp(fromX, toX, k);
            _y = Mathf.Lerp(fromY, toY, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (Sweep * 0.7f))));
            Apply();
            yield return null;
        }
        _x = toX;
        _y = toY;
        Apply();
        Sweeping = false;
    }

    public static void Impulse(float strength)
    {
        if (I == null || !Settings.ScreenShake) return;
        I._shake = Mathf.Min(1f, Mathf.Max(I._shake, strength));
    }

    /// <summary>A punch-in: the view closes in on `at` to `zoom` of its size (0.8 = 20% closer) over `inTime`,
    /// holds, and eases back out. Real time, held by the Bookmark.</summary>
    public static void Punch(Vector2 at, float zoom, float inTime, float hold, float outTime)
    {
        if (I == null || !Settings.ScreenShake) return;
        I._punchAt = at;
        I._punchZoom = zoom;
        I._punchIn = Mathf.Max(0.01f, inTime);
        I._punchHold = hold;
        I._punchOut = Mathf.Max(0.01f, outTime);
        I._punchT = 0f;
    }

    /// <summary>How far in the punch is right now (0 = none, 1 = all the way).</summary>
    public float Punched => _punch;

    private void LateUpdate()
    {
        var hero = HeroController.I;
        if (!Sweeping && hero != null)
        {
            float hx = hero.transform.position.x;
            float half = DeadZone / 2f;
            if (hx > _x + half) _x = hx - half;
            if (hx < _x - half) _x = hx + half;
            _x = ClampX(_x);
        }
        if (_shake > 0f)
        {
            _shake = Mathf.Max(0f, _shake - Time.unscaledDeltaTime * 3f);
            float a = _shake * _shake * 0.35f;
            _shakeOffset = new Vector2(Random.Range(-a, a), Random.Range(-a, a));
        }
        else _shakeOffset = Vector2.zero;
        if (_punchT >= 0f)
        {
            _punchT += BookmarkPause.UnpausedDelta;
            float t = _punchT;
            _punch = t < _punchIn ? Mathf.SmoothStep(0f, 1f, t / _punchIn)
                   : t < _punchIn + _punchHold ? 1f
                   : 1f - Mathf.SmoothStep(0f, 1f, (t - _punchIn - _punchHold) / _punchOut);
            if (t >= _punchIn + _punchHold + _punchOut) { _punchT = -1f; _punch = 0f; }
        }
        if (!Sweeping) Apply();
    }

    private void Apply()
    {
        // pull back along the view direction so the lane sits mid-frame despite the slight pitch
        Vector2 at = new Vector2(_x, _y);
        if (_punch > 0f) at = Vector2.Lerp(at, _punchAt, _punch * 0.55f);       // in on the action, not all the way
        Vector3 focus = new Vector3(at.x + _shakeOffset.x, at.y + _shakeOffset.y, 0f);
        transform.position = focus - transform.forward * 30f;
        Cam.orthographicSize = _baseSize * Mathf.Lerp(1f, _punchZoom, _punch);
    }

    /// <summary>Viewport point (0..1) on the page texture to a point on the z = 0 lane.</summary>
    public Vector2 ViewportToLane(Vector2 uv)
    {
        var ray = Cam.ViewportPointToRay(new Vector3(uv.x, uv.y, 0f));
        if (Mathf.Abs(ray.direction.z) < 1e-5f) return ray.origin;
        float t = -ray.origin.z / ray.direction.z;
        return ray.origin + ray.direction * t;
    }

    public Vector2 LaneToViewport(Vector2 p) => Cam.WorldToViewportPoint(new Vector3(p.x, p.y, 0f));
}
