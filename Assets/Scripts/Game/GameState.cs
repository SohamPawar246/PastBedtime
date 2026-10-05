using System;
using UnityEngine;

/// <summary>
/// The run's numbers (GDD sections 4, 5 and 13): Max's hearts, the Splash meter, the stars,
/// the style counter, the upgrades and which page we're on. Also the place every system
/// reports to ("a hit landed", "an Inkie hurt an Inkie"), so the HUD and lettering stay in
/// one spot.
/// </summary>
public class GameState : MonoBehaviour
{
    public static GameState I { get; private set; }

    public const int MaxHearts = 5;
    public int Page = 1;
    public float Splash;                     // 0 to 100
    public int StarsThisPage, StarsTotal;
    public int Choreography, LightsOut;      // style counter (the whole sitting)
    /// <summary>The style counter on this page alone (the page card's numbers).</summary>
    public int ChoreographyThisPage => Choreography - _choreoAtStart;
    public int LightsOutThisPage => LightsOut - _lightsOutAtStart;
    private int _choreoAtStart, _lightsOutAtStart;
    /// <summary>Stars found on this page that aren't banked yet (bit i = the page's star i); banked when the
    /// page is done, so a restart, or GROUNDED, puts them back.</summary>
    private int _found;
    public int Spring, Gear, Ratchet;        // upgrade levels
    public int Slippers = 3;                 // Mom's strikes left (GDD section 8)

    /// <summary>Hits landed in a row (resets after 1.2 s without one).</summary>
    public int Combo { get; private set; }
    private float _comboClock;

    public event Action Changed;

    private void Awake() => I = this;
    private void OnDestroy() { if (I == this) I = null; }

    private void Update()
    {
        if (Combo > 0 && (_comboClock -= Time.deltaTime) <= 0f) Combo = 0;
    }

    public void AddSplash(float amount)
    {
        Splash = Mathf.Clamp(Splash + amount, 0f, 100f);
        Changed?.Invoke();
    }

    /// <summary>Max landed a hit: +4 splash, +10 when a combo reaches 5 hits.</summary>
    public void HeroLanded()
    {
        Combo++;
        _comboClock = 1.2f;
        AddSplash(Combo == 5 ? 14f : 4f);
    }

    /// <summary>An Inkie was hurt by another Inkie or a hazard: +15 splash and gold lettering.</summary>
    public void ChoreographyHit(Vector2 where)
    {
        Choreography++;
        AddSplash(15f);
        GameEvents.Lettering("CHOREO!", where + new Vector2(0f, 2.2f), new Color(1f, 0.78f, 0.15f), 1.1f, burst: true);
    }

    public void Notify() => Changed?.Invoke();

    /// <summary>A page opens: its style counter starts at 0, and stars already banked on it count as found.</summary>
    public void PageStarted()
    {
        _choreoAtStart = Choreography;
        _lightsOutAtStart = LightsOut;
        _found = 0;
        StarsThisPage = Count(Settings.StarsFound(Page));
    }

    /// <summary>Has this page's star i been found (banked earlier, or picked up on this visit)?</summary>
    public bool StarFound(int page, int star) => ((Settings.StarsFound(page) | (page == Page ? _found : 0)) & (1 << star)) != 0;

    /// <summary>Max picked up this page's star i: it's in hand, not banked (each star counts once, ever).</summary>
    public void FindStar(int star)
    {
        if (StarFound(Page, star)) return;
        _found |= 1 << star;
        StarsThisPage++;
        StarsTotal++;
        Notify();
    }

    /// <summary>The page is done: its stars and the stars in hand are kept for good.</summary>
    public void BankStars()
    {
        Settings.SetStarsFound(Page, Settings.StarsFound(Page) | _found);
        _found = 0;
        Settings.Stars = StarsTotal;
        Settings.Save();
    }

    private static int Count(int mask)
    {
        int n = 0;
        for (; mask != 0; mask &= mask - 1) n++;
        return n;
    }
}

/// <summary>Fire-and-forget hooks other systems call without caring who listens.</summary>
public static class GameEvents
{
    public static void Lettering(string word, Vector2 lanePos, Color fill, float scale = 1f, bool burst = false) =>
        SfxLettering.Spawn(word, lanePos, fill, scale, burst);

    /// <summary>Shakes only the page camera, never the room (GDD section 3).</summary>
    public static void Impact(float strength) => PageCamera.Impulse(strength);

    /// <summary>A tiny pause on heavy contact so blows land.</summary>
    public static void HitStop(float seconds) => HitStopper.Stop(seconds);
}
