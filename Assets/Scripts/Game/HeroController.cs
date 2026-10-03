using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Max Voltage on the lane (GDD section 5): a kinematic 2D-on-a-lane controller.
/// 7 units/s run, a 3.2 unit jump when held (1.8 when tapped), 0.1 s coyote time and jump
/// buffer, no double jump. A 3-unit dodge with 0.25 s of i-frames. He obeys the light like
/// everything else: out of the beam he freezes, mid-jump he hangs, and everything resumes
/// from exactly where it stopped.
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(Lightable), typeof(Health))]
public class HeroController : MonoBehaviour
{
    public static HeroController I { get; private set; }

    public const float RunSpeed = 7f, Gravity = 30f, JumpHigh = 3.2f, JumpLow = 1.8f;
    public const float Coyote = 0.1f, Buffer = 0.1f;
    public const float DodgeDistance = 3f, DodgeTime = 0.2f, DodgeIFrames = 0.25f, DodgeCooldown = 0.6f;
    /// <summary>How long Max keeps his guard up after fighting before he relaxes.</summary>
    public const float GuardHold = 1.5f;
    /// <summary>The library sprint's ground speed at 1x (its planted foot, measured in Blender): the
    /// run plays at speed / this, so his feet keep pace with the ground instead of skating.</summary>
    private const float SprintClipSpeed = 8.9f;

    public float Facing { get; private set; } = 1f;
    public Vector2 Velocity;
    public bool Grounded { get; private set; }
    public bool Dodging => _dodge > 0f;
    public bool Hurting => _hurt > 0f;
    public bool Dead => _health != null && _health.Dead;
    /// <summary>Movement is handed over (combat moves, cutscenes, tier sweeps).</summary>
    public bool Locked;
    /// <summary>Lane bounds Max can't leave (the page edges).</summary>
    public float MinX = -1e4f, MaxX = 1e4f;

    public Lightable Light { get; private set; }
    public ClipPlayer Clips { get; private set; }
    public CharacterController Body { get; private set; }
    private Health _health;
    private HeroCombat _combat;
    private Transform _model;

    private float _coyote, _buffer, _dodge, _dodgeCooldown, _hurt, _airTime, _guard;
    private readonly Dictionary<Collider, bool> _passing = new();     // Inkie bodies Max passes through now (bats' boxes too)
    private readonly List<Collider> _gone = new();
    private bool _jumpCut = true;            // true when no jump is waiting to be cut short
    private float _yaw;

    private void Awake()
    {
        I = this;
        Body = GetComponent<CharacterController>();
        Light = GetComponent<Lightable>();
        _health = GetComponent<Health>();
        _health.team = Team.Hero;
        _health.Facing = () => Facing;
        _health.Hurt += OnHurt;
        Clips = GetComponent<ClipPlayer>();
        _combat = GetComponent<HeroCombat>();
        _model = transform.childCount > 0 ? transform.GetChild(0) : transform;
        _yaw = -70f;
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
    }

    public void Teleport(Vector2 p)
    {
        Body.enabled = false;
        transform.position = new Vector3(p.x, p.y, 0f);
        Body.enabled = true;
        Velocity = Vector2.zero;
        _passing.Clear();                     // re-enabling the body resets which bodies it ignores
    }

    /// <summary>
    /// Bodies. Awake Inkies never block or shove Max (brawler bodies pass through each other; the
    /// fighting is all hitboxes), so he can always get round behind an armoured one. Frozen Inkies
    /// are solid statues: he bumps into them and stands on them (the freeze-step). The dodge goes
    /// through everything. An Inkie that freezes while Max is inside it stays passable until he's
    /// out, so nothing ever pops him out of a body.
    /// </summary>
    private void Contacts()
    {
        if (Body == null || !Body.enabled) return;
        var mine = Body.bounds;
        mine.Expand(-0.12f);
        foreach (var h in Health.All)
        {
            if (h.team == Team.Hero || !h.TryGetComponent(out Collider other) || !other.enabled || other.isTrigger) continue;
            bool passing = _passing.TryGetValue(other, out bool p) && p;
            // a woken Inkie stays solid through its reorient (it isn't acting yet): a flicker of the beam's
            // edge doesn't drop Max off the frozen bat he's standing on
            bool pass = h.Dead || Dodging || (h.IsAwake && (h.Light == null || h.Light.AwakeFor >= EnemyBrain.Reorient));
            if (pass == passing) continue;
            if (!pass && mine.Intersects(other.bounds)) continue;     // still inside it: solid once he's out
            Physics.IgnoreCollision(Body, other, pass);
            _passing[other] = pass;
        }
        _gone.Clear();
        foreach (var c in _passing.Keys) if (c == null) _gone.Add(c);
        foreach (var c in _gone) _passing.Remove(c);
    }

