using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The pause menu, "Bookmark" (wireframe 08): Esc closes the comic on a red
/// ribbon. Resume / Restart page / How to read / Quit to title on the left, and the
/// settings sheet on the same screen; its Controls line opens the Controls page.
///
/// App adds one to every gameplay scene automatically. Pausing freezes game time,
/// pauses gameplay audio and tape-stops the music, the same way the torch does.
/// </summary>
public class BookmarkPause : MonoBehaviour
{
    public static bool IsPaused { get; private set; }
    public static event Action<bool> PauseChanged;

    /// <summary>Real time that stops while the game is bookmarked: for the cutscenes, which run on real time
    /// (hit-stop can't slow them) but mustn't play on behind the menu.</summary>
    public static float UnpausedDelta => IsPaused ? 0f : Time.unscaledDeltaTime;

    /// <summary>Waits this many seconds of <see cref="UnpausedDelta"/>.</summary>
    public static System.Collections.IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += UnpausedDelta) yield return null;
    }

    [Tooltip("The bookmark screen (switched off till the game's paused), and the pages that open over it.")]
    [SerializeField] private GameObject _root;
    [SerializeField] private Modal _howTo, _controlsModal;
    [SerializeField] private HowToReadLive _howToPage;
    [SerializeField] private ControlsPanel _controls;
    [SerializeField] private CaptionButton _resume, _restart, _howToButton, _quitTitle;
    [SerializeField] private SettingsPanel _settings;
    [Tooltip("SETTINGS: on a touch screen there's no room for the sheet beside the menu, so it's a page of its own " +
             "(switched off on a desktop).")]
    [SerializeField] private CaptionButton _settingsButton;
    [Tooltip("The closed comic beside the menu (it steps aside while the touch screen's settings page is up).")]
    [SerializeField] private GameObject _closedComic;
    private CursorLockMode _savedLock;
    private bool _savedVisible;
    private bool _sheetOpen;
    private readonly System.Collections.Generic.Dictionary<RectTransform, (Vector2 pos, Vector3 scale)> _drawn = new();

    private void Start()
    {
        UIKit.EnsureEventSystem();
        _resume.Bind(Resume);
        _restart.Bind(OnRestart);
        _howToButton.Bind(OpenHowTo);
        _quitTitle.Bind(OnQuitToTitle);
        _settings.Bind(CloseSheet, OpenControls);
        if (_settingsButton != null) _settingsButton.Bind(OpenSheet);
        _howToPage.Bind(CloseHowTo);
        _controls.Bind(CloseControls);
    }

    private CaptionButton[] Menu => _settingsButton != null
        ? new[] { _resume, _restart, _howToButton, _settingsButton, _quitTitle }
        : new[] { _resume, _restart, _howToButton, _quitTitle };

    /// <summary>The menu for this screen: on a desktop as drawn, the settings sheet beside it; on a touch screen bigger
    /// boxes a finger apart with SETTINGS among them, and the sheet a page of its own.</summary>
    private void Layout()
    {
        bool touch = Form.Touch;
        var menu = Menu;
        foreach (var b in menu)
        {
            var rt = (RectTransform)b.transform;
            if (!_drawn.ContainsKey(rt)) _drawn[rt] = (rt.anchoredPosition, rt.localScale);
        }
        var sheet = (RectTransform)_settings.transform;
        if (!_drawn.ContainsKey(sheet)) _drawn[sheet] = (sheet.anchoredPosition, sheet.localScale);
        if (_settingsButton != null) _settingsButton.gameObject.SetActive(touch && !_sheetOpen);
        if (!touch)
        {
            foreach (var (rt, d) in _drawn) { rt.anchoredPosition = d.pos; rt.localScale = d.scale; }
            foreach (var b in menu) if (b != _settingsButton) b.gameObject.SetActive(true);
            if (_closedComic != null) _closedComic.SetActive(true);
            _settings.gameObject.SetActive(true);
            return;
        }
        // the comic and the bigger boxes together, centred (the sheet isn't beside them)
        const float centre = 330f;
        float top = _drawn[(RectTransform)_resume.transform].pos.y + 40f;
        for (int i = 0; i < menu.Length; i++)
        {
            var rt = (RectTransform)menu[i].transform;
            rt.localScale = _drawn[rt].scale * 1.45f;
            rt.anchoredPosition = new Vector2(_drawn[rt].pos.x + centre, top - i * 122f);
            menu[i].gameObject.SetActive(!_sheetOpen);
        }
        if (_closedComic != null)
        {
            var comic = (RectTransform)_closedComic.transform;
            if (!_drawn.ContainsKey(comic)) _drawn[comic] = (comic.anchoredPosition, comic.localScale);
            comic.anchoredPosition = _drawn[comic].pos + new Vector2(centre, 0f);
            _closedComic.SetActive(!_sheetOpen);
        }
        sheet.anchoredPosition = Vector2.zero;
        _settings.gameObject.SetActive(_sheetOpen);
    }

    private void OpenSheet()
    {
        _sheetOpen = true;
        Layout();
        _settings.RefreshAll();
    }

    private void CloseSheet()
    {
        if (!_sheetOpen) return;
        _sheetOpen = false;
        AudioDirector.I?.Back();
        Layout();
    }

    private void Update()
    {
        if (App.I != null && App.I.Flow.IsLoading) return;
        // the Controls page answers its own Esc (and the Esc that closed it doesn't resume the game too)
        if ((_controlsModal != null && _controlsModal.IsOpen) || Time.frameCount == _controlsClosed) return;

        if (_sheetOpen)
        {
            if (ShellInput.BackPressed() || ShellInput.PausePressed()) CloseSheet();
            return;
        }
        if (_howTo != null && _howTo.IsOpen)
        {
            if (ShellInput.BackPressed() || ShellInput.PausePressed()) CloseHowTo();
            return;
        }
        if (ShellInput.PausePressed())
        {
            if (IsPaused) Resume(); else Pause();
        }
    }

    /// <summary>In the browser, clicking out of the game (onto the itch.io page around it) bookmarks it: the
    /// game runs in the background, and Mom shouldn't catch Roshan while the player reads the comments.</summary>
    private void OnApplicationFocus(bool focused)
    {
        if (focused || IsPaused || Application.isEditor || !App.IsWeb || _root == null) return;
        if (App.I != null && App.I.Flow.IsLoading) return;
        Pause();
    }

    /// <summary>On a phone, a call or the home button bookmarks the page, the same way (it's there to resume
    /// when the player comes back, and nothing plays on behind it).</summary>
    private void OnApplicationPause(bool paused)
    {
        if (!paused || IsPaused || !Application.isMobilePlatform || _root == null) return;
        if (App.I != null && App.I.Flow.IsLoading) return;
        Pause();
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;                 // gameplay sources stop where they are
        AudioDirector.I?.TapeStop(true);            // the comic's music winds down
        AudioDirector.I?.Click();

        _savedLock = Cursor.lockState;
        _savedVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _root.SetActive(true);
        _sheetOpen = false;
        Layout();
        _settings.RefreshAll();
        UIKit.Select(_resume.gameObject);
        PauseChanged?.Invoke(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        Unfreeze();
        AudioDirector.I?.TapeStop(false);
        AudioDirector.I?.Back();
        Cursor.lockState = _savedLock;
        Cursor.visible = _savedVisible;
        _root.SetActive(false);
        _howTo.Close();
        _controlsModal.Close();
        PauseChanged?.Invoke(false);
    }

    private void Unfreeze()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private void OnRestart()
    {
        Unfreeze();
        AudioDirector.I?.TapeStop(false, 0.01f);
        if (GameState.I != null) GameDirector.StartPage = GameState.I.Page;   // this page, not the one the sitting began on
        App.I.Flow.Reload();
    }

    private void OnQuitToTitle()
    {
        Unfreeze();
        App.I.Flow.Load(Scenes.Title);
    }

    private void OpenHowTo() => _howTo.Open();

    private void OpenControls() => _controlsModal.Open(_controls.First);

    private int _controlsClosed = -1;

    private void CloseControls()
    {
        _controlsModal.Close();
        _controlsClosed = Time.frameCount;
        AudioDirector.I?.Back();
        UIKit.Select(_settings.ControlsRow.gameObject);
    }

    private void CloseHowTo()
    {
        _howTo.Close();
        AudioDirector.I?.Back();
        UIKit.Select(_resume.gameObject);
    }

    private void OnDestroy()
    {
        // Scene unloaded while paused: never leak a frozen clock or muted listener.
        if (IsPaused)
        {
            Unfreeze();
            PauseChanged?.Invoke(false);
        }
    }
}
