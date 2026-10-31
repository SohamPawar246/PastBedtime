using UnityEngine;

/// <summary>
/// The Inkworks' stamping press (the proposal's moving machinery): a ram that slams down and rises
/// again, but only while it's lit, like everything else in the comic. Frozen high it's safe to walk
/// under; frozen low it's a step; awake it flattens whatever is under it, Inkies included (lure one
/// beneath it for some choreography).
///   up 1.1 s, slam 0.16 s (KA-CHUNK), down 0.5 s, rise 1.0 s
/// </summary>
public class Press : MonoBehaviour
{
    public const float HoldUp = 1.1f, Drop = 0.16f, HoldDown = 0.5f, Rise = 1.0f;
    private const float Cycle = HoldUp + Drop + HoldDown + Rise;

    [SerializeField] private Transform _rod;
    [Tooltip("The ram's resting heights (world), and the underside of its housing.")]
    [SerializeField] private float _low, _high, _housing;
    [Tooltip("Where in its cycle it is when the page is drawn (seconds).")]
    [SerializeField] private float _t;
    private Lightable _light;
    private Hitbox _crush;
    private bool _down;

    private void Awake()
    {
        _light = GetComponent<Lightable>();
        _crush = new Hitbox(gameObject, Team.Hazard);
        Place();
    }

    private void Update()
    {
        float dt = _light.Delta;
        if (dt <= 0f) return;                                   // frozen: wherever it is, it stays
        _t = (_t + dt) % Cycle;
        Place();
    }

    /// <summary>The ram's height at this point in its cycle.</summary>
    private float Height(out bool slamming)
    {
        slamming = false;
        if (_t < HoldUp) return _high;
        if (_t < HoldUp + Drop)
        {
            float k = (_t - HoldUp) / Drop;
            slamming = true;
            return Mathf.Lerp(_high, _low, k * k);              // falls, faster and faster
        }
        if (_t < HoldUp + Drop + HoldDown) return _low;
        return Mathf.Lerp(_low, _high, Mathf.SmoothStep(0f, 1f, (_t - HoldUp - Drop - HoldDown) / Rise));
    }

    /// <summary>The ram at height y, its rod reaching down to it from the housing.</summary>
    private void Pose(float y)
    {
        var p = transform.position;
        transform.position = new Vector3(p.x, y, p.z);
        if (_rod != null)
        {
            float rodTop = _housing, rodBottom = y + 0.45f;
            _rod.position = new Vector3(p.x, (rodTop + rodBottom) * 0.5f, 0.3f);
            _rod.localScale = new Vector3(0.3f, Mathf.Max(0.05f, rodTop - rodBottom), 0.3f);
        }
    }

    private void Place()
    {
        float y = Height(out bool slamming);
        Pose(y);

        // crushing: everything awake under the ram as it comes down
        if (slamming && !_crush.Active)
            _crush.Begin(new Hit { damage = 40f, hearts = 1, knockback = new Vector2(7f, 5f), stun = 0.8f, heavy = true, word = "KA-CHUNK!" },
                new Vector2(0f, -0.35f), new Vector2(transform.localScale.x * 0.92f, 1.1f));
        if (_crush.Active) _crush.Tick(1f);
        bool landed = !slamming && Mathf.Approximately(y, _low);
        if (landed && !_down)
        {
            _crush.End();
            SfxLettering.Spawn("KA-CHUNK!", (Vector2)transform.position + Vector2.up * 1.2f, Palette.Paper, 0.75f);
            GameAudio.Play("slam", 0.45f);
            GameEvents.Impact(0.25f);
        }
        if (!slamming && _crush.Active) _crush.End();
        _down = landed;
    }
}
