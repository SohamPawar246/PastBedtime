using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The visible shaft of torchlight in the air: a soft trapezoid from the lens to
/// the pool of light, brightest along its middle and near the bulb. Built from
/// several slices along its length so the gradient doesn't kink across the
/// trapezoid's diagonal.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class LightCone : MaskableGraphic
{
    public Vector2 start, end;            // local positions (parent space)
    public float startWidth = 40f, endWidth = 700f;
    private const int Slices = 12;

    private static Texture2D _tex;
    /// <summary>u: across the shaft, v: along it (0 at the lens).</summary>
    public override Texture mainTexture => _tex != null ? _tex : (_tex = Resources.Load<Texture2D>("UI/Textures/light_shaft"));

    public static LightCone Create(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(LightCone));
        go.transform.SetParent(parent, false);
        var cone = go.GetComponent<LightCone>();
        var rt = cone.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        cone.color = color;
        cone.raycastTarget = false;
        return cone;
    }

    public void Set(Vector2 from, Vector2 to, float fromWidth, float toWidth)
    {
        start = from; end = to; startWidth = fromWidth; endWidth = toWidth;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Vector2 dir = end - start;
        float len = dir.magnitude;
        if (len < 1f) return;
        Vector2 n = new Vector2(-dir.y, dir.x) / len;
        Color32 c = color;
        for (int i = 0; i <= Slices; i++)
        {
            float k = (float)i / Slices;
            Vector2 p = Vector2.Lerp(start, end, k);
            float w = Mathf.Lerp(startWidth, endWidth, k) * 0.5f;
            vh.AddVert(p - n * w, c, new Vector2(0f, k));
            vh.AddVert(p + n * w, c, new Vector2(1f, k));
            if (i > 0)
            {
                int b = (i - 1) * 2;
                vh.AddTriangle(b, b + 1, b + 3);
                vh.AddTriangle(b + 3, b + 2, b);
            }
        }
    }
}
