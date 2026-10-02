using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds the front-end in code: canvases, comic sprites, lettering. Every screen
/// assembles itself from these helpers at Start, so a scene file only holds a
/// camera and one screen component, and the whole look lives in source.
///
/// Sprites are generated once at runtime (tiny textures, no art files):
/// an ink-bordered caption box, discs, a soft shadow, a starburst, a warning
/// triangle and a bookmark ribbon.
/// </summary>
public static class UIKit
{
    public const float RefWidth = 1920f, RefHeight = 1080f;

    // ---- Fonts (TMP assets made by Past Bedtime > Build Shell) ------------------------
    private static TMP_FontAsset _display, _body, _mono;
    public static TMP_FontAsset Display => _display ??= LoadFont("Fonts/Bangers SDF");      // lettering, SFX
    public static TMP_FontAsset Body => _body ??= LoadFont("Fonts/ComicNeue SDF");          // captions, prose
    public static TMP_FontAsset Mono => _mono ??= LoadFont("Fonts/CourierPrime SDF");       // settings, hints

    private static TMP_FontAsset LoadFont(string path)
    {
        var f = Resources.Load<TMP_FontAsset>(path);
        return f != null ? f : TMP_Settings.defaultFontAsset;
    }

    // ---- Canvas + event system ----------------------------------------------------------

    public static Canvas Canvas(string name, int sortOrder, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (parent != null) go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        go.GetComponent<EventSystem>().sendNavigationEvents = true;
    }

