using UnityEngine;

/// <summary>
/// Shared Inkie behaviour (GDD section 6): a small state machine (Idle, Approach, Wind-up,
/// Attack, Recover, Hurt) driven by the Inkie's own clock, so freezing pauses it exactly.
///  - On waking, 0.3 s of "reorient" before acting, so re-entry is never an instant hit.
///  - Inkies can't cross gutters: each panel is its own arena.
///  - Attacks have friendly fire, and an Inkie frozen mid-swing keeps its hitbox armed.
///  - Waking in the light it shakes the ink off (a few drops, a stretch, a soft snap); freezing out of it, it's
///    pressed flat into the page for a moment and greys like drying ink (a paper tick).
///  - Knocked out by Max, it's sent flying off the panel toward the reader, in front of the panel's border.
/// Subclasses fill in <see cref="Think"/>.
/// </summary>
[RequireComponent(typeof(Lightable), typeof(Health))]
public abstract class EnemyBrain : MonoBehaviour
{
    public enum State { Idle, Approach, Windup, Attack, Recover, Hurt, Dead }

    public const float Reorient = 0.3f, Gravity = 30f;

    [Tooltip("Panel bounds on the lane: this Inkie never leaves them.")]
    public float MinX = -1e4f, MaxX = 1e4f;
    /// <summary>The panel this Inkie lives in (set by the page builder).</summary>
    [System.NonSerialized] public PanelLayout Panel;
    /// <summary>Where the page drew this Inkie (a bat that dies is drawn back in there).</summary>
    [System.NonSerialized] public Vector2 Home;
    /// <summary>Called up by Baron Blot: it doesn't come back when it dies.</summary>
    [System.NonSerialized] public bool Summoned;
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
    protected virtual float BodyRadius => Body != null ? Body.radius : 0.4f;
    protected virtual float TurnYaw => 70f;

    protected HeroController Hero => HeroController.I;
    protected float ToHeroX => Hero != null ? Hero.transform.position.x - transform.position.x : 999f;
    protected float HeroDistance => Hero != null ? Vector2.Distance(Hero.transform.position, transform.position) : 999f;
    protected bool HeroInPanel => Hero != null && Hero.transform.position.x > MinX - 0.5f && Hero.transform.position.x < MaxX + 0.5f;

    private float _yaw;
    private float _deadFor;
    private bool _melted;
    private float _lowest = -1e4f;

