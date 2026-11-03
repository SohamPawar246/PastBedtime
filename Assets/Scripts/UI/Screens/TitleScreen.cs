using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The title (wireframe 08): the cover of Max Voltage #1 lying on Roshan's bed in
/// the dark, Mom's door shut in the corner with a faint strip of hallway light
/// under it. The torch clicks on and lights the comic; after that the beam follows
/// the mouse, and drifts back to the comic when the mouse rests.
///
/// The first time the title opens (once per launch) it plays as a comic panel:
/// no menu, a paper-and-ink panel border with Ben-Day shading in the corners, and
/// sound-effect lettering while Mom checks in. The hallway
/// light comes on, slippers shuffle up, Roshan clicks the torch off, the door
/// creaks open, warm light pours across the room and the comic, and Mom glares
/// from the doorway ("...kids.") before shutting it again. The torch stays on the
/// comic; any key skips. Then the title and the caption-box menu are stamped onto
/// the page. Later visits to the title skip the opening.
///
/// Starting a page hands the torch beam to the scene transition, so the light the
/// player is looking at is the light that goes out.
/// </summary>
public class TitleScreen : MonoBehaviour
{
    [Tooltip("The title, drawn in the scene (switched off till it starts): the room and its menu, and the modal pages.")]
    [SerializeField] private GameObject _canvas, _modals;
    [SerializeField] private CanvasGroup _menuGroup;        // the caption-box menu (interaction)
    [SerializeField] private CanvasGroup _front;            // title lettering and menu (fades on leaving)
    [SerializeField] private CanvasGroup _litLayer;         // things that glow on their own (fade on leaving)
    [SerializeField] private CanvasGroup _doorGroup;        // Mom's door, its light, her eyes
    [SerializeField] private DoorVisit _door;
    [SerializeField] private TorchHand _torch;
    private readonly List<CaptionButton> _items = new();
    [SerializeField] private RectTransform _cover;
    [SerializeField] private TorchBeam _beam;
    [SerializeField] private Modal _settingsModal, _howToModal, _controlsModal;
    [SerializeField] private SettingsPanel _settings;
    [SerializeField] private ControlsPanel _controls;
    [SerializeField] private HowToReadLive _howTo;
    private GameObject _reselectOnClose;
    private bool _leaving;

    [Header("Menu")]
    [Tooltip("Every entry the menu can have; which show depends on the save (CONTINUE and START OVER once a page has " +
             "been read, else START READING) and the platform (no QUIT on the web).")]
    [SerializeField] private CaptionButton _continue, _startOver, _startReading, _settingsItem, _howToItem, _creditsItem, _quit;
    [Tooltip("CONTENTS (a touch screen's, in START OVER's place: START OVER is on the contents page) and its page.")]
    [SerializeField] private CaptionButton _contentsItem;
    [SerializeField] private Modal _contentsModal;
    [SerializeField] private ContentsPanel _contents;
    [Tooltip("Where the menu's slots run: the first one's middle, and the step down to each next.")]
    [SerializeField] private Vector2 _menuTop = new(560f, 150f);
    [SerializeField] private float _menuStep = 90f;
    [Tooltip("Each slot's tilt, in turn (repeating).")]
    [SerializeField] private float[] _menuTilts = { -1.6f, 1.2f, -0.8f, 1.5f, -1.2f, 0.9f };

    /// <summary>The opening film plays once per launch (tests set it to go straight to the menu).</summary>
    public static bool OpeningSeen;
    [SerializeField] private CanvasGroup _titleGroup, _tagGroup;
    [SerializeField] private RectTransform _titleRoot;
    [Tooltip("The burst behind the title as it pops on (switched off till then).")]
    [SerializeField] private Image _titleBurst;
    [Tooltip("The opening's comic-panel frame (switched off till the opening plays, once per launch).")]
    [SerializeField] private GameObject _opening;
    [SerializeField] private RawImage _frame;

    // Door + hallway gag
    [SerializeField] private RawImage _strip, _stripGlow;
    [SerializeField] private Image[] _feet;
    private float _stripLevel = 0.3f;

