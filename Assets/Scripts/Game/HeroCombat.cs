using UnityEngine;

/// <summary>
/// Max's moveset (GDD section 5). Small and readable, because half the player's attention is
/// on the torch.
///   E, E, E      Jab (6), Cross (6), Haymaker (14, knocks back 3 units, reflects pellets, KAPOW)
///   Q            Kick (10, pushes 2 units)
///   W + Q        Launcher (8, pops an enemy into the air: freeze it there to make a step)  page 2
///   Q in the air Dive kick (12, bounces Max up 2 units)                                   page 2
///   S + Q in air Ground slam (10 within 2 units, stuns 1.5 s, KRAK)                       page 4
///   F            Splash Page (30 to every Inkie awake in the beam, when the meter is full) page 5
/// Hits only land between two awake things; every landed hit feeds the Splash meter.
/// </summary>
[RequireComponent(typeof(HeroController))]
public class HeroCombat : MonoBehaviour
{
    private enum Move { None, Jab, Cross, Haymaker, Kick, Launcher, DiveKick, GroundSlam }

    private struct Def
    {
        public string state;
        public float total, from, to, speed, lunge;
        public float damage, stun;
        public Vector2 knock, offset, size;
        public string word;
        public bool heavy, reflects;
    }

    private static Def D(string state, float total, float from, float to, float damage, Vector2 knock, float stun,
        Vector2 offset, Vector2 size, string word, float speed = 1f, float lunge = 0f, bool heavy = false, bool reflects = false) =>
        new Def { state = state, total = total, from = from, to = to, damage = damage, knock = knock, stun = stun, offset = offset,
                  size = size, word = word, speed = speed, lunge = lunge, heavy = heavy, reflects = reflects };

    // Knockback is lane units per second; the target's friction turns it into distance. Every swing
    // reaches back over Max's own body too: bodies pass through each other, so an Inkie he has run
    // into is still in front of his fist.
    private static readonly Def Jab = D("Jab", 0.32f, 0.08f, 0.17f, 6f, new Vector2(3f, 0f), 0.3f, new Vector2(0.7f, 1.3f), new Vector2(1.6f, 0.9f), "POW!", 1f, 1.5f);
    private static readonly Def Cross = D("Cross", 0.36f, 0.1f, 0.19f, 6f, new Vector2(4f, 0f), 0.3f, new Vector2(0.75f, 1.3f), new Vector2(1.7f, 0.9f), "BIFF!", 1f, 1.5f);
    private static readonly Def Haymaker = D("Haymaker", 0.6f, 0.24f, 0.36f, 14f, new Vector2(10f, 3f), 0.7f, new Vector2(0.85f, 1.3f), new Vector2(1.9f, 1.2f), "KAPOW!", 1f, 2.5f, heavy: true, reflects: true);
    private static readonly Def Kick = D("Kick", 0.45f, 0.14f, 0.26f, 10f, new Vector2(7f, 1f), 0.45f, new Vector2(0.85f, 0.8f), new Vector2(1.8f, 0.9f), "THWACK!", 1f, 1f);
    private static readonly Def Launcher = D("Launcher", 0.5f, 0.14f, 0.26f, 8f, new Vector2(1.5f, 12f), 1.0f, new Vector2(0.75f, 1.0f), new Vector2(1.6f, 1.6f), "ZOING!");
    private static readonly Def DiveKick = D("DiveKick", 1.2f, 0.05f, 1.2f, 12f, new Vector2(6f, 2f), 0.5f, new Vector2(0.6f, 0.3f), new Vector2(1.2f, 1.0f), "WHAM!");
    private static readonly Def Slam = D("SlamDive", 1.2f, 0f, 0f, 10f, new Vector2(5f, 6f), 1.5f, Vector2.zero, new Vector2(4f, 1.6f), "KRAK!", heavy: true);

    public bool Busy => _move != Move.None;
    public bool Airborne => _move == Move.DiveKick || _move == Move.GroundSlam;
    public bool Slamming => _move == Move.GroundSlam;
    public bool OverridesGravity => Airborne;
    public float Lunge => Busy && _t < Current.from ? Current.lunge : 0f;

