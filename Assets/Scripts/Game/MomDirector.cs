using System;
using System.Collections.Generic;
using UnityEngine;

public enum MomState { Asleep, Stirring, Approaching, AtDoor, Opening, Leaving }

/// <summary>
/// Mom, the real-world clock (GDD section 8). You can see her coming: the hallway light clicks
/// on, slippers shuffle up, the handle turns, and sometimes the door creaks open and a wedge of
/// hallway light falls across the page, waking everything in it. It falls where it hurts: on the
/// biggest crowd of Inkies in view, or a bat. While her door is open Max holds his breath (he
/// can't move or fight, and nothing can hurt him); the torch is still Roshan's to switch off.
///
///   Asleep       25 to 55 s between visits          suspicion falls 15 a second
///   Stirring     1.5 s: the strip under the door brightens
///   Approaching  4 s (6 s in Act 1): footsteps, foot shadows, footprint icons. Your warning.
///   At the door  3 to 6 s: +45/s if the torch is on (light leaks under the door), +30/s twisting
///   Opening      4 s: the door swings open, her silhouette; +90/s if the torch is on
///   Leaving      2 s: the door closes, "...kids."
/// A flare while she's coming adds 50. At 100
/// you're Caught: BUSTED, a slipper lost, the panel restarts. The "easy" setting halves the rates.
/// A cat sometimes pads up instead: paws in the strip, a scratch, "mrrp". No need to hide.
/// Her first visit ever (once per save, Settings.MomTaught) is a tutorial: the page says to switch the torch
/// off before she reaches the door, she waits out in the hall up to 10 s while it's on, and afterwards it asks
/// for the torch back on.
/// </summary>
public class MomDirector : MonoBehaviour
{
    public static MomDirector I { get; private set; }

    public MomState State { get; private set; } = MomState.Asleep;
    public float Suspicion { get; private set; }
    public bool IsCat { get; private set; }
    /// <summary>0 = shut, 1 = open as far as she opens it.</summary>
    public float Open { get; private set; }
    public float Handle { get; private set; }
    /// <summary>The hallway strip under the door, 0.3 (night light) to 1 (hall light on).</summary>
    public float Strip { get; private set; } = 0.3f;
    /// <summary>0 = nobody at the door, 1 = feet right outside.</summary>
    public float Feet { get; private set; }
    /// <summary>Seconds left of BUSTED: the door stands wide open, the hall light floods in.</summary>
    public float Bust { get; private set; }
    /// <summary>The finale: she's in the open doorway with the room light on, watching the comic.</summary>
    public bool Watching { get; private set; }
    /// <summary>Her door is open: Max holds still (and nothing lands on him) till it shuts.</summary>
    public bool HoldingStill => !IsCat && !Watching && !_quietLeave && Bust <= 0f &&
                                (State == MomState.Opening || (State == MomState.Leaving && Open > 0f));
    public float StateTime => _t;
    public const string ShhLine = "Shh... don't move a muscle.";

    public event Action<MomState> Changed;

    private int _visitsLeft;
    private float _openChance, _approach = 4f, _atDoor = 4f, _next;
    private bool _catLeft, _opens, _firstOpens, _first, _quietLeave;

    /// <summary>Her first visit ever (once per save) is a tutorial: the page says what to do and when, and she
    /// waits out in the hall a while for the torch to go off before she comes on to the door.</summary>
    public bool Teaching { get; private set; }
    /// <summary>What the torch in Roshan's hand says while she's teaching ("OFF!", "ON!"), or null.</summary>
    public string TorchHint { get; private set; }
    private float _hold;
    private const float HoldUpTo = 10f;                 // the longest she waits in the hall for the light to go off
    private float _t, _step;
    private Vector2 _wedgeAt;                // the spot her light falls on, picked as the door opens
    private bool _shh;

    private void Awake() => I = this;
    private void OnDestroy()
    {
        if (I == this) I = null;
        if (LightField.I != null) LightField.I.Wedges.Clear();
    }

    private void Start()
    {
        var torch = TorchController.I;
        if (torch != null) torch.Charge.Flared += () =>
        {
            if (!IsCat && (State == MomState.Approaching || State == MomState.AtDoor || State == MomState.Opening)) Add(50f);
        };
    }

