using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Finger = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// The on-screen controls on a phone (Art/UI/TouchControls; they switch themselves off anywhere else). Each finger
/// does what it first touched:
///   the left of the screen   a stick that appears under the thumb: run; push it up or down for UP and DOWN
///                            (with KICK: the launcher and the slam)
///   a button                 JUMP (hold: higher), POW (hold: keeps punching), KICK (swipe up on it: the launcher,
///                            down: the slam), DODGE; SPLASH! once the meter is full (page 5 on); LENS once the Ghost
///                            lens is found. A thumb can roll from one button onto the next. KICK shows from page 2,
///                            where it's taught.
///   the torch in the hand    tap it: on / off; circle a finger round the crank (or rub it back and forth) to wind
///                            it; tap the lens for the ghost lens
///   the bookmark             the Bookmark (pause): a ribbon hanging from the comic's spine
///   anywhere else            the beam goes where the finger is; let go and it keeps that place beside Max for a
///                            few seconds (lean it ahead, keep something dark beside him), then glides back onto
///                            him; a double tap brings it back at once
/// It answers through <see cref="GameInput.Touch"/>, so the game reads it like the keyboard and mouse.
/// </summary>
public class TouchControls : MonoBehaviour
{
    private enum Role { None, Stick, Jump, Punch, Kick, Dodge, Splash, Lens, Pause, Torch, Aim }

    [Header("Parts")]
    [Tooltip("Kept inside the screen's safe area (clear of notches and the gesture bar).")]
    [SerializeField] private RectTransform _safeArea;
    [SerializeField] private CanvasGroup _group;
    [Tooltip("The stick's ring and knob, shown where the left thumb lands.")]
    [SerializeField] private RectTransform _stickBase, _stickKnob;
    [SerializeField] private RectTransform _jump, _punch, _kick, _dodge, _splash, _lens, _pause;

    [Header("Layout (millimetres on the screen)")]
    [Tooltip("Each button's middle, from the safe area's bottom-right corner (x to the left, y up), and its size: an " +
             "arc for the right thumb above the torch hand, which sits in the corner.")]
    [SerializeField] private Vector3 _jumpMm = new(13f, 34f, 14f);
    [SerializeField] private Vector3 _punchMm = new(31f, 30f, 12f), _kickMm = new(26f, 47f, 12f);
    [SerializeField] private Vector3 _dodgeMm = new(10f, 51f, 10f), _splashMm = new(46f, 44f, 11f);
    [SerializeField] private Vector3 _lensMm = new(42f, 17f, 10f);
    private float _restAlpha = 0.6f;                              // how see-through a button is till pressed (Settings > TOUCH)
    [Tooltip("The bookmark ribbon's width (it hangs from the top of the comic's spine).")]
    [SerializeField] private float _pauseMm = 11f;
    [Tooltip("A button takes a touch this much beyond its drawn circle (between two, the nearer).")]
    [SerializeField] private float _hitSlack = 1.25f;

    [Header("Feel")]
    [Tooltip("How much of the screen's width, from the left, is the stick's.")]
    [Range(0.2f, 0.6f)] [SerializeField] private float _stickZone = 0.42f;
    [Tooltip("How far the knob travels (mm): full speed at the edge.")]
    [SerializeField] private float _stickRadiusMm = 8f;
    [Tooltip("Share of the stick's travel before Max moves, and before it means UP or DOWN.")]
    [Range(0f, 0.9f)] [SerializeField] private float _deadZone = 0.3f, _upDown = 0.6f;
    [Tooltip("A turn of this many degrees round the crank is one twist (a scroll notch).")]
    [SerializeField] private float _degreesPerTwist = 90f;
    [Tooltip("A touch on the torch shorter and stiller than this is a tap (seconds, mm).")]
    [SerializeField] private float _tapSeconds = 0.3f, _tapSlopMm = 2.5f;
    [Tooltip("How near the lens a tap on the torch picks the lens (hand-render pixels).")]
    [SerializeField] private float _lensRadius = 55f;
    [Tooltip("KICK waits this long for a swipe (up: the launcher, down: the slam) before it's a plain kick (s).")]
    [SerializeField] private float _kickWindow = 0.09f;
    [Tooltip("Holding POW punches again this often (s).")]
    [SerializeField] private float _punchRepeat = 0.22f;
    [Tooltip("Let go of the beam and it keeps its place beside Max this long before gliding back onto him (s).")]
    [SerializeField] private float _parkSeconds = 3f;

