using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An ink wave Baron Blot sends rolling along the floor with a sweep of his cane (GDD section 6).
/// It is ink like everything else: it freezes in the dark, so pull the light off it, or jump it.
/// It knocks Max back two hearts (it's the boss's) and slams any Inkie in its way; it breaks on the panel's edge or
/// where the floor ends.
/// </summary>
[RequireComponent(typeof(Lightable))]
public class InkWave : MonoBehaviour
{
    public const float Speed = 5f, Life = 5f;

    private Lightable _light;
    private Hitbox _hit;
    private Transform _crest;
    private float _dir = 1f, _minX, _maxX, _age;

    public static InkWave Spawn(Vector2 at, float dir, float minX, float maxX, GameObject thrower = null)
    {
        var go = new GameObject("InkWave");
        go.transform.position = new Vector3(at.x, at.y, 0f);
        var l = go.AddComponent<Lightable>();
        l.points = new[] { new Vector3(0f, 0.6f, 0f), new Vector3(-0.65f, 0.25f, 0f), new Vector3(0.65f, 0.25f, 0f) };
        var w = go.AddComponent<InkWave>();
        w._dir = dir >= 0f ? 1f : -1f;
        w._minX = minX;
        w._maxX = maxX;
        w._crest.localScale = new Vector3(w._dir, 1f, 1f);
        w._hit.ignore = thrower;
        PageBuilder.SetLayer(go.transform);
        return w;
    }

    private void Awake()
    {
        _light = GetComponent<Lightable>();
        _hit = new Hitbox(gameObject, Team.Inkie);
        _hit.Begin(new Hit { damage = 10f, hearts = 2, knockback = new Vector2(7f, 6f), stun = 0.5f, word = "SPLASH!" },   // Blot's own: 2 hearts
            new Vector2(0.15f, 0.65f), new Vector2(1.45f, 1.3f));
        _hit.Landed += (h, hit) =>
        {
            SfxLettering.Spawn("SPLASH!", (Vector2)h.transform.position + Vector2.up * 1.8f, Palette.Paper, 1f, burst: true);
            GameAudio.Play("splat", 0.7f);
            GameEvents.HitStop(0.05f);
        };
        var crest = new GameObject("Crest");
        crest.transform.SetParent(transform, false);
        crest.AddComponent<MeshFilter>().sharedMesh = CrestMesh;
        var r = crest.AddComponent<MeshRenderer>();
        r.sharedMaterial = GameAssets.I != null ? GameAssets.I.ink : null;
        _crest = crest.transform;
    }

    private void Update()
    {
        float dt = _light.Delta;
        if (dt <= 0f) return;                          // frozen: a still wall of ink, harmless till it's lit
        _age += dt;
        var p = transform.position;
        p.x += _dir * Speed * dt;
        transform.position = p;
        _hit.Tick(_dir);
        _crest.localScale = new Vector3(_dir, 1f + 0.07f * Mathf.Sin(_age * 17f), 1f);   // it churns as it rolls
        bool floor = Physics.Raycast(p + new Vector3(_dir * 0.4f, 0.3f, 0f), Vector3.down, 0.8f, ~0, QueryTriggerInteraction.Ignore);
        if (_age > Life || p.x < _minX + 0.5f || p.x > _maxX - 0.5f || !floor) Break();
    }

    private void Break()
    {
        SfxLettering.Spawn("SPLSH!", (Vector2)transform.position + new Vector2(_dir * 0.4f, 1.1f), Palette.Paper, 0.7f);
        GameAudio.Play("splat", 0.45f);
        Destroy(gameObject);
    }

    // ---- the crest: a curling wave profile, extruded through the lane --------------------------------

    private static Mesh _mesh;
    private static Mesh CrestMesh => _mesh != null ? _mesh : _mesh = BuildCrest();

