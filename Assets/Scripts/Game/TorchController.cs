using System;
using UnityEngine;

/// <summary>
/// Roshan's wind-up torch (GDD sections 3 and 4): the mouse aims the beam through the page,
/// right click switches it on and off, the scroll wheel twists the crank, the middle button (or
/// Tab, or 1 and 2) switches the lens (Clear, or the Ghost lens), and holding left mouse makes the beam glide to Max
/// (follow assist).
///
/// Charge sets the beam:  100-40%  4.0 units
///                         39-11%  shrinking to 2.5 units, a faint buzz
///                          10-1%  2.5 units, flickering dropouts that freeze the page
///                             0%  off: everything freezes until you twist
/// Each twist jolts the beam (a 0.5 unit wobble that settles in 0.3 s), dims it a little while
/// you keep cranking and makes the bulb flicker like a real dynamo torch. All of it is pushed
/// into the <see cref="LightField"/> each frame.
/// </summary>
[DefaultExecutionOrder(-90)]
public class TorchController : MonoBehaviour
{
    public static TorchController I { get; private set; }

    public ChargeSystem Charge { get; private set; }
    public LensWheel Lenses { get; private set; }

    public bool On = true;
    public const float FullRadius = 4f, LowRadius = 2.5f, FollowSpeed = 12f;

    /// <summary>Where the beam is pointed (lane units) and where it actually is.</summary>
    public Vector2 Aim { get; private set; }
    public Vector2 Centre { get; private set; }
    public float Radius { get; private set; } = FullRadius;
    /// <summary>For tests: a lane point to aim at instead of the mouse.</summary>
    public Vector2? AimOverride;

    public event Action<bool> Toggled;
    public event Action Flickered;
    /// <summary>The crank was turned this many notches (even with the bulb cooling).</summary>
    public event Action<int> Cranked;
    /// <summary>The dynamo's flicker while it's being wound (set by the hand, 1 = steady).</summary>
    [NonSerialized] public float CrankFlicker = 1f;
    private float _glow = 1f;

    private float _wobble;
    private Vector2 _wobbleDir;
    private float _flickerIn = 2f, _dropoutFor;
    private Vector2 _followVel;

    // a turn of the wheel at full may overwind this many more notches (see Update), and has it clicked yet
    private int _overwindLeft;
    private bool _slipped;
    // the bulb's faint electrical buzz on a low charge
    private AudioSource _buzz;
    private static AudioClip _buzzClip;

    private void Awake()
    {
        I = this;
        Charge = gameObject.AddComponent<ChargeSystem>();
        Lenses = gameObject.AddComponent<LensWheel>();
        // the reader's hand is the real world: its sounds always play (GDD section 12 key cues)
        Charge.Twisted += overwind =>
        {
            Jolt();
            GameAudio.Play("twist", overwind > 0 ? 0.7f : 0.5f, overwind > 0 ? 0.12f : 0.05f);   // the ratchet; past full it rattles
        };
        Charge.Flared += OnFlare;
        Lenses.Swapped += _ => GameAudio.Play("lens", 0.6f, 0.03f);
        _buzz = gameObject.AddComponent<AudioSource>();
        _buzz.loop = true;
        _buzz.playOnAwake = false;
        _buzz.clip = BuzzClip();
        _buzz.volume = 0f;
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
    }

    private static readonly Collider[] _steps = new Collider[16];

    /// <summary>Is there a frozen freeze-step (a bat, a crate, an Inkie frozen in mid-air) under Max, or just
    /// ahead of him at or below his feet, that the light would wake?</summary>
    private static bool FrozenStepUnder(HeroController hero)
    {
        Vector3 feet = hero.transform.position;
        int n = Physics.OverlapBoxNonAlloc(feet + new Vector3(0f, -1.1f, 0f), new Vector3(3.0f, 1.2f, 1.5f), _steps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = _steps[i];
            if (c.GetComponentInParent<HeroController>() != null) continue;
            var light = c.GetComponentInParent<Lightable>();
            if (light == null || light.IsAwake) continue;              // floors have no Lightable; awake things aren't solid
            if (c.bounds.max.y > feet.y + 0.35f) continue;              // beside him, not under him
            if (c.GetComponentInParent<FallingProp>() != null) return true;
            var e = c.GetComponentInParent<EnemyBrain>();
            if (e != null && (e is SplotchBrain || !e.Grounded)) return true;   // a bat, or an Inkie launched and frozen
        }
        return false;
    }

    public void PlaceAt(Vector2 lanePoint)
    {
        Aim = Centre = lanePoint;
    }

