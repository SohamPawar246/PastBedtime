using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The room closes in as Mom comes: the edges of the view darken a little with her footsteps, most at the door
/// and while it's open, and lift as she goes. It lies over the room and the comic but under the reader's things
/// (the hand and torch, the footprints, the door's caption), and never over her door itself: a clear hole is
/// left round it, because the light under that door is the warning.
/// </summary>
public class MomVignette : MonoBehaviour
{
    private const int W = 192, H = 108;          // the shape is soft: a small texture, stretched
    private RawImage _image;
    private Texture2D _tex;
    private Rect _drawnFor;                      // the door (screen pixels) the texture's hole was drawn for
    private Vector2Int _screen;

    /// <summary>How dark the edges are right now (0 = not at all).</summary>
    public float Strength { get; private set; }
    public static MomVignette I { get; private set; }

    public static MomVignette Build(Transform roomCanvas, RectTransform front)
    {
        var img = UIKit.Raw(roomCanvas, "MomVignette");
        UIKit.Stretch(img.rectTransform);
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);
        img.enabled = false;
        img.transform.SetSiblingIndex(front.GetSiblingIndex());          // just under the reader's things
        var v = img.gameObject.AddComponent<MomVignette>();
        v._image = img;
        return v;
    }

    private void Awake() => I = this;

    private void OnDestroy()
    {
        if (I == this) I = null;
        if (_tex != null) Destroy(_tex);
    }

    private void LateUpdate()
    {
        var mom = MomDirector.I;
        var door = MomDoorView.I;
        float target = 0f;
        if (mom != null && !mom.Watching && mom.Bust <= 0f && (door == null || !door.Ending))
            target = mom.State switch
            {
                MomState.Stirring => 0.12f,
                MomState.Approaching => Mathf.Lerp(0.15f, 0.5f, mom.Feet),
                MomState.AtDoor => mom.IsCat ? 0.3f : 0.55f,
                MomState.Opening => 0.6f,
                _ => 0f,
            };
        Strength = Mathf.MoveTowards(Strength, target, Time.unscaledDeltaTime * (target > Strength ? 0.6f : 0.9f));
        bool show = Strength > 0.002f;
        if (_image.enabled != show) _image.enabled = show;
        if (!show) return;
        if (door != null) Redraw(door.DoorOnScreen());
        _image.color = new Color(1f, 1f, 1f, Strength);
    }

    /// <summary>The shape: dark toward the corners, clear in the middle, clear round the door. Redrawn only when
    /// the screen or the door moves.</summary>
    private void Redraw(Rect door)
    {
        var screen = new Vector2Int(Screen.width, Screen.height);
        if (_tex != null && screen == _screen && Mathf.Abs(door.x - _drawnFor.x) < 3f && Mathf.Abs(door.y - _drawnFor.y) < 3f &&
            Mathf.Abs(door.width - _drawnFor.width) < 3f && Mathf.Abs(door.height - _drawnFor.height) < 3f) return;
        _screen = screen;
        _drawnFor = door;
        if (_tex == null)
        {
            _tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "MomVignette", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            _image.texture = _tex;
        }
        float sw = Mathf.Max(1, screen.x), sh = Mathf.Max(1, screen.y);
        Vector2 c = new Vector2(door.center.x / sw, door.center.y / sh);
        Vector2 r = new Vector2(Mathf.Max(0.02f, door.width * 0.62f / sw), Mathf.Max(0.02f, door.height * 0.62f / sh));
        var ink = new Color(0.01f, 0.012f, 0.03f, 0f);
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W;
                float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1f, Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f));
                float hx = (u - c.x) / r.x, hy = (v - c.y) / r.y;
                float hole = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1.25f, Mathf.Sqrt(hx * hx + hy * hy)));
                ink.a = edge * (1f - hole);
                px[y * W + x] = ink;
            }
        }
        _tex.SetPixels(px);
        _tex.Apply(false);
    }
}