    private void Start()
    {
        UIKit.EnsureEventSystem();
        Menu();
        MakeCoverTappable();
        _settings.Bind(CloseModal, OpenControls);
        _howTo.Bind(CloseModal);
        if (_contents != null) _contents.Bind(OnPage, OnStartOver, CloseModal);
        _controls.Bind(CloseControls);
        SetStrip(_stripLevel);
        _canvas.SetActive(true);
        _modals.SetActive(true);
        if (Form.Touch)
        {
            Canvas.ForceUpdateCanvases();                 // (the canvas has only just come on: its scale is needed)
            FingerSizedMenu();
        }
        StartCoroutine(Intro());
    }

    /// <summary>The menu for this save and platform: its entries in their slots, top to bottom.</summary>
    private void Menu()
    {
        var entries = new List<(CaptionButton item, string label, UnityEngine.Events.UnityAction act)>();
        bool contents = GameInput.OnTouchDevice && _contentsItem != null;
        if (Settings.HighestPage > 0)
        {
            entries.Add((_continue, $"CONTINUE · PAGE {Settings.HighestPage}", OnRead));
            if (contents) entries.Add((_contentsItem, "CONTENTS", OnContents));
            else entries.Add((_startOver, "START OVER", OnStartOver));
        }
        else entries.Add((_startReading, "START READING", OnRead));
        entries.Add((_settingsItem, "SETTINGS", OnSettings));
        entries.Add((_howToItem, "HOW TO READ", OnHowTo));
        entries.Add((_creditsItem, "CREDITS", OnCredits));
        if (!App.IsWeb && !GameInput.OnTouchDevice) entries.Add((_quit, "QUIT", OnQuit));   // (a phone's Back quits)

        foreach (var item in new[] { _continue, _startOver, _startReading, _settingsItem, _howToItem, _creditsItem, _quit, _contentsItem })
            if (item != null) item.gameObject.SetActive(false);
        for (int i = 0; i < entries.Count; i++)
        {
            var (item, label, act) = entries[i];
            item.gameObject.SetActive(true);
            item.name = "Item " + label;
            item.SetLabel(label);
            ((RectTransform)item.transform).anchoredPosition = _menuTop - new Vector2(0f, _menuStep * i);
            item.Rest(_menuTilts[i % _menuTilts.Length]);
            item.Bind(act);
            _items.Add(item);
        }
        var sel = new Selectable[_items.Count];
        for (int i = 0; i < _items.Count; i++) sel[i] = _items[i].Button;
        CaptionButton.ChainVertical(sel);
    }

    /// <summary>On a touch screen the caption boxes grow to a finger's size (8 mm tall, 2 mm apart), in a column that
    /// starts under the tagline and stops above the screen's bottom edge (smaller boxes if it must).</summary>
    private void FingerSizedMenu()
    {
        if (_items.Count == 0) return;
        var first = (RectTransform)_items[0].transform;
        var parent = first.parent;
        var canvas = first.GetComponentInParent<Canvas>().rootCanvas;
        var screen = (RectTransform)canvas.transform;
        float toLocal = canvas.transform.lossyScale.x / parent.lossyScale.x;            // canvas units to the menu's
        float gap = Form.Mm(canvas, 2f) * toLocal;
        // from under the tagline to the bottom of the screen, in the menu's own (local) units
        var tagCorners = new Vector3[4];
        ((RectTransform)_tagGroup.transform).GetWorldCorners(tagCorners);
        float top = parent.InverseTransformPoint(tagCorners[0]).y - gap;
        float bottom = parent.InverseTransformPoint(screen.TransformPoint(new Vector3(0f, screen.rect.yMin, 0f))).y + gap;
        float h = first.rect.height;
        float scale = Mathf.Clamp(Form.Mm(canvas, 8f) * toLocal / h, 1f, 1.9f);
        float fit = (top - bottom - (_items.Count - 1) * gap) / (_items.Count * h);
        scale = Mathf.Max(1f, Mathf.Min(scale, fit));
        float step = h * scale + gap;
        Vector2 local = first.localPosition, anchored = first.anchoredPosition;
        Vector2 toAnchored = anchored - local;                                         // (the same for every box)
        // clear of the comic on the bed (it's a button too), and on the screen
        float half = first.rect.width * scale * 0.5f;
        var coverCorners = new Vector3[4];
        _cover.GetWorldCorners(coverCorners);
        float coverRight = parent.InverseTransformPoint(coverCorners[2]).x;
        float screenRight = parent.InverseTransformPoint(screen.TransformPoint(new Vector3(screen.rect.xMax, 0f, 0f))).x;
        float x = Mathf.Min(Mathf.Max(local.x, coverRight + gap + half), screenRight - gap - half) + toAnchored.x;
        for (int i = 0; i < _items.Count; i++)
        {
            var rt = (RectTransform)_items[i].transform;
            rt.localScale = Vector3.one * scale;
            rt.anchoredPosition = new Vector2(x, top - h * scale * 0.5f - step * i + toAnchored.y);
        }
    }