    /// <summary>Selects <paramref name="go"/> for keyboard/gamepad (null clears the selection).</summary>
    public static void Select(GameObject go)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go);
    }

    public static GameObject Selected =>
        EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

    // ---- Elements ---------------------------------------------------------------------------

    public static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Image Image(Transform parent, string name, Color color, Sprite sprite = null, bool sliced = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sliced) img.type = UnityEngine.UI.Image.Type.Sliced;
        return img;
    }

    public static RawImage Raw(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var raw = go.GetComponent<RawImage>();
        raw.raycastTarget = false;
        return raw;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font,
        float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.text = text;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    /// <summary>Comic lettering: ink outline plus a hard ink drop shadow (a second,
    /// offset copy, which keeps clear of shader-variant stripping on WebGL).</summary>
    public static TextMeshProUGUI Lettering(Transform parent, string name, string text, float size, Color fill,
        Vector2 pos, Vector2 box, float outline = 0.22f, float shadow = 6f, float tilt = 0f)
    {
        var root = Rect(parent, name);
        Place(root, pos, box);
        root.localRotation = Quaternion.Euler(0, 0, tilt);

        if (shadow > 0f)
        {
            var back = Text(root, "Shadow", text, Display, size, Palette.Ink);
            Stretch(back.rectTransform);
            back.rectTransform.anchoredPosition = new Vector2(shadow, -shadow);
            back.outlineWidth = outline;
            back.outlineColor = Palette.Ink;
        }
        var face = Text(root, "Face", text, Display, size, fill);
        Stretch(face.rectTransform);
        face.outlineWidth = outline;
        face.outlineColor = Palette.Ink;
        return face;
    }

    /// <summary>A comic panel: a hard ink drop shadow behind an ink-bordered face.</summary>
    public static Image Panel(Transform parent, string name, Vector2 pos, Vector2 size, Color face,
        float shadow = 10f, float tilt = 0f)
    {
        var root = Rect(parent, name);
        Place(root, pos, size);
        root.localRotation = Quaternion.Euler(0, 0, tilt);
        if (shadow > 0f)
        {
            var s = Image(root, "Shadow", Palette.Ink, Box, sliced: true);
            Stretch(s.rectTransform);
            s.rectTransform.anchoredPosition = new Vector2(shadow, -shadow);
        }
        var f = Image(root, "Face", face, Box, sliced: true);
        Stretch(f.rectTransform);
        return f;
    }

    /// <summary>A soft contact shadow for real objects (the comic lying on the bed).</summary>
    public static Image SoftShadow(Transform parent, string name, float spread = 40f, float alpha = 0.7f)
    {
        var s = Image(parent, name, Color.black.Alpha(alpha), SoftShadowSprite, sliced: true);
        Stretch(s.rectTransform, -spread);
        return s;
    }

    // ---- Layout helpers ---------------------------------------------------------------------

    public static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>Centre-anchored placement in reference pixels (1920 × 1080, origin at centre).</summary>
    public static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    /// <summary>Placement against a screen edge/corner (anchor in 0..1).</summary>
    public static void Pin(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    /// <summary>Screen-pixel centre of a UI element, whatever its canvas mode.</summary>
    public static Vector2 ScreenCenter(RectTransform rt) =>
        RectTransformUtility.WorldToScreenPoint(CanvasCamera(rt), rt.TransformPoint(rt.rect.center));

    /// <summary>Screen pixels → 0..1 across <paramref name="rt"/> (bottom-left origin).</summary>
    public static Vector2 ScreenToUV(RectTransform rt, Vector2 screenPx)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPx, CanvasCamera(rt), out var local);
        var r = rt.rect;
        return new Vector2((local.x - r.xMin) / Mathf.Max(1f, r.width), (local.y - r.yMin) / Mathf.Max(1f, r.height));
    }

    /// <summary>The camera a canvas renders through (null for Screen Space Overlay).</summary>
    public static Camera CanvasCamera(Component c)
    {
        var canvas = c.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    // ---- Materials ----------------------------------------------------------------------------

    public static Material NewLightMaskMaterial()
    {
        var baseMat = Resources.Load<Material>("Materials/LightMask");
        if (baseMat != null) return new Material(baseMat);
        var shader = Shader.Find("PastBedtime/UI/LightMask");
        return shader != null ? new Material(shader) : null;
    }

    // ---- Generated sprites --------------------------------------------------------------------

    private static Sprite _box, _soft, _disc, _ring, _glow, _burst, _triangle, _ribbon, _fade, _softShadow;

    /// <summary>White face, 5 px ink border, 9-sliced. Image.color tints the face only.</summary>
    public static Sprite Box => _box ??= MakeBox();
    /// <summary>Rounded white rectangle, no border, 9-sliced (bedding, door glow).</summary>
    public static Sprite Soft => _soft ??= MakeSoft();
    /// <summary>Anti-aliased white disc.</summary>
    public static Sprite Disc => _disc ??= MakeDisc(false);
    /// <summary>White disc with an ink rim.</summary>
    public static Sprite Ring => _ring ??= MakeDisc(true);
    /// <summary>Soft radial falloff for glows.</summary>
    public static Sprite Glow => _glow ??= MakeGlow();
    /// <summary>14-point starburst with an ink outline (SFX).</summary>
    public static Sprite Burst => _burst ??= MakeBurst();
    /// <summary>Equilateral triangle pointing up.</summary>
    public static Sprite Triangle => _triangle ??= MakeTriangle();
    /// <summary>Bookmark ribbon with a V notch at the bottom.</summary>
    public static Sprite Ribbon => _ribbon ??= MakeRibbon();
    /// <summary>Vertical fade: opaque at the bottom, clear at the top.</summary>
    public static Sprite Fade => _fade ??= MakeFade();
    /// <summary>Blurred rounded-rectangle shadow, 9-sliced, 40 px spread.</summary>
    public static Sprite SoftShadowSprite => _softShadow ??= MakeSoftShadow();

    private static Texture2D NewTex(int w, int h, TextureWrapMode wrap = TextureWrapMode.Clamp)
    {
        // Mipmapped: these sprites are often drawn much smaller than their texture
        // (eyes, dots, glows), and without mips they shimmer and turn jagged.
        return new Texture2D(w, h, TextureFormat.RGBA32, true)
        {
            filterMode = FilterMode.Trilinear,
            wrapMode = wrap,
            hideFlags = HideFlags.DontSave,
        };
    }

    private static Sprite ToSprite(Texture2D tex, Vector4 border = default, float ppu = 100f)
    {
        tex.Apply(true, true);
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
            ppu, 0, SpriteMeshType.FullRect, border);
    }

    private static Sprite MakeBox()
    {
        const int n = 48, b = 5;
        var tex = NewTex(n, n);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            bool edge = x < b || y < b || x >= n - b || y >= n - b;
            px[y * n + x] = edge ? new Color32(20, 20, 20, 255) : new Color32(255, 255, 255, 255);
        }
        tex.SetPixels32(px);
        return ToSprite(tex, new Vector4(b + 3, b + 3, b + 3, b + 3));
    }

    private static Sprite MakeSoft()
    {
        const int n = 64; const float r = 22f;
        var tex = NewTex(n, n);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            byte a = (byte)(255 * Mathf.Clamp01(r - d + 0.5f));
            px[y * n + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px);
        return ToSprite(tex, new Vector4(r + 2, r + 2, r + 2, r + 2));
    }

    private static Sprite MakeDisc(bool rim)
    {
        const int n = 128; const float r = 62f, rimWidth = 9f;
        var tex = NewTex(n, n);
        var px = new Color32[n * n];
        var c = new Vector2(n * 0.5f, n * 0.5f);
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
            float a = Mathf.Clamp01(r - d + 0.5f);
            float inkK = rim ? Mathf.Clamp01(d - (r - rimWidth) + 0.5f) : 0f;
            byte v = (byte)Mathf.Lerp(255, 20, inkK);
            px[y * n + x] = new Color32(v, v, v, (byte)(255 * a));
        }
        tex.SetPixels32(px);
        return ToSprite(tex);
    }

    private static Sprite MakeGlow()
    {
        const int n = 128;
        var tex = NewTex(n, n);
        var px = new Color32[n * n];
        var c = new Vector2(n * 0.5f, n * 0.5f);
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / (n * 0.5f);
            float a = Mathf.Clamp01(1f - d);
            px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * a * a));
        }
        tex.SetPixels32(px);
        return ToSprite(tex);
    }

    private static Sprite MakeBurst()
    {
        const int n = 256, points = 14;
        var tex = NewTex(n, n);
        var px = new Color32[n * n];
        float outer = n * 0.49f, inner = n * 0.33f;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = x + 0.5f - n * 0.5f, dy = y + 0.5f - n * 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * points;
            float tri = Mathf.Abs(ang - Mathf.Floor(ang) - 0.5f) * 2f;   // 1 at the tips, 0 between
            float edge = Mathf.Lerp(inner, outer, tri);
            float alpha = Mathf.Clamp01(edge - d + 0.5f);
            float ink = Mathf.Clamp01(d - (edge - 9f) + 0.5f);
            byte v = (byte)Mathf.Lerp(255, 20, ink);
            px[y * n + x] = new Color32(v, v, v, (byte)(255 * alpha));
        }
        tex.SetPixels32(px);
        return ToSprite(tex);
    }

    private static Sprite MakeTriangle()
    {
        const int n = 128;
        var tex = NewTex(n, n);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float fy = (y + 0.5f) / n;                  // 0 bottom, 1 top
            float half = (1f - fy) * 0.5f;              // half-width at this height
            float fx = Mathf.Abs((x + 0.5f) / n - 0.5f);
            float a = Mathf.Clamp01((half - fx) * n + 0.5f) * Mathf.Clamp01((fy - 0.04f) * n);
            px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * a));
        }
        tex.SetPixels32(px);
        return ToSprite(tex);
    }

    private static Sprite MakeRibbon()
    {
        const int w = 64, h = 256, notch = 30;
        var tex = NewTex(w, h);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float cut = notch * (1f - Mathf.Abs(x + 0.5f - w * 0.5f) / (w * 0.5f));
            float a = Mathf.Clamp01(y + 0.5f - cut);
            px[y * w + x] = new Color32(255, 255, 255, (byte)(255 * a));
        }
        tex.SetPixels32(px);
        return ToSprite(tex);
    }

    private static Sprite MakeFade()
    {
        const int w = 4, h = 128;
        var tex = NewTex(w, h);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float k = 1f - (y + 0.5f) / h;
            px[y * w + x] = new Color32(255, 255, 255, (byte)(255 * k * k));
        }
        tex.SetPixels32(px);
        return ToSprite(tex);
    }

    private static Sprite MakeSoftShadow()
    {
        const int n = 160; const float spread = 40f, r = 18f;
        var tex = NewTex(n, n);
        var px = new Color32[n * n];
        var half = new Vector2(n * 0.5f, n * 0.5f);
        var inner = half - Vector2.one * spread;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            var p = new Vector2(Mathf.Abs(x + 0.5f - half.x), Mathf.Abs(y + 0.5f - half.y));
            var q = new Vector2(p.x - inner.x + r, p.y - inner.y + r);
            float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
            float k = Mathf.Clamp01(1f - (d + spread * 0.25f) / spread);
            float a = k * k * (3f - 2f * k);
            px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * a * a));
        }
        tex.SetPixels32(px);
        const float b = 76f;
        return ToSprite(tex, new Vector4(b, b, b, b));
    }
}
