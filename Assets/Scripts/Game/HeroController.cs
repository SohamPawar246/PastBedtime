using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Max Voltage on the lane (GDD section 5): a kinematic 2D-on-a-lane controller.
/// 7 units/s run, a 3.2 unit jump when held (1.8 when tapped), 0.1 s coyote time and jump
/// buffer, no double jump. A 3-unit dodge with 0.25 s of i-frames. He obeys the light like
/// everything else: out of the beam he freezes, mid-jump he hangs, and everything resumes
/// from exactly where it stopped.
/// The feel: a jump hangs a moment at its peak and then drops quicker than it rose (the same time in
/// the air as a plain arc, so every gap is as wide as it was drawn), he stretches into a jump and squashes
/// into a hard landing (with a puff of dust), and a hit flashes him and blinks him through his i-frames.
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(Lightable), typeof(Health))]
public class HeroController : MonoBehaviour
{
    public static HeroController I { get; private set; }

    public const float RunSpeed = 7f, Gravity = 30f, JumpHigh = 3.2f, JumpLow = 1.8f;
    public const float Coyote = 0.1f, Buffer = 0.1f;
    /// <summary>Past the peak: half gravity until he's falling at HangSpeed, then FallGravity times it. Tuned so a
    /// full jump spends the same time coming down as a plain arc does (0.46 s), so its reach is unchanged.</summary>
    public const float HangSpeed = 2f, HangGravity = 0.5f, FallGravity = 1.5f;
    /// <summary>How long a hit blinks him (his i-frames).</summary>
    public const float HurtBlink = 1f;
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
    /// <summary>Mom's door is open: Max holds his breath. He can't move or fight, and nothing lands on
    /// him (the torch is still Roshan's to switch off).</summary>
    public bool Hushed => MomDirector.I != null && MomDirector.I.HoldingStill;
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
    // the feel: squash and stretch (signed: + stretch, - squash) and its clock; the hurt flash and blink
    private Vector3 _modelScale = Vector3.one;
    private float _squash, _squashT = 1f;
    private float _flash, _blink;
    private Renderer[] _skin;
    private MaterialPropertyBlock _block;
    private static readonly int PaperId = Shader.PropertyToID("_PaperColor"), InkId = Shader.PropertyToID("_InkColor");

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
        _modelScale = _model.localScale;
        _skin = _model != transform ? _model.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        _block = new MaterialPropertyBlock();
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


    private void OnHurt(Hit hit)
    {
        _hurt = Mathf.Max(0.3f, hit.stun);
        Velocity = new Vector2(hit.knockback.x != 0f ? hit.knockback.x : -Facing * 4f, Mathf.Max(hit.knockback.y, 4f));
        _health.Invulnerable = HurtBlink;
        _combat?.Interrupt();
        Clips?.Play(_health.Dead ? "Death" : "Hurt", 0.05f, 1f, restart: true);
        GameEvents.Impact(hit.hearts >= 2 ? 0.6f : 0.3f);
        GameState.I?.Notify();
        _flash = 0.07f;                                     // a red flash, then a blink through his i-frames
        _blink = _health.Dead ? 0f : HurtBlink;
    }

    /// <summary>The hit's flash (printed red, his ink gone pale) and the blink after it, on his clock: frozen
    /// mid-blink, he holds still and solid like everything else.</summary>
    private void HurtLook(float dt)
    {
        bool flash = dt > 0f && _flash > 0f;
        if (flash) _flash -= dt;
        if (dt > 0f && _blink > 0f) _blink = Dead ? 0f : _blink - dt;
        // on 70 ms, off 45: unmistakable, and never gone long enough to lose him; solid again for its last beat
        bool hidden = dt > 0f && _blink > 0.12f && Mathf.Repeat(_blink, 0.115f) < 0.045f;
        foreach (var r in _skin)
        {
            if (r == null) continue;
            if (r.enabled == hidden) r.enabled = !hidden;
            if (flash)
            {
                r.GetPropertyBlock(_block);
                _block.SetColor(PaperId, Palette.HeroRed);
                _block.SetColor(InkId, Palette.ComicPaper);
                r.SetPropertyBlock(_block);
            }
            else if (r.HasPropertyBlock()) r.SetPropertyBlock(null);
        }
    }

