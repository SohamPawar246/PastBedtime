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
/// The comic shapes are art files in Resources/UI/Sprites: an ink-bordered caption
/// box, discs, a soft shadow, a starburst, a warning triangle and a bookmark ribbon.
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
        return baseMat != null ? new Material(baseMat) : null;
    }

    // ---- Sprites (Resources/UI/Sprites) ------------------------------------------------------

    private static Sprite _box, _soft, _disc, _ring, _glow, _burst, _triangle, _ribbon, _fade, _softShadow;

    /// <summary>White face, 5 px ink border, 9-sliced. Image.color tints the face only.</summary>
    public static Sprite Box => _box ??= LoadSprite("box");
    /// <summary>Plain white: what a Filled image needs to show its fill amount (with no sprite it draws full).</summary>
    public static Sprite Solid => _solid ??= LoadSprite("solid");
    private static Sprite _solid;
    /// <summary>Rounded white rectangle, no border, 9-sliced (bedding, door glow).</summary>
    public static Sprite Soft => _soft ??= LoadSprite("soft");
    /// <summary>Anti-aliased white disc.</summary>
    public static Sprite Disc => _disc ??= LoadSprite("disc");
    /// <summary>White disc with an ink rim.</summary>
    public static Sprite Ring => _ring ??= LoadSprite("ring");
    /// <summary>Soft radial falloff for glows.</summary>
    public static Sprite Glow => _glow ??= LoadSprite("glow");
    /// <summary>14-point starburst with an ink outline (SFX).</summary>
    public static Sprite Burst => _burst ??= LoadSprite("burst");
    /// <summary>Equilateral triangle pointing up.</summary>
    public static Sprite Triangle => _triangle ??= LoadSprite("triangle");
    /// <summary>Bookmark ribbon with a V notch at the bottom.</summary>
    public static Sprite Ribbon => _ribbon ??= LoadSprite("ribbon");
    /// <summary>Vertical fade: opaque at the bottom, clear at the top.</summary>
    public static Sprite Fade => _fade ??= LoadSprite("fade");
    /// <summary>Blurred rounded-rectangle shadow, 9-sliced, 40 px spread.</summary>
    public static Sprite SoftShadowSprite => _softShadow ??= LoadSprite("soft_shadow");

    /// <summary>A sprite from Resources/UI/Sprites (the game's own drawn shapes, kept as art files).</summary>
    public static Sprite LoadSprite(string name) => Resources.Load<Sprite>("UI/Sprites/" + name);
}
