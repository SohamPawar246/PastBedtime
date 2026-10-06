using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A touch of manga in the printing: focus lines that burst round a heavy blow, speed lines that
/// trail a dash, a dust puff that rolls out from under a hard landing, and the drops of ink an
/// Inkie shakes off as it wakes in the light. White with an ink edge (the drops are just ink) so
/// they read on the dark night sky and on pale paper alike, and gone in a moment. They're the
/// artist's marks, not part of the world, so they run on real time (a hit-stop doesn't hold them).
/// </summary>
public class MangaFx : MonoBehaviour
{
    private const float Z = 0.85f;                  // lines: behind the figures
    private const float FrontZ = -0.7f;             // dust and drops: in front of them
    private enum Kind { Focus, Dash, Puff, Drops }

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
    private struct Bit { public Vector2 p, v; public float r; }
    private Bit[] _bits;

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

    /// <summary>A puff of dust rolling out both ways from under someone landing hard (strength about 0.6 to 1.4).</summary>
    public static void Puff(Vector2 feet, float strength = 1f)
    {
        var fx = Make("DustPuff", Kind.Puff, 0.34f, FrontZ);
        if (fx == null) return;
        int each = strength > 1.1f ? 3 : 2;
        fx._bits = new Bit[each * 2];
        for (int i = 0; i < fx._bits.Length; i++)
        {
            float side = i < each ? -1f : 1f;
            int k = i % each;
            fx._bits[i] = new Bit
            {
                p = feet + new Vector2(side * (0.25f + 0.2f * k), 0.13f + 0.05f * k),
                v = new Vector2(side * Random.Range(2.2f, 3.3f) * strength, Random.Range(0.3f, 0.9f)),
                r = Random.Range(0.18f, 0.26f) * Mathf.Lerp(0.85f, 1.25f, Mathf.InverseLerp(0.6f, 1.4f, strength)),
            };
        }
    }

    /// <summary>Drops of ink flicked off an Inkie as it shakes itself awake in the light.</summary>
    public static void Drops(Vector2 at)
    {
        var fx = Make("InkDrops", Kind.Drops, 0.36f, FrontZ);
        if (fx == null) return;
        fx._bits = new Bit[6];
        for (int i = 0; i < fx._bits.Length; i++)
        {
            float a = Mathf.Lerp(15f, 165f, (i + Random.value) / fx._bits.Length) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            fx._bits[i] = new Bit { p = at + dir * 0.4f, v = dir * Random.Range(2.6f, 4.4f), r = Random.Range(0.05f, 0.09f) };
        }
    }

    private static MangaFx Make(string name, Kind kind, float life, float z = Z)
    {
        var A = GameAssets.I;
        if (A == null) return null;
        var go = new GameObject(name);
        var fx = go.AddComponent<MangaFx>();
        fx._kind = kind;
        fx._life = life;
        fx._ink = Layer(go.transform, "Ink", A.border, z + 0.01f);
        fx._paper = Layer(go.transform, "Paper", A.window, z);
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
        else if (_kind == Kind.Dash) BuildDash();
        else BuildBits(_t / _life, Time.unscaledDeltaTime);
        Apply(_paper, _pv, _pt);
        Apply(_ink, _iv, _it);
    }

    private void BuildBits(float k, float dt)
    {
        for (int i = 0; i < _bits.Length; i++)
        {
            var b = _bits[i];
            if (_kind == Kind.Puff)
            {
                // little clouds rolling out along the ground, swelling then thinning away
                b.v *= Mathf.Exp(-dt * 7f);
                b.p += b.v * dt;
                float size = b.r * (k < 0.25f ? Mathf.Lerp(0.5f, 1.15f, k / 0.25f) : Mathf.Lerp(1.15f, 0f, (k - 0.25f) / 0.75f));
                if (size > 0.01f)
                {
                    Disc(_iv, _it, b.p, size + 0.035f);
                    Disc(_pv, _pt, b.p, size);
                }
            }
            else
            {
                // drops of ink arcing off and drying up
                b.v.y -= 16f * dt;
                b.p += b.v * dt;
                float size = b.r * (1f - k * k);
                if (size > 0.008f) Disc(_iv, _it, b.p, size);
            }
            _bits[i] = b;
        }
    }

    private static void Disc(List<Vector3> v, List<int> t, Vector2 c, float r)
    {
        const int seg = 10;
        int i0 = v.Count;
        v.Add(c);
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            v.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
        for (int i = 0; i < seg; i++)
        {
            int a = i0 + 1 + i, b = i0 + 1 + (i + 1) % seg;
            t.Add(i0); t.Add(a); t.Add(b);
            t.Add(i0); t.Add(b); t.Add(a);                            // both faces, like the lines
        }
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
