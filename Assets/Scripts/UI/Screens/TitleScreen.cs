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
    private CanvasGroup _menuGroup;        // the caption-box menu (interaction)
    private CanvasGroup _front;            // title lettering and menu (fades on leaving)
    private CanvasGroup _litLayer;         // things that glow on their own (fade on leaving)
    private CanvasGroup _doorGroup;        // Mom's door, its light, her eyes
    private DoorVisit _door;
    private TorchHand _torch;
    private readonly List<CaptionButton> _items = new();
    private RectTransform _cover;
    private TorchBeam _beam;
    private Modal _settingsModal, _howToModal;
    private SettingsPanel _settings;
    private CaptionButton _howToBack;
    private GameObject _reselectOnClose;
    private bool _leaving;

    private static bool _openingSeen;      // the opening film plays once per launch
    private CanvasGroup _titleGroup, _tagGroup;
    private RectTransform _titleRoot;

    // Door + hallway gag
    private RawImage _strip, _stripGlow;
    private Image[] _feet;
    private float _stripLevel = 0.3f;

    private void Start()
    {
        UIKit.EnsureEventSystem();
        var canvas = UIKit.Canvas("TitleCanvas", 0);
        var root = canvas.transform;

        BuildRoom(root);
        BuildFront(root);

        var modalCanvas = UIKit.Canvas("TitleModals", 20);
        _settingsModal = Modal.Create(modalCanvas.transform, "SettingsModal");
        _settings = SettingsPanel.Build(_settingsModal.Content, Vector2.zero, withButtons: true, onBack: CloseModal);
        _howToModal = Modal.Create(modalCanvas.transform, "HowToModal");
        _howToBack = HowToReadPanel.Build(_howToModal.Content, CloseModal);

        StartCoroutine(Intro());
    }

    // ---- Room (pre-rendered in Blender) -------------------------------------------------------
    //
    // Two renders of the same shot: the room by moonlight, and the room lit by Roshan's
    // torch. The torch-lit one sits underneath; the moonlit one is the beam's "dark"
    // side on top, so wherever the beam points you see that part of the room lit.
    // Positions below are where things land in the 1920 x 1080 render (origin at the
    // centre), exported from Blender alongside it.

    private static readonly Vector2 ComicCentre = new(34f, -120f);
    // The part of the gap under Mom's door that isn't hidden behind Roshan's blanket.
    private static readonly Vector2 DoorGapLeft = new(-425f, -54f), DoorGapRight = new(-347f, -54f);
    private static readonly Rect HandRect = Rect.MinMaxRect(416f, -702f, 1016f, -254f);   // rendered with overscan
    private static readonly Vector2 Lens = new(445f, -322f), LensAim = new(33f, -229f), Wrist = new(852f, -548f);
    private static readonly Rect DoorCrop = new(-769f, -136f, 656f, 668f);
    private static readonly Vector2 MomEyes = new(-305.6f, 329.2f);

    private void BuildRoom(Transform root)
    {
        var bg = UIKit.Image(root, "Night", Palette.Night);           // under any letterboxing
        UIKit.Stretch(bg.rectTransform);

        var roomLayer = CoverScaler.Create(root, "Room");
        var lit = UIKit.Raw(roomLayer, "RoomTorchLit");
        lit.texture = Resources.Load<Texture2D>("Room/room_lit");
        UIKit.Stretch(lit.rectTransform);

        var home = UIKit.Rect(roomLayer, "BeamHome");
        UIKit.Place(home, ComicCentre, new Vector2(10f, 10f));
        _cover = home;
        _beam = TorchBeam.Create(roomLayer, home, Resources.Load<Texture2D>("Room/room_dark"));
        _beam.darkColor = Color.white;                 // the moonlit render, as is
        _beam.darkness = 1f;
        _beam.litAlpha = 0f;
        _beam.radius = 0.42f;
        _beam.softness = 0.15f;
        _beam.followSelection = false;                 // keys move the yellow box; the torch stays on the comic

        // Mom's door: rendered open at several angles, with the light it lets in.
        var doorLayer = CoverScaler.Create(root, "DoorLayer");
        _doorGroup = doorLayer.gameObject.AddComponent<CanvasGroup>();
        _doorGroup.blocksRaycasts = false;
        _door = DoorVisit.Create(doorLayer, DoorCrop, MomEyes, 21f);

        // Things above the darkness: the hallway light under Mom's door, Roshan's torch.
        var litLayer = CoverScaler.Create(root, "RoomLights");
        _litLayer = litLayer.gameObject.AddComponent<CanvasGroup>();
        _litLayer.blocksRaycasts = false;

        // Strip and glow are baked with the blanket cut out of them, so the light stays
        // behind it (sizes and centres from the bake).
        _stripGlow = UIKit.Raw(litLayer, "HallwayGlow");
        _stripGlow.texture = Resources.Load<Texture2D>("Room/gap_glow");
        UIKit.Place(_stripGlow.rectTransform, new Vector2(-385f, -48f), new Vector2(152f, 72f));
        _strip = UIKit.Raw(litLayer, "HallwayStrip");
        _strip.texture = Resources.Load<Texture2D>("Room/gap_strip");
        UIKit.Place(_strip.rectTransform, new Vector2(-384f, -54f), new Vector2(88f, 4f));
        _feet = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            _feet[i] = UIKit.Image(litLayer, "Foot" + i, Palette.Ink, UIKit.Disc);
            UIKit.Place(_feet[i].rectTransform, FootPos(i, 1f), new Vector2(30f, 6f));
        }
        SetStrip(_stripLevel);

        var hand = Resources.Load<Texture2D>("Room/torch_hand");
        if (hand != null) _torch = TorchHand.Create(litLayer, litLayer, _beam, hand, HandRect, Lens, LensAim, Wrist);
    }

    /// <summary>Where Mom's foot shadow i sits in the door gap; k = 0 far off to the side, 1 at the door.</summary>
    private static Vector2 FootPos(int i, float k)
    {
        Vector2 gapMid = (DoorGapLeft + DoorGapRight) * 0.5f;
        return gapMid + new Vector2(-18f + i * 36f - (1f - k) * 40f, 0f);   // shuffle in from the left
    }

    // ---- Title lettering and menu -------------------------------------------------------------

    private void BuildFront(Transform root)
    {
        var front = UIKit.Rect(root, "Front");
        UIKit.Stretch(front);
        _front = front.gameObject.AddComponent<CanvasGroup>();

        var title = UIKit.Lettering(front, "GameTitle", "PAST BEDTIME", 118f, Palette.Paper,
            new Vector2(600f, 368f), new Vector2(720f, 150f), outline: 0.24f, shadow: 9f, tilt: -3f);
        title.characterSpacing = 2f;
        _titleRoot = (RectTransform)title.transform.parent;
        _titleGroup = _titleRoot.gameObject.AddComponent<CanvasGroup>();
        var tag = UIKit.Text(front, "Tagline", "2 AM. The comic only happens where it's being read.",
            UIKit.Body, 28f, Palette.PaperDim);
        tag.fontStyle = FontStyles.Italic;
        UIKit.Place(tag.rectTransform, new Vector2(600f, 270f), new Vector2(720f, 44f));

        var menu = UIKit.Rect(front, "Menu");
        UIKit.Stretch(menu);
        _menuGroup = menu.gameObject.AddComponent<CanvasGroup>();

        var entries = new List<(string label, UnityEngine.Events.UnityAction act, bool quiet)>();
        void Entry(string label, UnityEngine.Events.UnityAction act, bool quiet) => entries.Add((label, act, quiet));
        if (Settings.HighestPage > 0)
        {
            Entry($"CONTINUE · PAGE {Settings.HighestPage}", OnRead, true);
            Entry("START OVER", OnStartOver, true);
        }
        else Entry("START READING", OnRead, true);
        Entry("SETTINGS", OnSettings, false);
        Entry("HOW TO READ", OnHowTo, false);
        Entry("CREDITS", OnCredits, false);
        if (!App.IsWeb) Entry("QUIT", OnQuit, true);

        float[] tilts = { -1.6f, 1.2f, -0.8f, 1.5f, -1.2f, 0.9f };
        float y = 150f;
        for (int i = 0; i < entries.Count; i++)
        {
            var (label, act, quiet) = entries[i];
            var item = CaptionButton.Create(menu, "Item " + label, label, act,
                new Vector2(560f, y), new Vector2(440f, 78f), 44f, tilts[i % tilts.Length], quiet);
            _items.Add(item);
            y -= 90f;
        }
        var sel = new Selectable[_items.Count];
        for (int i = 0; i < _items.Count; i++) sel[i] = _items[i].Button;
        CaptionButton.ChainVertical(sel);

        _tagGroup = tag.gameObject.AddComponent<CanvasGroup>();
    }

    // ---- Flow ------------------------------------------------------------------------------------------

    private IEnumerator Intro()
    {
        _menuGroup.interactable = false;
        _menuGroup.alpha = 0f;
        _titleGroup.alpha = _tagGroup.alpha = 0f;
        _beam.SetPowerInstant(0f);

        if (!_openingSeen)
        {
            _openingSeen = true;
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

        var canvas = UIKit.Canvas("OpeningPanel", 5);
        var frame = UIKit.Raw(canvas.transform, "PanelFrame");
        UIKit.Stretch(frame.rectTransform);
        var shader = Shader.Find("PastBedtime/UI/ComicFrame");
        if (shader != null) { _frameMat = new Material(shader); frame.material = _frameMat; }
        else frame.color = Color.clear;
        SetFrame(0f);
        _sfx = canvas.transform;

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
        Destroy(canvas.gameObject);
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
        var burst = UIKit.Image(_titleRoot.parent, "TitleBurst", Palette.Yellow, UIKit.Burst);
        UIKit.Place(burst.rectTransform, _titleRoot.anchoredPosition, new Vector2(760f, 330f));
        burst.transform.SetSiblingIndex(_titleRoot.GetSiblingIndex());
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
        if (_leaving) return;
        bool modalOpen = _settingsModal.IsOpen || _howToModal.IsOpen;
        if (modalOpen && ShellInput.BackPressed()) CloseModal();
    }

    private void OnRead()
    {
        if (_leaving) return;
        StartCoroutine(Leave(Scenes.Game));
    }

    private void OnStartOver()
    {
        Settings.HighestPage = 0;
        Settings.Commit();
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
    private void OnHowTo() => OpenModal(_howToModal, _howToBack.Button);

    private void OpenModal(Modal modal, Selectable first)
    {
        _reselectOnClose = UIKit.Selected;
        _menuGroup.interactable = false;
        AudioDirector.I?.Duck(0.6f);
        modal.Open(first);
    }

    private void CloseModal()
    {
        _settingsModal.Close();
        _howToModal.Close();
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