    public void Face(float dir)
    {
        if (Mathf.Abs(dir) > 0.01f) Facing = Mathf.Sign(dir);
    }

    /// <summary>The Green lens ("Mend", GDD section 4): a heart back for every 3 s Max spends in its beam.</summary>
    private void Mend(float dt)
    {
        var f = LightField.I;
        Vector2 chest = (Vector2)transform.position + Vector2.up * 0.9f;
        bool inGreen = f != null && f.Lens == Lens.Green && f.BeamLive && _health.hp < _health.maxHp &&
                       (chest - f.BeamCentre).magnitude <= f.BeamRadius + LightField.HeroGrace;   // the green beam, not a wedge or flare
        if (!inGreen) { _mend = 0f; return; }
        _mend += dt;
        if (_mend < MendEvery) return;
        _mend = 0f;
        _health.Heal(1f);
        GameState.I?.Notify();
        SfxLettering.Spawn("+1", (Vector2)transform.position + Vector2.up * 2.2f, Palette.LensGreen, 0.8f);
        GameAudio.Play("star", 0.5f);
    }

    public const float MendEvery = 3f;
    private float _mend;

    private void OnHurt(Hit hit)
    {
        _hurt = Mathf.Max(0.3f, hit.stun);
        Velocity = new Vector2(hit.knockback.x != 0f ? hit.knockback.x : -Facing * 4f, Mathf.Max(hit.knockback.y, 4f));
        _health.Invulnerable = 1.0f;
        _combat?.Interrupt();
        Clips?.Play(_health.Dead ? "Death" : "Hurt", 0.05f, 1f, restart: true);
        GameEvents.Impact(hit.hearts >= 2 ? 0.6f : 0.3f);
        GameState.I?.Notify();
    }

    private void Update()
    {
        Contacts();
        float dt = Light.Delta;
        if (dt <= 0f) return;                       // frozen: everything waits, velocity kept

        if (Dead)
        {
            Fall(dt);
            return;
        }

        if (_hurt > 0f) _hurt -= dt;
        if (_dodgeCooldown > 0f) _dodgeCooldown -= dt;
        Mend(dt);

        float input = Locked || _hurt > 0f ? 0f : GameInput.MoveX;
        bool attacking = _combat != null && _combat.Busy;

        // ---- dodge ---------------------------------------------------------------------------
        if (!Locked && _hurt <= 0f && _dodge <= 0f && _dodgeCooldown <= 0f && GameInput.DodgePressed)
        {
            _combat?.Interrupt();
            _dodge = DodgeTime;
            _dodgeCooldown = DodgeCooldown;
            if (Mathf.Abs(input) > 0.1f) Facing = Mathf.Sign(input);
            _health.Invulnerable = Mathf.Max(_health.Invulnerable, DodgeIFrames);
            Clips?.Play("Dodge", 0.04f, 1f, restart: true);
            MangaFx.Dash(transform, Facing, DodgeTime);
        }

        // ---- run -----------------------------------------------------------------------------
        if (_dodge > 0f)
        {
            _dodge -= dt;
            Velocity.x = Facing * DodgeDistance / DodgeTime;
            Velocity.y = Mathf.Max(Velocity.y, Grounded ? -2f : Velocity.y);
        }
        else if (_hurt > 0f)
        {
            Velocity.x = Mathf.MoveTowards(Velocity.x, 0f, 12f * dt);
        }
        else
        {
            float target = input * RunSpeed;
            if (attacking) target = Grounded ? _combat.Lunge * Facing : Velocity.x;
            float accel = Grounded ? 70f : 40f;
            Velocity.x = Mathf.MoveTowards(Velocity.x, target, accel * dt);
            if (!attacking && Mathf.Abs(input) > 0.1f) Facing = Mathf.Sign(input);
        }

        // ---- jump: coyote time, buffer, hold for height ----------------------------------------
        _coyote = Grounded ? Coyote : _coyote - dt;
        _buffer = !Locked && GameInput.JumpPressed ? Buffer : _buffer - dt;
        if (_buffer > 0f && _coyote > 0f && !attacking && _hurt <= 0f && _dodge <= 0f)
        {
            Velocity.y = Mathf.Sqrt(2f * Gravity * JumpHigh);
            _buffer = _coyote = 0f;
            _jumpCut = false;
            Grounded = false;
            Clips?.Play("JumpStart", 0.05f, 1f, restart: true);
        }
        if (!GameInput.JumpHeld && !_jumpCut && Velocity.y > 0f && !(_combat != null && _combat.Airborne))
        {
            // released early: cap the rise so the apex lands near 1.8 units
            Velocity.y = Mathf.Min(Velocity.y, Mathf.Sqrt(2f * Gravity * JumpLow) * 0.75f);
            _jumpCut = true;
        }

        Fall(dt);
        Animate(dt, input, attacking);
    }