    /// <summary>Sets up this page's visits from its definition.</summary>
    public void Schedule(PageDef def)
    {
        _visitsLeft = def.momVisits;
        _openChance = def.momOpenChance;
        _catLeft = def.catFakeout;
        EndTeaching(taught: false);                      // a new page: an unfinished lesson starts again on her next visit
        _firstOpens = def.momFirstOpens;
        _first = true;
        _approach = def.number <= 3 ? 6f : 4f;
        _next = def.momFirstVisit > 0f ? def.momFirstVisit : UnityEngine.Random.Range(25f, 55f);
        Suspicion = 0f;
        Go(MomState.Asleep);
        Open = Feet = Handle = Bust = 0f;
        Strip = 0.3f;
        LightField.I?.Wedges.Clear();
        Unhush();
    }

    /// <summary>Starts a visit now (scripted moments and tests).</summary>
    public void VisitNow(bool opens = false, bool cat = false)
    {
        IsCat = cat;
        _opens = opens && !cat;
        _atDoor = UnityEngine.Random.Range(3f, 6f);
        BeginTeaching();
        Go(cat ? MomState.Approaching : MomState.Stirring);
    }

    /// <summary>The finale (<see cref="Finale"/>): she stands in the open doorway, no visits, no suspicion;
    /// then she goes (quietly: whoever ended the scene has said her line).</summary>
    public void Watch(bool on)
    {
        if (Watching == on) return;
        Watching = on;
        EndTeaching(taught: true);
        Suspicion = 0f;
        _visitsLeft = 0;
        LightField.I?.Wedges.Clear();
        if (on)
        {
            Go(MomState.Opening);
            AudioDirector.I?.DoorCreak();
        }
        else
        {
            _quietLeave = true;
            Go(MomState.Leaving);
            AudioDirector.I?.DoorClose();
        }
    }

    private void Go(MomState s)
    {
        State = s;
        _t = 0f;
        Changed?.Invoke(s);
    }

    private void Add(float amount)
    {
        if (IsCat) return;
        Suspicion = Mathf.Clamp(Suspicion + amount * (Settings.EasySuspicion ? 0.5f : 1f), 0f, 100f);
    }

