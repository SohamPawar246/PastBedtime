using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A touch of manga in the printing: focus lines that burst round a heavy blow, and speed lines that
/// trail a dash. Drawn on the page behind the figures, white with an ink edge so they read on the
/// dark night sky and on pale paper alike, and gone in a moment. They're the artist's marks, not
/// part of the world, so they run on real time (a hit-stop doesn't hold them).
/// </summary>
public class MangaFx : MonoBehaviour
{
    private const float Z = 0.85f;
    private enum Kind { Focus, Dash }

    private Kind _kind;
    private Vector2 _at;
    private Rect _clip;
    private float _t, _life, _strength, _facing;
    private Transform _who;
    private Vector2 _start;
    private Mesh _paper, _ink;
    private readonly List<Vector3> _pv = new(), _iv = new();
    private readonly List<int> _pt = new(), _it = new();
    private Line[] _lines;

    private struct Line { public float angle, inner, width, y, length; }

    /// <summary>Focus lines round a heavy hit, out to the edges of the panel it's in.</summary>
    public static void Focus(Vector2 at, float strength = 1f)
    {
        if (!PanelAt(at, out var rect)) return;
        var fx = Make("FocusLines", Kind.Focus, 0.32f);
        fx._at = at;
        fx._clip = rect;
        fx._strength = strength;
        int n = Mathf.RoundToInt(34 + 14 * strength);
        fx._lines = new Line[n];
        for (int i = 0; i < n; i++)
            fx._lines[i] = new Line
            {
                angle = (i + Random.Range(-0.35f, 0.35f)) * Mathf.PI * 2f / n,
                inner = Random.Range(1.5f, 2.7f) * Mathf.Lerp(0.85f, 1.15f, strength - 0.5f),
                width = Random.Range(0.05f, 0.17f) * Mathf.Clamp(strength, 0.7f, 1.4f),
            };
    }

    /// <summary>Speed lines streaming behind someone dashing, for as long as the dash lasts.</summary>
    public static void Dash(Transform who, float facing, float seconds)
    {
        if (!PanelAt(who.position, out var rect)) return;
        var fx = Make("SpeedLines", Kind.Dash, seconds + 0.2f);
        fx._who = who;
        fx._facing = facing;
        fx._start = who.position;
        fx._clip = rect;
        fx._lines = new Line[7];
        for (int i = 0; i < fx._lines.Length; i++)
            fx._lines[i] = new Line
            {
                y = Mathf.Lerp(0.2f, 1.75f, (i + Random.Range(0.1f, 0.9f)) / fx._lines.Length),
                width = Random.Range(0.045f, 0.1f),
                length = Random.Range(0.55f, 1f),
                inner = Random.Range(0.25f, 0.6f),
            };
    }

    private static MangaFx Make(string name, Kind kind, float life)
    {
        var A = GameAssets.I;
        if (A == null) return null;
        var go = new GameObject(name);
        var fx = go.AddComponent<MangaFx>();
        fx._kind = kind;
        fx._life = life;
        fx._ink = Layer(go.transform, "Ink", A.border, Z + 0.01f);
        fx._paper = Layer(go.transform, "Paper", A.window, Z);
        PageBuilder.SetLayer(go.transform);
        return fx;
    }

    private static Mesh Layer(Transform parent, string name, Material m, float z)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0f, z);
        var mesh = new Mesh { name = name };
        mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        return mesh;
    }

    private static bool PanelAt(Vector2 p, out Rect rect)
    {
        rect = default;
        var layout = PageManager.I != null ? PageManager.I.Layout : null;
        if (layout == null) return false;
        foreach (var tier in layout.tiers)
            foreach (var pl in tier.panels)
                if (pl.rect.Contains(p))
                {
                    rect = pl.rect;
                    return true;
                }
        return false;
    }

    private void Update()
    {
        _t += Time.unscaledDeltaTime;
        if (_t >= _life)
        {
            Destroy(gameObject);
            return;
        }
        _pv.Clear(); _iv.Clear(); _pt.Clear(); _it.Clear();
        if (_kind == Kind.Focus) BuildFocus(_t / _life);
        else BuildDash();
        Apply(_paper, _pv, _pt);
        Apply(_ink, _iv, _it);
    }

    private void BuildFocus(float k)
    {
        // the lines shoot in at once, then draw back to the panel's edge as they thin out
        var clip = new Rect(_clip.xMin + 0.12f, _clip.yMin + 0.12f, _clip.width - 0.24f, _clip.height - 0.24f);
        float retreat = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1f, k));
        foreach (var l in _lines)
        {
            var dir = new Vector2(Mathf.Cos(l.angle), Mathf.Sin(l.angle));
            float reach = ToEdge(_at, dir, clip);
            float inner = Mathf.Lerp(l.inner, reach, retreat * 0.85f);
            if (reach - inner < 0.3f) continue;
            float w = l.width * (1f - retreat * 0.7f);
            Wedge(_at + dir * inner, _at + dir * reach, w, 0.035f);
        }
    }

    private void BuildDash()
    {
        if (_who == null) return;
        float k = Mathf.InverseLerp(_life - 0.2f, _life, _t);          // after the dash: they draw in after him
        Vector2 now = _who.position;
        float back = Mathf.Abs(now.x - _start.x) + 0.6f;
        foreach (var l in _lines)
        {
            float x0 = now.x - _facing * l.inner;
            float len = Mathf.Max(0f, back * l.length - l.inner) * (1f - k);
            if (len < 0.15f) continue;
            float x1 = x0 - _facing * len;
            x1 = Mathf.Clamp(x1, _clip.xMin + 0.1f, _clip.xMax - 0.1f);
            x0 = Mathf.Clamp(x0, _clip.xMin + 0.1f, _clip.xMax - 0.1f);
            float y = now.y + l.y;
            Wedge(new Vector2(x0, y), new Vector2(x1, y), l.width * (1f - k * 0.5f), 0.03f);
        }
    }

    /// <summary>A line tapering to a point at `tip`, `w` wide at `tail`, with an ink edge round it.</summary>
    private void Wedge(Vector2 tip, Vector2 tail, float w, float edge)
    {
        var d = (tail - tip).normalized;
        var n = new Vector2(-d.y, d.x);
        Tri(_pv, _pt, tip, tail + n * (w / 2f), tail - n * (w / 2f));
        Tri(_iv, _it, tip - d * edge * 2f, tail + n * (w / 2f + edge), tail - n * (w / 2f + edge));
    }

    private static void Tri(List<Vector3> v, List<int> t, Vector2 a, Vector2 b, Vector2 c)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
        t.Add(i); t.Add(i + 2); t.Add(i + 1);                        // both faces: no culling surprises
    }

    private static void Apply(Mesh m, List<Vector3> v, List<int> t)
    {
        m.Clear();
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        var normals = new Vector3[v.Count];
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.back;
        m.normals = normals;
        m.RecalculateBounds();
    }

    /// <summary>How far a ray from `p` along `dir` runs before it leaves the rect.</summary>
    private static float ToEdge(Vector2 p, Vector2 dir, Rect r)
    {
        float tx = dir.x > 1e-4f ? (r.xMax - p.x) / dir.x : dir.x < -1e-4f ? (r.xMin - p.x) / dir.x : float.MaxValue;
        float ty = dir.y > 1e-4f ? (r.yMax - p.y) / dir.y : dir.y < -1e-4f ? (r.yMin - p.y) / dir.y : float.MaxValue;
        return Mathf.Max(0f, Mathf.Min(tx, ty));
    }
}
