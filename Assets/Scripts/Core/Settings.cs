using System;
using UnityEngine;

public enum TwistInput { Scroll, Circle, Mash }

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
    public static TwistInput Twist;
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
        Twist = TwistInput.Scroll;
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
        Twist = (TwistInput)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "read.twist", (int)Twist), 0, 2);
        EasySuspicion = GetBool("read.easysus", EasySuspicion);
        FlatPage = GetBool("read.flat", FlatPage);
        ScreenShake = GetBool("read.shake", ScreenShake);
        ReduceFlashing = GetBool("read.noflash", ReduceFlashing);
        SoundCaptions = GetBool("read.captions", SoundCaptions);
        Fullscreen = GetBool("video.fullscreen", Fullscreen);
        HighestPage = PlayerPrefs.GetInt(Prefix + "progress.page", 0);
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(Prefix + "vol.master", MasterVolume);
        PlayerPrefs.SetFloat(Prefix + "vol.music", MusicVolume);
        PlayerPrefs.SetFloat(Prefix + "vol.sfx", SfxVolume);
        SetBool("read.follow", FollowAssist);
        PlayerPrefs.SetInt(Prefix + "read.twist", (int)Twist);
        SetBool("read.easysus", EasySuspicion);
        SetBool("read.flat", FlatPage);
        SetBool("read.shake", ScreenShake);
        SetBool("read.noflash", ReduceFlashing);
        SetBool("read.captions", SoundCaptions);
        SetBool("video.fullscreen", Fullscreen);
        PlayerPrefs.SetInt(Prefix + "progress.page", HighestPage);
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

    private static bool GetBool(string key, bool fallback) =>
        PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) == 1;

    private static void SetBool(string key, bool value) =>
        PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
}