    private void Update()
    {
        if (Bust > 0f)                                  // caught: the door stands open through the card
        {
            Bust = Mathf.Max(0f, Bust - Time.unscaledDeltaTime);
            Open = Mathf.Clamp01(Bust / 0.4f);
            Strip = Bust > 0f ? 1f : 0.3f;
            Feet = Bust > 0f ? 1f : 0f;
            return;
        }
        if (Watching)                                   // the finale: the door wide, the hall light on
        {
            Open = Mathf.MoveTowards(Open, 1f, Time.unscaledDeltaTime / 0.35f);
            Strip = Feet = 1f;
            Handle = 0f;
            return;
        }
        if (BookmarkPause.IsPaused || (PageManager.I != null && PageManager.I.Busy)) return;
        float dt = Time.deltaTime;
        _t += dt;
        var torch = TorchController.I;
        bool torchOn = torch != null && torch.On && LightField.I != null && LightField.I.BeamLive;
        bool twisting = torch != null && torch.Charge.SinceTwist < 0.3f;

        switch (State)
        {
            case MomState.Asleep:
                Suspicion = Mathf.Max(0f, Suspicion - 15f * dt);
                Strip = Mathf.MoveTowards(Strip, 0.3f, dt);
                if (_visitsLeft > 0 && (_next -= dt) <= 0f)
                {
                    _visitsLeft--;
                    bool scriptedOpen = _first && _firstOpens;
                    // the cat comes once on its page: on a coin flip, or for sure on the last visit
                    IsCat = _catLeft && !scriptedOpen && (_visitsLeft == 0 || UnityEngine.Random.value < 0.5f);
                    if (IsCat) _catLeft = false;
                    _opens = !IsCat && (scriptedOpen || UnityEngine.Random.value < _openChance);
                    _first = false;
                    _atDoor = UnityEngine.Random.Range(3f, 6f);
                    BeginTeaching();
                    Go(IsCat ? MomState.Approaching : MomState.Stirring);
                }
                break;

            case MomState.Stirring:                       // the hall light clicks on
                Strip = Mathf.MoveTowards(Strip, 1f, dt / 0.4f);
                if (_t >= 1.5f) Go(MomState.Approaching);
                break;

            case MomState.Approaching:                    // your warning window
                if (Teaching && torchOn && _t > _approach * 0.7f && _hold < HoldUpTo)
                {
                    _hold += dt;                          // the first time, she waits out in the hall (her steps keep
                    _t = _approach * 0.7f;                // coming) until the light goes off, or for a good while
                }
                Feet = Mathf.Clamp01(_t / _approach);
                if ((_step -= dt) <= 0f)
                {
                    _step = IsCat ? 0.3f : 0.5f;
                    AudioDirector.I?.Footstep(Mathf.Lerp(0.05f, IsCat ? 0.12f : 0.32f, Feet));
                }
                if (_t >= _approach) Go(MomState.AtDoor);
                break;

            case MomState.AtDoor:                         // the handle turns a few degrees, holds, turns back
                Handle = Mathf.Abs(Mathf.Sin(_t * 2.2f)) * (IsCat ? 0f : 1f);
                if (IsCat)
                {
                    if (_t < dt * 1.5f) GameHUD.I?.MomSays("mrrp?");    // a real-world sound: not printed on the page
                    if (_t >= 1.5f) Go(MomState.Leaving);
                    break;
                }
                Add(((torchOn ? 45f : 0f) + (twisting ? 30f : 0f)) * dt);
                if (_opens && _t >= 1.2f) { _wedgeAt = PickWedgeTarget(); Go(MomState.Opening); AudioDirector.I?.DoorCreak(); }
                else if (_t >= _atDoor) Go(MomState.Leaving);
                break;

            case MomState.Opening:                        // her silhouette, and a wedge of hall light on the page
                Handle = 0f;
                Open = Mathf.Clamp01(_t / 1.2f);
                Add(((torchOn ? 90f : 0f) + (twisting ? 30f : 0f)) * dt);
                SetWedge(Open);
                var hero = HeroController.I;
                if (!_shh && hero != null && hero.Light.IsAwake)        // in the light, and holding his breath
                {
                    _shh = true;
                    GameHUD.I?.HeroSays(ShhLine, 3f);
                }
                if (_t >= 4f)
                {
                    Go(MomState.Leaving);
                    AudioDirector.I?.DoorClose();
                }
                break;

            case MomState.Leaving:
                Handle = 0f;
                Suspicion = Mathf.Max(0f, Suspicion - 15f * dt);
                if (Open > 0f) { Open = Mathf.Max(0f, Open - dt / 0.5f); SetWedge(Open); }
                if (Open <= 0f) Unhush();
                Feet = Mathf.Max(0f, Feet - dt / 2f);
                Strip = Mathf.MoveTowards(Strip, 0.3f, dt / 1.5f);
                if (_t < dt * 1.5f && !IsCat && !_quietLeave) GameHUD.I?.MomSays("...kids.");
                if (_t >= 2f)
                {
                    LightField.I?.Wedges.Clear();
                    if (!IsCat && !_quietLeave) GameState.I.LightsOut++;
                    IsCat = false;
                    _quietLeave = false;
                    _next = UnityEngine.Random.Range(25f, 55f);
                    Go(MomState.Asleep);
                }
                break;
        }

        if (Suspicion >= 100f) Caught();
        else Teach(torchOn);
    }

    // ---- the first visit: a tutorial ----------------------------------------------------------------------------

    private const string Coming = "<color=#E10600>MOM'S COMING!</color>  SWITCH THE TORCH OFF: {TORCH}\n" +
                                  "<size=68%>BEFORE SHE GETS TO THE DOOR, OR SHE'LL SEE THE LIGHT AND CATCH YOU!</size>";

    private void BeginTeaching()
    {
        Teaching = !IsCat && !Settings.MomTaught;
        _hold = 0f;
        if (!Teaching) return;
        GameHUD.I?.Teach(Coming);
        GameHUD.I?.HeroSays("Psst! The hall light! It's your mom! Quick, kid, kill the light!", 4f);
    }

    /// <summary>The lesson, step by step: the torch off before she's at the door, off while she's there, then on
    /// again once she's gone (and that's it learned, for good).</summary>
    private void Teach(bool torchOn)
    {
        if (!Teaching) return;
        switch (State)
        {
            case MomState.Stirring:
            case MomState.Approaching:
                TorchHint = torchOn ? "OFF! ({TORCH})" : null;
                GameHUD.I?.Teach(torchOn ? Coming
                    : "GOOD! THE COMIC'S FROZEN, BUT YOU'RE SAFE.\n<size=68%>KEEP THE TORCH OFF UNTIL SHE'S GONE.</size>");
                break;
            case MomState.AtDoor:
            case MomState.Opening:
                TorchHint = torchOn ? "OFF! ({TORCH})" : null;
                GameHUD.I?.Teach(torchOn ? "<color=#E10600>OFF! SHE'LL SEE IT UNDER THE DOOR!</color>  {TORCH}"
                                         : "SHHH... SHE'S AT THE DOOR.\n<size=68%>KEEP THE TORCH OFF UNTIL SHE'S GONE.</size>");
                break;
            default:                                      // leaving, or gone
                if (!torchOn)
                {
                    TorchHint = "ON! ({TORCH})";
                    GameHUD.I?.Teach("PHEW! SHE'S GONE.  TORCH BACK ON: {TORCH}\n<size=68%>WATCH THE DOOR: THE HALL LIGHT MEANS SHE'S COMING.</size>");
                }
                else EndTeaching(taught: true);
                break;
        }
    }

