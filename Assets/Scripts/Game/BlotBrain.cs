using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Baron Blot (GDD section 6), 300 HP over three pages, and too heavy to knock about: only a heavy
/// blow staggers him. He keeps his distance, sends ink waves rolling along the floor with a sweep
/// of his cane (ink like everything else: they freeze in the dark), calls geysers up through the
/// floor round Max ("WELL, WELL, WELL..."), lunges with the cane ("EN GARDE!": jump it or dodge
/// through), and summons help. Pile on the damage and he melts into his ink and pops up across the
/// room. Nobody leaves his panel while he's in it.
///   Phase 1, the Office (page 6)  300 to 150: Smudges. Then he dives into his pool: "TO BE CONTINUED".
///   Phase 2, the Vat (page 8)     150 to 80: he slips into invisible ink every few seconds (only his
///                                  eyes and monocle show; the Ghost lens finds him); bats. Then up to the roof.
///   Phase 3, the Roof (page 9)    140 to 0:  all of it, a Smudge and a Dot-Shot at a time; at 60 Mom
///                                  flips the room light on (<see cref="Finale"/>), and at 0 he's done.
/// His exits, the blink and the getaway, run on the page's own time, not his clock: once he's melted
/// into his ink there's nothing of him left to light, so they always finish (the page turns).
/// </summary>
public class BlotBrain : EnemyBrain
{
    /// <summary>His health page by page: the office from full to 150, the vat from 150 to 80, the roof from 140
    /// to nothing, with the room light on from 60.</summary>
    public const float MaxHp = 300f, VatHp = 150f, RoofHp = 140f, FinaleAt = 60f;
    public const float Speed = 1.8f, KeepAway = 6.5f;
    public const float VisibleFor = 6f, HiddenFor = 5f;
    public const float ThrowLength = 1f, ThrowRelease = 0.54f;          // the clips' timings (24 fps)
    public const float SummonLength = 1.25f, SummonRelease = 0.71f, DiveLength = 1.5f;

    protected override float TurnYaw => 40f;                             // he plays to the reader
    protected override bool Flings => false;                             // he makes his own exits
    protected override bool ShowsWaking => false;

    public const float LungeWindup = 0.55f, LungeFor = 0.65f, LungeSpeed = 9f;
    /// <summary>This much damage in quick succession (it drains 8 a second) and he blinks away.</summary>
    public const float BlinkAfter = 28f, BlinkFor = 0.55f;

    private new enum Move { None, Throw, Summon, Laugh, Geysers, Lunge }      // (his own moves, not EnemyBrain.Move)
    private Move _move, _last;
    private float _hurtTally, _blinkFor, _secondWave;
    private bool _blinking;
    private Vector3 _blinkTo;
    private readonly List<Renderer> _blinked = new();
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
    private float EscapeHp => Phase == 1 ? 150f : 80f;

    protected override void Awake()
    {
        base.Awake();
        int page = GameState.I != null ? GameState.I.Page : 6;
        Phase = page >= 9 ? 3 : page >= 8 ? 2 : 1;
        Health.maxHp = MaxHp;
        Health.hp = Phase switch { 3 => RoofHp, 2 => VatHp, _ => MaxHp };
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
        if (_escaping) return;                                          // (his exits run on the page's time: LateUpdate)
        _hurtTally = Mathf.Max(0f, _hurtTally - 8f * dt);
        if (_blinking) return;
        if (_secondWave > 0f && (_secondWave -= dt) <= 0f) SecondWave();

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
            else Begin(Choose());
            return;
        }

