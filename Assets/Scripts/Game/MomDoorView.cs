using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mom's door (GDD sections 8 and 13), read straight off the <see cref="MomDirector"/>: the real
/// door in the bedroom render, top-left behind the comic, with the same layers as the title's:
/// the strip of hallway light under it, the shadows of her feet, the door swinging open with her
/// angry eyes in the gap and the light it lets into the room. The door frame glows orange to red
/// as suspicion rises, with a heartbeat you can hear. Around the comic: a caption by the door for
/// every sound (and her lines), the three slippers on the floor bottom-left (Mom's strikes), and
/// footprints marching in along the top edge toward the door while she approaches.
/// </summary>
public class MomDoorView : MonoBehaviour
{
    public static MomDoorView I { get; private set; }

    // where things sit in the 1920 x 1080 room render (centre origin, y up), from Blender
    private static readonly Rect DoorCrop = new(-769f, -136f, 656f, 668f);
    private static readonly Vector2 MomEyes = new(-305.6f, 329.2f);
    private static readonly Vector2 DoorGapLeft = new(-425f, -54f), DoorGapRight = new(-347f, -54f);
    private static readonly Rect DoorFrame = Rect.MinMaxRect(-508f, -62f, -238f, 480f);
    // around the comic, on the screen-fixed front layer (1920 x 1080, centre origin)
    private static readonly Vector2 CaptionAt = new(-800f, -150f), SayAt = new(-760f, 70f);
    private static readonly Vector2[] SlipperAt = { new(-900f, -488f), new(-836f, -505f), new(-772f, -490f) };

    private DoorVisit _door;
    private RawImage _strip, _glow;
    private Image[] _feet;
    private Image _frame;
    private TextMeshProUGUI _caption, _say;
    private CanvasGroup _sayGroup;
    private Image[] _slippers;
    private RectTransform _track;
    private Image[] _prints;
    private Image _flood;
    private Vector2[] _slipperHome;
    private int _slipperCount = -1, _flying;
    private float _beat, _floodT = 1f, _lastBust, _sayFor;

    /// <summary>The ending: someone's reading by torchlight on the other side of the door (0 = no, 1 = a
    /// fully wound torch).</summary>
    [System.NonSerialized] public float TorchUnderDoor;
    /// <summary>The ending: the colour of that torchlight (its lens).</summary>
    [System.NonSerialized] public Color TorchTint = TorchWhite;
    public static readonly Color TorchWhite = new(0.86f, 0.93f, 1f);
    /// <summary>The ending: the game's door HUD (slippers, footprints, captions) steps aside.</summary>
    [System.NonSerialized] public bool Ending;
    private float _torchClock;

    /// <summary>The middle of Mom's door in the room (zoomed-layer units), for the ending's push in.</summary>
    public static readonly Vector2 DoorCentre = new(-373f, 209f);
    /// <summary>Just above the gap under the door, for the ending's last, slow push down onto its light.</summary>
    public static readonly Vector2 AboveGap = new(-386f, -20f);

    /// <summary>Where the light under the door is on screen (pixels), for the ending's iris.</summary>
    public Vector2 LightOnScreen()
    {
        var canvas = _glow.canvas != null ? _glow.canvas.rootCanvas : null;
        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.WorldToScreenPoint(cam, _glow.rectTransform.position);
    }

    private void Awake() => I = this;
    private void OnDestroy() { if (I == this) I = null; }

    public static MomDoorView Build(RectTransform doorLayer, RectTransform front)
    {
        var view = doorLayer.gameObject.AddComponent<MomDoorView>();
        view.BuildUI(doorLayer, front);
        return view;
    }

    private void BuildUI(RectTransform room, RectTransform front)
    {
        // ---- the door itself, in the room (zooms with it) --------------------------------------------
        _door = DoorVisit.Create(room, DoorCrop, MomEyes, 21f, "spill_open_");   // its light, minus the comic the title has there
        _glow = UIKit.Raw(room, "HallwayGlow");
        _glow.texture = Resources.Load<Texture2D>("Room/gap_glow");
        UIKit.Place(_glow.rectTransform, new Vector2(-385f, -48f), new Vector2(152f, 72f));
        _strip = UIKit.Raw(room, "HallwayStrip");
        _strip.texture = Resources.Load<Texture2D>("Room/gap_strip");
        UIKit.Place(_strip.rectTransform, new Vector2(-384f, -54f), new Vector2(88f, 4f));
        _feet = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            _feet[i] = UIKit.Image(room, "Foot" + i, Palette.Ink.Alpha(0f), UIKit.Disc);
            UIKit.Place(_feet[i].rectTransform, FootPos(i, 1f), new Vector2(30f, 6f));
        }
        // the frame's glow: a ring of light traced round the door frame
        _frame = UIKit.Image(room, "SuspicionFrame", Palette.Hallway.Alpha(0f), FrameGlow);
        _frame.type = Image.Type.Sliced;
        UIKit.Place(_frame.rectTransform, DoorFrame.center, DoorFrame.size + new Vector2(64f, 64f));
        _frame.raycastTarget = false;