    private class Hold
    {
        public Role role;
        public Vector2 start, last;
        public float since, crankAngle, crankTurned, repeatAt;
        public bool moved, cranking, kickPending;
    }

    private readonly GameInput.Pad _pad = new();
    private readonly Dictionary<int, Hold> _holds = new();
    private Rect _safe;
    private (RectTransform rt, Role role)[] _buttons;
    private readonly Dictionary<RectTransform, CanvasGroup> _fade = new();
    private readonly Dictionary<RectTransform, (Image face, Color rest)> _faces = new();
    private float _tuck;
    private Vector2 _pauseHome;
    private Canvas _canvas;
    // the beam's place beside Max after a finger lets go of it, and for how much longer
    private Vector2 _offset;
    private float _parkFor, _lastTapAt = -1f;

    private void Awake()
    {
        // in the editor they stay, hidden, for Unity Remote (which can connect after the scene starts)
        if (!GameInput.OnTouchDevice && !Application.isEditor) gameObject.SetActive(false);
        var buttons = new List<(RectTransform, Role)>
        {
            (_pause, Role.Pause), (_jump, Role.Jump), (_punch, Role.Punch), (_kick, Role.Kick), (_dodge, Role.Dodge), (_splash, Role.Splash),
        };
        if (_lens != null) buttons.Add((_lens, Role.Lens));
        _buttons = buttons.ToArray();
        foreach (var (rt, role) in _buttons)
        {
            if (role == Role.Pause) continue;
            _fade[rt] = rt.TryGetComponent<CanvasGroup>(out var g) ? g : rt.gameObject.AddComponent<CanvasGroup>();
            var face = rt.Find("Face")?.GetComponent<Image>();
            if (face != null) _faces[rt] = (face, face.color);             // its own colour (SPLASH! yellow, LENS purple)
        }
        _pauseHome = _pause.anchoredPosition;
        _canvas = GetComponent<Canvas>();
    }

    /// <summary>Millimetres on the screen in this canvas's units.</summary>
    private float Mm => Form.Mm(_canvas, 1f);

    private void OnEnable() => EnhancedTouchSupport.Enable();

    private void OnDisable() => Release();

    /// <summary>The game goes back to the keyboard and mouse alone.</summary>
    private void Release()
    {
        if (GameInput.Touch == _pad) GameInput.Touch = null;
        GameInput.TouchCranking = GameInput.TouchCrankStarted = false;
    }

    private float Scale => _safeArea.lossyScale.x;               // screen pixels per canvas unit

