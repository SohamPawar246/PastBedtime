using TMPro;
using UnityEngine;

/// <summary>A yellow caption box printed in a panel's corner (GDD section 11). Its words can name the
/// player's controls ("HOLD {FOLLOW}: ..."): they're lettered from <see cref="Bindings"/>, and lettered
/// again if the controls change.</summary>
public static class Caption
{
    public static void Create(Transform parent, Vector2 topLeft, string text, Rect panel = default)
    {
        var root = new GameObject("Caption");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(topLeft.x, topLeft.y, -2.2f);
        var t = root.AddComponent<TextMeshPro>();
        t.font = Resources.Load<TMP_FontAsset>("Fonts/ComicNeue SDF");
        t.fontSize = 3.2f;
        t.fontStyle = FontStyles.Bold;
        t.color = Palette.Ink;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.margin = new Vector4(0.25f, 0.18f, 0.25f, 0.18f);
        t.rectTransform.pivot = new Vector2(0f, 1f);
        t.textWrappingMode = TextWrappingModes.Normal;

        var box = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(box.GetComponent<Collider>());
        box.name = "CaptionBox";
        box.transform.SetParent(root.transform, false);
        box.GetComponent<MeshRenderer>().sharedMaterial = CaptionMaterial();
        var edge = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(edge.GetComponent<Collider>());
        edge.name = "CaptionEdge";
        edge.transform.SetParent(root.transform, false);
        edge.GetComponent<MeshRenderer>().sharedMaterial = GameAssets.I != null ? GameAssets.I.border : null;

        var words = root.AddComponent<CaptionWords>();
        words.Set(t, box.transform, edge.transform, text);
        if (panel.width > 0f)
        {
            var keep = root.AddComponent<CaptionInView>();
            keep.panel = panel;
            keep.words = words;
        }
    }

    private static Material CaptionMaterial() => GameAssets.I != null ? GameAssets.I.caption : null;
}

/// <summary>A caption's words and its box, fitted to them; re-lettered when the controls change.</summary>
public class CaptionWords : MonoBehaviour
{
    public float Width { get; private set; }
    private TextMeshPro _text;
    private Transform _box, _edge;
    private string _raw;

    public void Set(TextMeshPro text, Transform box, Transform edge, string raw)
    {
        _text = text;
        _box = box;
        _edge = edge;
        _raw = raw;
        Fit();
    }

    private void OnEnable() => Bindings.Changed += Fit;
    private void OnDisable() => Bindings.Changed -= Fit;

    private void Fit()
    {
        if (_text == null) return;
        string text = Bindings.Format(_raw);
        _text.text = text;
        _text.rectTransform.sizeDelta = new Vector2(Mathf.Min(7f, 0.22f * text.Length + 0.8f), 1f);
        _text.ForceMeshUpdate();
        var size = _text.GetRenderedValues(false) + new Vector2(0.5f, 0.36f);
        var sz = new Vector2(Mathf.Max(size.x, 1f), Mathf.Max(size.y, 0.6f));
        _text.rectTransform.sizeDelta = sz;
        _box.localPosition = new Vector3(sz.x / 2f, -sz.y / 2f, 0.02f);
        _box.localScale = new Vector3(sz.x, sz.y, 1f);
        _edge.localPosition = new Vector3(sz.x / 2f, -sz.y / 2f, 0.04f);
        _edge.localScale = new Vector3(sz.x + 0.12f, sz.y + 0.12f, 1f);
        Width = sz.x;
    }
}

/// <summary>A caption stays readable while its panel is on the page: when the page camera has panned so
/// the panel's corner is off the edge, the box slides along the panel's top to stay in view (never out of
/// its panel).</summary>
public class CaptionInView : MonoBehaviour
{
    public Rect panel;
    public CaptionWords words;
    private float _homeX;

    private void Start() => _homeX = transform.position.x;

    private void LateUpdate()
    {
        var cam = PageCamera.I;
        if (cam == null) return;
        float y = transform.position.y, width = words != null ? words.Width : 1f;
        float viewL = cam.ViewportToLane(new Vector2(0f, 0.5f)).x + 0.35f;
        float viewR = cam.ViewportToLane(new Vector2(1f, 0.5f)).x - 0.35f;
        float x = Mathf.Max(_homeX, viewL);                              // pushed in from the left edge
        x = Mathf.Min(x, Mathf.Max(_homeX, viewR - width));             // or the right
        x = Mathf.Clamp(x, panel.xMin + 0.2f, Mathf.Max(panel.xMin + 0.2f, panel.xMax - width - 0.2f));
        var p = transform.position;
        transform.position = new Vector3(x, y, p.z);
    }
}