    private void Fall(float dt)
    {
        float vy = Velocity.y;
        if (_dodge <= 0f && !(_combat != null && _combat.OverridesGravity)) Velocity.y -= Gravity * dt;
        if (Grounded && Velocity.y < 0f) Velocity.y = -2f;
        Velocity.y = Mathf.Max(Velocity.y, -24f);
        if (Grounded && vy < 0f) vy = Velocity.y;

        // midpoint step: the jump traces the same arc at 30 fps as at 144 (WebGL frame rates vary);
        // a running conveyor belt under his feet carries him along
        float belt = Grounded ? Conveyor.Carry(transform.position) : 0f;
        var flags = Body.Move(new Vector3(Velocity.x + belt, (vy + Velocity.y) * 0.5f, 0f) * dt);
        var p = transform.position;
        p.z = 0f;
        p.x = Mathf.Clamp(p.x, MinX, MaxX);
        transform.position = p;

        bool was = Grounded;
        Grounded = (flags & CollisionFlags.Below) != 0;
        if ((flags & CollisionFlags.Above) != 0 && Velocity.y > 0f) Velocity.y = 0f;
        if ((flags & CollisionFlags.Sides) != 0 && _dodge <= 0f) Velocity.x *= 0.5f;
        if (Grounded && !was)
        {
            _combat?.Landed();
            bool slam = _combat != null && _combat.Slamming;     // the slam has its own landing
            if (_airTime > 0.25f && !Dead && !slam) Clips?.Play("Land", 0.05f, 1.4f, restart: true);
        }
        _airTime = Grounded ? 0f : _airTime + dt;
    }

    private void Animate(float dt, float input, bool attacking)
    {
        // turn to face the way he's going, three-quarters to the page camera
        float targetYaw = Facing > 0f ? -70f : 70f;
        _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, 900f * dt);
        if (_model != transform) _model.localRotation = Quaternion.Euler(0f, _yaw, 0f);

        _guard = attacking || _dodge > 0f || _hurt > 0f ? GuardHold : Mathf.Max(0f, _guard - dt);
        if (Clips == null || attacking || _dodge > 0f || _hurt > 0f) return;
        string cur = Clips.Current;
        // one-shots that end in a stance play out unless he runs: the landing, the dodge's skid, the slam's rise
        bool recovering = (cur == "Land" && Clips.Normalized < 0.6f) ||
                          ((cur == "Dodge" || cur == "GroundSlam") && Clips.Normalized < 0.95f);
        if (!Grounded)
        {
            if (cur != "JumpStart" || Clips.Normalized > 0.8f) Clips.Play("JumpLoop", 0.15f);
        }
        else if (Mathf.Abs(Velocity.x) > 0.5f && Mathf.Abs(input) > 0.1f)
        {
            Clips.Play("Run", 0.1f, Mathf.Clamp(Mathf.Abs(Velocity.x) / SprintClipSpeed, 0.5f, 1f));
        }
        else if (!recovering)
        {
            // keeps his guard up between attacks, then relaxes
            if (_guard > 0f) Clips.Play("Guard", 0.12f);
            else Clips.Play("Idle", cur == "Guard" ? 0.4f : 0.15f);
        }
    }
}
