using UnityEngine;

/// <summary>
/// Blot's rising ink (Act 3): a flood that fills a panel from the bottom. It is ink, so it obeys
/// the light like everything else: wherever the beam, Mom's wedge or a flare touches its surface,
/// it rises and its waves roll; in the dark it holds perfectly still, waves and all. Lighting the
/// low steps you need is what brings it up, so keep Max at the beam's bottom rim. Falling in
/// costs a heart; the panel's ink drains back down for the next try.
/// </summary>
public class InkFlood : MonoBehaviour
{
    public float Start, Level, Top, Rise = 0.55f;
    public bool Lit { get; private set; }
    public PanelLayout Panel { get; private set; }

    private const int Columns = 48;
    private const float Probe = 0.5f;            // surface samples for "is it lit?"
    private Mesh _mesh, _crest;
    private Vector3[] _verts, _crestVerts;
    private float _clock;                        // the waves' own time: it only runs while lit

    public static InkFlood Create(PanelLayout pl, float start, float top, float rise, Material ink)
    {
        var go = new GameObject("InkFlood");
        go.transform.SetParent(pl.root, false);
        var f = go.AddComponent<InkFlood>();
        f.Panel = pl;
        f.Start = f.Level = pl.rect.yMin + start;
        f.Top = pl.rect.yMin + top;
        f.Rise = rise;
        f._mesh = new Mesh { name = "Flood" };
        f._mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = f._mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = ink;
        // a white crest along the waves, the way an inker shows the shine on wet ink
        var crest = new GameObject("Crest");
        crest.transform.SetParent(go.transform, false);
        f._crest = new Mesh { name = "FloodCrest" };
        f._crest.MarkDynamic();
        crest.AddComponent<MeshFilter>().sharedMesh = f._crest;
        crest.AddComponent<MeshRenderer>().sharedMaterial = GameAssets.I != null ? GameAssets.I.paper : ink;
        f.Build();
        return f;
    }

    /// <summary>The panel restarted: the ink drains back to where it began.</summary>
    public void Drain()
    {
        Level = Start;
        Build();
    }

    /// <summary>Is a lane point under the ink?</summary>
    public bool Covers(Vector2 p) => p.x >= Panel.rect.xMin && p.x <= Panel.rect.xMax && p.y < Level - 0.15f;

    private void Update()
    {
        var lf = LightField.I;
        Lit = false;
        // only while the reader is actually reading this row: not under a title card, a BUSTED, a
        // page turn, or while Max is still on another tier
        var pm = PageManager.I;
        var hero = HeroController.I;
        if (pm == null || pm.Busy || hero == null || hero.Locked || pm.Layout == null ||
            pm.Layout.tiers[pm.TierIndex] != pm.Layout.tiers[Panel.tier]) return;
        if (lf != null)
            for (float x = Panel.rect.xMin; x <= Panel.rect.xMax && !Lit; x += Probe)
                Lit = lf.IsLit(new Vector2(x, Level));
        if (!Lit) return;                        // frozen: not a ripple
        float dt = Time.deltaTime;
        _clock += dt;
        Level = Mathf.Min(Top, Level + Rise * dt);
        Build();
    }

    private float Wave(float x) =>
        0.12f * Mathf.Sin(x * 1.7f + _clock * 2.6f) + 0.07f * Mathf.Sin(x * 3.9f - _clock * 3.4f);

    private void Build()
    {
        var r = Panel.rect;
        if (_verts == null) _verts = new Vector3[(Columns + 1) * 2];
        float bottom = r.yMin - 0.3f;
        for (int i = 0; i <= Columns; i++)
        {
            float x = Mathf.Lerp(r.xMin, r.xMax, i / (float)Columns);
            _verts[i * 2] = new Vector3(x, bottom, -1.2f);                     // in front of the rooftops' faces
            _verts[i * 2 + 1] = new Vector3(x, Mathf.Max(bottom, Level + Wave(x)), -1.2f);
        }
        if (_crestVerts == null) _crestVerts = new Vector3[(Columns + 1) * 2];
        for (int i = 0; i <= Columns; i++)
        {
            float x = _verts[i * 2].x, y = _verts[i * 2 + 1].y;
            float k = 0.5f + 0.5f * Mathf.Sin(x * 2.3f + _clock * 1.3f);       // thick on the crests, thin in the troughs
            _crestVerts[i * 2] = new Vector3(x, y - Mathf.Lerp(0.02f, 0.09f, k), -1.22f);
            _crestVerts[i * 2 + 1] = new Vector3(x, y + 0.02f, -1.22f);
        }
        Fill(_mesh, _verts);
        Fill(_crest, _crestVerts);
    }

    private static void Fill(Mesh m, Vector3[] verts)
    {
        m.vertices = verts;
        if (m.triangles.Length == 0)
        {
            var tris = new int[Columns * 6];
            for (int i = 0; i < Columns; i++)
            {
                int a = i * 2;
                tris[i * 6 + 0] = a; tris[i * 6 + 1] = a + 1; tris[i * 6 + 2] = a + 2;
                tris[i * 6 + 3] = a + 2; tris[i * 6 + 4] = a + 1; tris[i * 6 + 5] = a + 3;
            }
            m.triangles = tris;
        }
        m.RecalculateNormals();
        m.RecalculateBounds();
    }
}
