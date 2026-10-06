using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The comic's sound effects by name (Resources/Audio/Game/&lt;name&gt;/*, a random variation
/// each time). Sound obeys the light: callers only play sounds for awake things.
/// </summary>
public static class GameAudio
{
    private static readonly Dictionary<string, AudioClip[]> _clips = new();

    /// <param name="pitch">The sound's own pitch (1 = as recorded): a combo's blows climb, a tight crank ticks higher.</param>
    public static void Play(string name, float volume = 1f, float pitchJitter = 0.07f, float pitch = 1f)
    {
        if (AudioDirector.I == null) return;
        if (!_clips.TryGetValue(name, out var set))
        {
            set = Resources.LoadAll<AudioClip>("Audio/Game/" + name);
            if (set.Length == 0 && name == "whoosh") set = new[] { Whoosh() };   // no file for this one: it's made in code
            _clips[name] = set;
        }
        if (set == null || set.Length == 0) return;
        AudioDirector.I.PlaySfx(set[Random.Range(0, set.Length)], volume, pitchJitter, pitch);
    }

    private static AudioClip _snap, _tick;

    /// <summary>An Inkie shaking itself awake in the light: a wet little flick of ink, made in code (a pop at
    /// 1.1 kHz falling away under a quick bright hiss).</summary>
    public static void Snap(float volume)
    {
        if (AudioDirector.I == null) return;
        if (_snap == null) _snap = Click("inksnap", 0.07f, 1100f, 620f, 0.55f, 26f, 5);
        AudioDirector.I.PlaySfx(_snap, volume, 0.08f);
    }

    /// <summary>An Inkie freezing out of the light: a dry paper tick, made in code (a short high click).</summary>
    public static void Tick(float volume)
    {
        if (AudioDirector.I == null) return;
        if (_tick == null) _tick = Click("papertick", 0.035f, 2300f, 2100f, 0.75f, 70f, 9);
        AudioDirector.I.PlaySfx(_tick, volume, 0.06f);
    }

    /// <summary>A short click: a sine sliding from `from` to `to` Hz, mixed with `noise` of filtered noise, dying
    /// away at `decay` per second.</summary>
    private static AudioClip Click(string name, float length, float from, float to, float noise, float decay, int seed)
    {
        const int rate = 44100;
        int n = (int)(rate * length);
        var data = new float[n];
        var rng = new System.Random(seed);
        double phase = 0;
        float prev = 0f, peak = 1e-4f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            phase += 2.0 * System.Math.PI * Mathf.Lerp(from, to, t / length) / rate;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            float bright = white - prev;                               // a first difference: the hiss without its rumble
            prev = white;
            float env = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-t * decay);
            data[i] = ((float)System.Math.Sin(phase) * (1f - noise) + bright * 0.5f * noise) * env;
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        for (int i = 0; i < n; i++) data[i] *= 0.8f / peak;
        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
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