        // ---- around the comic (screen-fixed) -------------------------------------------------------
        _caption = UIKit.Text(front, "DoorCaption", "", UIKit.Display, 30f, Palette.PaperDim);
        UIKit.Place(_caption.rectTransform, CaptionAt, new Vector2(300f, 44f));
        _caption.raycastTarget = false;

        var say = UIKit.Panel(front, "MomSays", SayAt, new Vector2(230f, 64f), Palette.Ink, shadow: 5f, tilt: 2f);
        _sayGroup = say.transform.parent.gameObject.AddComponent<CanvasGroup>();
        _sayGroup.alpha = 0f;
        _sayGroup.blocksRaycasts = false;
        _say = UIKit.Text(say.transform, "Text", "", UIKit.Display, 34f, Palette.Paper);
        UIKit.Stretch(_say.rectTransform, 6f);

        _slippers = new Image[3];
        _slipperHome = new Vector2[3];
        for (int i = 0; i < 3; i++)
        {
            var s = UIKit.Image(front, "Slipper" + i, Palette.HeroBlue, Slipper);
            s.rectTransform.sizeDelta = new Vector2(30f, 60f);
            _slipperHome[i] = SlipperAt[i];
            s.rectTransform.anchoredPosition = _slipperHome[i];
            s.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -78f + i * 14f);   // kicked off by the bed
            s.raycastTarget = false;
            _slippers[i] = s;
        }

        // footprints along the top edge, marching in toward the door (the audio's visual twin)
        _track = UIKit.Rect(front, "FootprintTrack");
        _track.anchorMin = _track.anchorMax = new Vector2(0.5f, 0.5f);
        _track.sizeDelta = new Vector2(1300f, 60f);
        _track.anchoredPosition = new Vector2(240f, 488f);
        _prints = new Image[10];
        for (int i = 0; i < _prints.Length; i++)
        {
            _prints[i] = UIKit.Image(_track, "Print" + i, Palette.Hallway.Alpha(0f), Slipper);
            _prints[i].rectTransform.anchorMin = _prints[i].rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _prints[i].raycastTarget = false;
        }

        // BUSTED: the hall light floods the whole screen for a moment
        _flood = UIKit.Image(front, "HallwayFlood", Palette.Hallway.Alpha(0f));
        UIKit.Stretch(_flood.rectTransform, -400f);
        _flood.raycastTarget = false;
    }

    /// <summary>Mom's line through the door, by the door.</summary>
    public void Say(string text)
    {
        _say.text = text;
        _sayFor = Mathf.Clamp(1.6f + text.Length * 0.07f, 2.2f, 5.5f);
    }

    /// <summary>Where Mom's foot shadow i sits in the door gap; k = 0 far off to the side, 1 at the door.</summary>
    private static Vector2 FootPos(int i, float k)
    {
        Vector2 gapMid = (DoorGapLeft + DoorGapRight) * 0.5f;
        return gapMid + new Vector2(-18f + i * 36f - (1f - k) * 40f, 0f);
    }

    private System.Collections.IEnumerator SlipperFlies(int i)
    {
        var rt = _slippers[i].rectTransform;
        Vector2 home = _slipperHome[i];
        float spin = Random.Range(540f, 900f) * (Random.value < 0.5f ? -1f : 1f);
        for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.9f;
            rt.anchoredPosition = home + new Vector2(260f * k, 420f * k - 520f * k * k);   // up and over, out of the room
            rt.localRotation = Quaternion.Euler(0f, 0f, -78f + i * 14f + spin * k);
            _slippers[i].color = Palette.HeroBlue.Alpha(1f - k * k);
            yield return null;
        }
        rt.anchoredPosition = home;
        rt.localRotation = Quaternion.Euler(0f, 0f, -78f + i * 14f);
        _flying &= ~(1 << i);
    }

    private void Update()
    {
        var mom = MomDirector.I;
        if (mom == null) return;
        float framing = PageView.I != null ? PageView.I.Framing : 1f;

        _strip.color = Palette.Hallway.Alpha(Mathf.Lerp(0.25f, 1f, mom.Strip));
        _glow.color = Palette.Hallway.Alpha(0.5f * mom.Strip);
        if (TorchUnderDoor > 0f)
        {
            // a torch beam moving about on the far side: the strip and the light it throws on the floor
            // flicker in the lens's colour, brighter and dimmer, sliding left and right with the beam
            _torchClock += Time.deltaTime;
            float flick = 0.6f + 0.3f * Mathf.PerlinNoise(_torchClock * 1.7f, 0.3f) + 0.1f * Mathf.Sin(_torchClock * 9f);
            Color torch = TorchTint;
            _strip.color = Color.Lerp(_strip.color, torch.Alpha(Mathf.Clamp01(flick)), TorchUnderDoor);
            _glow.color = torch.Alpha(Mathf.Clamp01(0.95f * flick * TorchUnderDoor));
            float sway = 26f * (Mathf.PerlinNoise(_torchClock * 0.6f, 4.1f) - 0.5f) * 2f;
            UIKit.Place(_glow.rectTransform, new Vector2(-385f + sway, -58f), new Vector2(Mathf.Lerp(152f, 300f, TorchUnderDoor), Mathf.Lerp(72f, 120f, TorchUnderDoor)));
            // (only under it: a glow round the whole frame read as a lit sign, not as a light behind a door)
        }
        for (int i = 0; i < _feet.Length; i++)
        {
            _feet[i].color = Palette.Ink.Alpha(0.85f * mom.Feet * mom.Feet);
            _feet[i].rectTransform.anchoredPosition = FootPos(i, mom.Feet);
        }
        // she opens it far enough for her eyes to show; BUSTED bangs it wide and the light floods in
        _door.Angle = mom.Bust > 0f || mom.Watching ? DoorVisit.MaxAngle * mom.Open : 34f * mom.Open;
        if (mom.Bust > _lastBust) _floodT = 0f;
        _lastBust = mom.Bust;
        if (_floodT < 1f)
        {
            _floodT = Mathf.Min(1f, _floodT + Time.unscaledDeltaTime / 0.9f);
            _flood.color = Palette.Hallway.Alpha(0.7f * (1f - _floodT) * (1f - _floodT));
        }

        // the frame glows orange to red with suspicion, with a heartbeat you can hear (lub... dub)
        float s = mom.Suspicion / 100f;
        float before = _beat;
        _beat = Mathf.Repeat(_beat + Time.deltaTime * Mathf.Lerp(1.2f, 3.2f, s) * Mathf.PI * 2f, Mathf.PI * 2f);
        float after = _beat;
        if (s > 0.08f && !BookmarkPause.IsPaused)
        {
            float loud = Mathf.Lerp(0.2f, 0.75f, s);
            if (after < before) GameAudio.Heartbeat(loud);
            else if (before < 1.1f && after >= 1.1f) GameAudio.Heartbeat(loud * 0.6f);
        }
        float pulse = s > 0.05f ? 0.75f + 0.25f * Mathf.Sin(_beat) : 0f;
        _frame.color = Color.Lerp(Palette.Hallway, Palette.HeroRed, s).Alpha(Mathf.Clamp01(s * 1.4f) * pulse);

        // the door's sounds, captioned (Settings > Captions for sound; the strip, feet and footprints show regardless)
        bool sounds = Settings.SoundCaptions;
        _caption.text = Ending ? "" : mom.Bust > 0f ? (sounds ? "ROSHAN!" : "") : mom.Watching ? "MOM IS WATCHING..." : !sounds ? "" : mom.State switch
        {
            MomState.Stirring => "HALL LIGHT ON",
            MomState.Approaching => mom.IsCat ? "PADDING..." : "FOOTSTEPS!",
            MomState.AtDoor => mom.IsCat ? "SCRATCH SCRATCH" : "THE HANDLE TURNS...",
            MomState.Opening => "THE DOOR OPENS!",
            _ => "",
        };
        _caption.color = mom.Bust > 0f ? Palette.HeroRed
            : mom.Watching || mom.State is MomState.Approaching or MomState.AtDoor or MomState.Opening ? Palette.Hallway : Palette.PaperDim;
        _caption.alpha = framing;

        _sayFor -= Time.unscaledDeltaTime;
        _sayGroup.alpha = Mathf.Clamp01(Mathf.Min(_sayFor / 0.3f, 1f));

        // a slipper flies off for every strike
        var gs = GameState.I;
        if (gs != null)
        {
            if (_slipperCount >= 0)
                for (int i = gs.Slippers; i < _slipperCount && i < _slippers.Length; i++)
                {
                    _flying |= 1 << i;
                    StartCoroutine(SlipperFlies(i));
                }
            _slipperCount = gs.Slippers;
            for (int i = 0; i < _slippers.Length; i++)
                if ((_flying & (1 << i)) == 0)
                    _slippers[i].color = (i < gs.Slippers ? Palette.HeroBlue : Palette.Ink.Alpha(0.3f)).Alpha(Ending ? 0f : framing);
        }

        // footprints (or paws) march in toward the door while someone approaches, stay while she's
        // at the door and fade as she goes
        int count = _prints.Length;
        float showing = mom.State switch
        {
            MomState.Approaching => mom.Feet * count,
            MomState.AtDoor or MomState.Opening or MomState.Leaving => count,
            _ => 0f,
        };
        float fade = mom.State == MomState.Leaving ? Mathf.Clamp01(1f - mom.StateTime / 1.5f) : 1f;
        float step = _track.rect.width / count;
        var sprite = mom.IsCat ? Paw : Slipper;
        Color ink = mom.IsCat ? Palette.Paper : Palette.Hallway;
        for (int i = 0; i < count; i++)
        {
            var print = _prints[i];
            if (print.sprite != sprite) print.sprite = sprite;
            print.color = ink.Alpha(Ending ? 0f : Mathf.Clamp01(showing - i) * fade * 0.85f);
            print.rectTransform.anchoredPosition = new Vector2(-(i + 0.5f) * step, i % 2 == 0 ? 9f : -9f);
            print.rectTransform.sizeDelta = mom.IsCat ? new Vector2(28f, 28f) : new Vector2(22f, 42f);
            print.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);     // toes toward the door
        }
    }

    // ---- drawn shapes ------------------------------------------------------------------------------

    private static Sprite _slipper, _paw, _frameGlow;

    /// <summary>A slipper's sole, toe up: a wide forefoot and a smaller heel.</summary>
    private static Sprite Slipper => _slipper != null ? _slipper : _slipper = Draw(32, 64, (u, v) =>
        Mathf.Max(Blob(u, v, 0.5f, 0.66f, 0.44f, 0.31f), Blob(u, v, 0.5f, 0.2f, 0.34f, 0.18f)));

    /// <summary>A paw print: one pad and four toes.</summary>
    private static Sprite Paw => _paw != null ? _paw : _paw = Draw(64, 64, (u, v) =>
    {
        float a = Blob(u, v, 0.5f, 0.32f, 0.27f, 0.21f);
        a = Mathf.Max(a, Blob(u, v, 0.2f, 0.62f, 0.1f, 0.12f));
        a = Mathf.Max(a, Blob(u, v, 0.38f, 0.8f, 0.1f, 0.12f));
        a = Mathf.Max(a, Blob(u, v, 0.62f, 0.8f, 0.1f, 0.12f));
        return Mathf.Max(a, Blob(u, v, 0.8f, 0.62f, 0.1f, 0.12f));
    });

    /// <summary>A soft ring of light for a rectangular frame, nine-sliced (hollow in the middle).</summary>
    private static Sprite FrameGlow
    {
        get
        {
            if (_frameGlow != null) return _frameGlow;
            const int n = 96, border = 40;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // distance to the frame line, which sits 32 px in from the sprite's edge
                    float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - n * 0.5f) - (n * 0.5f - 32f));
                    float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - n * 0.5f) - (n * 0.5f - 32f));
                    float outside = Mathf.Sqrt(dx * dx + dy * dy);
                    float inside = Mathf.Min(n * 0.5f - 32f - Mathf.Abs(x + 0.5f - n * 0.5f), n * 0.5f - 32f - Mathf.Abs(y + 0.5f - n * 0.5f));
                    float d = outside > 0f ? outside : Mathf.Max(0f, inside);
                    float a = Mathf.Exp(-d * d / (outside > 0f ? 180f : 40f));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            _frameGlow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            return _frameGlow;
        }
    }

    /// <summary>A soft-edged ellipse: 1 inside, 0 outside.</summary>
    private static float Blob(float u, float v, float cx, float cy, float rx, float ry)
    {
        float dx = (u - cx) / rx, dy = (v - cy) / ry;
        return Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * 12f);
    }

    private static Sprite Draw(int w, int h, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color32(255, 255, 255, (byte)(alpha((x + 0.5f) / w, (y + 0.5f) / h) * 255f));
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }
}
