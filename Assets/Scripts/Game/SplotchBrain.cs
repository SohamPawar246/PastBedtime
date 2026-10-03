using UnityEngine;

/// <summary>
/// Splotch, the ink bat: 8 HP. Erratic flight, dives at the hero. Frozen in mid-air it's a
/// solid stepping stone; keep Max at the beam's bottom rim so the bat under him stays dark.
/// </summary>
public class SplotchBrain : EnemyBrain
{
    public const float DiveSpeed = 9f, Notice = 6.5f;
    protected override bool Flies => true;
    protected override float BodyRadius => 0.6f;
    protected override float TurnYaw => 25f;

    private Vector2 _anchor, _diveDir;
    private float _wander, _cooldown = 1f;

    protected override void Awake()
    {
        base.Awake();
        Health.maxHp = Health.hp = 8f;
        _wander = Random.Range(0f, 10f);
    }

    protected override void Start()
    {
        base.Start();
        _anchor = transform.position;
        Clips?.Play("Fly", 0f);
    }

    protected override void Think(float dt)
    {
        _cooldown -= dt;
        switch (Mode)
        {
            case State.Hurt:
                Velocity = Vector2.MoveTowards(Velocity, Vector2.zero, 20f * dt);
                if (StateTime >= HurtFor) { Enter(State.Recover); Clips?.Play("Fly", 0.1f); }
                return;
            case State.Windup:                              // a shiver before the dive
                Velocity = Vector2.zero;
                if (StateTime >= 0.35f)
                {
                    Vector2 target = Hero != null ? (Vector2)Hero.transform.position + Vector2.up * 1.0f : (Vector2)transform.position + Vector2.down;
                    _diveDir = (target - (Vector2)transform.position).normalized;
                    Enter(State.Attack);
                    Clips?.Play("Dive", 0.05f, 1f, restart: true);
                    Attack.Begin(Melee(5f, new Vector2(4f, 3f), 0.3f, "SKREE!"), Vector2.zero, new Vector2(1.1f, 0.8f));
                }
                return;
            case State.Attack:
                Velocity = _diveDir * DiveSpeed;
                if (StateTime >= 0.9f || (Hero != null && transform.position.y < Hero.transform.position.y + 0.3f))
                {
                    Attack.End();
                    Enter(State.Recover);
                    Clips?.Play("Fly", 0.1f);
                    _cooldown = 1.6f;
                }
                return;
            case State.Recover:
                Velocity = Vector2.ClampMagnitude((_anchor - (Vector2)transform.position) * 3f, 4f);
                if (((Vector2)transform.position - _anchor).sqrMagnitude < 0.2f) Enter(State.Idle);
                return;
        }

        // erratic hover round its perch
        _wander += dt;
        Vector2 hover = _anchor + new Vector2(Mathf.Sin(_wander * 1.1f) * 2f + Mathf.Sin(_wander * 2.9f) * 0.5f,
                                              Mathf.Sin(_wander * 1.7f) * 0.6f + Mathf.Sin(_wander * 4.3f) * 0.15f);
        Velocity = Vector2.ClampMagnitude((hover - (Vector2)transform.position) * 3f, 5f);
        if (Mathf.Abs(Velocity.x) > 0.3f) Facing = Mathf.Sign(Velocity.x);
        Clips?.Play("Fly", 0.1f);

        if (_cooldown <= 0f && Hero != null && !Hero.Dead && HeroInPanel && Mathf.Abs(ToHeroX) < Notice &&
            Hero.transform.position.y < transform.position.y - 0.8f)
        {
            FaceHero();
            Enter(State.Windup);
        }
    }

    protected override void OnHurt(Hit hit)
    {
        base.OnHurt(hit);
        Attack.End();
    }

    protected override void DeadTick(float dt)
    {
        Velocity.y -= Gravity * dt;                         // drops out of the air
        transform.position += (Vector3)(Velocity * dt);
        Model.Rotate(0f, 0f, 400f * dt, Space.Self);
        base.DeadTick(0f);
    }
}
