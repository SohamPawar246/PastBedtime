using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Baron Blot (GDD section 6), 150 HP over three pages, and too heavy to knock about: only a heavy
/// blow staggers him. He keeps his distance, sends ink waves rolling along the floor with a sweep
/// of his cane (ink like everything else: they freeze in the dark) and summons help. Green light
/// heals him 2 HP a second. Nobody leaves his panel while he's in it.
///   Phase 1, the Office (page 6)  150 to 75: Smudges. Then he dives into his pool: "TO BE CONTINUED".
///   Phase 2, the Vat (page 8)      75 to 40: he slips into invisible ink every few seconds (only his
///                                  eyes and monocle show; the Ghost lens finds him); bats. Then up to the roof.
///   Phase 3, the Roof (page 9)     70 to 0:  all of it, a Smudge and a Dot-Shot at a time; at 30 Mom
///                                  flips the room light on (<see cref="Finale"/>), and at 0 he's done.
/// </summary>
public class BlotBrain : EnemyBrain
{
    public const float MaxHp = 150f, Speed = 1.8f, KeepAway = 6.5f;
    public const float VisibleFor = 6f, HiddenFor = 5f, FinaleAt = 30f;
    public const float ThrowLength = 1f, ThrowRelease = 0.54f;          // the clips' timings (24 fps)
    public const float SummonLength = 1.25f, SummonRelease = 0.71f, DiveLength = 1.5f;

    protected override float TurnYaw => 40f;                             // he plays to the reader
    protected override float GreenHealRate => 2f;

    private enum Move { None, Throw, Summon, Laugh }
    private Move _move;
    private bool _fired, _met, _escaping, _gone, _finished, _finale, _hidden;
    private float _inkClock;
    private SkinnedMeshRenderer _skin;
    private Material[] _shownMats, _hiddenMats;
    private float _cool = 1.4f, _summonCool, _escapeFor, _heroHp = -1f;
    private Health _heroHealth;
    private readonly List<EnemyBrain> _summoned = new();

    public bool Escaping => _escaping;
    /// <summary>1 the Office, 2 the Vat, 3 the Roof (from the page he's on).</summary>
    public int Phase { get; private set; } = 1;
    /// <summary>In invisible ink right now, and not found by the Ghost lens.</summary>
    public bool Hidden { get; private set; }
    private float EscapeHp => Phase == 1 ? 75f : 40f;

    protected override void Awake()
    {
        base.Awake();
        int page = GameState.I != null ? GameState.I.Page : 6;
        Phase = page >= 9 ? 3 : page >= 8 ? 2 : 1;
        Health.maxHp = MaxHp;
        Health.hp = Phase switch { 3 => 70f, 2 => 75f, _ => MaxHp };
        _skin = GetComponentInChildren<SkinnedMeshRenderer>();
        if (_skin != null)
        {
            _shownMats = _skin.sharedMaterials;
            var nothing = GameAssets.I != null ? GameAssets.I.nothing : null;
            _hiddenMats = new Material[_shownMats.Length];
            for (int i = 0; i < _shownMats.Length; i++)
            {
                string n = _shownMats[i] != null ? _shownMats[i].name : "";
                bool keep = n.StartsWith("EN_Eye") || n.StartsWith("EN_Pupil") || n.StartsWith("EN_Glass");
                _hiddenMats[i] = keep || nothing == null ? _shownMats[i] : nothing;
            }
        }
    }

    protected override void Think(float dt)
    {
        if (Hero != null && HeroInPanel) Hero.MaxX = Mathf.Min(Hero.MaxX, MaxX - 0.35f);   // the door's locked
        InvisibleInk(dt);
        if (_escaping) { Escape(dt); return; }

        switch (Mode)
        {
            case State.Hurt:
                Velocity.x = Mathf.MoveTowards(Velocity.x, 0f, 20f * dt);
                if (StateTime >= HurtFor) Enter(State.Approach);
                return;
            case State.Attack:
                Velocity.x = Mathf.MoveTowards(Velocity.x, 0f, 20f * dt);
                Act();
                return;
            case State.Recover:
                Velocity.x = Mathf.MoveTowards(Velocity.x, 0f, 20f * dt);
                if (StateTime >= 0.45f) Enter(State.Approach);
                return;
        }

        if (Hero == null || !HeroInPanel || Hero.Dead)
        {
            Velocity.x = Mathf.MoveTowards(Velocity.x, 0f, 10f * dt);
            Clips?.Play("Idle", 0.2f);
            return;
        }
        FaceHero();
        if (_heroHealth == null) _heroHealth = Hero.GetComponent<Health>();
        if (!_met)
        {
            _met = true;                                                 // a villain's welcome
            Begin(Move.Laugh);
            return;
        }
        if (_heroHp >= 0f && _heroHealth.hp < _heroHp && _cool > 0.5f)   // he enjoys every hit
        {
            _heroHp = _heroHealth.hp;
            Begin(Move.Laugh);
            return;
        }
        _heroHp = _heroHealth.hp;

        _cool -= dt;
        _summonCool -= dt;
        _summoned.RemoveAll(e => e == null || e.Health.Dead);
        if (_cool <= 0f)
        {
            if (_summonCool <= 0f && _summoned.Count == 0) Begin(Move.Summon);
            else if (Mathf.Abs(ToHeroX) > 2.2f) Begin(Move.Throw);
            else _cool = 0.3f;
            return;
        }

        // keep his distance: glide back from Max, or after him, never leaving his office
        float dist = Mathf.Abs(ToHeroX);
        float want = dist < KeepAway - 1.2f ? -Facing : dist > KeepAway + 1.2f ? Facing : 0f;
        Velocity.x = Mathf.MoveTowards(Velocity.x, want * Speed, 8f * dt);
        Clips?.Play(Mathf.Abs(Velocity.x) > 0.25f ? "Move" : "Idle", 0.18f);
    }

