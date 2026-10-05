using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An attack's active area, owned by whoever swings it. While active and its owner is
/// awake it hits every awake <see cref="Health"/> it overlaps once per swing. An attack that
/// freezes mid-swing stays armed: when it wakes, it lands on whatever was lit in front of it
/// ("choreography", GDD section 6).
/// </summary>
public class Hitbox
{
    public readonly GameObject owner;
    public readonly Team team;
    public readonly Lightable light;
    public Vector2 offset;        // from the owner, x mirrored by facing
    public Vector2 size = new(1.2f, 1f);
    public Hit hit;
    /// <summary>Never hits this one (whoever threw it: Blot's own ink wave).</summary>
    public GameObject ignore;
    public bool Active { get; private set; }

    /// <summary>Called for every target this swing lands on.</summary>
    public event Action<Health, Hit> Landed;

    private readonly HashSet<Health> _struck = new();
    private static readonly Collider[] _buffer = new Collider[32];

    public Hitbox(GameObject owner, Team team)
    {
        this.owner = owner;
        this.team = team;
        light = owner.GetComponent<Lightable>();
    }

    public void Begin(Hit h, Vector2 offset, Vector2 size)
    {
        hit = h;
        hit.team = team;
        hit.source = owner;
        this.offset = offset;
        this.size = size;
        _struck.Clear();
        Active = true;
    }

    public void End() => Active = false;

    public Vector2 Centre(float facing) =>
        (Vector2)owner.transform.position + new Vector2(offset.x * facing, offset.y);

    /// <summary>Checks for targets; call every frame while the swing is out.</summary>
    public int Tick(float facing)
    {
        if (!Active || (light != null && !light.IsAwake)) return 0;
        Vector2 c = Centre(facing);
        int n = Physics.OverlapBoxNonAlloc(new Vector3(c.x, c.y, 0f), new Vector3(size.x / 2f, size.y / 2f, 1.5f), _buffer);
        int landed = 0;
        for (int i = 0; i < n; i++)
        {
            var h = _buffer[i].GetComponentInParent<Health>();
            if (h == null || h.gameObject == owner || h.gameObject == ignore || _struck.Contains(h) || h.Dead) continue;
            if (!Health.CanHurt(team, h.team) || !h.IsAwake) continue;
            var dealt = hit;
            float dx = h.transform.position.x - owner.transform.position.x;
            float away = Mathf.Abs(dx) > 0.05f ? Mathf.Sign(dx) : facing;   // knockback always pushes away
            dealt.knockback.x = Mathf.Abs(hit.knockback.x) * away;
            if (h.Apply(dealt, out var asLanded))
            {
                _struck.Add(h);
                landed++;
                Landed?.Invoke(h, asLanded);                           // as it landed: armour makes it a CLANK!
            }
        }
        return landed;
    }
}
