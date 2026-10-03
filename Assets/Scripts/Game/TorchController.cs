using System;
using UnityEngine;

/// <summary>
/// Roshan's wind-up torch (GDD sections 3 and 4): the mouse aims the beam through the page,
/// right click switches it on and off, the scroll wheel twists the crank, the middle button (or
/// Tab, or 1 to 4) twists the lens wheel, and holding left mouse makes the beam glide to Max
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
    public const float FullRadius = 4f, LowRadius = 2.5f, RedRadius = 3f, FollowSpeed = 12f;

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

    private void Awake()
    {
        I = this;
        Charge = gameObject.AddComponent<ChargeSystem>();
        Lenses = gameObject.AddComponent<LensWheel>();
        Charge.Twisted += _ => Jolt();
        Charge.Flared += OnFlare;
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
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
        AudioDirector.I?.TapeStop(!on);               // the music is in the comic too
        Toggled?.Invoke(on);
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
    }

    private void Update()
    {
        if (BookmarkPause.IsPaused) return;
        float dt = Time.deltaTime;

        // ---- the reader's hand -----------------------------------------------------------
        if (GameInput.TorchTogglePressed) Toggle();
        int twists = GameInput.Twists;
        if (twists > 0)
        {
            Charge.Twist(twists);
            Cranked?.Invoke(twists);
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
        if (Lenses.Current == Lens.Red) r = Mathf.Min(r, RedRadius);
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
    }
}
