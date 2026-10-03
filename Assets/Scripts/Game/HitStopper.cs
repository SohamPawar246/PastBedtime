using System;
using UnityEngine;

/// <summary>Freezes game time for a few frames on impact (skipped while the game is paused).</summary>
public class HitStopper : MonoBehaviour
{
    private static HitStopper _i;
    private float _until;

    public static void Stop(float seconds)
    {
        if (_i == null)
        {
            _i = new GameObject("HitStop").AddComponent<HitStopper>();
        }
        if (BookmarkPause.IsPaused) return;
        _i._until = Mathf.Max(_i._until, Time.unscaledTime + seconds);
        Time.timeScale = 0.05f;
    }

    private void Update()
    {
        if (BookmarkPause.IsPaused) return;
        if (Time.timeScale < 1f && Time.unscaledTime >= _until) Time.timeScale = 1f;
    }

    private void OnDestroy()
    {
        if (_i == this)
        {
            _i = null;
            if (!BookmarkPause.IsPaused) Time.timeScale = 1f;
        }
    }
}
