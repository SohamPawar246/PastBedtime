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

    [SerializeField] private Transform _crest;     // a curling wave profile, extruded through the lane
    private Lightable _light;
    private Hitbox _hit;
    private float _dir = 1f, _minX, _maxX, _age;

    public static InkWave Spawn(Vector2 at, float dir, float minX, float maxX, GameObject thrower = null)
    {
        var w = Instantiate(GameAssets.I.inkWave, new Vector3(at.x, at.y, 0f), Quaternion.identity);
        w.name = "InkWave";
        w._dir = dir >= 0f ? 1f : -1f;
        w._minX = minX;
        w._maxX = maxX;
        w._crest.localScale = new Vector3(w._dir, 1f, 1f);
        w._hit.ignore = thrower;
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
}
