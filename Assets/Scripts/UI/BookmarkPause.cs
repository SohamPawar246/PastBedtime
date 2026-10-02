using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The pause menu, "Bookmark" (wireframe 08): Esc closes the comic on a red
/// ribbon. Resume / Restart page / How to read / Quit to title on the left, and the
/// settings sheet on the same screen (no nested menus).
///
/// App adds one to every gameplay scene automatically. Pausing freezes game time,
/// pauses gameplay audio and tape-stops the music, the same way the torch does.
/// </summary>
public class BookmarkPause : MonoBehaviour
{
    public static bool IsPaused { get; private set; }
    public static event Action<bool> PauseChanged;

    private GameObject _root;
    private Modal _howTo;
    private CaptionButton _resume;
    private SettingsPanel _settings;
    private CursorLockMode _savedLock;
    private bool _savedVisible;

    private void Start()
    {
        UIKit.EnsureEventSystem();
        var canvas = UIKit.Canvas("BookmarkCanvas", 900, transform);
        Build(canvas.transform);
        _root.SetActive(false);
    }

    private void Build(Transform canvas)
    {
        var root = UIKit.Rect(canvas, "Bookmark");
        UIKit.Stretch(root);
        _root = root.gameObject;

        var dim = UIKit.Image(root, "Dim", Palette.Night); // opaque: the comic is closed
        UIKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        BuildClosedComic(root);

        // Left column of caption buttons.
        float x = -150f, y = 190f, step = 96f;
        var size = new Vector2(380f, 74f);
        _resume = CaptionButton.Create(root, "Resume", "RESUME", Resume, new Vector2(x, y), size, 40f, tilt: -1.2f, quiet: true);
        var restart = CaptionButton.Create(root, "Restart", "RESTART PAGE", OnRestart, new Vector2(x, y - step), size, 40f, tilt: 1f);
        var howTo = CaptionButton.Create(root, "HowTo", "HOW TO READ", OpenHowTo, new Vector2(x, y - step * 2), size, 40f, tilt: -0.6f);
        var quitTitle = CaptionButton.Create(root, "QuitToTitle", "QUIT TO TITLE", OnQuitToTitle, new Vector2(x, y - step * 3), size, 40f, tilt: 1.3f);

        _settings = SettingsPanel.Build(root, new Vector2(470f, 0f), withButtons: false, onBack: null);

        var nav = new System.Collections.Generic.List<Selectable>
            { _resume.Button, restart.Button, howTo.Button, quitTitle.Button };
        nav.AddRange(_settings.Rows);
        CaptionButton.ChainVertical(nav.ToArray());

        var hint = UIKit.Text(root, "Hint", "ESC  RESUME     ←/→  CHANGE A SETTING", UIKit.Mono, 22f, Palette.PaperDim);
        UIKit.Pin(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(1200f, 40f));

        // How-to-read opens above everything, inside the same canvas.
        _howTo = Modal.Create(canvas, "HowToModal");
        HowToReadPanel.Build(_howTo.Content, CloseHowTo);
    }

    /// <summary>The comic, closed on its ribbon: the same cover as on the bed, a little
    /// page thickness behind it, the ribbon hanging out of the bottom, and a caption
    /// plate above it saying where we are.</summary>
    private static void BuildClosedComic(Transform root)
    {
        const float w = 400f, h = 547f;
        var book = UIKit.Rect(root, "ClosedComic");
        UIKit.Place(book, new Vector2(-640f, -10f), new Vector2(w, h));
        book.localRotation = Quaternion.Euler(0, 0, -4f);

        var shadow = UIKit.SoftShadow(book, "Shadow", 40f, 0.8f);
        shadow.rectTransform.anchoredPosition = new Vector2(14f, -20f);

        // Ribbon first, so only the part hanging below the book shows.
        var ribbon = UIKit.Image(book, "Ribbon", Palette.HeroRed, UIKit.Ribbon);
        UIKit.Place(ribbon.rectTransform, new Vector2(118f, -h * 0.5f - 40f), new Vector2(40f, 170f));

        // Page edges: two sheets peeking out behind the cover give it thickness.
        for (int i = 2; i >= 1; i--)
        {
            var sheet = UIKit.Image(book, "Pages" + i, Palette.PaperDim, UIKit.Box, sliced: true);
            sheet.pixelsPerUnitMultiplier = 2.5f;
            UIKit.Stretch(sheet.rectTransform);
            sheet.rectTransform.anchoredPosition = new Vector2(5f * i, -4f * i);
        }

        var cover = UIKit.Rect(book, "Cover");
        UIKit.Stretch(cover);
        ComicCoverView.Build(cover, w, h);

        var plate = UIKit.Panel(book, "BookmarkedPlate", new Vector2(0f, h * 0.5f + 64f), new Vector2(330f, 72f),
            Palette.Yellow, shadow: 6f, tilt: 2f);
        var label = UIKit.Text(plate.transform, "Text", "BOOKMARKED", UIKit.Display, 48f, Palette.Ink);
        label.characterSpacing = 4f;
        UIKit.Stretch(label.rectTransform);
    }

    private void Update()
    {
        if (App.I != null && App.I.Flow.IsLoading) return;

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
        App.I.Flow.Reload();
    }

    private void OnQuitToTitle()
    {
        Unfreeze();
        App.I.Flow.Load(Scenes.Title);
    }

    private void OpenHowTo() => _howTo.Open();

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