    public void Toggle() => SetOn(!On);

    public void SetOn(bool on)
    {
        if (On == on) return;
        On = on;
        if (on) AudioDirector.I?.TorchOn(); else AudioDirector.I?.TorchOff();
        Toggled?.Invoke(on);                          // (the comic's music follows the light itself: GameMusic)
    }

    private void Jolt()
    {
        _wobble = 0.5f;
        float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        _wobbleDir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    private void OnFlare()
    {
        GameEvents.Lettering("SPRONG!", Centre + Vector2.up * 2f, Palette.Yellow, 1.4f, burst: true);
        GameAudio.Play("flare", 0.9f, 0.03f);
    }

    /// <summary>A second of mains-like hum, made in code: 100 Hz with its harmonics and a little grit, looping cleanly.</summary>
    private static AudioClip BuzzClip()
    {
        if (_buzzClip != null) return _buzzClip;
        const int rate = 22050;
        var data = new float[rate];
        var rng = new System.Random(3);
        for (int i = 0; i < rate; i++)
        {
            double t = i / (double)rate, w = 2.0 * System.Math.PI * 100.0 * t;
            double s = 0.55 * System.Math.Sin(w) + 0.3 * System.Math.Sin(2 * w) + 0.18 * System.Math.Sin(3 * w) + 0.1 * System.Math.Sign(System.Math.Sin(w));
            data[i] = (float)(s * 0.6 + (rng.NextDouble() - 0.5) * 0.06);
        }
        _buzzClip = AudioClip.Create("lowbattery", rate, 1, rate, false);
        _buzzClip.SetData(data, 0);
        return _buzzClip;
    }

    /// <summary>Below 40% charge the bulb buzzes faintly, louder as it fades; nothing when it's dark or dead.</summary>
    private void Buzz(bool lit, float share)
    {
        if (_buzz == null) return;
        float want = lit && share < 0.4f ? Mathf.Lerp(0.02f, 0.08f, Mathf.InverseLerp(0.4f, 0.1f, share)) : 0f;
        want *= Settings.MasterVolume * Settings.SfxVolume;
        _buzz.volume = Mathf.MoveTowards(_buzz.volume, want, Time.unscaledDeltaTime * 0.3f);
        if (_buzz.volume > 0.001f && !_buzz.isPlaying) _buzz.Play();
        else if (_buzz.volume <= 0.001f && _buzz.isPlaying) _buzz.Stop();
    }

    private void Update()
    {
        if (BookmarkPause.IsPaused) return;
        float dt = Time.deltaTime;

        // ---- the reader's hand -----------------------------------------------------------
        if (GameInput.TorchTogglePressed) Toggle();
        int twists = GameInput.Twists;
        // Past full, every notch on the wheel has to be its own deliberate twist: a turn of the wheel (a run of
        // scrolling without a pause) that starts at full may overwind one notch, and one that fills the spring stops
        // there. So a trackpad's swipe and its coasting, or spinning on past full, can't flare the bulb by itself;
        // twisting again (and again) still can. A key (R) counts every tap.
        if (GameInput.CrankStreamStarted)
        {
            _overwindLeft = Charge.Full ? 1 : 0;
            _slipped = false;
        }
        if (twists > 0)
        {
            int turned = twists;
            if (GameInput.CrankByWheel && Charge.Full)
            {
                twists = Mathf.Min(twists, _overwindLeft);
                _overwindLeft -= twists;
                if (twists == 0 && !_slipped) { _slipped = true; GameAudio.Play("twist", 0.25f, 0.02f); }   // full: the crank slips
            }
            if (twists > 0) Charge.Twist(twists);
            Cranked?.Invoke(turned);
        }
        int step = GameInput.LensStep;
        if (step != 0) Lenses.Step(step);
        int pick = GameInput.LensPick;
        if (pick >= 0) Lenses.Pick(pick);

        // ---- aim: through the page, or gliding to Max -------------------------------------
        if (AimOverride.HasValue) Aim = AimOverride.Value;
        else if (GameInput.Pointer is Vector2 screen && PageView.ScreenToLane(screen, out var lane)) Aim = lane;

        var hero = HeroController.I;
        bool follow = GameInput.FollowHeld && hero != null;
        if (follow)
        {
            Vector2 target = (Vector2)hero.transform.position + Vector2.up * 1.0f;
            // a freeze-step: up on (or just over) something frozen, a bat, a crate, the light rides
            // above him with its bottom rim at his feet, so the thing under him stays dark
            if (FrozenStepUnder(hero)) target = (Vector2)hero.transform.position + Vector2.up * (0.2f + Radius - 0.4f);
            Centre = Vector2.SmoothDamp(Centre, target, ref _followVel, 0.12f, FollowSpeed, dt);
        }
        else
        {
            _followVel = Vector2.zero;
            Centre = Vector2.Lerp(Centre, Aim, 1f - Mathf.Exp(-dt * 30f));
        }

        // ---- charge and the beam it buys ---------------------------------------------------
        bool live = On && !Charge.Dead && Charge.Charge > 0f;
        if (live) Charge.Drain(dt, LensWheel.DrainRate(Lenses.Current));
        float share = Charge.Share;
        float r = share >= 0.4f ? FullRadius
                : share > 0.1f ? Mathf.Lerp(LowRadius, FullRadius, (share - 0.1f) / 0.3f)
                : LowRadius;
        Radius = Mathf.MoveTowards(Radius, r, dt * 4f);

        // low charge: flickering dropouts (80 to 200 ms every 1 to 3 s)
        bool dropout = false;
        if (live && share <= 0.1f)
        {
            if (_dropoutFor > 0f)
            {
                _dropoutFor -= dt;
                dropout = _dropoutFor > 0f;
            }
            else if ((_flickerIn -= dt) <= 0f)
            {
                _flickerIn = UnityEngine.Random.Range(1f, 3f);
                _dropoutFor = UnityEngine.Random.Range(0.08f, 0.2f);
                dropout = true;
                Flickered?.Invoke();
            }
        }

        // twisting jolts and dims the beam
        _wobble = Mathf.MoveTowards(_wobble, 0f, dt * (0.5f / 0.3f));
        Vector2 jolt = _wobbleDir * _wobble * Mathf.Sin(Time.time * 60f);
        bool twisting = Charge.SinceTwist < 0.25f;

        var field = LightField.I;
        if (field == null) return;
        field.BeamCentre = Centre + jolt;
        field.BeamRadius = Radius;
        field.BeamOn = live;
        field.Dropout = dropout;
        field.Lens = Lenses.Current;
        _glow = Mathf.MoveTowards(_glow, twisting ? 0.85f : 1f, dt * 6f);
        field.Brightness = _glow * CrankFlicker;
        Buzz(live && !dropout, share);
        GhostLesson(dt);
    }

    // ---- the Ghost lens, taught once (per save) on page 7, where it's first needed --------------------------------

    /// <summary>What the torch in Roshan's hand says while the Ghost lens is being taught ("PURPLE!"), or null.</summary>
    public string LessonHint { get; private set; }
    private bool _lessonShowing;
    private float _lessonDone = -1f;           // seconds left of the "that's it" step (-1: not reached yet)

    private const string GhostIntro = "<color=#B26BFF>THE GHOST LENS!</color>  PRESS {LENS} FOR PURPLE LIGHT\n" +
                                      "<size=68%>IT SHOWS INVISIBLE INK: HIDDEN LEDGES, STARS... AND BLOT. IT DRAINS THE TORCH TWICE AS FAST.</size>";
    private const string GhostOn = "PURPLE LIGHT: THE INVISIBLE LEDGES ARE SOLID NOW.\n" +
                                   "<size=68%>PRESS {LENS} AGAIN FOR NORMAL LIGHT (IT LASTS LONGER).</size>";

    private void GhostLesson(float dt)
    {
        var gs = GameState.I;
        bool momTeaching = MomDirector.I != null && MomDirector.I.Teaching;    // her lesson has the box first
        bool due = !Settings.GhostTaught && Lenses.Unlocked[(int)Lens.Ghost] && gs != null && gs.Page >= 7 &&
                   PageManager.I != null && !PageManager.I.Busy && !momTeaching;
        if (!due)
        {
            if (_lessonShowing && !momTeaching) GameHUD.I?.Teach(null);
            _lessonShowing = false;
            LessonHint = null;
            return;
        }
        if (Lenses.Current == Lens.Ghost && _lessonDone < 0f) _lessonDone = 4.5f;
        if (_lessonDone < 0f)
        {
            LessonHint = "PURPLE! ({LENS})";
            GameHUD.I?.Teach(GhostIntro);
            _lessonShowing = true;
            return;
        }
        LessonHint = null;
        GameHUD.I?.Teach(GhostOn);
        _lessonShowing = true;
        if ((_lessonDone -= dt) > 0f && Lenses.Current == Lens.Ghost) return;
        // (back to Clear, or a few seconds of purple light): it's learned
        Settings.GhostTaught = true;
        Settings.Save();
        GameHUD.I?.Teach(null);
        _lessonShowing = false;
    }
}
