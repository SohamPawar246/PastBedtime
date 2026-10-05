using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The comic's sound effects by name (Resources/Audio/Game/&lt;name&gt;/*, a random variation
/// each time). Sound obeys the light: callers only play sounds for awake things.
/// </summary>
public static class GameAudio
{
    private static readonly Dictionary<string, AudioClip[]> _clips = new();

    public static void Play(string name, float volume = 1f, float pitchJitter = 0.07f)
    {
        if (AudioDirector.I == null) return;
        if (!_clips.TryGetValue(name, out var set))
        {
            set = Resources.LoadAll<AudioClip>("Audio/Game/" + name);
            if (set.Length == 0 && name == "whoosh") set = new[] { Whoosh() };   // no file for this one: it's made in code
            _clips[name] = set;
        }
        if (set == null || set.Length == 0) return;
        AudioDirector.I.PlaySfx(set[Random.Range(0, set.Length)], volume, pitchJitter);
    }

    /// <summary>A swing's whoosh, made in code: white noise through a band-pass that falls from 2.4 kHz to 600 Hz,
    /// swelling and dying away over a quarter of a second (the air a fist moves).</summary>
    private static AudioClip Whoosh()
    {
        const int rate = 44100;
        const float length = 0.24f;
        int n = (int)(rate * length);
        var data = new float[n];
        var rng = new System.Random(11);
        float low = 0f, band = 0f, peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)n;
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Lerp(2400f, 600f, k) / rate);   // a state-variable filter's tuning
            float x = (float)(rng.NextDouble() * 2.0 - 1.0);
            low += f * band;
            float high = x - low - 0.7f * band;
            band += f * high;
            float env = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.6f)), 2f);
            data[i] = band * env;
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        for (int i = 0; i < n; i++) data[i] *= 0.7f / peak;
        var clip = AudioClip.Create("whoosh", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip _thump;

    /// <summary>A low heartbeat thump, made in code: a 62 Hz sine sagging to 38 Hz as it dies away.</summary>
    public static void Heartbeat(float volume)
    {
        if (AudioDirector.I == null) return;
        if (_thump == null)
        {
            const int rate = 44100;
            const float length = 0.22f;
            int n = (int)(rate * length);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                phase += 2.0 * System.Math.PI * Mathf.Lerp(62f, 38f, t / length) / rate;
                float env = Mathf.Min(1f, t / 0.006f) * Mathf.Exp(-t * 18f);
                data[i] = (float)System.Math.Sin(phase) * env * 0.9f;
            }
            _thump = AudioClip.Create("heartbeat", n, 1, rate, false);
            _thump.SetData(data, 0);
        }
        AudioDirector.I.PlaySfx(_thump, volume, 0.02f);
    }
}
