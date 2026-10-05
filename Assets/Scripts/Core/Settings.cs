using System;
using UnityEngine;

/// <summary>
/// Every player-facing option plus the little progress we keep, behind Load/Save.
/// Storage is PlayerPrefs (IndexedDB on WebGL). Nothing else touches PlayerPrefs.
///
/// The reading options mirror GDD section 13 (Settings screen) and wireframe 08
/// (the Bookmark pause). Gameplay reads the static fields directly and can listen
/// to <see cref="Changed"/> to react live.
/// </summary>
public static class Settings
{
    // ---- Sound --------------------------------------------------------------------
    public static float MasterVolume;
    public static float MusicVolume;
    public static float SfxVolume;

    // ---- Reading (gameplay assists, GDD sections 3, 8, 13) --------------------------
    public static bool FollowAssist;
    public static bool EasySuspicion;   // Mom's suspicion fills at half speed
    public static bool FlatPage;        // room camera straight above the page
    public static bool ScreenShake;
    public static bool ReduceFlashing;  // softens the flare and other full-screen flashes
    public static bool SoundCaptions;   // a caption for every audio cue

    // ---- Display --------------------------------------------------------------------
    public static bool Fullscreen;

    // ---- Progress ---------------------------------------------------------------------
    /// <summary>Highest comic page the player has reached (0 = never started).</summary>
    public static int HighestPage;
    /// <summary>Stars in hand, and the torch's upgrade levels from the Nightstand (0 to 2 each).</summary>
    public static int Stars, Spring, Gear, Ratchet;
    /// <summary>Mom's first visit has taught the player to switch the torch off (it's a tutorial once per save).</summary>
    public static bool MomTaught;
    /// <summary>The Ghost lens has been taught (once per save, on page 7).</summary>
    public static bool GhostTaught;
    /// <summary>Which stars have been found on each page (bit i = the page's star i): found stars aren't drawn
    /// again, so each counts once.</summary>
    private static readonly int[] _starsFound = new int[MaxPages + 1];
    private const int MaxPages = 12;

    public static int StarsFound(int page) => page >= 1 && page <= MaxPages ? _starsFound[page] : 0;
    public static void SetStarsFound(int page, int mask)
    {
        if (page >= 1 && page <= MaxPages) _starsFound[page] = mask;
    }

    /// <summary>START OVER: back to page 1 with no stars, no upgrades and every star to find again.</summary>
    public static void ResetProgress()
    {
        HighestPage = 0;
        Stars = Spring = Gear = Ratchet = 0;
        System.Array.Clear(_starsFound, 0, _starsFound.Length);
        MomTaught = GhostTaught = false;
        Commit();
    }

    /// <summary>Raised after any option changes, so live systems can re-read them.</summary>
    public static event Action Changed;

    private const string Prefix = "pb.";

    static Settings() => ApplyDefaults();

    private static void ApplyDefaults()
    {
        MasterVolume = 1f;
        MusicVolume = 0.7f;
        SfxVolume = 1f;
        FollowAssist = true;
        EasySuspicion = false;
        FlatPage = false;
        ScreenShake = true;
        ReduceFlashing = false;
        SoundCaptions = true;
        Fullscreen = false;
    }

    public static void Load()
    {
        ApplyDefaults();
        MasterVolume = PlayerPrefs.GetFloat(Prefix + "vol.master", MasterVolume);
        MusicVolume = PlayerPrefs.GetFloat(Prefix + "vol.music", MusicVolume);
        SfxVolume = PlayerPrefs.GetFloat(Prefix + "vol.sfx", SfxVolume);
        FollowAssist = GetBool("read.follow", FollowAssist);
        EasySuspicion = GetBool("read.easysus", EasySuspicion);
        FlatPage = GetBool("read.flat", FlatPage);
        ScreenShake = GetBool("read.shake", ScreenShake);
        ReduceFlashing = GetBool("read.noflash", ReduceFlashing);
        SoundCaptions = GetBool("read.captions", SoundCaptions);
        Fullscreen = GetBool("video.fullscreen", Fullscreen);
        HighestPage = PlayerPrefs.GetInt(Prefix + "progress.page", 0);
        Stars = PlayerPrefs.GetInt(Prefix + "progress.stars", 0);
        Spring = PlayerPrefs.GetInt(Prefix + "progress.spring", 0);
        Gear = PlayerPrefs.GetInt(Prefix + "progress.gear", 0);
        Ratchet = PlayerPrefs.GetInt(Prefix + "progress.ratchet", 0);
        for (int p = 1; p <= MaxPages; p++) _starsFound[p] = PlayerPrefs.GetInt(Prefix + "progress.found" + p, 0);
        MomTaught = GetBool("progress.momtaught", false);
        GhostTaught = GetBool("progress.ghosttaught", false);
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(Prefix + "vol.master", MasterVolume);
        PlayerPrefs.SetFloat(Prefix + "vol.music", MusicVolume);
        PlayerPrefs.SetFloat(Prefix + "vol.sfx", SfxVolume);
        SetBool("read.follow", FollowAssist);
        SetBool("read.easysus", EasySuspicion);
        SetBool("read.flat", FlatPage);
        SetBool("read.shake", ScreenShake);
        SetBool("read.noflash", ReduceFlashing);
        SetBool("read.captions", SoundCaptions);
        SetBool("video.fullscreen", Fullscreen);
        PlayerPrefs.SetInt(Prefix + "progress.page", HighestPage);
        PlayerPrefs.SetInt(Prefix + "progress.stars", Stars);
        PlayerPrefs.SetInt(Prefix + "progress.spring", Spring);
        PlayerPrefs.SetInt(Prefix + "progress.gear", Gear);
        PlayerPrefs.SetInt(Prefix + "progress.ratchet", Ratchet);
        for (int p = 1; p <= MaxPages; p++) PlayerPrefs.SetInt(Prefix + "progress.found" + p, _starsFound[p]);
        SetBool("progress.momtaught", MomTaught);
        SetBool("progress.ghosttaught", GhostTaught);
        PlayerPrefs.Save(); // flush now: a browser tab can close at any moment
    }

    /// <summary>Save and tell listeners. Call after changing any field.</summary>
    public static void Commit()
    {
        Save();
        Changed?.Invoke();
    }

    /// <summary>Options back to defaults. Progress is deliberately kept.</summary>
    public static void ResetOptions()
    {
        ApplyDefaults();
        Commit();
    }

    // ---- Controls: <see cref="Bindings"/> keeps its slots here --------------------------------------
    public static string GetText(string key, string fallback) => PlayerPrefs.GetString(Prefix + key, fallback);
    public static void SetText(string key, string value) => PlayerPrefs.SetString(Prefix + key, value);

    private static bool GetBool(string key, bool fallback) =>
        PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) == 1;

    private static void SetBool(string key, bool value) =>
        PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
}