        // keep his distance: glide back from Max, or after him, never leaving his office
        float dist = Mathf.Abs(ToHeroX);
        float want = dist < KeepAway - 1.2f ? -Facing : dist > KeepAway + 1.2f ? Facing : 0f;
        Velocity.x = Mathf.MoveTowards(Velocity.x, want * Speed, 8f * dt);
        Clips?.Play(Mathf.Abs(Velocity.x) > 0.25f ? "Move" : "Idle", 0.18f);
    }

    /// <summary>His repertoire: the wave along the floor (not at point-blank range), geysers round Max,
    /// and the lunge when Max is near enough to reach. Never the same trick twice running if he can help it.</summary>
    private Move Choose()
    {
        float dist = Mathf.Abs(ToHeroX);
        float wave = dist > 2.2f ? (Phase == 1 ? 0.45f : 0.36f) : 0f;
        float geysers = Phase == 1 ? 0.3f : 0.32f;
        float lunge = dist < 7.5f ? (Phase == 1 ? 0.25f : 0.32f) : 0f;
        Move pick = Move.Geysers;
        for (int tries = 0; tries < 2; tries++)
        {
            float roll = Random.value * (wave + geysers + lunge);
            pick = roll < wave ? Move.Throw : roll < wave + geysers ? Move.Geysers : Move.Lunge;
            if (pick != _last) break;
        }
        return pick;
    }

    private void Begin(Move m)
    {
        _move = m;
        if (m != Move.Laugh && m != Move.Summon) _last = m;
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
            case Move.Geysers:
                Clips?.Play("Summon", 0.06f, 1f, restart: true);               // the cane up... and down on the floor
                SfxLettering.Spawn("WELL, WELL, WELL...", (Vector2)transform.position + Vector2.up * 3.4f, Palette.Paper, 0.75f);
                break;
            case Move.Lunge:
                Clips?.Play("Throw", 0.06f, 0.5f, restart: true);              // the cane drawn back, slowly: the tell
                SfxLettering.Spawn("EN GARDE!", (Vector2)transform.position + Vector2.up * 3.4f, Palette.Paper, 0.85f);
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
                    if (Phase >= 2) _secondWave = 0.5f;                     // from the vat on, a twin a beat behind
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
            case Move.Geysers:
                if (!_fired && StateTime >= SummonRelease)
                {
                    _fired = true;
                    Geysers();
                }
                if (StateTime >= SummonLength) Done();
                break;
            case Move.Lunge:
                Lunge();
                break;
        }
    }

    private void Done()
    {
        _move = Move.None;
        _cool = Phase switch { 1 => Random.Range(1.0f, 1.8f), 2 => Random.Range(0.8f, 1.5f), _ => Random.Range(0.7f, 1.3f) };
        Enter(State.Recover);
        Clips?.Play("Idle", 0.15f);
    }

    /// <summary>"WELL, WELL, WELL...": geysers up through the floor round Max, from under his feet
    /// outward, with gaps between them to stand in.</summary>
    private void Geysers()
    {
        if (Hero == null) return;
        float hx = Hero.transform.position.x, from = Hero.transform.position.y + 1.5f;
        float[] at = Phase == 1 ? new[] { 0f, -2.6f, 2.6f } : new[] { 0f, -2.4f, 2.4f, -4.8f, 4.8f };
        bool lettered = false;                                           // one BLUB and one SPURT! a row
        for (int i = 0; i < at.Length; i++)
        {
            float x = hx + at[i];
            if (x < MinX + 0.7f || x > MaxX - 0.7f || !FloorAt(x, from, out float y)) continue;
            InkGeyser.Spawn(new Vector2(x, y), gameObject, delay: (i + 1) / 2 * 0.22f, letters: !lettered);
            lettered = true;
        }
        GameEvents.Impact(0.25f);
        GameAudio.Play("splat", 0.6f);
    }

    /// <summary>The lunge: a slow tell, then he glides at Max with the cane out. It's on his clock like
    /// everything else, so freeze him mid-lunge and the cane's still out when he wakes.</summary>
    private void Lunge()
    {
        if (StateTime < LungeWindup) return;
        if (!_fired)
        {
            _fired = true;
            FaceHero();
            Clips?.Play("Move", 0.05f, 2.2f, restart: true);
            Attack.Begin(Melee(15f, new Vector2(9f, 3f), 0.6f, "WHACK!", hearts: 2), new Vector2(1.2f, 1.3f), new Vector2(1.7f, 1.9f));   // the boss: 2 hearts (GDD section 5)
            MangaFx.Dash(transform, Facing, LungeFor);
            GameAudio.Play("whoosh", 0.6f);
        }
        Velocity.x = Facing * LungeSpeed;
        bool atWall = (Facing > 0f && transform.position.x > MaxX - 1.2f) || (Facing < 0f && transform.position.x < MinX + 1.2f);
        if (StateTime >= LungeWindup + LungeFor || atWall)
        {
            Attack.End();
            Velocity.x = 0f;
            Done();
        }
    }

    private void SecondWave()
    {
        if (Mode == State.Dead || _escaping || Hero == null) return;
        InkWave.Spawn((Vector2)transform.position + new Vector2(Facing * 1.1f, 0f), Facing, MinX, MaxX, gameObject);
        SfxLettering.Spawn("AND ANOTHER!", (Vector2)transform.position + new Vector2(Facing * 1.4f, 2.6f), Palette.Paper, 0.7f);
        GameAudio.Play("splat", 0.45f);
    }

    // ---- the blink: beaten on, he melts into his ink and pops up across the room -------------------

    private void BeginBlink()
    {
        if (Hero == null || !BlinkSpot(out _blinkTo)) return;
        _blinking = true;
        _blinkFor = BlinkFor;
        _hurtTally = 0f;
        _move = Move.None;
        Attack.End();
        Velocity = Vector2.zero;
        Health.Invulnerable = Mathf.Max(Health.Invulnerable, BlinkFor + 0.3f);
        Enter(State.Recover);
        SfxLettering.Spawn("SPLURT!", (Vector2)transform.position + Vector2.up * 1.4f, Palette.Paper, 0.9f, burst: true);
        GameAudio.Play("splat", 0.8f);
        _blinked.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r.enabled) { r.enabled = false; _blinked.Add(r); }
    }

    /// <summary>His exits run on the page's own time, not his clock, so they always finish: melted into his ink
    /// (mid-blink, or gone under for good) there's nothing of him left to light, and a page waiting for a beam on
    /// an empty spot would never turn (its door stays locked while he's in the room).</summary>
    private void LateUpdate()
    {
        float dt = Time.deltaTime;                                       // still held by the Bookmark and a hit-stop
        if (dt <= 0f || Mode == State.Dead) return;
        if (_escaping) Escape(dt);
        else if (_blinking) Blink(dt);
    }

    /// <summary>The blink: a puddle for a moment, then he pops up across the room.</summary>
    private void Blink(float dt)
    {
        Velocity = Vector2.zero;
        if ((_blinkFor -= dt) > 0f) return;
        _blinking = false;
        if (Body != null) Body.enabled = false;
        transform.position = _blinkTo;
        if (Body != null) Body.enabled = true;
        foreach (var r in _blinked) if (r != null) r.enabled = true;
        _blinked.Clear();
        FaceHero();
        SfxLettering.Spawn("TA-DA!", (Vector2)transform.position + Vector2.up * 3.2f, Palette.Paper, 0.9f);
        GameAudio.Play("splat", 0.6f);
        _cool = 0.5f;
        Enter(State.Recover);
    }

    /// <summary>Somewhere across the room from Max, on the floor, with nothing in the way.</summary>
    private bool BlinkSpot(out Vector3 spot)
    {
        spot = transform.position;
        float hx = Hero.transform.position.x, best = 4f;
        bool found = false;
        foreach (float x in new[] { MinX + 2.5f, MaxX - 2.5f, (MinX + MaxX) * 0.5f })
        {
            float away = Mathf.Abs(x - hx);
            if (away <= best || !FloorAt(x, transform.position.y + 1.5f, out float y) || Mathf.Abs(y - transform.position.y) > 0.6f) continue;
            var p = new Vector3(x, y + 0.05f, 0f);
            if (!ClearAt(p)) continue;
            best = away;
            spot = p;
            found = true;
        }
        return found;
    }

    private static readonly RaycastHit[] _floorHits = new RaycastHit[8];
    private static readonly Collider[] _clearHits = new Collider[8];

    /// <summary>The top of the floor under a point (not somebody's head, not a loose prop).</summary>
    private bool FloorAt(float x, float fromY, out float y)
    {
        y = float.NegativeInfinity;
        int n = Physics.RaycastNonAlloc(new Vector3(x, fromY, 0f), Vector3.down, _floorHits, 6f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = _floorHits[i].collider;
            if (c.GetComponentInParent<Health>() != null || c.GetComponentInParent<FallingProp>() != null) continue;
            y = Mathf.Max(y, _floorHits[i].point.y);
        }
        return !float.IsNegativeInfinity(y);
    }

    /// <summary>Room for him to stand at a point (Max and other Inkies don't count: bodies pass).</summary>
    private bool ClearAt(Vector3 feet)
    {
        float r = Body != null ? Body.radius : 0.5f, h = Body != null ? Body.height : 3f;
        int n = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * (r + 0.1f), feet + Vector3.up * (h - r), r, _clearHits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = _clearHits[i];
            if (c.transform.IsChildOf(transform) || c.GetComponentInParent<Health>() != null) continue;
            return false;
        }
        return true;
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
            if (e != null) e.Summoned = true;
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
        if (hit.heavy && _move != Move.Throw && _move != Move.Summon && _move != Move.Geysers && !_blinking)
        {
            Attack.End();
            _move = Move.None;
            Enter(State.Hurt);
            HurtFor = 0.45f;
            Clips?.Play("Hit", 0.04f, 1f, restart: true);
        }
        if (Phase < 3 && Health.hp <= EscapeHp) { BeginEscape(); return; }
        if (Phase == 3 && !_finale && Health.hp <= FinaleAt)
        {
            _finale = true;
            Finale.I?.Begin();                                          // Mom flips the room light on
            return;
        }
        // he won't stand there and take a beating: pile it on and he melts away across the room
        _hurtTally += hit.damage;
        if (_hurtTally >= BlinkAfter && !_blinking && !_finale && _move != Move.Lunge) BeginBlink();
    }

    /// <summary>His help melts without him (when he escapes, and when he falls for good: nothing's left to
    /// fight through Mom's lines at the end).</summary>
    private void MeltSummons()
    {
        foreach (var e in _summoned)
        {
            if (e == null || e.Health.Dead) continue;
            SfxLettering.Spawn("SPLOOSH!", (Vector2)e.transform.position + Vector2.up * 1.4f, Palette.Paper, 0.8f);
            PageManager.I?.EnemyDown(e);
            Destroy(e.gameObject);
        }
        _summoned.Clear();
    }

    protected override void OnDied(Hit hit)
    {
        if (Phase < 3) { BeginEscape(); return; }                       // the first two always end in a getaway
        ShowAll();
        MeltSummons();
        base.OnDied(hit);
        SfxLettering.Spawn("NOOOO...!", (Vector2)transform.position + Vector2.up * 3.4f, Palette.Paper, 1.2f, burst: true, after: 0.35f);   // after the blow's own word
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
        if (_blinking)                                                   // caught mid-blink: back in the room to make his exit
        {
            _blinking = false;
            foreach (var r in _blinked) if (r != null) r.enabled = true;
            _blinked.Clear();
        }
        _escaping = true;
        _escapeFor = 0f;
        Health.hp = Mathf.Max(Health.hp, 1f);
        Health.Invulnerable = 1e4f;
        Attack.End();
        Velocity = Vector2.zero;
        Clips?.Play("Dive", 0.08f, 1f, restart: true);
        SfxLettering.Spawn("CURSES!", (Vector2)transform.position + Vector2.up * 3.2f, Palette.Paper, 1.1f, burst: true);
    }

    /// <summary>The getaway: he dives into his pool (GLORP!), and the page ends on its card.</summary>
    private void Escape(float dt)
    {
        _escapeFor += dt;
        Velocity = Vector2.zero;                                        // gone under, his body doesn't sink on through the floor
        if (!_gone && _escapeFor >= DiveLength)
        {
            _gone = true;
            Model.gameObject.SetActive(false);
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            SfxLettering.Spawn("GLORP!", (Vector2)transform.position + Vector2.up * 1.2f, Palette.Paper, 1.2f, burst: true);
            GameAudio.Play("splat", 0.9f);
            MeltSummons();
        }
        if (_gone && !_finished && _escapeFor >= DiveLength + 0.8f)
        {
            _finished = true;
            PageManager.I?.EnemyDown(this);
            PageManager.I?.Finish(Phase == 1 ? "TO BE CONTINUED ON PAGE 7!" : "UP TO THE ROOF!\n<size=45%>TO BE CONTINUED ON PAGE 9!</size>");
        }
    }
}
