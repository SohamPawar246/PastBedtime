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

    private Lightable _light;
    private Transform _rod;
    private Hitbox _crush;
    private float _t, _low, _high, _housing;
    private bool _down;

    /// <summary>A press over the floor point `floor` (the ram comes down to rest on it).</summary>
    public static Press Create(PanelLayout pl, Vector2 floor, float width, float phase)
    {
        var A = GameAssets.I;
        float top = pl.rect.yMax - 0.5f;
        float ramH = 0.9f;
        // the frame: two rails up to a housing under the panel's top border
        foreach (float side in new[] { -1f, 1f })
            PageBuilder.Box(pl.root, "PressRail", new Vector3(floor.x + side * (width * 0.5f + 0.18f), (floor.y + top) * 0.5f, 0.9f),
                new Vector3(0.16f, top - floor.y, 0.3f), A.metal, collider: false);
        PageBuilder.Box(pl.root, "PressHousing", new Vector3(floor.x, top - 0.4f, 0.8f), new Vector3(width + 0.9f, 0.8f, 1.2f), A.metal, collider: false);

        var ram = PageBuilder.Box(pl.root, "PressRam", new Vector3(floor.x, floor.y + 4.4f + ramH * 0.5f, 0f), new Vector3(width, ramH, 1.6f), A.wall);
        // its striking plate: a band of solid ink along the bottom
        var plate = PageBuilder.Box(ram.transform, "Plate", Vector3.zero, Vector3.one, A.border, collider: false);
        plate.transform.localPosition = new Vector3(0f, -0.42f, -0.02f);
        plate.transform.localScale = new Vector3(1.04f, 0.18f, 1.04f);
        var p = ram.AddComponent<Press>();
        var l = ram.AddComponent<Lightable>();
        l.points = new[] { new Vector3(0f, 0f, 0f), new Vector3(-0.45f, -0.5f, 0f), new Vector3(0.45f, -0.5f, 0f) };
        p._light = l;
        p._low = floor.y + ramH * 0.5f;
        p._high = floor.y + 4.4f + ramH * 0.5f;
        p._housing = top - 0.8f;
        p._t = phase * Cycle;
        p._rod = PageBuilder.Box(pl.root, "PressRod", new Vector3(floor.x, top, 0.3f), new Vector3(0.3f, 1f, 0.3f), A.metal, collider: false).transform;
        p._crush = new Hitbox(ram, Team.Hazard);
        p.Place();
        return p;
    }

    private void Update()
    {
        float dt = _light.Delta;
        if (dt <= 0f) return;                                   // frozen: wherever it is, it stays
        _t = (_t + dt) % Cycle;
        Place();
    }

    private void Place()
    {
        float y;
        bool slamming = false;
        if (_t < HoldUp) y = _high;
        else if (_t < HoldUp + Drop)
        {
            float k = (_t - HoldUp) / Drop;
            y = Mathf.Lerp(_high, _low, k * k);                 // falls, faster and faster
            slamming = true;
        }
        else if (_t < HoldUp + Drop + HoldDown) y = _low;
        else y = Mathf.Lerp(_low, _high, Mathf.SmoothStep(0f, 1f, (_t - HoldUp - Drop - HoldDown) / Rise));

        var p = transform.position;
        transform.position = new Vector3(p.x, y, p.z);
        if (_rod != null)
        {
            float rodTop = _housing, rodBottom = y + 0.45f;
            _rod.position = new Vector3(p.x, (rodTop + rodBottom) * 0.5f, 0.3f);
            _rod.localScale = new Vector3(0.3f, Mathf.Max(0.05f, rodTop - rodBottom), 0.3f);
        }

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
