using TMPro;
using UnityEngine;

/// <summary>
/// A speech bubble you can stand on (the proposal's comic theme): someone in the panel says something
/// and the balloon hangs there in paper and ink, solid as a ledge. It's lettering, printed on the page,
/// so it never freezes or fades: only the torch decides whether you can read it.
/// </summary>
public static class SpeechBubble
{
    /// <summary>A balloon centred at `centre` with its tail pointing at `speaker`; Max can land on its top.</summary>
    public static GameObject Create(Transform parent, Vector2 centre, string text, float width, Vector2 speaker)
    {
        var A = GameAssets.I;
        var root = new GameObject("SpeechBubble");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(centre.x, centre.y, 0f);

        // the words first: the balloon is sized to them
        var t = new GameObject("Words").AddComponent<TextMeshPro>();
        t.transform.SetParent(root.transform, false);
        t.transform.localPosition = new Vector3(0f, 0.02f, 0.5f);
        t.font = Resources.Load<TMP_FontAsset>("Fonts/ComicNeue SDF");
        t.fontStyle = FontStyles.Bold;
        t.text = text;
        t.fontSize = 3.4f;
        t.color = Palette.Ink;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.rectTransform.sizeDelta = new Vector2(width * 0.78f, 3f);
        t.ForceMeshUpdate();
        var words = t.GetRenderedValues(false);
        float w = width, h = Mathf.Max(1.0f, words.y + 0.7f);

        // tail toward the speaker, then the balloon over its root, each with an ink rim behind
        Vector2 toSpeaker = speaker - centre;
        Vector2 dir = toSpeaker.sqrMagnitude > 1e-4f ? toSpeaker.normalized : new Vector2(-0.5f, -0.86f);
        Vector2 baseAt = Ellipse(dir, w * 0.5f, h * 0.5f) * 0.82f;
        Vector2 tip = baseAt + dir * Mathf.Clamp(toSpeaker.magnitude * 0.45f, 0.5f, 1.3f);
        Vector2 side = new Vector2(-dir.y, dir.x) * 0.24f;
        Mesh(root.transform, "TailInk", A != null ? A.border : null, 0.58f, TriangleMesh(baseAt + side * 1.45f, baseAt - side * 1.45f, tip + dir * 0.09f));
        Mesh(root.transform, "BalloonInk", A != null ? A.border : null, 0.57f, EllipseMesh(w * 0.5f + 0.07f, h * 0.5f + 0.07f));
        Mesh(root.transform, "Tail", A != null ? A.window : null, 0.55f, TriangleMesh(baseAt + side, baseAt - side, tip));
        Mesh(root.transform, "Balloon", A != null ? A.window : null, 0.54f, EllipseMesh(w * 0.5f, h * 0.5f));

        // solid across its top: a slab under the crown of the balloon, on Max's lane
        var slab = new GameObject("Ledge");
        slab.transform.SetParent(root.transform, false);
        var box = slab.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, h * 0.5f - 0.25f, 0f);
        box.size = new Vector3(w * 0.72f, 0.5f, 2f);
        slab.AddComponent<OneWayLedge>();                                   // jump up through it, land on top
        PageBuilder.SetLayer(root.transform);
        return root;
    }

    private static Vector2 Ellipse(Vector2 dir, float a, float b)
    {
        float k = 1f / Mathf.Sqrt(dir.x * dir.x / (a * a) + dir.y * dir.y / (b * b));
        return dir * k;
    }

    private static void Mesh(Transform parent, string name, Material m, float z, Mesh mesh)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0f, z);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
    }

    private static Mesh EllipseMesh(float a, float b)
    {
        const int n = 40;
        var v = new Vector3[n + 1];
        var t = new int[n * 6];
        for (int i = 0; i < n; i++)
        {
            float ang = i * Mathf.PI * 2f / n;
            v[i + 1] = new Vector3(Mathf.Cos(ang) * a, Mathf.Sin(ang) * b, 0f);
            int j = 1 + (i + 1) % n;
            t[i * 6] = 0; t[i * 6 + 1] = j; t[i * 6 + 2] = 1 + i;          // both faces
            t[i * 6 + 3] = 0; t[i * 6 + 4] = 1 + i; t[i * 6 + 5] = j;
        }
        return Finish(v, t);
    }

    private static Mesh TriangleMesh(Vector2 a, Vector2 b, Vector2 c) =>
        Finish(new Vector3[] { a, b, c }, new[] { 0, 1, 2, 0, 2, 1 });

    private static Mesh Finish(Vector3[] v, int[] t)
    {
        var m = new Mesh { vertices = v, triangles = t };
        var nm = new Vector3[v.Length];
        for (int i = 0; i < nm.Length; i++) nm[i] = Vector3.back;
        m.normals = nm;
        m.RecalculateBounds();
        return m;
    }
}
