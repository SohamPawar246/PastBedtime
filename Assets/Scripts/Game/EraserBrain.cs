using UnityEngine;

/// <summary>
/// The Eraser: 25 HP. A rubber creature that rubs out the platforms it slides over while
/// it's awake. Keep it frozen until you're past; lure it under a hazard.
/// </summary>
public class EraserBrain : EnemyBrain
{
    public const float Speed = 1.5f, Reach = 1.9f;
    protected override float BodyRadius => 0.65f;
    protected override float TurnYaw => 90f;

    private bool _struck;

    protected override void Awake()
    {
        base.Awake();
        Health.maxHp = Health.hp = 25f;
    }

    protected override void Think(float dt)
    {
        switch (Mode)
        {
            case State.Hurt:
                if (StateTime >= HurtFor) Enter(State.Approach);
                return;
            case State.Attack:
                Velocity.x = 0f;
                if (!_struck && StateTime >= 0.4f)
                {
                    _struck = true;
                    Attack.Begin(Melee(8f, new Vector2(5f, 3f), 0.4f, "SCRUB!"), new Vector2(0.9f, 0.4f), new Vector2(1.6f, 0.9f));
                }
                if (StateTime >= 0.65f) Attack.End();
                if (StateTime >= 1.15f) Enter(State.Approach);
                return;
        }

        if (Hero != null && !Hero.Dead && HeroInPanel && Mathf.Abs(ToHeroX) < Reach &&
            Mathf.Sign(ToHeroX) == Facing && Mathf.Abs(Hero.transform.position.y - transform.position.y) < 1.2f)
        {
            Enter(State.Attack);
            _struck = false;
            Velocity.x = 0f;
            Clips?.Play("Attack", 0.05f, 1f, restart: true);
            return;
        }

        // patrol, turning at ledges and panel edges, rubbing out the floor it leaves behind: the
        // bridge gets eaten a block at a time, and its own patrol shrinks with it
        Mode = State.Approach;
        Vector3 ahead = transform.position + new Vector3(Facing * 0.8f, 0.4f, 0f);
        bool floorAhead = Physics.Raycast(ahead, Vector3.down, 1.0f, ~0, QueryTriggerInteraction.Ignore);
        if (!floorAhead || transform.position.x <= MinX + BodyRadius + 0.05f || transform.position.x >= MaxX - BodyRadius - 0.05f)
            Facing = -Facing;
        Velocity.x = Facing * Speed;
        Clips?.Play("Move", 0.12f);
        Vector3 behind = transform.position + new Vector3(-Facing * 0.75f, 0.3f, 0f);
        if (Physics.Raycast(behind, Vector3.down, out var hit, 0.8f, ~0, QueryTriggerInteraction.Ignore))
            hit.collider.GetComponentInParent<ErasableBlock>()?.Rub(dt);
    }

    /// <summary>No character controller: the rubber block slides along the floor and is solid when frozen.</summary>
    protected override void Move(float dt)
    {
        dt = Mathf.Min(dt, 1f / 30f);                     // a hitch (a browser stall) can't drop it into the floor
        Vector3 p = transform.position;
        p.x += Velocity.x * dt;
        float reach = 0.35f + Mathf.Max(0f, -Velocity.y * dt);                // long enough not to fall through when dropping fast
        if (FloorUnder(p, reach, out float floorY))
        {
            p.y = floorY;
            Velocity.y = 0f;
            Grounded = true;
        }
        else
        {
            Velocity.y -= Gravity * dt;
            p.y += Velocity.y * dt;
            Grounded = false;
        }
        p.z = 0f;
        p.x = Mathf.Clamp(p.x, MinX + BodyRadius, MaxX - BodyRadius);
        transform.position = p;
        if (Mode == State.Hurt) Velocity.x = Mathf.MoveTowards(Velocity.x, 0f, 25f * dt);
    }

    private static readonly float[] Feet = { -0.3f, 0f, 0.3f };

    /// <summary>Short probes from just above its base, across it (inside its own box, so the box never
    /// answers): the highest floor under any of them. One probe could slip down a seam in the bridge.</summary>
    private bool FloorUnder(Vector3 p, float reach, out float y)
    {
        y = float.NegativeInfinity;
        foreach (float dx in Feet)
            if (Physics.Raycast(p + new Vector3(dx, 0.2f, 0f), Vector3.down, out var floor, reach, ~0, QueryTriggerInteraction.Ignore)
                && !floor.collider.transform.IsChildOf(transform))
                y = Mathf.Max(y, floor.point.y);
        return !float.IsNegativeInfinity(y);
    }
}
