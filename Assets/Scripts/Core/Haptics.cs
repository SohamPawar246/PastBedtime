using UnityEngine;

/// <summary>
/// Short buzzes on a phone (Settings > Haptics): a tick for every crank notch and menu tap, a bump for a landed blow or a
/// hit taken, a heavy buzz for a flare, and Mom felt before she's heard: each footstep a tap, her heartbeat a pulse (a
/// phone's speaker can't play its low thump). Android's vibrator with a strength (API 26 on), the plain buzz before
/// that; nothing anywhere else.
/// </summary>
public static class Haptics
{
    /// <summary>A tap: crank notches, menu taps.</summary>
    public static void Tick() => Buzz(12, 70);
    /// <summary>A blow landed or taken, a heavy landing.</summary>
    public static void Bump() => Buzz(28, 160);
    /// <summary>A flare, a knockout, being caught.</summary>
    public static void Heavy() => Buzz(70, 255);
    /// <summary>Mom's footstep or heartbeat, stronger as she comes (0..1).</summary>
    public static void Pulse(float strength) => Buzz(Mathf.RoundToInt(Mathf.Lerp(14f, 40f, strength)), Mathf.RoundToInt(Mathf.Lerp(50f, 200f, strength)));

    private static float _quietUntil;

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject _vibrator;
    private static int _api = -1;

    private static void Buzz(int ms, int amplitude)
    {
        if (!Settings.Haptics || Time.unscaledTime < _quietUntil) return;
        _quietUntil = Time.unscaledTime + ms / 1000f * 0.6f;              // buzzes on top of each other turn to mush
        try
        {
            if (_api < 0)
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                _api = version.GetStatic<int>("SDK_INT");
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            if (_vibrator == null) return;
            if (_api >= 26)
            {
                using var effect = new AndroidJavaClass("android.os.VibrationEffect");
                using var oneShot = effect.CallStatic<AndroidJavaObject>("createOneShot", (long)ms, Mathf.Clamp(amplitude, 1, 255));
                _vibrator.Call("vibrate", oneShot);
            }
            else _vibrator.Call("vibrate", (long)ms);
        }
        catch (System.Exception) { _vibrator = null; }
    }

    // (never called: it's here so the build asks Android for the vibrate permission)
    private static void NeedsVibratePermission() => Handheld.Vibrate();
#else
    private static void Buzz(int ms, int amplitude)
    {
        if (!Settings.Haptics || Time.unscaledTime < _quietUntil) return;
        _quietUntil = Time.unscaledTime + ms / 1000f * 0.6f;
    }
#endif
}
