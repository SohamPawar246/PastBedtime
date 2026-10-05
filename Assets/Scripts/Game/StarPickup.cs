using TMPro;
using UnityEngine;

/// <summary>A gold star (5 per page): Max picks it up when he touches it, awake.</summary>
public class StarPickup : MonoBehaviour
{
    private Lightable _light;
    private float _spin;
    /// <summary>Which of its page's stars this is (stars found once aren't drawn again).</summary>
    public int Index;

    public static GameObject Create(Transform parent, Vector2 at, Material m, int index)
    {
        var go = new GameObject("Star");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(at.x, at.y, -2.4f);     // in front of the captions (-2.2), behind the borders (-2.5)
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = StarMesh();
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        var l = go.AddComponent<Lightable>();
        l.points = new[] { Vector3.zero };
        go.AddComponent<StarPickup>().Index = index;
        return go;
    }

    private void Awake() => _light = GetComponent<Lightable>();

    private void Update()
    {
        float dt = _light.Delta;
        _spin += dt * 120f;
        transform.rotation = Quaternion.Euler(0f, _spin, 0f);
        var hero = HeroController.I;
        if (hero == null || !hero.Light.IsAwake) return;
        Vector2 d = (Vector2)hero.transform.position + Vector2.up * 0.9f - (Vector2)transform.position;
        if (Mathf.Abs(d.x) < 0.7f && Mathf.Abs(d.y) < 1.1f)
        {
            var gs = GameState.I;
            gs?.FindStar(Index);
            SfxLettering.Spawn("TWINKLE!", (Vector2)transform.position + Vector2.up, Palette.Yellow, 0.8f);
            GameAudio.Play("star", 0.7f);
            Destroy(gameObject);
        }
    }

    private static Mesh _star;
    private static Mesh StarMesh()
    {
        if (_star != null) return _star;
        // a chunky 5-point star, extruded so the outline pass has an edge to draw
        var v = new System.Collections.Generic.List<Vector3>();
        var t = new System.Collections.Generic.List<int>();
        const float d = 0.12f;
        for (int side = 0; side < 2; side++)
        {
            float z = side == 0 ? -d : d;
            v.Add(new Vector3(0f, 0f, z));
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 0.45f : 0.19f;
                v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z));
            }
        }
        for (int i = 0; i < 10; i++)
        {
            int a = 1 + i, b = 1 + (i + 1) % 10;
            t.AddRange(new[] { 0, b, a });                // front (facing -z, toward the camera)
            t.AddRange(new[] { 11, 11 + a, 11 + b });     // back
            t.AddRange(new[] { a, b, 11 + b, a, 11 + b, 11 + a });  // rim
        }
        _star = new Mesh { name = "Star" };
        _star.SetVertices(v);
        _star.SetTriangles(t, 0);
        _star.RecalculateNormals();
        return _star;
    }
}
