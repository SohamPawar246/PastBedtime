using UnityEngine;

/// <summary>
/// The comic's soundtrack (GDD section 12): a quiet heroic theme on the pages, a jazzy fight theme while Baron
/// Blot is in the panel, and the bedroom's lo-fi again once the comic is shut for the night. The
/// music is in the comic too, so switching the torch off tape-stops it (TorchController) and it
/// spins back up with the light.
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

    private void Start()
    {
        _pages = Resources.Load<AudioClip>("Music/pages");
        _boss = Resources.Load<AudioClip>("Music/boss");
        _bedroom = Resources.Load<AudioClip>("Music/menu");
        Play(_pages);
    }

    private void Update()
    {
        if (Finale.I != null && Finale.I.Ended)
        {
            Play(_bedroom);
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
    }

    private void Play(AudioClip clip)
    {
        if (clip == null || clip == _playing) return;
        _playing = clip;
        AudioDirector.I?.PlayMusic(clip);
    }
}