    private void EndTeaching(bool taught)
    {
        if (Teaching) GameHUD.I?.Teach(null);
        Teaching = false;
        TorchHint = null;
        if (taught && !Settings.MomTaught)
        {
            Settings.MomTaught = true;
            Settings.Save();
        }
    }

    /// <summary>The wedge of hallway light: a band slanting down the page in view from the upper left
    /// (where her door is), through the spot she picked, widening toward the bottom as the door opens.</summary>
    private void SetWedge(float amount)
    {
        var field = LightField.I;
        var cam = PageCamera.I;
        if (field == null || cam == null) return;
        field.Wedges.Clear();
        if (amount <= 0.01f) return;
        const float slant = 0.38f;                                       // across for every unit down
        float top = cam.ViewportToLane(new Vector2(0f, 1f)).y + 0.5f;
        float bottom = cam.ViewportToLane(new Vector2(0f, 0f)).y - 0.5f;
        float reach = Mathf.Lerp(0.15f, 1f, amount);
        float xTop = _wedgeAt.x - slant * (top - _wedgeAt.y), xBottom = _wedgeAt.x + slant * (_wedgeAt.y - bottom);
        field.Wedges.Add(new[]
        {
            new Vector2(xTop - 1.4f * reach, top),
            new Vector2(xTop + 1.4f * reach, top),
            new Vector2(xBottom + 4.2f * reach, bottom),
            new Vector2(xBottom - 4.2f * reach, bottom),
        });
    }

    /// <summary>Where her light falls: on the trouble. Each Inkie in view scores one, one more for every
    /// friend within 2.5 units (a crowd), and a half more for a bat (frozen, it's somebody's step; awake,
    /// it's everywhere); she picks one of the two worst spots. Nobody in view: somewhere near Max.</summary>
    private Vector2 PickWedgeTarget()
    {
        var cam = PageCamera.I;
        var pm = PageManager.I;
        var hero = HeroController.I;
        Vector2 near = hero != null ? (Vector2)hero.transform.position + new Vector2(UnityEngine.Random.Range(-3f, 3f), 1f) : Vector2.zero;
        if (cam == null || pm == null || pm.Layout == null || pm.TierIndex >= pm.Layout.tiers.Count) return near;
        float left = cam.ViewportToLane(new Vector2(0.06f, 0.5f)).x, right = cam.ViewportToLane(new Vector2(0.94f, 0.5f)).x;
        var inkies = new List<EnemyBrain>();
        foreach (var panel in pm.Layout.tiers[pm.TierIndex].panels)
            foreach (var e in panel.enemies)
                if (e != null && !e.Health.Dead && e.transform.position.x > left && e.transform.position.x < right) inkies.Add(e);
        if (inkies.Count == 0) return near;
        var spots = new List<(Vector2 at, float score)>();
        foreach (var e in inkies)
        {
            float score = 1f + (e is SplotchBrain ? 0.5f : 0f);
            foreach (var other in inkies)
                if (other != e && Vector2.Distance(other.transform.position, e.transform.position) < 2.5f) score += 1f;
            spots.Add(((Vector2)e.transform.position + Vector2.up, score));
        }
        spots.Sort((a, b) => b.score.CompareTo(a.score));
        var pick = spots[spots.Count > 1 && UnityEngine.Random.value < 0.4f ? 1 : 0].at;
        return pick + new Vector2(UnityEngine.Random.Range(-0.6f, 0.6f), 0f);
    }

    /// <summary>The door's shut again: Max can breathe (and his "shh" is old news).</summary>
    private void Unhush()
    {
        if (!_shh) return;
        _shh = false;
        GameHUD.I?.Unsay(ShhLine);
    }

    private void Caught()
    {
        Unhush();
        EndTeaching(taught: true);                        // told, and caught anyway: it isn't shown again
        Suspicion = 0f;
        LightField.I?.Wedges.Clear();
        Bust = 2.6f;
        Open = 1f;
        Handle = 0f;
        Go(MomState.Asleep);
        _next = UnityEngine.Random.Range(30f, 55f);
        PageManager.I?.Busted();
    }
}
