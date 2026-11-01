using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Studio Kamikaze logo. Plays StreamingAssets/StudioIntro.mp4 (by URL, the only
/// way video works on WebGL) with its own sound, cropped to fill the screen, then
/// moves on. Skippable after a short debounce. If the video is missing or fails,
/// a lettered card stands in so the boot flow never hangs.
/// </summary>
public class StudioIntroScreen : MonoBehaviour
{
    [SerializeField] private string videoFile = "StudioIntro.mp4";
    [SerializeField] private float debounceSeconds = 0.6f;
    [SerializeField] private float prepareTimeout = 5f;
    [SerializeField] private float fallbackSeconds = 2.6f;

    [Tooltip("The intro, drawn in the scene (switched off till it starts): white under the video, and the lettered " +
             "card (switched off) that stands in if the video fails.")]
    [SerializeField] private GameObject _canvas, _card;
    [SerializeField] private RawImage _surface;
    [SerializeField] private VideoPlayer _player;
    private RenderTexture _rt;
    private bool _done, _failed, _advancing;
    private float _elapsed;

    private void Start()
    {
        UIKit.EnsureEventSystem();
        _canvas.SetActive(true);
        _player.url = Application.streamingAssetsPath + "/" + videoFile;
        _player.prepareCompleted += OnPrepared;
        _player.loopPointReached += _ => _done = true;
        _player.errorReceived += (_, msg) =>
        {
            Debug.LogWarning("[StudioIntro] Video failed, showing the card instead: " + msg);
            _failed = true;
        };
        _player.Prepare();
        StartCoroutine(PrepareWatchdog());
    }

    private IEnumerator PrepareWatchdog()
    {
        yield return new WaitForSecondsRealtime(prepareTimeout);
        if (!_player.isPrepared) _failed = true;
    }

    private void OnPrepared(VideoPlayer vp)
    {
        _rt = new RenderTexture((int)vp.width, (int)vp.height, 0);
        vp.targetTexture = _rt;
        _surface.texture = _rt;
        _surface.uvRect = CoverRect((float)vp.width / Mathf.Max(1, vp.height));
        _surface.gameObject.SetActive(true);
        if (vp.audioTrackCount > 0) vp.SetDirectAudioVolume(0, Settings.MasterVolume * Settings.SfxVolume);
        vp.Play();
    }

    /// <summary>UV rect that crops the video to cover the screen (CSS background-size: cover).</summary>
    private static Rect CoverRect(float videoAspect)
    {
        float screen = (float)Screen.width / Mathf.Max(1, Screen.height);
        if (videoAspect > screen)
        {
            float w = screen / videoAspect;
            return new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        float h = videoAspect / screen;
        return new Rect(0f, (1f - h) * 0.5f, 1f, h);
    }

    private void Update()
    {
        _elapsed += Time.unscaledDeltaTime;
        if (_failed && !_advancing) { _failed = false; StartCoroutine(FallbackCard()); }
        if (_elapsed > debounceSeconds && ShellInput.AnyPress()) Advance();
        if (_done) Advance();
    }

    private IEnumerator FallbackCard()
    {
        if (_player != null) _player.Stop();
        _surface.gameObject.SetActive(false);
        _card.SetActive(true);
        var studio = _card.transform.Find("Studio");
        studio.localScale = Vector3.one * 0.9f;
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            studio.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, t / 0.4f);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(fallbackSeconds);
        _done = true;
    }

    private void Advance()
    {
        if (_advancing) return;
        _advancing = true;
        if (_player != null && _player.isPlaying) _player.Pause();
        App.I.Flow.Load(App.WarningShown ? Scenes.Title : Scenes.Warning);
    }

    private void OnDestroy()
    {
        if (_rt != null) _rt.Release();
    }
}