    private Move _move;
    private Def Current => _move switch
    {
        Move.Jab => Jab, Move.Cross => Cross, Move.Haymaker => Haymaker, Move.Kick => Kick,
        Move.Launcher => Launcher, Move.DiveKick => DiveKick, Move.GroundSlam => Slam, _ => default,
    };

    private float _t;
    private bool _queued, _slammed;
    private Hitbox _hitbox;
    private HeroController _hero;
    private Lightable _light;

    private int PageNumber => GameState.I != null ? GameState.I.Page : 1;

    private void Awake()
    {
        _hero = GetComponent<HeroController>();
        _light = GetComponent<Lightable>();
        _hitbox = new Hitbox(gameObject, Team.Hero);
        _hitbox.Landed += OnLanded;
    }

    public void Interrupt()
    {
        _hitbox.End();
        _move = Move.None;
        _queued = false;
    }

    private void Begin(Move m)
    {
        _move = m;
        _t = 0f;
        _queued = false;
        _slammed = false;
        var d = Current;
        _hitbox.End();
        _hero.Clips?.Play(d.state, 0.04f, d.speed, restart: true);
        if (m == Move.DiveKick) _hero.Velocity = new Vector2(_hero.Facing * 9f, -13f);
        if (m == Move.GroundSlam) _hero.Velocity = new Vector2(0f, -22f);
        GameAudio.Play("whoosh", 0.35f);
    }

    private void Update()
    {
        float dt = _light.Delta;
        if (dt <= 0f) return;
        if (_hero.Dead)
        {
            if (Busy) Interrupt();                       // a swing (or a slam on its way down) dies with him
            return;
        }
        if (_hero.Hurting || _hero.Dodging || _hero.Locked || _hero.Hushed)
        {
            if (Busy) Interrupt();
            return;
        }

        // ---- input -------------------------------------------------------------------------
        if (GameInput.PunchPressed)
        {
            if (_move == Move.None) Begin(Move.Jab);
            else if (_move == Move.Jab || _move == Move.Cross) _queued = true;
        }
        if (GameInput.KickPressed && _move == Move.None)
        {
            if (!_hero.Grounded)
            {
                if (GameInput.Down && PageNumber >= 4) Begin(Move.GroundSlam);
                else if (PageNumber >= 2) Begin(Move.DiveKick);
            }
            else Begin(GameInput.Up && PageNumber >= 2 ? Move.Launcher : Move.Kick);
        }
        if (GameInput.SplashPressed) TrySplashPage();

        if (_move == Move.None) return;
        _t += dt;
        var d = Current;

        // ---- the move ------------------------------------------------------------------------
        if (_move == Move.DiveKick)
        {
            _hero.Velocity = new Vector2(_hero.Facing * 9f, -13f);
            if (!_hitbox.Active) _hitbox.Begin(HitFrom(d), d.offset, d.size);
            _hitbox.Tick(_hero.Facing);
            if (_t > d.total) Interrupt();
            return;
        }
        if (_move == Move.GroundSlam)
        {
            if (!_slammed) _hero.Velocity = new Vector2(0f, -22f);
            else if (_t > 0.35f) Interrupt();
            return;
        }

        bool active = _t >= d.from && _t <= d.to;
        if (active && !_hitbox.Active) _hitbox.Begin(HitFrom(d), d.offset, d.size);
        if (!active && _hitbox.Active) _hitbox.End();
        if (_hitbox.Active)
        {
            _hitbox.Tick(_hero.Facing);
            if (d.reflects) InkPellet.ReflectIn(_hitbox.Centre(_hero.Facing), d.size, gameObject);
        }

        if (_t >= d.total)
        {
            Move next = _queued ? (_move == Move.Jab ? Move.Cross : _move == Move.Cross ? Move.Haymaker : Move.None) : Move.None;
            if (next != Move.None) Begin(next);
            else Interrupt();
        }
    }