    private void Begin(Move m)
    {
        _move = m;
        _fired = false;
        Enter(State.Attack);
        Velocity.x = 0f;
        switch (m)
        {
            case Move.Throw:
                Clips?.Play("Throw", 0.06f, 1f, restart: true);
                break;
            case Move.Summon:
                Clips?.Play("Summon", 0.06f, 1f, restart: true);
                SfxLettering.Spawn("RISE, MY INKIES!", (Vector2)transform.position + Vector2.up * 3.4f, Palette.Paper, 0.75f);
                break;
            case Move.Laugh:
                Clips?.Play("Laugh", 0.1f, 1f, restart: true);
                SfxLettering.Spawn("MWAHAHA!", (Vector2)transform.position + new Vector2(Facing * 0.6f, 3.2f), Palette.Paper, 1f);
                break;
        }
    }

    /// <summary>The move in progress, on Blot's own clock (freeze him and his swing waits).</summary>
    private void Act()
    {
        switch (_move)
        {
            case Move.Throw:
                if (!_fired && StateTime >= ThrowRelease)
                {
                    _fired = true;
                    InkWave.Spawn((Vector2)transform.position + new Vector2(Facing * 1.1f, 0f), Facing, MinX, MaxX, gameObject);
                    SfxLettering.Spawn("WHOOSH!", (Vector2)transform.position + new Vector2(Facing * 1.4f, 1.3f), Palette.Paper, 0.8f);
                    GameAudio.Play("splat", 0.55f);
                }
                if (StateTime >= ThrowLength) Done();
                break;
            case Move.Summon:
                if (!_fired && StateTime >= SummonRelease)
                {
                    _fired = true;
                    _summonCool = 8f;
                    Summon();
                }
                if (StateTime >= SummonLength) Done();
                break;
            case Move.Laugh:
                if (StateTime >= 1.2f) Done();
                break;
        }
    }

    private void Done()
    {
        _move = Move.None;
        _cool = Random.Range(1.4f, 2.4f);
        Enter(State.Recover);
        Clips?.Play("Idle", 0.15f);
    }

    /// <summary>Help drops out of the ink on the ceiling, either side of him: Smudges in his office, bats
    /// over the vat, a Smudge and a Dot-Shot on the roof.</summary>
    private void Summon()
    {
        if (Panel == null) return;
        foreach (float side in new[] { -1f, 1f })
        {
            float x = Mathf.Clamp(transform.position.x + side * 2.4f, MinX + 1f, MaxX - 1f);
            var kind = Phase == 2 ? EnemyKind.Splotch : Phase == 3 && side > 0f ? EnemyKind.DotShot : EnemyKind.Smudge;
            float up = kind == EnemyKind.Splotch ? 4.5f : 3.5f;
            var e = PageBuilder.Spawn(kind, new Vector2(x, transform.position.y + up), Panel, transform.parent, faceRight: side < 0f);
            if (e == null) continue;
            _summoned.Add(e);
            SfxLettering.Spawn("SPLAT!", new Vector2(x, transform.position.y + 1.2f), Palette.Paper, 0.8f, burst: true);
        }
        GameAudio.Play("splat", 0.7f);
    }

    // ---- getting hurt, and getting away ------------------------------------------------------------

