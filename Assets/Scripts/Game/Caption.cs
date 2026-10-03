using TMPro;
using UnityEngine;

/// <summary>A yellow caption box printed in a panel's corner (GDD section 11).</summary>
public static class Caption
{
    public static void Create(Transform parent, Vector2 topLeft, string text)
    {
        var root = new GameObject("Caption");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(topLeft.x, topLeft.y, -2.2f);
        var t = root.AddComponent<TextMeshPro>();
        t.font = Resources.Load<TMP_FontAsset>("Fonts/ComicNeue SDF");
        t.text = text;
        t.fontSize = 3.2f;
        t.fontStyle = FontStyles.Bold;
        t.color = Palette.Ink;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.margin = new Vector4(0.25f, 0.18f, 0.25f, 0.18f);
        t.rectTransform.pivot = new Vector2(0f, 1f);
        t.rectTransform.sizeDelta = new Vector2(Mathf.Min(7f, 0.22f * text.Length + 0.8f), 1f);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.ForceMeshUpdate();
        var size = t.GetRenderedValues(false) + new Vector2(0.5f, 0.36f);
        t.rectTransform.sizeDelta = new Vector2(Mathf.Max(size.x, 1f), Mathf.Max(size.y, 0.6f));

        var box = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(box.GetComponent<Collider>());
        box.name = "CaptionBox";
        box.transform.SetParent(root.transform, false);
        var sz = t.rectTransform.sizeDelta;
        box.transform.localPosition = new Vector3(sz.x / 2f, -sz.y / 2f, 0.02f);
        box.transform.localScale = new Vector3(sz.x, sz.y, 1f);
        var mr = box.GetComponent<MeshRenderer>();
        mr.sharedMaterial = CaptionMaterial();
        var edge = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(edge.GetComponent<Collider>());
        edge.name = "CaptionEdge";
        edge.transform.SetParent(root.transform, false);
        edge.transform.localPosition = new Vector3(sz.x / 2f, -sz.y / 2f, 0.04f);
        edge.transform.localScale = new Vector3(sz.x + 0.12f, sz.y + 0.12f, 1f);
        edge.GetComponent<MeshRenderer>().sharedMaterial = GameAssets.I != null ? GameAssets.I.border : null;
    }

    private static Material CaptionMaterial() => GameAssets.I != null ? GameAssets.I.caption : null;
}