    private static Mesh BuildCrest()
    {
        // the profile, counter-clockwise, lip toward +x: rises from the back, curls over, falls in front
        var p = new[]
        {
            new Vector2(-0.8f, 0f), new Vector2(0.72f, 0f), new Vector2(0.6f, 0.1f), new Vector2(0.45f, 0.26f),
            new Vector2(0.38f, 0.45f), new Vector2(0.45f, 0.6f), new Vector2(0.6f, 0.64f), new Vector2(0.7f, 0.77f),
            new Vector2(0.6f, 0.93f), new Vector2(0.38f, 1.02f), new Vector2(0.12f, 0.94f), new Vector2(-0.12f, 0.68f),
            new Vector2(-0.42f, 0.32f),
        };
        const float d = 0.5f, k = 1.3f;                                  // k: the crest's size
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var ears = Triangulate(p);
        // the two caps (front, toward the camera at -z; and back)
        foreach (float z in new[] { -d, d })
        {
            int b = verts.Count;
            foreach (var v in p) verts.Add(new Vector3(v.x * k, v.y * k, z));
            for (int i = 0; i < ears.Count; i += 3)
                AddTri(verts, tris, b + ears[i], b + ears[i + 1], b + ears[i + 2], new Vector3(0f, 0f, Mathf.Sign(z)));
        }
        // the sides, one flat quad per edge
        for (int i = 0; i < p.Length; i++)
        {
            Vector2 a = p[i], c = p[(i + 1) % p.Length];
            var n = new Vector3(c.y - a.y, a.x - c.x, 0f).normalized;            // outward for a CCW outline
            int b = verts.Count;
            verts.Add(new Vector3(a.x * k, a.y * k, -d));
            verts.Add(new Vector3(c.x * k, c.y * k, -d));
            verts.Add(new Vector3(c.x * k, c.y * k, d));
            verts.Add(new Vector3(a.x * k, a.y * k, d));
            AddTri(verts, tris, b, b + 1, b + 2, n);
            AddTri(verts, tris, b, b + 2, b + 3, n);
        }
        var mesh = new Mesh { name = "InkWaveCrest" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Adds a triangle wound so its face points along `normal`.</summary>
    private static void AddTri(List<Vector3> v, List<int> t, int a, int b, int c, Vector3 normal)
    {
        if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), normal) < 0f) (b, c) = (c, b);
        t.Add(a);
        t.Add(b);
        t.Add(c);
    }

    /// <summary>Ear clipping for a simple counter-clockwise polygon.</summary>
    private static List<int> Triangulate(Vector2[] p)
    {
        var idx = new List<int>();
        for (int i = 0; i < p.Length; i++) idx.Add(i);
        var tris = new List<int>();
        for (int guard = 0; idx.Count > 3 && guard < 500; guard++)
        {
            for (int i = 0; i < idx.Count; i++)
            {
                int a = idx[(i + idx.Count - 1) % idx.Count], b = idx[i], c = idx[(i + 1) % idx.Count];
                Vector2 ab = p[b] - p[a], bc = p[c] - p[b];
                if (ab.x * bc.y - ab.y * bc.x <= 0f) continue;                    // a reflex corner
                bool ear = true;
                foreach (int j in idx)
                    if (j != a && j != b && j != c && InTriangle(p[j], p[a], p[b], p[c])) { ear = false; break; }
                if (!ear) continue;
                tris.Add(a);
                tris.Add(b);
                tris.Add(c);
                idx.RemoveAt(i);
                break;
            }
        }
        if (idx.Count == 3) tris.AddRange(idx);
        return tris;
    }

    private static bool InTriangle(Vector2 q, Vector2 a, Vector2 b, Vector2 c)
    {
        float s1 = (b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x);
        float s2 = (c.x - b.x) * (q.y - b.y) - (c.y - b.y) * (q.x - b.x);
        float s3 = (a.x - c.x) * (q.y - c.y) - (a.y - c.y) * (q.x - c.x);
        return s1 >= 0f && s2 >= 0f && s3 >= 0f;
    }
}