    /// <summary>Knocked out by Max, the body flies off toward the reader (Blot makes his own exits).</summary>
    protected virtual bool Flings => true;
    /// <summary>The wake/freeze marks (Blot keeps his own business).</summary>
    protected virtual bool ShowsWaking => true;
    /// <summary>Where a flung body flies: in front of the panel borders (-2.5), behind the lettering.</summary>
    public const float FlungZ = -3.3f;
    private bool _flung, _flying;
    private float _spin, _settle;
    private Vector3 _modelScale = Vector3.one;
    private float _bornAt, _markAt = -10f;
    private Coroutine _mark;
    private Renderer[] _skin;
    private MaterialPropertyBlock _block;

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
        _modelScale = Model.localScale;
        Light.Changed += OnLightChanged;
    }

    protected virtual void Start()
    {
        Clips?.Play("Idle", 0f);
        ApplyYaw(1f, snap: true);
        _lowest = transform.position.y - 12f;
        _bornAt = Time.time;
    }

    private void Update()
    {
        float dt = Light.Delta;
        if (dt <= 0f) return;                                // frozen: armed, silent, still

        if (Mode == State.Dead)
        {
            _deadFor += dt;
            DeadTick(dt);
            if (_flung) FlungDepth(dt);
            return;
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


    protected virtual void Move(float dt)
    {
        if (!Flies)
        {
            Velocity.y -= Gravity * dt;
            if (Grounded && Velocity.y < 0f) Velocity.y = -2f;
        }
        float belt = Grounded && !Flies ? Conveyor.Carry(transform.position) : 0f;   // a running belt carries Inkies too
        // walkers never step off a ledge of their own accord (a punch can still send them over)
        if (!Flies && Grounded && Mode != State.Hurt && Mode != State.Dead && Mathf.Abs(Velocity.x) > 0.01f && !GroundAhead(Mathf.Sign(Velocity.x)))
            Velocity.x = 0f;
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

    private static readonly RaycastHit[] _probe = new RaycastHit[6];

    /// <summary>Is there floor just past the leading edge of this Inkie's body?</summary>
    protected bool GroundAhead(float dir)
    {
        Vector3 from = transform.position + new Vector3(dir * (BodyRadius + 0.2f), 0.45f, 0f);
        int n = Physics.RaycastNonAlloc(from, Vector3.down, _probe, 1.3f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = _probe[i].collider;
            if (c.attachedRigidbody != null && c.attachedRigidbody.gameObject == gameObject) continue;
            if (c.transform.IsChildOf(transform)) continue;
            if (c.GetComponentInParent<HeroController>() != null) continue;      // Max isn't a floor
            return true;
        }
        return false;
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
        PageManager.I?.EnemyDown(this);
        EndMark();
        // knocked out by Max: off the panel, spinning, toward the reader (on its own clock: out of the light it
        // hangs where it is, like anything else)
        if (Flings && hit.team == Team.Hero)
        {
            _flung = _flying = true;
            float dir = Mathf.Abs(hit.knockback.x) > 0.1f ? Mathf.Sign(hit.knockback.x) : -Facing;
            float power = hit.heavy ? 1f : 0.7f;
            Velocity = new Vector2(dir * Mathf.Lerp(5f, 9f, power), Mathf.Lerp(5.5f, 8.5f, power));
            _spin = -dir * Random.Range(420f, 620f);
        }
    }

    /// <summary>After death: let the animation play out (on the Inkie's own clock), then melt away.</summary>
    protected virtual void DeadTick(float dt)
    {
        if (_flying && dt > 0f) Fly(dt);                     // (a bat drops on its own: see SplotchBrain)
        else if (!_flying)
        {
            transform.position += (Vector3)(Velocity * dt);
            Velocity = Vector2.MoveTowards(Velocity, Vector2.zero, 10f * dt);
        }
        if (_deadFor > 1.6f)
        {
            // the blow that did it printed its own word; the melt gets one of its own, a beat later at the puddle
            if (!_melted)
            {
                _melted = true;
                SfxLettering.Spawn("SPLOOSH!", (Vector2)transform.position + Vector2.up * 0.8f, Palette.Paper, 0.85f);
            }
            float k = Mathf.Clamp01((_deadFor - 1.6f) / 0.5f);
            Model.localScale = _modelScale * (1f - k);
            if (k >= 1f) Destroy(gameObject);
        }
    }

    // ---- knocked out: the flight ----------------------------------------------------------------------------

    private void Fly(float dt)
    {
        var p = transform.position;
        // in small steps, so a long frame (a hitch) traces the same arc instead of cutting it short
        for (float left = dt; left > 1e-5f && _flying; left -= 0.02f)
        {
            float h = Mathf.Min(0.02f, left);
            Velocity.y -= Gravity * h;
            Vector2 step = Velocity * h;
            // it lands on whatever floor it comes down on (its own colliders are off: it's a body now)
            if (Velocity.y < 0f && FloorBelow(p, -step.y, out float floor))
            {
                p.y = floor;
                p.x += step.x;
                Velocity = new Vector2(Velocity.x * 0.35f, 0f);
                _flying = false;
                _settle = 0f;
                GameAudio.Play("splat", 0.35f, 0.08f);
            }
            else
            {
                p.x += step.x;
                p.y += step.y;
            }
        }
        transform.position = p;
        if (_flying) Model.Rotate(0f, 0f, _spin * dt, Space.World);
    }

    /// <summary>Flying, the body comes off the page toward the reader; landed, it lies back down in the panel.</summary>
    private void FlungDepth(float dt)
    {
        var p = transform.position;
        p.z = Mathf.MoveTowards(p.z, _flying ? FlungZ : 0f, dt * (_flying ? 16f : 9f));
        transform.position = p;
        if (!_flying && _settle < 1f)
        {
            _settle = Mathf.Min(1f, _settle + dt / 0.15f);
            var upright = Quaternion.Euler(0f, _yaw, 0f);
            Model.localRotation = Quaternion.Slerp(Model.localRotation, upright, _settle);
        }
    }

    private bool FloorBelow(Vector3 p, float drop, out float floor)
    {
        floor = 0f;
        Vector3 from = new Vector3(p.x, p.y + 0.5f, 0f);
        int n = Physics.RaycastNonAlloc(from, Vector3.down, _probe, 0.5f + Mathf.Max(0f, drop) + 0.02f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var c = _probe[i].collider;
            if (c.transform.IsChildOf(transform) || c.GetComponentInParent<Health>() != null) continue;   // bodies aren't floors
            if (_probe[i].distance < best) { best = _probe[i].distance; floor = _probe[i].point.y; }
        }
        return best < float.MaxValue;
    }

    // ---- waking and freezing, made readable -----------------------------------------------------------------

    private void OnLightChanged(bool awake)
    {
        if (!ShowsWaking || Mode == State.Dead || Time.time - _bornAt < 0.5f) return;   // not the page drawing itself in
        if (PageManager.I != null && PageManager.I.Busy) return;
        float now = Time.unscaledTime;
        if (now - _markAt < 0.3f) return;                          // the beam's edge flickering over it: once is enough
        _markAt = now;
        ComicFx.InkCue(awake);
        if (awake) MangaFx.Drops((Vector2)transform.position + Vector2.up * (BodyRadius + 0.7f));
        EndMark();
        _mark = StartCoroutine(Mark(awake));
    }

    /// <summary>Waking: a little stretch as it shakes the ink off. Freezing: pressed flat into the page, greying
    /// like drying ink, then back to itself (the statue it's now is the drawing, not a squashed one).</summary>
    private System.Collections.IEnumerator Mark(bool awake)
    {
        if (_skin == null) _skin = Model.GetComponentsInChildren<Renderer>(true);
        _block ??= new MaterialPropertyBlock();
        float length = awake ? 0.16f : 0.22f;
        for (float t = 0f; t < length; t += Time.unscaledDeltaTime)
        {
            float k = t / length;
            float e = 1f - k;
            float y = awake ? 1f + 0.09f * Mathf.Sin(k * Mathf.PI) * e : 1f - 0.11f * e * e;
            float xz = awake ? 1f - 0.05f * Mathf.Sin(k * Mathf.PI) * e : 1f + 0.07f * e * e;
            Model.localScale = Vector3.Scale(_modelScale, new Vector3(xz, y, xz));
            if (!awake) Grey(e * e);
            yield return null;
        }
        Model.localScale = _modelScale;
        if (!awake) Grey(0f);
        _mark = null;
    }

    private void Grey(float k)
    {
        if (_skin == null) return;
        foreach (var r in _skin)
        {
            if (r == null) continue;
            if (k <= 0.001f) { r.SetPropertyBlock(null); continue; }
            r.GetPropertyBlock(_block);
            _block.SetColor(InkColorId, Color.Lerp(Palette.Ink, new Color(0.45f, 0.45f, 0.47f), k));
            r.SetPropertyBlock(_block);
        }
    }

    private static readonly int InkColorId = Shader.PropertyToID("_InkColor");

    private void EndMark()
    {
        if (_mark == null) return;
        StopCoroutine(_mark);
        _mark = null;
        Model.localScale = _modelScale;
        Grey(0f);
    }

    protected virtual void OnAttackLanded(Health target, Hit hit)
    {
        GameEvents.HitStop(hit.heavy ? 0.08f : 0.04f);
        GameAudio.Play(hit.heavy ? "punch_heavy" : "punch", 0.7f);
        if (target.team == Team.Hero)
            SfxLettering.Spawn(hit.word ?? "BAM!", (Vector2)target.transform.position + Vector2.up * 1.9f, Palette.Paper, 0.9f,
                lean: Mathf.Clamp(hit.knockback.x / 6f, -1f, 1f));
    }

    /// <summary>Standard melee hit from an Inkie: 1 heart to Max, `damage` to another Inkie.</summary>
    protected Hit Melee(float damage, Vector2 knock, float stun, string word, int hearts = 1, bool heavy = false) => new Hit
    {
        damage = damage, hearts = hearts, knockback = knock, stun = stun, word = word, heavy = heavy,
    };
}