    // The part of the gap under Mom's door (where it lands in the 1920 x 1080 room render, centre origin) that
    // isn't hidden behind Roshan's blanket.
    private static readonly Vector2 DoorGapLeft = new(-425f, -54f), DoorGapRight = new(-347f, -54f);

    /// <summary>Where Mom's foot shadow i sits in the door gap; k = 0 far off to the side, 1 at the door.</summary>
    private static Vector2 FootPos(int i, float k)
    {
        Vector2 gapMid = (DoorGapLeft + DoorGapRight) * 0.5f;
        return gapMid + new Vector2(-18f + i * 36f - (1f - k) * 40f, 0f);   // shuffle in from the left
    }

    // ---- Flow ------------------------------------------------------------------------------------------

    private IEnumerator Intro()
    {
        _menuGroup.interactable = false;
        _menuGroup.alpha = 0f;
        _titleGroup.alpha = _tagGroup.alpha = 0f;
        _beam.SetPowerInstant(0f);

        if (!OpeningSeen)
        {
            OpeningSeen = true;
            yield return Opening();
        }
        else
        {
            StartCoroutine(_beam.SwitchOn(0.4f));
            yield return Wait(0.7f);
        }
        yield return Reveal();
    }

    // ---- The opening panel --------------------------------------------------------------------------------

    private Material _frameMat;
    private Transform _sfx;                // where the opening's lettering pops up (null otherwise)
    private float _skipAfter;
    private static readonly int AmountId = Shader.PropertyToID("_Amount");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int HeightId = Shader.PropertyToID("_HeightPx");

    /// <summary>The opening, framed as a comic panel: the torch comes on, Mom visits,
    /// the frame lifts. The torch stays on the comic; any key skips to the menu.</summary>
    private IEnumerator Opening()
    {
        _beam.followPointer = false;

        _opening.SetActive(true);
        var frameMat = Resources.Load<Material>("Materials/ComicFrame");
        if (frameMat != null) { _frameMat = new Material(frameMat); _frame.material = _frameMat; }
        else _frame.color = Color.clear;
        SetFrame(0f);
        _sfx = _opening.transform;

        bool skipped = false;
        IEnumerator Watch(IEnumerator step)
        {
            while (!skipped && step.MoveNext())
            {
                if (Time.unscaledTime > _skipAfter && ShellInput.AnyPress()) { skipped = true; break; }
                yield return step.Current;
            }
        }
        _skipAfter = Time.unscaledTime + 0.6f;

        StartCoroutine(_beam.SwitchOn(0.5f));
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            SetFrame(Mathf.SmoothStep(0f, 1f, t / 0.6f));
            yield return null;
        }
        SetFrame(1f);
        yield return Watch(WaitSteps(1.8f));
        if (!skipped) yield return Watch(MomVisitSteps());
        if (!skipped) yield return Watch(WaitSteps(0.3f));
        if (skipped) ResetVisit();