    /// <summary>Squash (a hard landing, negative) or stretch (taking off, positive), easing back over 0.14 s.</summary>
    private void Squash(float amount)
    {
        _squash = amount;
        _squashT = 0f;
    }

    private void Shape(float dt)
    {
        _squashT = Mathf.Min(1f, _squashT + dt / 0.14f);
        float e = (1f - _squashT) * (1f - _squashT);
        float y = 1f + _squash * e, xz = 1f - _squash * 0.55f * e;
        if (_model != transform) _model.localScale = Vector3.Scale(_modelScale, new Vector3(xz, y, xz));
    }

    private void Update()
    {
        Contacts();
        bool hushed = Hushed;
        if (hushed) _health.Invulnerable = Mathf.Max(_health.Invulnerable, 0.2f);   // set before her light can wake him
        float dt = Light.Delta;
        HurtLook(dt);
        if (dt <= 0f) return;                       // frozen: everything waits, velocity kept
        Shape(dt);

        if (Dead)
        {
            Fall(dt);
            return;
        }

        if (_hurt > 0f) _hurt -= dt;
        if (_dodgeCooldown > 0f) _dodgeCooldown -= dt;

        float input = Locked || hushed || _hurt > 0f ? 0f : GameInput.MoveX;
        bool attacking = _combat != null && _combat.Busy;

        // ---- dodge ---------------------------------------------------------------------------
        // on the ground only: a dash in mid-air (with gravity paused) carried him over gaps that are meant to
        // need a frozen step (no double jump, GDD section 7). A press that can't dodge is gone, not saved for later.
        bool dodgePressed = GameInput.DodgePressed;
        if (dodgePressed && !Locked && !hushed && Grounded && _hurt <= 0f && _dodge <= 0f && _dodgeCooldown <= 0f)
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
        _buffer = !Locked && !hushed && GameInput.JumpPressed ? Buffer : _buffer - dt;
        if (_buffer > 0f && _coyote > 0f && !attacking && _hurt <= 0f && _dodge <= 0f)
        {
            Velocity.y = Mathf.Sqrt(2f * Gravity * JumpHigh);
            _buffer = _coyote = 0f;
            _jumpCut = false;
            Grounded = false;
            Clips?.Play("JumpStart", 0.05f, 1f, restart: true);
            Squash(0.11f);                                  // up onto his toes
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
        // past the peak: a hang, then a quicker drop (the same time in the air overall)
        float g = Gravity;
        if (!Grounded && !Dead && Velocity.y <= 0f) g *= Velocity.y > -HangSpeed ? HangGravity : FallGravity;
        // the dodge holds him to the floor, but a dash that runs off a ledge falls like anything else
        if ((_dodge <= 0f || !Grounded) && !(_combat != null && _combat.OverridesGravity)) Velocity.y -= g * dt;
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
            bool slam = _combat != null && _combat.Slamming;     // the slam has its own landing
            _combat?.Landed();
            if (_airTime > 0.25f && !Dead && !slam) Clips?.Play("Land", 0.05f, 1.4f, restart: true);
            // squashed into the landing as hard as he came down; a long drop (or the slam) kicks up dust
            float impact = -vy;
            if (_airTime > 0.12f && !Dead) Squash(-Mathf.Clamp(impact / 110f, 0.04f, slam ? 0.2f : 0.16f));
            if (!Dead && (slam || impact > 18f)) MangaFx.Puff(transform.position, slam ? 1.4f : Mathf.Clamp(impact / 20f, 0.8f, 1.2f));
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
