using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Music and front-end sound. Two music sources crossfade; one source plays
/// one-shots. Clips load from Resources/Audio and Resources/Music by name, so
/// swapping a sound is a file replace.
///
/// "Sound obeys the light" (GDD section 12): <see cref="TapeStop"/> slides the
/// music's pitch down to a halt, the way switching the torch off stops the comic.
/// The Bookmark pause uses it too.
/// </summary>
public class AudioDirector : MonoBehaviour
{
    public static AudioDirector I { get; private set; }

    [SerializeField] private float musicFadeSeconds = 1.2f;

    private AudioSource _musicA, _musicB, _sfx;
    private bool _usingA = true;
    private Coroutine _crossfade, _tape;
    private float _duck = 1f;

    private AudioClip _hover, _click, _confirm, _back, _toggle, _torchOn, _torchOff, _doorCreak, _doorClose;
    private AudioClip[] _footsteps;
    private AudioClip _menuMusic, _creditsMusic;

    private AudioSource CurrentMusic => _usingA ? _musicA : _musicB;

    /// <summary>The music is tape-stopped (wound down or paused), or nothing is playing.</summary>
    public bool MusicStopped => CurrentMusic.clip == null || !CurrentMusic.isPlaying || CurrentMusic.pitch < 0.5f;

    private void Awake()
    {
        I = this;
        _musicA = gameObject.AddComponent<AudioSource>();
        _musicB = gameObject.AddComponent<AudioSource>();
        _sfx = gameObject.AddComponent<AudioSource>();
        foreach (var s in new[] { _musicA, _musicB, _sfx })
        {
            s.playOnAwake = false;
            s.ignoreListenerPause = true; // menus keep sounding while the game is paused
        }
        _musicA.loop = _musicB.loop = true;

        _hover = Load("Audio/ui_hover");
        _click = Load("Audio/ui_click");
        _confirm = Load("Audio/ui_confirm");
        _back = Load("Audio/ui_back");
        _toggle = Load("Audio/ui_toggle");
        _torchOn = Load("Audio/torch_on");
        _torchOff = Load("Audio/torch_off");
        _doorCreak = Load("Audio/door_creak");
        _doorClose = Load("Audio/door_close");
        _footsteps = Resources.LoadAll<AudioClip>("Audio/Footsteps");
        _menuMusic = Load("Music/menu");
        _creditsMusic = Load("Music/credits");

        Settings.Changed += ApplyVolumes;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        Settings.Changed -= ApplyVolumes;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private static AudioClip Load(string path) => Resources.Load<AudioClip>(path);

    /// <summary>Music policy per scene. Gameplay scenes drive their own music.</summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case Scenes.Title: PlayMusic(_menuMusic); break;
            case Scenes.Credits: PlayMusic(_creditsMusic); break;
            case Scenes.Game: StopMusic(); break;
        }
    }

    // ---- Music ------------------------------------------------------------------------

    /// <param name="spinUp">Start it from a standstill and spin it up to speed, like a tape (the comic's
    /// music coming back with the torch).</param>
    public void PlayMusic(AudioClip clip, bool spinUp = false)
    {
        if (clip == null) return;
        if (CurrentMusic.clip == clip && CurrentMusic.isPlaying) return;
        if (_crossfade != null) StopCoroutine(_crossfade);
        _crossfade = StartCoroutine(CrossfadeTo(clip, spinUp));
    }

    public void StopMusic()
    {
        if (_crossfade != null) StopCoroutine(_crossfade);
        _crossfade = StartCoroutine(CrossfadeTo(null));
    }

    private IEnumerator CrossfadeTo(AudioClip clip, bool spinUp = false)
    {
        var from = CurrentMusic;
        var to = _usingA ? _musicB : _musicA;
        _usingA = !_usingA;
        spinUp &= clip != null;

        if (clip != null)
        {
            to.clip = clip;
            to.volume = 0f;
            to.pitch = spinUp ? 0.05f : 1f;
            to.Play();
        }

        float fromStart = from.volume;
        for (float t = 0f; t < musicFadeSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / musicFadeSeconds;
            if (clip != null) to.volume = MusicVolume * k;
            if (spinUp) to.pitch = Mathf.Lerp(0.05f, 1f, 1f - (1f - k) * (1f - k));
            from.volume = fromStart * (1f - k);
            yield return null;
        }
        from.Stop();
        to.volume = clip != null ? MusicVolume : 0f;
        if (spinUp) to.pitch = 1f;
        _crossfade = null;
    }

    /// <summary>Tape-stop (stop=true) or spin back up (stop=false) the current music.</summary>
    public void TapeStop(bool stop, float seconds = 0.3f)
    {
        if (_tape != null) StopCoroutine(_tape);
        _tape = StartCoroutine(TapeRoutine(stop, seconds));
    }

    private IEnumerator TapeRoutine(bool stop, float seconds)
    {
        var src = CurrentMusic;
        if (!stop && src.clip != null && !src.isPlaying) src.UnPause();
        float from = src.pitch, to = stop ? 0.05f : 1f;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;
            src.pitch = Mathf.Lerp(from, to, stop ? k * k : 1f - (1f - k) * (1f - k));
            yield return null;
        }
        src.pitch = to;
        if (stop) src.Pause();
        _tape = null;
    }

    /// <summary>Scale the music under a modal or a dramatic beat (1 = full).</summary>
    public void Duck(float level)
    {
        _duck = Mathf.Clamp01(level);
        ApplyVolumes();
    }

    // ---- Sound effects ---------------------------------------------------------------

    public void PlaySfx(AudioClip clip, float volume = 1f, float pitchJitter = 0.04f)
    {
        if (clip == null) return;
        _sfx.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        _sfx.PlayOneShot(clip, volume * SfxVolume);
    }

    public void Hover() => PlaySfx(_hover, 0.45f, 0.03f);
    public void Click() => PlaySfx(_click, 0.8f);
    public void Confirm() => PlaySfx(_confirm, 0.8f);
    public void Back() => PlaySfx(_back, 0.7f);
    public void Toggle() => PlaySfx(_toggle, 0.7f);
    public void TorchOn(float volume = 0.9f) => PlaySfx(_torchOn, volume, 0.02f);
    public void TorchOff() => PlaySfx(_torchOff, 0.9f, 0.02f);
    public void DoorCreak() => PlaySfx(_doorCreak, 0.55f, 0.03f);
    public void DoorClose() => PlaySfx(_doorClose, 0.5f, 0.03f);

    public void Footstep(float volume)
    {
        if (_footsteps == null || _footsteps.Length == 0) return;
        PlaySfx(_footsteps[Random.Range(0, _footsteps.Length)], volume, 0.06f);
    }

    // ---- Volume ------------------------------------------------------------------------

    private float MusicVolume => Settings.MasterVolume * Settings.MusicVolume * _duck;
    private float SfxVolume => Settings.MasterVolume * Settings.SfxVolume;

    public void ApplyVolumes()
    {
        if (_crossfade == null) CurrentMusic.volume = MusicVolume;
    }
}