    private Hit HitFrom(Def d) => new Hit
    {
        damage = d.damage,
        hearts = 1,
        knockback = d.knock,
        stun = d.stun,
        heavy = d.heavy,
        word = d.word,
    };

    /// <summary>Called by the controller when Max touches down.</summary>
    public void Landed()
    {
        if (_hero.Dead) { Interrupt(); return; }         // a body landing is no slam
        if (_move == Move.DiveKick) Interrupt();
        if (_move == Move.GroundSlam && !_slammed)
        {
            _slammed = true;
            _t = 0f;
            _hero.Clips?.Play("GroundSlam", 0.03f, 1f, restart: true);     // the superhero landing
            var d = Slam;
            var hit = HitFrom(d);
            hit.pierce = true;                                            // down through the Bruiser's guard
            _hitbox.Begin(hit, new Vector2(0f, 0.6f), d.size);
            _hitbox.Tick(1f);
            _hitbox.End();
            SfxLettering.Spawn("KRAK!", (Vector2)transform.position + new Vector2(0f, 2.5f), Palette.Yellow, 1.3f, burst: true);   // above the landing pose
            MangaFx.Focus((Vector2)transform.position + Vector2.up * 0.8f, 1.3f);
            GameEvents.Impact(0.7f);
            GameAudio.Play("slam", 0.9f);
        }
    }

    private void OnLanded(Health target, Hit hit)
    {
        GameState.I?.HeroLanded();
        Vector2 at = (Vector2)target.transform.position + new Vector2(0f, 1.9f);
        // a blow on the Bruiser's armoured front: a dull grey CLANK! and the clank, no burst (go round behind him)
        bool clank = hit.word == "CLANK!";
        if (_move != Move.GroundSlam)
            SfxLettering.Spawn(hit.word ?? "POW!", at, clank ? Palette.PaperDim : Palette.Yellow, hit.heavy ? 1.25f : 0.9f, burst: hit.heavy && !clank);
        if ((hit.heavy || _move == Move.Launcher) && _move != Move.GroundSlam && !clank)
            MangaFx.Focus(at - new Vector2(0f, 0.9f), hit.heavy ? 1.1f : 0.8f);
        GameEvents.HitStop(hit.heavy && !clank ? 0.09f : 0.05f);
        GameEvents.Impact(hit.heavy && !clank ? 0.5f : 0.2f);
        GameAudio.Play(clank ? "clank" : hit.heavy ? "punch_heavy" : "punch", 0.8f);
        if (_move == Move.DiveKick)
        {
            _hero.Velocity = new Vector2(_hero.Facing * 3f, Mathf.Sqrt(2f * HeroController.Gravity * 2f));
            Interrupt();
        }
    }

    /// <summary>The Splash Page super: 30 to every Inkie awake inside the beam (meter full, page 5 on).</summary>
    private void TrySplashPage()
    {
        var gs = GameState.I;
        if (gs == null || gs.Splash < 100f || PageNumber < 5 || LightField.I == null) return;
        gs.AddSplash(-100f);
        int struck = 0;
        foreach (var h in Health.All.ToArray())
        {
            if (h.team != Team.Inkie || h.Dead || !h.IsAwake) continue;
            if (!LightField.I.IsLit(h.transform.position + Vector3.up)) continue;
            h.Apply(new Hit { damage = 30f, knockback = new Vector2(6f, 6f), stun = 1f, team = Team.Hero, source = gameObject, heavy = true, word = "KA-POW!" });
            SfxLettering.Spawn("KA-POW!", (Vector2)h.transform.position + new Vector2(0f, 2f), Palette.Yellow, 1.4f, burst: true);
            struck++;
        }
        GameEvents.Impact(1f);
        MangaFx.Focus((Vector2)transform.position + Vector2.up * 1.1f, 1.5f);
        GameHUD.I?.SplashPage(struck);
    }
}

internal static class HealthListExtensions
{
    public static Health[] ToArray(this System.Collections.Generic.IReadOnlyList<Health> list)
    {
        var a = new Health[list.Count];
        for (int i = 0; i < a.Length; i++) a[i] = list[i];
        return a;
    }
}
