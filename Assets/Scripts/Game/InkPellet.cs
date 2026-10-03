using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A Dot-Shot's ink pellet (GDD section 6): 8 units/s, 1 heart. Like everything else it only
/// moves while lit, so pellets hang at the beam's edge and a frozen bullet curtain has dark
/// gaps you can walk through. A pellet that touches a frozen target freezes on contact and
/// resumes if the target wakes. Max's Haymaker sends them back.
/// </summary>
[RequireComponent(typeof(Lightable))]
public class InkPellet : MonoBehaviour
{
    public Vector2 Velocity;
    public Team team = Team.Inkie;
    public GameObject shooter;
    public const float Radius = 0.2f, Speed = 8f;

    private Lightable _light;
    private float _life = 6f, _ignoreShooter = 0.35f;
    private static readonly Collider[] _buffer = new Collider[16];
    private static readonly List<InkPellet> _live = new();

    private void Awake()
    {
        _light = GetComponent<Lightable>();
        _light.points = new[] { Vector3.zero };
    }

    private void OnEnable() => _live.Add(this);
    private void OnDisable() => _live.Remove(this);

    public static InkPellet Fire(Vector2 from, Vector2 dir, GameObject shooter, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "InkPellet";
        go.layer = LayerMask.NameToLayer("Comic");
        Destroy(go.GetComponent<Collider>());
        go.transform.position = new Vector3(from.x, from.y, 0f);
        go.transform.localScale = Vector3.one * Radius * 2f;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        var p = go.AddComponent<InkPellet>();
        p.Velocity = dir.normalized * Speed;
        p.shooter = shooter;
        return p;
    }

    /// <summary>The Haymaker: every pellet inside the box flies back the way it came, now Max's.</summary>
    public static void ReflectIn(Vector2 centre, Vector2 size, GameObject by)
    {
        foreach (var p in _live.ToArray())
        {
            if (p == null || p.team == Team.Hero) continue;
            Vector2 d = (Vector2)p.transform.position - centre;
            if (Mathf.Abs(d.x) > size.x / 2f + Radius || Mathf.Abs(d.y) > size.y / 2f + Radius) continue;
            p.team = Team.Hero;
            p.shooter = by;
            p._ignoreShooter = 0.35f;
            p._life = 6f;
            p.Velocity = new Vector2(Mathf.Sign(p.transform.position.x - by.transform.position.x) * Speed * 1.4f, 0f);
            SfxLettering.Spawn("PING!", (Vector2)p.transform.position + Vector2.up * 0.8f, Palette.Yellow, 0.8f);
            GameAudio.Play("ping", 0.7f);
        }
    }

    private void Update()
    {
        float dt = _light.Delta;
        if (dt <= 0f) return;                               // hangs in the dark, velocity kept
        _life -= dt;
        _ignoreShooter -= dt;
        if (_life <= 0f) { Splat(false); return; }

        Vector2 pos = transform.position;
        int n = Physics.OverlapSphereNonAlloc(new Vector3(pos.x, pos.y, 0f), Radius, _buffer);
        for (int i = 0; i < n; i++)
        {
            var c = _buffer[i];
            if (c.isTrigger) continue;
            var h = c.GetComponentInParent<Health>();
            if (h != null)
            {
                if (h.gameObject == shooter && _ignoreShooter > 0f) continue;
                if (h.Dead || !Health.CanHurt(team, h.team)) continue;
                if (!h.IsAwake) return;                       // touching a frozen target: wait for it
                if (h.Apply(new Hit { damage = 6f, hearts = 1, knockback = new Vector2(Mathf.Sign(Velocity.x) * 3f, 2f), stun = 0.3f,
                                      team = team, source = shooter != null ? shooter : gameObject, word = "SPLAT!" }))
                {
                    if (team == Team.Hero) GameState.I?.HeroLanded();
                    SfxLettering.Spawn("SPLAT!", (Vector2)h.transform.position + Vector2.up * 1.8f, Palette.Yellow, 0.9f);
                }
                Splat(true);
                return;
            }
            if (shooter != null && c.transform.IsChildOf(shooter.transform)) continue;
            if (c.GetComponentInParent<InkPellet>() != null) continue;
            Splat(true);                                    // hit the inked scenery
            return;
        }
        transform.position += (Vector3)(Velocity * dt);
    }

    private void Splat(bool sound)
    {
        if (sound) GameAudio.Play("splat", 0.5f);
        Destroy(gameObject);
    }
}
