using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The game's view (GDD sections 3, 13 and 14): Roshan's bedroom, the same render as the title,
/// with Max Voltage #1 lying open on his knees, tilted toward the room. The page camera's texture
/// is the open spread, drawn with true perspective; Mom's real door stays in view top-left, the
/// torch in Roshan's hand bottom-right. "Start reading" on the title cuts straight into this scene
/// in the title's framing; then the cover swings open and the view zooms in onto the spread.
/// Turns the mouse into a point on the comic's lane ("a ray through the mouse hits the page; the
/// hit's UV becomes a point in the page camera's view").
/// </summary>
public class PageView : MonoBehaviour
{
    public static PageView I { get; private set; }

    /// <summary>The room's 1920 x 1080 layer (cover-scaled to the screen) and, inside it, the part that zooms.</summary>
    [field: SerializeField] public RectTransform Room { get; private set; }
    [field: SerializeField] public RectTransform Zoomed { get; private set; }
    /// <summary>A screen-fixed 1920 x 1080 layer over the room: Roshan's hand, the HUD around the comic.</summary>
    [field: SerializeField] public RectTransform Front { get; private set; }
    /// <summary>The open spread (zoomed-layer local units).</summary>
    [field: SerializeField] public QuadImage Page { get; private set; }
    public bool Opening { get; private set; }
    /// <summary>0 = the title's framing, 1 = the game's.</summary>
    public float Framing { get; private set; }

    // the shut comic's middle in the room render (centre origin, y up)
    public static readonly Vector2 ComicCentre = new(34f, -120f);
    // the game's framing: the room zoomed about Pivot (Mom's door stays in view top-left)
    public const float Zoom = 1.3f;
    public static readonly Vector2 Pivot = new(860f, -100f);
    // the room light the torch throws round where it lands (overlay heights)
    private const float TitleSpill = 0.42f, TitleSoft = 0.15f, GameSoft = 0.12f;

    [SerializeField] private QuadImage _left, _right, _cover, _edge, _shadow, _fold;
    private Texture2D _coverArt;
    [SerializeField] private RawImage _lit, _dark;
    private bool _openPlates;
    [SerializeField] private TorchBeam _roomLight;
    [SerializeField] private RectTransform _spot;
    [SerializeField] private RectTransform _comic;
    [SerializeField] private CanvasGroup _comicGroup;

    /// <summary>Mom's room light (the finale): 0 the room by torchlight, 1 the big light on.</summary>
    [System.NonSerialized] public float RoomLight;
    [Tooltip("The spread's corners (bl, tl, tr, br; zoomed-layer units): shut, where the cover stands in the room render " +
             "(Blender: room_points.json), and open in the game's framing, a touch tilted, filling most of the screen.")]
    [SerializeField] private Vector2[] _from, _to;
    private float _flip;                     // the cover's swing, 0 shut .. 1 open

    private CoverScaler _roomScaler, _frontScaler;

    private void Awake()
    {
        I = this;
        _roomScaler = Room.GetComponent<CoverScaler>();
        _frontScaler = Front.GetComponent<CoverScaler>();
    }

    private void OnDestroy() { if (I == this) I = null; }

    /// <summary>Flat page view: the open comic faces the reader straight on, fitted to the screen (a phone's view).</summary>
    public static bool Flat => Settings.FlatPage;

    /// <summary>The page camera's texture on the spread, the comic shut on Roshan's knees (the title's framing).</summary>
    public void Show(RenderTexture page)
    {
        Page.texture = _left.texture = _right.texture = page;
        _roomLight.SetPowerInstant(1f);
        SetFraming(0f);
        SetFlip(0f);
    }

    // ---- the opening: the cover swings open, the view zooms in ------------------------------------