        // The panel lifts off the screen.
        _sfx = null;
        for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(1f, 0f, t / 0.45f);
            SetFrame(k);
            yield return null;
        }
        Destroy(_opening);
        if (_frameMat != null) Destroy(_frameMat);
        _beam.followPointer = true;
    }

    private static IEnumerator WaitSteps(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    private void SetFrame(float amount)
    {
        if (_frameMat == null) return;
        _frameMat.SetFloat(AmountId, amount);
        _frameMat.SetFloat(AspectId, (float)Screen.width / Mathf.Max(1, Screen.height));
        _frameMat.SetFloat(HeightId, Screen.height);
    }

    /// <summary>Lettering for the opening's sounds (only while the opening panel is up).</summary>
    private void Sfx(IEnumerator pop)
    {
        if (_sfx != null) StartCoroutine(pop);
    }

    private static IEnumerator PopIn(CanvasGroup g, RectTransform rt)
    {
        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.2f;
            float back = 1f + 2.6f * Mathf.Pow(k - 1f, 3f) + 1.6f * Mathf.Pow(k - 1f, 2f);
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.5f, 1f, back);
            g.alpha = Mathf.Clamp01(k * 3f);
            yield return null;
        }
        rt.localScale = Vector3.one;
        g.alpha = 1f;
    }

    /// <summary>Puts the room back as if Mom had never come (after a skip).</summary>
    private void ResetVisit()
    {
        SetDoor(0f);
        _door.Eyes.Blink = 0f;
        _door.Eyes.Look = 0f;
        foreach (var f in _feet) { f.enabled = true; f.color = Palette.Ink.Alpha(0f); }
        SetStrip(0.3f);
        if (_beam.Power < 0.99f) StartCoroutine(_beam.SwitchOn(0f));
        AudioDirector.I?.Duck(1f);
    }

    // ---- The title and menu arrive -----------------------------------------------------------------------

    /// <summary>The title pops onto the page with a burst behind it, then each caption
    /// box is stamped down one after another, like stickers slapped on a page.</summary>
    private IEnumerator Reveal()
    {
        var restRot = _titleRoot.localRotation;
        var burst = _titleBurst;
        burst.gameObject.SetActive(true);
        for (float t = 0f; t < 0.42f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.42f;
            float back = 1f + 2.6f * Mathf.Pow(k - 1f, 3f) + 1.6f * Mathf.Pow(k - 1f, 2f);
            _titleGroup.alpha = Mathf.Clamp01(k * 4f);
            _titleRoot.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1f, back);
            _titleRoot.localRotation = restRot * Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(8f, 0f, back));
            burst.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.25f, Mathf.Sqrt(k));
            burst.color = Palette.Yellow.Alpha(0.9f * (1f - k));
            yield return null;
        }
        Destroy(burst.gameObject);
        _titleGroup.alpha = 1f;
        _titleRoot.localScale = Vector3.one;
        _titleRoot.localRotation = restRot;

        StartCoroutine(PopIn(_tagGroup, (RectTransform)_tagGroup.transform));
        yield return Wait(0.12f);

        _menuGroup.alpha = 1f;
        foreach (var item in _items) { item.Body.localScale = Vector3.zero; }
        for (int i = 0; i < _items.Count; i++)
        {
            StartCoroutine(Stamp(_items[i].Body, i % 2 == 0 ? -3f : 3f));
            yield return Wait(0.08f);
        }
        yield return Wait(0.25f);
        _menuGroup.interactable = true;
        yield return null; // let the CanvasGroup change reach the buttons first
        UIKit.Select(_items[0].gameObject);
    }

    /// <summary>A caption box slapped onto the page: drops from big, overshoots, settles.</summary>
    private static IEnumerator Stamp(RectTransform body, float extraTilt)
    {
        var rest = body.localRotation;
        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.2f;
            float back = 1f + 2.6f * Mathf.Pow(k - 1f, 3f) + 1.6f * Mathf.Pow(k - 1f, 2f);
            body.localScale = Vector3.one * Mathf.LerpUnclamped(1.6f, 1f, back);
            body.localRotation = rest * Quaternion.Euler(0f, 0f, extraTilt * (1f - k));
            yield return null;
        }
        body.localScale = Vector3.one;
        body.localRotation = rest;
    }

    private void Update()
    {
        // the Controls page answers its own Esc (and the Esc that closed it isn't this card's too)
        if (_leaving || _controlsModal.IsOpen || Time.frameCount == _controlsClosed) return;
        bool modalOpen = _settingsModal.IsOpen || _howToModal.IsOpen || (_contentsModal != null && _contentsModal.IsOpen);
        if (modalOpen && ShellInput.BackPressed()) CloseModal();
        else if (!modalOpen && Application.isMobilePlatform && ShellInput.BackPressed()) BackOnTitle();
    }

    private float _backAt = -10f;
    private string _tagLine;

    /// <summary>Android's Back on the title: once says how to leave, twice in a row leaves (an edge swipe shouldn't
    /// close the comic by accident).</summary>
    private void BackOnTitle()
    {
        var tag = _tagGroup.GetComponentInChildren<TMP_Text>();
        if (Time.unscaledTime - _backAt < 2.5f)
        {
            App.I.Quit();
            return;
        }
        _backAt = Time.unscaledTime;
        AudioDirector.I?.Back();
        if (tag == null) return;
        _tagLine ??= tag.text;
        tag.text = "BACK AGAIN TO STOP READING";
        StartCoroutine(PutTagBack(tag));
    }

    private IEnumerator PutTagBack(TMP_Text tag)
    {
        yield return new WaitForSecondsRealtime(2.5f);
        tag.text = _tagLine;
    }

    /// <summary>On a touch screen the comic on the bed is the biggest button there is: a tap opens it (as CONTINUE).</summary>
    private void MakeCoverTappable()
    {
        if (!GameInput.OnTouchDevice || _cover == null) return;
        if (!_cover.TryGetComponent<Graphic>(out _))
        {
            var hit = _cover.gameObject.AddComponent<Image>();
            hit.color = Color.clear;                                // invisible: it only catches the tap
        }
        var button = _cover.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => { if (_menuGroup.interactable) OnRead(); });
    }

    private void OnRead()
    {
        if (_leaving) return;
        // "continue" picks up at the furthest page reached; a first read starts at page 1
        GameDirector.StartPage = Mathf.Max(1, Settings.HighestPage);
        PageManager.ResumeNext = GameInput.OnTouchDevice;       // a phone goes back to the very panel it was on
        StartCoroutine(OpenTheComic());
    }

    /// <summary>"Start reading": the words fade, the torch settles back on the comic, and the game
    /// takes over this exact picture (a cut nobody sees); there the cover swings open and the view
    /// zooms in onto the page.</summary>
    private IEnumerator OpenTheComic()
    {
        _leaving = true;
        _menuGroup.interactable = false;
        AudioDirector.I?.Duck(1f);                     // in case Mom was mid-visit
        AudioDirector.I?.Confirm();
        UIKit.Select(null);
        _beam.followPointer = false;                   // the light drifts back onto the comic
        _beam.followSelection = false;
        ResetVisit();
        for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
        {
            _front.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / 0.45f);
            yield return null;
        }
        _front.alpha = 0f;
        GameDirector.OpenFromTitle = true;
        App.I.Flow.Cut(Scenes.Game);
    }

    private void OnStartOver()
    {
        if (_contentsModal != null) _contentsModal.Close();
        Settings.ResetProgress();                    // a fresh comic: no stars or torch upgrades carried over
        OnRead();
    }

    /// <summary>The words and the room's own lights fade while the room sinks into
    /// the dark, then the scene transition takes over this exact beam and closes it.</summary>
    private IEnumerator Leave(string scene)
    {
        _leaving = true;
        _menuGroup.interactable = false;
        AudioDirector.I?.Duck(1f);                     // in case Mom was mid-visit
        AudioDirector.I?.Confirm();
        UIKit.Select(null);

        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
            _front.alpha = 1f - k;
            _litLayer.alpha = 1f - k;
            _doorGroup.alpha = 1f - k;
            _beam.darkColor = Color.Lerp(Color.white, Color.black, k);   // the room sinks into the dark
            yield return null;
        }
        _beam.darkColor = Color.black;
        App.I.Flow.Load(scene, _beam.ScreenPosition, _beam.ScreenRadius);
    }

    private void OnSettings() => OpenModal(_settingsModal, _settings.First);
    private void OnHowTo() => OpenModal(_howToModal, _howTo.Back.Button);
    private void OnContents() => OpenModal(_contentsModal, _contents.Back.Button);

    /// <summary>A page picked on the contents page: read from its start (to look for the stars still on it).</summary>
    private void OnPage(int page)
    {
        if (_leaving) return;
        _contentsModal.Close();
        GameDirector.StartPage = page;
        PageManager.ResumeNext = false;
        StartCoroutine(OpenTheComic());
    }

    private void OpenModal(Modal modal, Selectable first)
    {
        _reselectOnClose = UIKit.Selected;
        _menuGroup.interactable = false;
        AudioDirector.I?.Duck(0.6f);
        modal.Open(first);
    }

    private void OpenControls() => _controlsModal.Open(_controls.First);

    /// <summary>Back from the Controls page to the settings card it was opened from.</summary>
    private int _controlsClosed = -1;

    private void CloseControls()
    {
        _controlsModal.Close();
        _controlsClosed = Time.frameCount;
        AudioDirector.I?.Back();
        UIKit.Select(_settings.ControlsRow.gameObject);
    }

    private void CloseModal()
    {
        _controlsModal.Close();
        _settingsModal.Close();
        _howToModal.Close();
        if (_contentsModal != null) _contentsModal.Close();
        _menuGroup.interactable = true;
        AudioDirector.I?.Duck(1f);
        AudioDirector.I?.Back();
        UIKit.Select(_reselectOnClose != null ? _reselectOnClose : _items[0].gameObject);
    }

    private void OnCredits()
    {
        if (_leaving) return;
        _leaving = true;
        App.I.Flow.Load(Scenes.Credits);
    }

    private void OnQuit() => App.I.Quit();

    // ---- Mom's visits ----------------------------------------------------------------------------------

    private IEnumerator MomVisitSteps()
    {
        // The hallway light clicks on and slippers shuffle up to the door.
        yield return FadeStrip(1f, 0.25f);
        for (int step = 0; step < 5 && !_leaving; step++)
        {
            float k = (step + 1) / 5f;
            AudioDirector.I?.Footstep(Mathf.Lerp(0.08f, 0.3f, k));
            if (step == 1 || step == 3) Sfx(ComicSfx.Word(_sfx, "shuffle", new Vector2(-330f + step * 22f, 40f), 30f, Palette.Paper, -4f + step * 3f, 0.5f));
            for (int i = 0; i < _feet.Length; i++)
            {
                _feet[i].rectTransform.anchoredPosition = FootPos(i, k);
                _feet[i].color = Palette.Ink.Alpha(0.85f * k * k);
            }
            yield return Wait(0.42f);
        }
        if (_leaving) yield break;
        yield return Wait(0.35f);

        // Roshan clicks the torch off and holds his breath.
        AudioDirector.I?.TorchOff();
        Sfx(ComicSfx.Word(_sfx, "CLICK!", new Vector2(470f, -230f), 54f, Palette.Yellow, -8f, 0.6f));
        StartCoroutine(_beam.PowerTo(0f, 0.12f));
        AudioDirector.I?.Duck(0.3f);
        yield return Wait(0.55f);

        // The door creaks open; warm light pours across the room and the comic.
        AudioDirector.I?.DoorCreak();
        Sfx(ComicSfx.Word(_sfx, "CREEEAK...", new Vector2(-20f, 430f), 58f, Palette.Paper, 5f, 1.3f));
        yield return SwingDoor(0f, DoorVisit.MaxAngle, 1.5f, easeOut: true);

        // Mom glares: a blink, a look round the room, back at Roshan.
        var eyes = _door.Eyes;
        const float hold = 2.6f;
        for (float t = 0f; t < hold && !_leaving; t += Time.unscaledDeltaTime)
        {
            eyes.Blink = BlinkAt(t, 0.55f) + BlinkAt(t, 2.05f);
            eyes.Look = t < 1.0f ? 0f : t < 1.45f ? Mathf.SmoothStep(0f, -0.7f, (t - 1f) / 0.15f)
                      : t < 1.9f ? Mathf.SmoothStep(-0.7f, 0.6f, (t - 1.45f) / 0.2f)
                      : Mathf.SmoothStep(0.6f, 0f, (t - 1.9f) / 0.2f);
            yield return null;
        }
        eyes.Blink = 0f; eyes.Look = 0f;
        Sfx(ComicSfx.Balloon(_sfx, "...kids.", new Vector2(-150f, 420f), 1.1f));
        yield return Wait(0.9f);

        // ...and shuts it.
        yield return SwingDoor(DoorVisit.MaxAngle, 0f, 0.7f, easeOut: false);
        AudioDirector.I?.DoorClose();
        Sfx(ComicSfx.Word(_sfx, "THUD!", new Vector2(-360f, 230f), 70f, Palette.HeroRed, -6f, 0.6f, burst: true));

        // Slippers shuffle away, the hallway goes dark again.
        for (int step = 0; step < 4 && !_leaving; step++)
        {
            float k = 1f - (step + 1) / 4f;
            AudioDirector.I?.Footstep(Mathf.Lerp(0.05f, 0.25f, k));
            foreach (var f in _feet) f.color = Palette.Ink.Alpha(0.85f * k);
            yield return Wait(0.45f);
        }
        yield return FadeStrip(0.3f, 0.5f);
        yield return Wait(0.5f);

        // Roshan risks it: the torch comes back on.
        if (_leaving) yield break;
        AudioDirector.I?.TorchOn();
        StartCoroutine(_beam.SwitchOn(0f));
        AudioDirector.I?.Duck(1f);
    }

    /// <summary>Animates the door between two angles; the strip of light under it and
    /// Mom's foot shadows give way to the open doorway.</summary>
    private IEnumerator SwingDoor(float from, float to, float seconds, bool easeOut)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;
            float e = easeOut ? 1f - (1f - k) * (1f - k) * (1f - k) : k * k;
            SetDoor(Mathf.Lerp(from, to, e));
            yield return null;
        }
        SetDoor(to);
    }

    private void SetDoor(float angle)
    {
        _door.Angle = angle;
        float gapHidden = Mathf.InverseLerp(0f, 10f, angle);
        _strip.enabled = _stripGlow.enabled = gapHidden < 0.999f;
        _strip.color = Palette.Hallway.Alpha((1f - gapHidden) * Mathf.Lerp(0.25f, 1f, _stripLevel));
        _stripGlow.color = Palette.Hallway.Alpha((1f - gapHidden) * 0.5f * _stripLevel);
        foreach (var f in _feet) f.enabled = gapHidden < 0.999f;
    }

    /// <summary>A quick blink centred on <paramref name="at"/> seconds (0 open .. 1 shut).</summary>
    private static float BlinkAt(float t, float at)
    {
        float d = Mathf.Abs(t - at);
        return d > 0.09f ? 0f : 1f - d / 0.09f;
    }

    private static IEnumerator Wait(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
    }

    private void OnDestroy()
    {
        AudioDirector.I?.Duck(1f);                     // never leave the music ducked
    }

    private IEnumerator FadeStrip(float to, float seconds)
    {
        float from = _stripLevel;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            SetStrip(Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        SetStrip(to);
    }

    private void SetStrip(float level)
    {
        _stripLevel = level;
        _strip.color = Palette.Hallway.Alpha(Mathf.Lerp(0.25f, 1f, level));
        _stripGlow.color = Palette.Hallway.Alpha(0.5f * level);
    }
}
