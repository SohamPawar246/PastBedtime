using UnityEngine;

/// <summary>
/// Shared Inkie behaviour (GDD section 6): a small state machine (Idle, Approach, Wind-up,
/// Attack, Recover, Hurt) driven by the Inkie's own clock, so freezing pauses it exactly.
///  - On waking, 0.3 s of "reorient" before acting, so re-entry is never an instant hit.
///  - In green light Inkies regain 4 HP a second, and a hurt Inkie steps toward green light.
///  - Inkies can't cross gutters: each panel is its own arena.
///  - Attacks have friendly fire, and an Inkie frozen mid-swing keeps its hitbox armed.
/// Subclasses fill in <see cref="Think"/>.
/// </summary>
[RequireComponent(typeof(Lightable), typeof(Health))]
public abstract class EnemyBrain : MonoBehaviour
{
    public enum State { Idle, Approach, Windup, Attack, Recover, Hurt, Dead }

    public const float Reorient = 0.3f, Gravity = 30f, GreenHeal = 4f;

    [Tooltip("Panel bounds on the lane: this Inkie never leaves them.")]
    public float MinX = -1e4f, MaxX = 1e4f;
    /// <summary>The panel this Inkie lives in (set by the page builder).</summary>
    [System.NonSerialized] public PanelLayout Panel;
    public float Facing = -1f;
    public Vector2 Velocity;

    public State Mode { get; protected set; }
    public bool Grounded { get; protected set; }
    public Lightable Light { get; private set; }
    public Health Health { get; private set; }
    public ClipPlayer Clips { get; private set; }

    protected CharacterController Body;
    protected Transform Model;
    protected float StateTime;
    protected Hitbox Attack;
    protected virtual bool Flies => false;
    /// <summary>HP a second this Inkie regains in green light (Baron Blot: 2).</summary>
    protected virtual float GreenHealRate => GreenHeal;
    protected virtual float BodyRadius => Body != null ? Body.radius : 0.4f;
    protected virtual float TurnYaw => 70f;

    protected HeroController Hero => HeroController.I;
    protected float ToHeroX => Hero != null ? Hero.transform.position.x - transform.position.x : 999f;
    protected float HeroDistance => Hero != null ? Vector2.Distance(Hero.transform.position, transform.position) : 999f;
    protected bool HeroInPanel => Hero != null && Hero.transform.position.x > MinX - 0.5f && Hero.transform.position.x < MaxX + 0.5f;

    private float _yaw;
    private float _deadFor;
    private float _lowest = -1e4f;

    protected virtual void Awake()
    {
        Light = GetComponent<Lightable>();
        Health = GetComponent<Health>();
        Clips = GetComponent<ClipPlayer>();
        Body = GetComponent<CharacterController>();
        Model = transform.childCount > 0 ? transform.GetChild(0) : transform;
        Health.team = Team.Inkie;
        Health.Facing = () => Facing;
        Health.Hurt += OnHurt;
        Health.Died += OnDied;
        Attack = new Hitbox(gameObject, Team.Inkie);
        Attack.Landed += OnAttackLanded;
        _yaw = Facing > 0 ? -TurnYaw : TurnYaw;
    }

    protected virtual void Start()
    {
        Clips?.Play("Idle", 0f);
        ApplyYaw(1f, snap: true);
        _lowest = transform.position.y - 12f;
    }

    private void Update()
    {
        float dt = Light.Delta;
        if (dt <= 0f) return;                                // frozen: armed, silent, still

        if (Mode == State.Dead)
        {
            _deadFor += dt;
            DeadTick(dt);
            return;
        }

        // green light heals Inkies too (GDD section 4)
        if (LightField.I != null && LightField.I.Lens == Lens.Green && LightField.I.BeamLive &&
            LightField.I.IsLit((Vector2)transform.position + Vector2.up) && Health.hp < Health.maxHp)
        {
            Health.Heal(GreenHealRate * dt);
            if (Random.value < dt * 3f) SfxLettering.Spawn("+", (Vector2)transform.position + new Vector2(Random.Range(-0.4f, 0.4f), 2f), Palette.LensGreen, 0.6f);
        }

        StateTime += dt;
        Attack.Tick(Facing);                                // a swing frozen mid-air lands when it wakes
        if (Light.AwakeFor >= Reorient) Think(dt);
        // reorienting: its own walking stops at once (only a knock keeps carrying it), so sweeping the
        // beam across an Inkie never nudges it along
        else if (Mode is State.Idle or State.Approach or State.Recover) Velocity.x = 0f;
        Move(dt);
        ApplyYaw(dt, false);
        if (transform.position.y < _lowest)                // fell out of its panel (down an erased gap)
        {
            PageManager.I?.EnemyDown(this);
            Destroy(gameObject);
        }
    }

    /// <summary>The Inkie's decisions for this frame (only when awake and reoriented).</summary>
    protected abstract void Think(float dt);

    protected void Enter(State s)
    {
        Mode = s;
        StateTime = 0f;
    }