    public IEnumerator Open()
    {
        Opening = true;
        // the title's last frame is still held over this scene for a moment: start once it's gone
        while (App.I != null && App.I.Flow.IsLoading) yield return null;
        const float total = 2.15f;
        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            SetFlip(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 1.05f, t)));
            SetFraming(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 2.05f, t)));
            yield return null;
        }
        Opening = false;
        SetFraming(1f);
        SetFlip(1f);
    }

    /// <summary>The ending: Mom takes the comic. It lifts off Roshan's knees toward her door and is gone.</summary>
    public IEnumerator TakeAway(float seconds)
    {
        Vector2 from = _comic.anchoredPosition, to = from + new Vector2(-640f, 330f);
        for (float t = 0f; t < seconds; t += BookmarkPause.UnpausedDelta)             // a cutscene: it waits for the menu
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            _comic.anchoredPosition = Vector2.Lerp(from, to, k);
            _comic.localScale = Vector3.one * Mathf.Lerp(1f, 0.45f, k);
            _comic.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 14f, k));
            _comicGroup.alpha = 1f - Mathf.InverseLerp(0.45f, 1f, k);
            yield return null;
        }
        _comicGroup.alpha = 0f;
    }

    /// <summary>The ending's last shot: a slow push in until a room point (zoomed-layer units) sits at a
    /// screen point (1920 x 1080 design units, centre origin) at the given zoom.</summary>
    public IEnumerator PushIn(Vector2 onto, Vector2 atScreen, float zoom, float seconds)
    {
        float z0 = Zoomed.localScale.x;
        Vector2 p0 = Zoomed.anchoredPosition, p1 = atScreen - onto * zoom;
        for (float t = 0f; t < seconds; t += BookmarkPause.UnpausedDelta)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            float z = Mathf.Lerp(z0, zoom, k);
            Zoomed.localScale = new Vector3(z, z, 1f);
            Zoomed.anchoredPosition = Vector2.Lerp(p0, p1, k);
            yield return null;
        }
    }

    /// <summary>Jump straight to the game's framing (no title before this scene, e.g. testing).</summary>
    public void SkipOpening()
    {
        SetFlip(1f);
        SetFraming(1f);
        Opening = false;
    }

    private void SetFraming(float k)
    {
        Framing = k;
        float z = Mathf.Lerp(1f, Zoom, k);
        Zoomed.localScale = new Vector3(z, z, 1f);
        Zoomed.anchoredPosition = Pivot * (1f - z);
        var c = new Vector2[4];
        for (int i = 0; i < 4; i++) c[i] = Vector2.Lerp(_from[i], Flat ? FlatCorner(i) : _to[i], k);
        // while it's the size of the closed comic, show the middle of the page (the spread is wider)
        float crop = Mathf.Lerp(0.09f, 0f, k);
        Page.uvRect = new Rect(crop, 0f, 1f - 2f * crop, 1f);
        Page.SetCorners(c[0], c[1], c[2], c[3]);
        Vector2 mb = (c[0] + c[3]) * 0.5f, mt = (c[1] + c[2]) * 0.5f;
        _left.uvRect = new Rect(crop, 0f, 0.5f - crop, 1f);
        _left.SetCorners(c[0], c[1], mt, mb);
        _right.uvRect = new Rect(0.5f, 0f, 0.5f - crop, 1f);
        _right.SetCorners(mb, mt, c[2], c[3]);
        _fold.SetCorners(c[0], c[1], c[2], c[3]);
        Vector2 Grow(Vector2 p, Vector2 centre, float px) => p + (p - centre).normalized * px;
        Vector2 mid = (c[0] + c[1] + c[2] + c[3]) * 0.25f;
        _edge.SetCorners(Grow(c[0], mid, 7f) + new Vector2(2f, -7f), Grow(c[1], mid, 5f), Grow(c[2], mid, 5f), Grow(c[3], mid, 7f) + new Vector2(2f, -7f));
        var drop = new Vector2(14f, -20f);
        _shadow.SetCorners(Grow(c[0], mid, 16f) + drop, Grow(c[1], mid, 10f) + drop, Grow(c[2], mid, 10f) + drop, Grow(c[3], mid, 16f) + drop);
        SetFlip(_flip);
    }

    /// <summary>The open spread squared up (Flat page view): the game's framing, straight on, in the page texture's
    /// shape.</summary>
    private Vector2 FlatCorner(int i)
    {
        float yB = (_to[0].y + _to[3].y) * 0.5f, yT = (_to[1].y + _to[2].y) * 0.5f;
        float h = yT - yB, w = h * PageCamera.TextureWidth / PageCamera.TextureHeight;
        float cx = (_to[0].x + _to[1].x + _to[2].x + _to[3].x) * 0.25f;
        return new Vector2(i < 2 ? cx - w / 2f : cx + w / 2f, i == 0 || i == 3 ? yB : yT);
    }

    // ---- where the room sits on the screen ---------------------------------------------------------------

    /// <summary>Canvas units kept clear left of the comic, so Mom's door shows beside it in Flat page view.</summary>
    private const float DoorRoom = 170f;

    private void Update() => Layout();

    /// <summary>
    /// The room on the screen. A desktop covers the screen with it, as it always has. On a tablet the whole bedroom
    /// shows (a 4:3 screen would cut off Mom's door and the hand). In Flat page view (a phone's way) the open comic
    /// fills the screen's height, with a margin on the left for Mom's door. The title's framing always covers, so the
    /// opening zooms from exactly the title's picture into this. The front layer (the hand, the door's captions and
    /// Mom's strikes) moves with the room, so it stays where it belongs.
    /// </summary>
    private void Layout()
    {
        bool manual = Flat || Form.Touch;
        _roomScaler.Manual = _frontScaler.Manual = manual;
        if (!manual) return;
        var canvas = (RectTransform)Room.parent;
        Vector2 size = canvas.rect.size;
        float cover = Mathf.Max(size.x / 1920f, size.y / 1080f);
        Rect safe = SafeRect(size);
        float s;
        Vector2 at;
        if (Flat) FitSpread(safe, out s, out at);
        else
        {
            s = Mathf.Min(safe.width / 1920f, safe.height / 1080f);
            at = safe.center;
        }
        float scale = Mathf.Lerp(cover, s, Framing);
        Vector2 pos = at * Framing;
        foreach (var layer in new[] { Room, Front })
        {
            layer.localScale = new Vector3(scale, scale, 1f);
            layer.anchoredPosition = pos;
            layer.sizeDelta = new Vector2(1920f, 1080f);
        }
    }

    /// <summary>The comic, fitted: as tall as the safe area allows (or narrower, to leave Mom's door room), and pushed
    /// right just enough for the door.</summary>
    private void FitSpread(Rect safe, out float s, out Vector2 at)
    {
        // the spread in the game's framing, in room units, its paper edge and shadow included
        Vector2 zp = Pivot * (1f - Zoom);
        Vector2 min = zp + (FlatCorner(0) - new Vector2(18f, 26f)) * Zoom;
        Vector2 max = zp + (FlatCorner(2) + new Vector2(18f, 10f)) * Zoom;
        Vector2 box = max - min;
        s = Mathf.Min(safe.height * 0.97f / box.y, (safe.width - DoorRoom) / box.x);
        float left = Mathf.Max(DoorRoom, (safe.width - box.x * s) * 0.5f);
        var centre = new Vector2(safe.xMin + left + box.x * s * 0.5f, safe.center.y);
        at = centre - (min + max) * 0.5f * s;
    }

    /// <summary>The open spread in Flat page view at the game's framing, in room units (which the front layer shares
    /// on a touch screen).</summary>
    public Rect FlatSpread
    {
        get
        {
            Vector2 zp = Pivot * (1f - Zoom);
            Vector2 a = zp + FlatCorner(0) * Zoom, b = zp + FlatCorner(2) * Zoom;
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }
    }

    /// <summary>The safe part of the screen, in room units.</summary>
    public Rect SafeInRoom
    {
        get
        {
            Rect safe = SafeRect(((RectTransform)Room.parent).rect.size);
            float s = Mathf.Max(0.001f, Room.localScale.x);
            Vector2 o = Room.anchoredPosition;
            return Rect.MinMaxRect((safe.xMin - o.x) / s, (safe.yMin - o.y) / s, (safe.xMax - o.x) / s, (safe.yMax - o.y) / s);
        }
    }

    /// <summary>The top of the comic's spine on the screen (pixels): where the bookmark ribbon hangs on a phone.</summary>
    public Vector2 SpineTopOnScreen
    {
        get
        {
            Rect r = FlatSpread;
            return RectTransformUtility.WorldToScreenPoint(null, Room.TransformPoint(new Vector3(r.center.x, r.yMax, 0f)));
        }
    }

    /// <summary>The screen's safe area (clear of notches and rounded corners) in canvas units, centre origin.</summary>
    private static Rect SafeRect(Vector2 size)
    {
        var sa = UnityEngine.Device.Screen.safeArea;
        float sw = Mathf.Max(1, UnityEngine.Device.Screen.width), sh = Mathf.Max(1, UnityEngine.Device.Screen.height);
        return Rect.MinMaxRect(sa.xMin / sw * size.x - size.x / 2f, sa.yMin / sh * size.y - size.y / 2f,
                               sa.xMax / sw * size.x - size.x / 2f, sa.yMax / sh * size.y - size.y / 2f);
    }

    /// <summary>The cover swinging round its spine, from shut on the right page to open on the left.</summary>
    private void SetFlip(float k)
    {
        _flip = k;
        if (k > 0f && !_openPlates)
        {
            // the cover quad now stands exactly where the render's cover was: swap to the plates without it
            _openPlates = true;
            _lit.texture = Resources.Load<Texture2D>("Room/room_lit_open");
            _dark.texture = Resources.Load<Texture2D>("Room/room_dark_open");
        }
        var c = Page.Corners;                                         // bl, tl, tr, br
        Vector2 mb = (c[0] + c[3]) * 0.5f, mt = (c[1] + c[2]) * 0.5f;
        float a = k * Mathf.PI;
        float lift = Mathf.Sin(a);
        // the free edge sweeps over the spine; standing up it comes toward the reader (up and larger)
        Vector2 freeTop = mt + (c[2] - mt) * Mathf.Cos(a);
        Vector2 freeBot = mb + (c[3] - mb) * Mathf.Cos(a);
        float rise = 0.32f * (c[2] - mt).magnitude * lift;
        Vector2 edgeMid = (freeTop + freeBot) * 0.5f + new Vector2(0f, rise);
        float grow = 1f + 0.16f * lift;
        freeTop = edgeMid + (freeTop - freeBot) * 0.5f * grow;
        freeBot = edgeMid - (freeTop - edgeMid);
        bool back = k > 0.5f;
        if (_coverArt == null) _coverArt = Resources.Load<Texture2D>("Room/cover_flat");
        _cover.texture = back ? null : _coverArt;
        float shade = Mathf.Lerp(1f, 0.72f, lift);
        // the inside of the cover: plain paper in the moonlight (the torch is on the page, not on it)
        Color face = back ? Palette.ComicPaper * (0.5f * shade) : new Color(shade, shade, shade, 1f);
        face.a = Mathf.Clamp01((1f - k) / 0.12f);                    // it lands on the left page and is gone
        _cover.color = face;
        _cover.SetCorners(mb, mt, freeTop, freeBot);
        _cover.enabled = k > 0.001f && k < 0.999f;                    // shut, the room render's own cover shows

        // the right page is under the cover from the start; the left one comes as the cover lands
        bool split = Opening || k < 0.999f;
        _right.enabled = split && k > 0.001f;
        _left.enabled = split;
        _left.color = new Color(1f, 1f, 1f, Mathf.Clamp01((k - 0.85f) / 0.15f));
        Page.enabled = !split;
        _fold.enabled = k > 0.5f;
        _fold.color = new Color(0f, 0f, 0f, 0.22f * Mathf.Clamp01((k - 0.5f) / 0.5f));
        // the spread's paper edges and shadow arrive with the left page, as the cover lands
        _edge.enabled = _shadow.enabled = k > 0.7f;
        float edgeA = Mathf.Clamp01((k - 0.7f) / 0.3f);
        _edge.color = (Palette.ComicPaper * 0.62f).Alpha(edgeA);
        _shadow.color = new Color(0f, 0f, 0f, 0.5f * edgeA);
    }

    private void LateUpdate()
    {
        // the room light: in the title's framing it sits on the closed comic like the title's torch;
        // in the game it spills round wherever the beam lands on the page, brighter while it's on
        var field = LightField.I;
        Vector2 at = ComicCentre;
        float spill = TitleSpill;
        if (field != null && PageCamera.I != null && Framing > 0f)
        {
            Vector2 onPage = Page.UVToLocal(PageCamera.I.LaneToViewport(field.BeamCentre));
            float unit = (_to[3] - _to[0]).magnitude / PageCamera.Width;        // zoomed-local units per lane unit
            float gameSpill = field.BeamRadius * unit * 1.7f / 1080f;
            at = Vector2.Lerp(ComicCentre, onPage, Framing);
            spill = Mathf.Lerp(TitleSpill, gameSpill, Framing);
        }
        _spot.anchoredPosition = at;
        _roomLight.radius = spill;
        _roomLight.softness = Mathf.Lerp(TitleSoft, GameSoft, Framing);
        float power = 1f;
        if (field != null && Framing > 0f) power = Mathf.Lerp(1f, field.BeamLive ? field.Brightness : 0f, Framing);
        if (field != null && field.FlareTime > 0f) power = 1.4f;
        _roomLight.SetPowerInstant(power);
        _roomLight.darkness = 1f - Mathf.Clamp01(RoomLight);           // the big light: the lit room everywhere
    }

    // ---- the mouse and the comic --------------------------------------------------------------------

    /// <summary>A lane point to where it is on the open comic, in the zoomed layer's local units.</summary>
    public Vector2 LaneToRoom(Vector2 lane) => Page.UVToLocal(PageCamera.I.LaneToViewport(lane));

    /// <summary>A lane point in world space (for things drawn in other layers: the hand's light shaft).</summary>
    public Vector3 LaneToWorld(Vector2 lane) => Page.rectTransform.TransformPoint(LaneToRoom(lane));

    /// <summary>A lane point on the screen (pixels): where a finger would touch to aim there.</summary>
    public Vector2 LaneToScreen(Vector2 lane) => RectTransformUtility.WorldToScreenPoint(UIKit.CanvasCamera(Page), LaneToWorld(lane));

    /// <summary>Screen pixel to a point on the comic's z = 0 lane. False if it's well off the page.</summary>
    public static bool ScreenToLane(Vector2 screen, out Vector2 lane)
    {
        lane = default;
        if (I == null || I.Page == null || PageCamera.I == null || I.Opening) return false;
        var cam = UIKit.CanvasCamera(I.Page);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(I.Page.rectTransform, screen, cam, out var local)) return false;
        I.Page.LocalToUV(local, out var uv);
        if (uv.x < -0.4f || uv.x > 1.4f || uv.y < -0.4f || uv.y > 1.4f) return false;
        uv.x = Mathf.Clamp(uv.x, -0.05f, 1.05f);
        uv.y = Mathf.Clamp(uv.y, -0.05f, 1.05f);
        lane = PageCamera.I.ViewportToLane(uv);
        return true;
    }
}
