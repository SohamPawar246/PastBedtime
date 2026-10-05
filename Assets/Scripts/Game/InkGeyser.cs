using UnityEngine;

/// <summary>
/// An ink geyser Baron Blot calls up through the floor ("WELL, WELL, WELL..."). First a puddle bubbles
/// up where it'll come through (the warning), then it spouts a column of ink taller than Max and sinks
/// away. Ink like everything else: a puddle in the dark never comes up, and a spout frozen mid-gush is
/// a harmless pillar until it's lit again. It pops Max up for two hearts (it's the boss's), and any Inkie in it gets the same
/// (Blot is never caught in his own).
/// </summary>
[RequireComponent(typeof(Lightable))]
public class InkGeyser : MonoBehaviour
{
    public const float Warn = 0.85f, Spout = 0.45f, Sink = 0.3f, Height = 3.1f, Width = 1f;

    private Lightable _light;
    private Hitbox _hit;
    private Transform _puddle, _column;
    private float _age;
    private bool _spouted, _letters;

    /// <summary>A geyser on the floor at `floor`; `delay` (on its own clock) staggers a row of them, and
    /// only the one with `letters` says BLUB and SPURT! (a row of them would print over each other).</summary>
    public static InkGeyser Spawn(Vector2 floor, GameObject caller, float delay = 0f, bool letters = true)
    {
        var go = new GameObject("InkGeyser");
        go.transform.position = new Vector3(floor.x, floor.y, 0f);
        var l = go.AddComponent<Lightable>();
        l.points = new[] { new Vector3(0f, 0.15f, 0f), new Vector3(0f, 1.3f, 0f), new Vector3(0f, 2.6f, 0f) };
        var g = go.AddComponent<InkGeyser>();
        g._age = -delay;
        g._letters = letters;
        g._hit.ignore = caller;
        PageBuilder.SetLayer(go.transform);
        return g;
    }

    private void Awake()
    {
        _light = GetComponent<Lightable>();
        _hit = new Hitbox(gameObject, Team.Inkie);
        _hit.Landed += (h, hit) =>
        {
            if (h.team == Team.Hero) SfxLettering.Spawn("GLUG!", (Vector2)h.transform.position + Vector2.up * 1.9f, Palette.Paper, 0.9f);
            GameAudio.Play("splat", 0.7f);
            GameEvents.HitStop(0.05f);
        };
        var ink = GameAssets.I != null ? GameAssets.I.ink : null;
        _puddle = Part("Puddle", SphereMesh, ink);
        _column = Part("Column", CylinderMesh, ink);
        _puddle.localScale = Vector3.zero;
        _column.gameObject.SetActive(false);
    }

    private void Update()
    {
        float dt = _light.Delta;
        if (dt <= 0f) return;                          // frozen: a still puddle, or a still pillar of ink
        _age += dt;
        if (_age < 0f) return;                         // a row comes up one after another
        if (_age < Warn)
        {
            // the puddle spreads and bubbles: here it comes
            if (_letters && _age - dt < 0f) SfxLettering.Spawn("BLUB", (Vector2)transform.position + new Vector2(0f, 0.7f), Palette.Paper, 0.55f);
            float spread = Mathf.Clamp01(_age / 0.25f);
            _puddle.localScale = new Vector3(Width * 1.6f * spread, 0.4f * (1f + 0.15f * Mathf.Sin(_age * 30f)), 0.7f);
            return;
        }
        if (!_spouted)
        {
            _spouted = true;
            _column.gameObject.SetActive(true);
            _hit.Begin(new Hit { damage = 10f, hearts = 2, knockback = new Vector2(3f, 11f), stun = 0.45f, word = "GLUG!" },   // Blot's own: 2 hearts
                new Vector2(0f, Height * 0.5f), new Vector2(Width, Height));
            if (_letters) SfxLettering.Spawn("SPURT!", (Vector2)transform.position + new Vector2(0f, Height + 0.4f), Palette.Paper, 0.8f);
            GameAudio.Play("splat", 0.5f);
        }
        float t = _age - Warn;
        // up in a blink, churning, then back down into the floor
        float rise = t < Spout ? Mathf.Clamp01(t / 0.08f) : 1f - Mathf.Clamp01((t - Spout) / Sink);
        float h = Mathf.Max(0.01f, Height * rise);
        _column.localScale = new Vector3(Width * (1f + 0.1f * Mathf.Sin(t * 40f)), h * 0.5f, 0.7f);   // a cylinder is 2 tall
        _column.localPosition = new Vector3(0f, h * 0.5f, 0f);
        _puddle.localScale = new Vector3(Width * 1.6f * rise, 0.4f, 0.7f);
        if (t < Spout) _hit.Tick(1f);
        else if (_hit.Active) _hit.End();
        if (t >= Spout + Sink) Destroy(gameObject);
    }

    private Transform Part(string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    private static Mesh _sphere, _cylinder;
    private static Mesh SphereMesh => _sphere != null ? _sphere : _sphere = Builtin(PrimitiveType.Sphere);
    private static Mesh CylinderMesh => _cylinder != null ? _cylinder : _cylinder = Builtin(PrimitiveType.Cylinder);

    private static Mesh Builtin(PrimitiveType type)
    {
        var go = GameObject.CreatePrimitive(type);
        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        go.SetActive(false);                           // its collider never touches anything
        Destroy(go);
        return mesh;
    }
}