    protected void FaceHero()
    {
        if (Hero != null && Mathf.Abs(ToHeroX) > 0.1f) Facing = Mathf.Sign(ToHeroX);
    }

    /// <summary>A hurt Inkie walks toward green light if it can see it.</summary>
    protected bool SeekGreen(float speed)
    {
        var f = LightField.I;
        if (f == null || f.Lens != Lens.Green || !f.BeamLive || Health.hp >= Health.maxHp * 0.7f) return false;
        float dx = f.BeamCentre.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.6f || Mathf.Abs(dx) > 10f) { Velocity.x = 0f; return Mathf.Abs(dx) < 0.6f; }
        Facing = Mathf.Sign(dx);
        Velocity.x = Facing * speed;
        Clips?.Play("Move", 0.1f);
        return true;
    }

    protected virtual void Move(float dt)
    {
        if (!Flies)
        {
            Velocity.y -= Gravity * dt;
            if (Grounded && Velocity.y < 0f) Velocity.y = -2f;
        }
        float belt = Grounded && !Flies ? Conveyor.Carry(transform.position) : 0f;   // a running belt carries Inkies too
        Vector3 step = new Vector3(Velocity.x + belt, Velocity.y, 0f) * dt;
        if (Body != null && Body.enabled)
        {
            var flags = Body.Move(step);
            Grounded = (flags & CollisionFlags.Below) != 0;
            if ((flags & CollisionFlags.Above) != 0 && Velocity.y > 0f) Velocity.y = 0f;
        }
        else transform.position += step;
        var p = transform.position;
        p.z = 0f;
        float r = BodyRadius;
        if (p.x < MinX + r) { p.x = MinX + r; Velocity.x = Mathf.Max(0f, Velocity.x); }
        if (p.x > MaxX - r) { p.x = MaxX - r; Velocity.x = Mathf.Min(0f, Velocity.x); }
        transform.position = p;
        // knockback fades on the ground
        if (Grounded && (Mode == State.Hurt || Mode == State.Dead)) Velocity.x = Mathf.MoveTowards(Velocity.x, 0f, 25f * dt);
    }

    protected void ApplyYaw(float dt, bool snap)
    {
        float target = Facing > 0f ? -TurnYaw : TurnYaw;
        _yaw = snap ? target : Mathf.MoveTowardsAngle(_yaw, target, 720f * dt);
        if (Model != transform) Model.localRotation = Quaternion.Euler(0f, _yaw, 0f);
    }

    protected virtual void OnHurt(Hit hit)
    {
        if (Mode == State.Dead) return;
        Velocity += hit.knockback;
        if (hit.team != Team.Hero) GameState.I?.ChoreographyHit(transform.position);
        if (hit.stun > 0f && !SuperArmour)
        {
            Attack.End();
            Enter(State.Hurt);
            HurtFor = hit.stun;
            Clips?.Play("Hit", 0.04f, 1f, restart: true);
        }
        GameAudio.Play("hit", 0.6f);
    }

    /// <summary>True while an attack can't be interrupted by a hit (the Bruiser's wind-up).</summary>
    protected virtual bool SuperArmour => false;
    protected float HurtFor;

    protected virtual void OnDied(Hit hit)
    {
        Attack.End();
        Enter(State.Dead);
        _deadFor = 0f;
        Velocity = hit.knockback * 0.5f;
        Clips?.Play("Death", 0.05f, 1f, restart: true);
        if (Body != null) Body.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        SfxLettering.Spawn("SPLOOSH!", (Vector2)transform.position + Vector2.up * 1.6f, Palette.Yellow, 1f, burst: true);
        PageManager.I?.EnemyDown(this);
    }

    /// <summary>After death: let the animation play out (on the Inkie's own clock), then melt away.</summary>
    protected virtual void DeadTick(float dt)
    {
        if (!Flies && Body == null) { }
        transform.position += (Vector3)(Velocity * dt);
        Velocity = Vector2.MoveTowards(Velocity, Vector2.zero, 10f * dt);
        if (_deadFor > 1.6f)
        {
            float k = Mathf.Clamp01((_deadFor - 1.6f) / 0.5f);
            Model.localScale = Vector3.one * (1f - k);
            if (k >= 1f) Destroy(gameObject);
        }
    }

    protected virtual void OnAttackLanded(Health target, Hit hit)
    {
        GameEvents.HitStop(hit.heavy ? 0.08f : 0.04f);
        GameAudio.Play(hit.heavy ? "punch_heavy" : "punch", 0.7f);
        if (target.team == Team.Hero) SfxLettering.Spawn(hit.word ?? "BAM!", (Vector2)target.transform.position + Vector2.up * 1.9f, Palette.Paper, 0.9f);
    }

    /// <summary>Standard melee hit from an Inkie: 1 heart to Max, `damage` to another Inkie.</summary>
    protected Hit Melee(float damage, Vector2 knock, float stun, string word, int hearts = 1, bool heavy = false) => new Hit
    {
        damage = damage, hearts = hearts, knockback = knock, stun = stun, word = word, heavy = heavy,
    };
}