    protected override void OnHurt(Hit hit)
    {
        if (Mode == State.Dead || _escaping) return;
        if (hit.team != Team.Hero) GameState.I?.ChoreographyHit(transform.position);
        GameAudio.Play("hit", 0.6f);
        Velocity.x += hit.knockback.x * 0.2f;                           // too heavy to knock about
        if (hit.heavy && _move != Move.Throw && _move != Move.Summon)
        {
            Attack.End();
            _move = Move.None;
            Enter(State.Hurt);
            HurtFor = 0.45f;
            Clips?.Play("Hit", 0.04f, 1f, restart: true);
        }
        if (Phase < 3 && Health.hp <= EscapeHp) BeginEscape();
        if (Phase == 3 && !_finale && Health.hp <= FinaleAt)
        {
            _finale = true;
            Finale.I?.Begin();                                          // Mom flips the room light on
        }
    }

    protected override void OnDied(Hit hit)
    {
        if (Phase < 3) { BeginEscape(); return; }                       // the first two always end in a getaway
        ShowAll();
        base.OnDied(hit);
        SfxLettering.Spawn("NOOOO...!", (Vector2)transform.position + Vector2.up * 3.4f, Palette.Paper, 1.2f, burst: true);
        Finale.I?.Defeated();
    }

    // ---- invisible ink --------------------------------------------------------------------------------

    /// <summary>Phases 2 and 3: every few seconds he slips into invisible ink. Hidden, he can't be hit and
    /// only his eyes and monocle float in the dark, until the Ghost lens's beam finds him. The room
    /// light doesn't show invisible ink, but in the finale he's too rattled to hide.</summary>
    private void InvisibleInk(float dt)
    {
        if (Phase < 2 || _skin == null || _escaping || Mode == State.Dead || _finale)
        {
            if (Hidden || _hidden) ShowAll();
            return;
        }
        _inkClock += dt;
        if (_inkClock >= (_hidden ? HiddenFor : VisibleFor))
        {
            _inkClock = 0f;
            _hidden = !_hidden;
            SfxLettering.Spawn(_hidden ? "NOW YOU SEE ME..." : "...BOO!", (Vector2)transform.position + Vector2.up * 3.4f, Palette.LensGhost, 0.8f);
            GameAudio.Play("splat", 0.4f);
        }
        var lf = LightField.I;
        Vector2 me = (Vector2)transform.position + Vector2.up * 1.3f;
        bool found = lf != null && lf.Lens == Lens.Ghost && lf.BeamLive && (me - lf.BeamCentre).magnitude <= lf.BeamRadius + 0.8f;
        Hidden = _hidden && !found;
        _skin.sharedMaterials = Hidden ? _hiddenMats : _shownMats;
        if (Hidden) Health.Invulnerable = Mathf.Max(Health.Invulnerable, 0.1f);
    }

    private void ShowAll()
    {
        _hidden = false;
        Hidden = false;
        if (_skin != null && _shownMats != null) _skin.sharedMaterials = _shownMats;
    }

    private void BeginEscape()
    {
        if (_escaping) return;
        ShowAll();
        _escaping = true;
        _escapeFor = 0f;
        Health.hp = Mathf.Max(Health.hp, 1f);
        Health.Invulnerable = 1e4f;
        Attack.End();
        Velocity = Vector2.zero;
        Clips?.Play("Dive", 0.08f, 1f, restart: true);
        SfxLettering.Spawn("CURSES!", (Vector2)transform.position + Vector2.up * 3.2f, Palette.Paper, 1.1f, burst: true);
    }

    /// <summary>The dive runs on his clock too: catch him in the dark and he's stuck halfway in.</summary>
    private void Escape(float dt)
    {
        _escapeFor += dt;
        if (!_gone && _escapeFor >= DiveLength)
        {
            _gone = true;
            Model.gameObject.SetActive(false);
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            SfxLettering.Spawn("GLORP!", (Vector2)transform.position + Vector2.up * 1.2f, Palette.Paper, 1.2f, burst: true);
            GameAudio.Play("splat", 0.9f);
            foreach (var e in _summoned)                                 // his Smudges melt without him
            {
                if (e == null || e.Health.Dead) continue;
                SfxLettering.Spawn("SPLOOSH!", (Vector2)e.transform.position + Vector2.up * 1.4f, Palette.Paper, 0.8f);
                PageManager.I?.EnemyDown(e);
                Destroy(e.gameObject);
            }
            _summoned.Clear();
        }
        if (_gone && !_finished && _escapeFor >= DiveLength + 0.8f)
        {
            _finished = true;
            PageManager.I?.EnemyDown(this);
            PageManager.I?.Finish(Phase == 1 ? "TO BE CONTINUED ON PAGE 7!" : "UP TO THE ROOF!\n<size=45%>TO BE CONTINUED ON PAGE 9!</size>");
        }
    }
}
