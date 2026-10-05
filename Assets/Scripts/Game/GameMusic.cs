using UnityEngine;

/// <summary>
/// The comic's soundtrack (GDD section 12): a quiet heroic theme on the pages, a jazzy fight theme while Baron
/// Blot is in the panel, and the bedroom's lo-fi again once the comic is shut for the night (until
/// someone down the hall opens it again). The music is in the comic too: it tape-stops whenever the comic
/// isn't being read (the torch off, run dry or cooling) and spins back up with the light (see Follow).
///   pages.ogg  "Into The Wilds", Scott Buckley (scottbuckley.com.au), CC BY 4.0 (6 dB down, not normalised on import)
///   boss.ogg   "Jazzy Battle Theme", MintoDog, CC0
///   menu.ogg   "First Snow", HoliznaCC0, CC0
/// </summary>
public class GameMusic : MonoBehaviour
{
    private AudioClip _pages, _boss, _bedroom;
    private AudioClip _playing;
    private BlotBrain _blot;
    private float _look;
    private bool _reading = true;

    private void Start()
    {
        _pages = Resources.Load<AudioClip>("Music/pages");
        _boss = Resources.Load<AudioClip>("Music/boss");
        _bedroom = Resources.Load<AudioClip>("Music/menu");
        Play(_pages);
    }

    private void OnEnable() => BookmarkPause.PauseChanged += OnPause;
    private void OnDisable() => BookmarkPause.PauseChanged -= OnPause;

    // back from the Bookmark: the music comes back only if the comic is still being read
    private void OnPause(bool paused)
    {
        if (!paused && !(Finale.I != null && Finale.I.Ended)) Follow(force: true);
    }

    /// <summary>The music is in the comic: it plays while the comic is being read (the torch's beam on it, a flare,
    /// or the room light) and tape-stops while it isn't: the torch off, run dry or cooling after a flare; and a
    /// dying bulb's flickers stutter it.</summary>
    private void Follow(bool force)
    {
        var f = LightField.I;
        bool reading = f == null || f.BeamLive || f.FlareTime > 0f || f.RoomLight;
        if (!force && reading == _reading) return;
        _reading = reading;
        AudioDirector.I?.TapeStop(!reading);
    }

    private void Update()
    {
        if (Finale.I != null && Finale.I.Ended)
        {
            if (Finale.I.ReadingAgain) Play(_pages, spinUp: true);
            else Play(_bedroom);
            return;
        }
        if ((_look -= Time.unscaledDeltaTime) <= 0f)
        {
            _look = 0.5f;
            _blot = FindFirstObjectByType<BlotBrain>();
        }
        var hero = HeroController.I;
        bool fight = _blot != null && hero != null && !_blot.Escaping && _blot.Mode != EnemyBrain.State.Dead &&
                     hero.transform.position.x > _blot.MinX - 0.5f && hero.transform.position.x < _blot.MaxX + 0.5f &&
                     Mathf.Abs(hero.transform.position.y - _blot.transform.position.y) < 8f;
        Play(fight ? _boss : _pages);
        if (!BookmarkPause.IsPaused) Follow(force: false);
    }

    private void Play(AudioClip clip, bool spinUp = false)
    {
        if (clip == null || clip == _playing) return;
        _playing = clip;
        AudioDirector.I?.PlayMusic(clip, spinUp);
    }
}