    private void Update()
    {
        if (!GameInput.TouchMode)
        {
            Release();
            _holds.Clear();
            _group.alpha = 0f;
            return;
        }
        GameInput.Touch = _pad;
        FitSafeArea();
        Layout();
        _pad.moveX = 0f;
        _pad.up = _pad.down = _pad.jumpHeld = false;
        _pad.pointer = null;
        GameInput.TouchCranking = GameInput.TouchCrankStarted = false;

        // under the Bookmark, the Nightstand or a card the menus have the screen
        var hero = HeroController.I;
        bool live = !BookmarkPause.IsPaused && !Nightstand.Open && (App.I == null || !App.I.Flow.IsLoading);
        _group.alpha = Mathf.MoveTowards(_group.alpha, live ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        if (!live)
        {
            _holds.Clear();
            _stickBase.gameObject.SetActive(false);
            _pad.followHeld = false;
            return;
        }

        // each button shows once its move is taught: KICK on page 2 (and after, for good), SPLASH! when the meter's full
        // from page 5, LENS once the Ghost lens is found
        var gs = GameState.I;
        int page = gs != null ? gs.Page : 1;
        _kick.gameObject.SetActive(page >= 2 || Settings.HighestPage >= 2);
        _splash.gameObject.SetActive(gs != null && gs.Splash >= 100f && page >= 5);
        if (_lens != null)
            _lens.gameObject.SetActive(TorchController.I != null && TorchController.I.Lenses.Unlocked[(int)global::Lens.Ghost]);

        bool aiming = false;
        foreach (var t in Finger.activeTouches)
        {
            int id = t.touchId;
            Vector2 pos = t.screenPosition;
            if (!_holds.TryGetValue(id, out var h))
            {
                h = new Hold { role = RoleAt(pos), start = pos, last = pos, since = 0f };
                _holds[id] = h;
                Began(h);
            }
            h.since += Time.unscaledDeltaTime;
            if ((pos - h.start).magnitude / Scale > _tapSlopMm * Mm) h.moved = true;
            bool ended = t.phase is UnityEngine.InputSystem.TouchPhase.Ended or UnityEngine.InputSystem.TouchPhase.Canceled;
            Held(h, pos);
            if (h.role == Role.Aim && !ended) aiming = true;
            h.last = pos;
            if (ended)
            {
                Ended(h, pos);
                _holds.Remove(id);
            }
        }
        // fingers lifted between frames (no Ended seen)
        if (_holds.Count > 0 && _holds.Count > Finger.activeTouches.Count) Prune();

        // let go of the beam and it keeps its place beside Max for a while, then glides back onto him
        if (aiming) _parkFor = _parkSeconds;
        else if (_parkFor > 0f && hero != null && PageView.I != null)
        {
            _parkFor -= Time.deltaTime;
            _pad.pointer = PageView.I.LaneToScreen((Vector2)hero.transform.position + _offset);
        }
        // tilting the phone (Settings > TOUCH) leans the following beam ahead of him or above him
        Vector2 tilt = Tilt();
        bool tilted = !aiming && _parkFor <= 0f && hero != null && PageView.I != null && tilt.sqrMagnitude > 0.25f;
        if (tilted) _pad.pointer = PageView.I.LaneToScreen((Vector2)hero.transform.position + Vector2.up + tilt);
        _pad.followHeld = !aiming && _parkFor <= 0f && !tilted && hero != null;

        if (!Holding(Role.Stick)) _stickBase.gameObject.SetActive(false);
        Press(_jump, Holding(Role.Jump));
        Press(_punch, Holding(Role.Punch));
        Press(_kick, Holding(Role.Kick));
        Press(_dodge, Holding(Role.Dodge));
        Press(_splash, Holding(Role.Splash));
        if (_lens != null) Press(_lens, Holding(Role.Lens));
    }

    private Role RoleAt(Vector2 screen)
    {
        // a button takes a touch a little beyond its drawn circle; in the gap between two, the nearer one has it
        Role best = Role.None;
        float nearest = _hitSlack;
        foreach (var (rt, role) in _buttons)
        {
            float d = Reach(rt, screen);
            if (d < nearest) { nearest = d; best = role; }
        }
        if (best != Role.None) return best;
        if (GameTorch.I != null && GameTorch.I.OnHand(screen)) return Role.Torch;
        var safe = UnityEngine.Device.Screen.safeArea;
        bool stick = Settings.LeftHanded ? screen.x > safe.xMax - safe.width * _stickZone : screen.x < safe.xMin + safe.width * _stickZone;
        return stick ? Role.Stick : Role.Aim;
    }

    /// <summary>How far a screen point is from a round button's middle, in its radii (1 = on its edge; a switched-off
    /// button is never near).</summary>
    private static float Reach(RectTransform rt, Vector2 screen)
    {
        if (!rt.gameObject.activeInHierarchy) return float.MaxValue;
        Vector2 centre = RectTransformUtility.WorldToScreenPoint(null, rt.position);
        float radius = rt.rect.width * 0.5f * rt.lossyScale.x;
        return (screen - centre).magnitude / Mathf.Max(1f, radius);
    }

    private bool Hit(RectTransform rt, Vector2 screen) => Reach(rt, screen) <= _hitSlack;

    private void Began(Hold h)
    {
        switch (h.role)
        {
            case Role.Jump: _pad.jump = true; break;
            case Role.Punch:
                _pad.punch = true;
                h.repeatAt = h.since + _punchRepeat;
                break;
            case Role.Kick: h.kickPending = true; break;                 // a swipe may yet make it the launcher or the slam
            case Role.Dodge: _pad.dodge = true; break;
            case Role.Splash: _pad.splash = true; break;
            case Role.Lens: _pad.lensStep++; break;
            case Role.Stick:
                _stickBase.gameObject.SetActive(true);
                _stickBase.position = h.start;
                _stickKnob.localPosition = Vector3.zero;
                break;
        }
    }

    private Vector2 _tiltZero;
    private bool _tiltOn;

    /// <summary>How far the phone is tipped from the way it's being held (lane units the beam leans by, up to 5
    /// sideways and 3 up or down); nothing with Tilt to aim off. "The way it's held" drifts slowly after the hands,
    /// so lying back in bed doesn't leave the beam leaning for good.</summary>
    private Vector2 Tilt()
    {
        var acc = UnityEngine.InputSystem.Accelerometer.current;
        if (!Settings.TiltAim || acc == null)
        {
            _tiltOn = false;
            return Vector2.zero;
        }
        if (!acc.enabled) UnityEngine.InputSystem.InputSystem.EnableDevice(acc);
        Vector3 a = acc.acceleration.ReadValue();
        // the accelerometer reads in the phone's portrait axes: turn them to the landscape screen's
        Vector2 s = UnityEngine.Device.Screen.orientation == ScreenOrientation.LandscapeRight ? new Vector2(a.y, -a.x) : new Vector2(-a.y, a.x);
        if (!_tiltOn)
        {
            _tiltOn = true;
            _tiltZero = s;
        }
        _tiltZero = Vector2.Lerp(_tiltZero, s, Time.unscaledDeltaTime * 0.15f);
        Vector2 d = s - _tiltZero;
        return new Vector2(Mathf.Clamp(d.x * 12f, -5f, 5f), Mathf.Clamp(d.y * 8f, -3f, 3f));
    }

    /// <summary>KICK, with the way the finger swiped on it (1 up: the launcher, -1 down: the slam, 0 a plain kick).</summary>
    private void Kick(Hold h, int swipe)
    {
        h.kickPending = false;
        _pad.kick = true;
        if (swipe > 0) _pad.up = true;
        if (swipe < 0) _pad.down = true;
    }

    /// <summary>The button under a finger, inside its drawn circle (for rolling from one onto the next).</summary>
    private Role ButtonUnder(Vector2 screen)
    {
        foreach (var (rt, role) in _buttons)
            if ((role is Role.Jump or Role.Punch or Role.Kick or Role.Dodge) && Reach(rt, screen) <= 1f) return role;
        return Role.None;
    }

    private void Held(Hold h, Vector2 pos)
    {
        if (h.kickPending)
        {
            Vector2 d = (pos - h.start) / Scale;
            if (Mathf.Abs(d.y) > _tapSlopMm * Mm && Mathf.Abs(d.y) > Mathf.Abs(d.x)) Kick(h, d.y > 0f ? 1 : -1);
            else if (h.since >= _kickWindow) Kick(h, 0);
            return;
        }
        if (h.role is Role.Jump or Role.Punch or Role.Dodge or Role.Kick)
        {
            Role under = ButtonUnder(pos);
            if (under != Role.None && under != h.role)
            {
                h.role = under;
                h.start = pos;
                Began(h);
                return;
            }
        }
        switch (h.role)
        {
            case Role.Jump: _pad.jumpHeld = true; break;
            case Role.Punch:
                if (h.since >= h.repeatAt)
                {
                    _pad.punch = true;
                    h.repeatAt += _punchRepeat;
                }
                break;
            case Role.Aim:
                _pad.pointer = pos;
                if (HeroController.I != null && PageView.ScreenToLane(pos, out var lane))
                    _offset = lane - (Vector2)HeroController.I.transform.position;
                break;
            case Role.Stick:
            {
                float radius = _stickRadiusMm * Mm;
                Vector2 d = (pos - h.start) / Scale / radius;
                if (d.magnitude > 1f) d.Normalize();
                _stickKnob.localPosition = d * radius;
                if (Mathf.Abs(d.x) > _deadZone) _pad.moveX = Mathf.Sign(d.x);      // full speed, like the keys
                if (d.y > _upDown) _pad.up = true;
                if (d.y < -_upDown) _pad.down = true;
                break;
            }
            case Role.Torch:
            {
                if (!h.moved || GameTorch.I == null) break;
                // round the crank: every quarter turn (either way) is one twist. The crank can sit at (or past) the
                // screen's edge, so the turn is measured round a point kept a thumb's width inside it
                Vector2 c = GameTorch.I.CrankOnScreen;
                float inset = Form.MmToPixels(8f);
                c = new Vector2(Mathf.Clamp(c.x, inset, UnityEngine.Device.Screen.width - inset),
                                Mathf.Clamp(c.y, inset, UnityEngine.Device.Screen.height - inset));
                float angle = Mathf.Atan2(pos.y - c.y, pos.x - c.x) * Mathf.Rad2Deg;
                if (!h.cranking)
                {
                    h.cranking = true;
                    h.crankAngle = angle;
                    GameInput.TouchCrankStarted = true;
                    break;
                }
                h.crankTurned += Mathf.Abs(Mathf.DeltaAngle(h.crankAngle, angle));
                h.crankAngle = angle;
                while (h.crankTurned >= _degreesPerTwist)
                {
                    h.crankTurned -= _degreesPerTwist;
                    _pad.twists++;
                    GameInput.TouchCranking = true;
                }
                break;
            }
        }
    }

    private void Ended(Hold h, Vector2 pos)
    {
        if (h.kickPending) Kick(h, 0);
        if (h.role == Role.Pause && Hit(_pause, pos)) FindFirstObjectByType<BookmarkPause>()?.Pause();
        // a double tap on the page brings the beam straight back onto Max
        if (h.role == Role.Aim && !h.moved && h.since <= _tapSeconds)
        {
            if (Time.unscaledTime - _lastTapAt < 0.35f) _parkFor = 0f;
            _lastTapAt = Time.unscaledTime;
        }
        if (h.role == Role.Torch && !h.moved && h.since <= _tapSeconds && GameTorch.I != null)
        {
            if (GameTorch.I.OnLens(pos, _lensRadius)) _pad.lensStep++;
            else _pad.torchToggle = true;
        }
    }

    private void Prune()
    {
        var live = new HashSet<int>();
        foreach (var t in Finger.activeTouches) live.Add(t.touchId);
        var gone = new List<int>();
        foreach (var id in _holds.Keys) if (!live.Contains(id)) gone.Add(id);
        foreach (var id in gone) _holds.Remove(id);
    }

    private bool Holding(Role role)
    {
        foreach (var h in _holds.Values) if (h.role == role) return true;
        return false;
    }

    /// <summary>A held button sinks a little, turns process yellow and stops being see-through.</summary>
    private void Press(RectTransform button, bool down)
    {
        button.localScale = Vector3.one * (down ? 0.92f : 1f);
        if (_fade.TryGetValue(button, out var g)) g.alpha = down ? 1f : _restAlpha;
        if (_faces.TryGetValue(button, out var f)) f.face.color = down ? Palette.Yellow.Alpha(f.rest.a) : f.rest;
    }

    /// <summary>Everything at its size in millimetres: the buttons in their arc from the bottom-right corner, the stick,
    /// and the bookmark, which hangs from the top of the comic's spine in Flat page view (clear of the HUD at the
    /// page's corners) and from the screen's corner otherwise.</summary>
    private void Layout()
    {
        // Settings > TOUCH: small, normal or big buttons (their arc grows with them), and a left-handed mirror
        float mm = Mm * Settings.ButtonSize switch { 0 => 0.85f, 2 => 1.2f, _ => 1f };
        bool left = Settings.LeftHanded;
        _restAlpha = Mathf.Lerp(0.25f, 1f, Settings.ButtonOpacity);
        Place(_jump, _jumpMm, mm, left);
        Place(_punch, _punchMm, mm, left);
        Place(_kick, _kickMm, mm, left);
        Place(_dodge, _dodgeMm, mm, left);
        Place(_splash, _splashMm, mm, left);
        if (_lens != null) Place(_lens, _lensMm, mm, left);
        mm = Mm;
        _stickBase.sizeDelta = Vector2.one * _stickRadiusMm * 2.3f * mm;
        _stickKnob.sizeDelta = Vector2.one * _stickRadiusMm * mm;

        _pause.localScale = Vector3.one * (_pauseMm * mm / _pause.rect.width);
        if (PageView.I != null && PageView.Flat)
        {
            // while a lesson or a warning is printed across the top of the page, the ribbon tucks up, its tip showing
            bool tucked = GameHUD.I != null && GameHUD.I.TopCaptionUp;
            _tuck = Mathf.MoveTowards(_tuck, tucked ? 0.62f : 0f, Time.unscaledDeltaTime * 3f);
            Vector2 top = PageView.I.SpineTopOnScreen;
            float height = _pause.rect.height * _pause.lossyScale.y;
            _pause.position = new Vector3(top.x, Mathf.Min(top.y, UnityEngine.Device.Screen.safeArea.yMax) - height * (0.5f - _tuck), 0f);
        }
        else _pause.anchoredPosition = _pauseHome;
    }

    private static void Place(RectTransform button, Vector3 mmAtAndSize, float mm, bool leftHanded)
    {
        button.anchorMin = button.anchorMax = new Vector2(leftHanded ? 0f : 1f, 0f);
        button.anchoredPosition = new Vector2((leftHanded ? 1f : -1f) * mmAtAndSize.x * mm, mmAtAndSize.y * mm);
        button.sizeDelta = Vector2.one * mmAtAndSize.z * mm;
        var label = button.GetComponentInChildren<TMPro.TMP_Text>();
        if (label != null) label.fontSize = mmAtAndSize.z * mm * 0.24f;
    }

    /// <summary>The controls stay inside the safe area (notches, rounded corners, the gesture bar).</summary>
    private void FitSafeArea()
    {
        var safe = UnityEngine.Device.Screen.safeArea;
        if (safe == _safe) return;
        _safe = safe;
        float w = Mathf.Max(1, UnityEngine.Device.Screen.width), h = Mathf.Max(1, UnityEngine.Device.Screen.height);
        _safeArea.anchorMin = new Vector2(safe.xMin / w, safe.yMin / h);
        _safeArea.anchorMax = new Vector2(safe.xMax / w, safe.yMax / h);
        _safeArea.offsetMin = _safeArea.offsetMax = Vector2.zero;
    }
}
