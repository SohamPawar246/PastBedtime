using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// The player's controls, rebindable from Settings > Controls (title menu and the Bookmark). Every
/// action has two slots, a main control and a spare, and a control is a key, a mouse button or the
/// scroll wheel. Gameplay reads them through <see cref="GameInput"/>; captions and hints name them
/// through <see cref="Format"/>, so "HOLD {FOLLOW}" always says what the player has set. DEFAULTS
/// puts the jam layout back: the left hand on W A S D is Max, the right hand on the mouse is the torch.
/// Fixed, whatever the bindings: Esc and P (Bookmark), 1 and 2 (pick a lens).
/// </summary>
public static class Bindings
{
    public enum Act { Left, Right, Up, Down, Jump, Punch, Kick, Dodge, Splash, Torch, Follow, Crank, Lens }
    public enum Kind { None, Key, Mouse, Wheel }

    public readonly struct Control : IEquatable<Control>
    {
        public readonly Kind kind;
        public readonly int code;                        // a Key, or a mouse button (0 left, 1 right, 2 middle, 3 forward, 4 back)
        public Control(Kind kind, int code = 0) { this.kind = kind; this.code = code; }
        public bool IsNone => kind == Kind.None;
        public bool Equals(Control o) => kind == o.kind && (kind == Kind.Wheel || kind == Kind.None || code == o.code);
        public override bool Equals(object o) => o is Control c && Equals(c);
        public override int GetHashCode() => ((int)kind * 397) ^ (kind == Kind.Key || kind == Kind.Mouse ? code : 0);

        public override string ToString() => kind switch
        {
            Kind.Key => "k:" + (Key)code,
            Kind.Mouse => "m:" + code,
            Kind.Wheel => "w",
            _ => "",
        };

        public static Control Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return default;
            if (s == "w") return new Control(Kind.Wheel);
            if (s.StartsWith("m:") && int.TryParse(s.Substring(2), out int b) && b >= 0 && b <= 4) return new Control(Kind.Mouse, b);
            if (s.StartsWith("k:") && Enum.TryParse(s.Substring(2), out Key k) && k != Key.None && Enum.IsDefined(typeof(Key), k))
                return new Control(Kind.Key, (int)k);
            return default;
        }
    }

    public static readonly int Count = Enum.GetValues(typeof(Act)).Length;

    /// <summary>What each action is called on the Controls page.</summary>
    public static readonly string[] Labels =
    {
        "Run left", "Run right", "Up (+ kick: launcher)", "Down (+ kick: slam)", "Jump", "Punch", "Kick", "Dodge",
        "Splash page", "Torch on / off", "Beam follows Max", "Twist the crank", "Ghost lens on / off",
    };

    private static readonly string[][] Defaults =
    {
        new[] { "k:A", "k:LeftArrow" },            // run left
        new[] { "k:D", "k:RightArrow" },           // run right
        new[] { "k:W", "k:UpArrow" },              // up
        new[] { "k:S", "k:DownArrow" },            // down
        new[] { "k:Space", "" },                   // jump
        new[] { "k:E", "" },                       // punch
        new[] { "k:Q", "" },                       // kick
        new[] { "k:LeftShift", "k:RightShift" },   // dodge
        new[] { "k:F", "" },                       // splash page
        new[] { "m:1", "" },                       // torch: right click
        new[] { "m:0", "" },                       // follow: hold left
        new[] { "w", "k:R" },                      // crank: scroll, or tap R
        new[] { "m:2", "k:Tab" },                  // lens: middle click, or Tab
    };

    /// <summary>Raised whenever a binding changes (hints and the How to Read page re-read their names).</summary>
    public static event Action Changed;

    private static readonly Control[,] _bound = new Control[Count, 2];

    static Bindings() => Load();

    public static Control Get(Act a, int slot) => _bound[(int)a, slot];

    /// <summary>Holds while a held action (run, follow, up, down, jump height) is down. Never the wheel.</summary>
    public static bool Held(Act a)
    {
        for (int s = 0; s < 2; s++) if (IsHeld(_bound[(int)a, s])) return true;
        return false;
    }

    /// <summary>Pressed this frame (the wheel counts as a press on any frame it turns).</summary>
    public static bool Pressed(Act a) => Pulses(a) != 0;

    /// <summary>Presses this frame, counting each wheel notch; `signedWheel` makes scrolling down count back.</summary>
    public static int Pulses(Act a, bool signedWheel = false)
    {
        int n = 0;
        for (int s = 0; s < 2; s++)
        {
            var c = _bound[(int)a, s];
            switch (c.kind)
            {
                case Kind.Key:
                case Kind.Mouse:
                    if (Button(c) is ButtonControl b && b.wasPressedThisFrame) n++;
                    break;
                case Kind.Wheel:
                    int w = ReadWheel();
                    if (w != 0) n += signedWheel ? w : Mathf.Abs(w);
                    break;
            }
        }
        return n;
    }

    // ---- the wheel -----------------------------------------------------------------------------------------
    // A mouse wheel turns notch by notch; a trackpad (or a free-spinning wheel) scrolls a little every frame and
    // coasts on after the fingers lift, and browsers report a notch as anything from 3 to 120. So the wheel is
    // read by time, not size: a frame's scroll is one notch (two when a desktop wheel reports a double notch at
    // once), at most one every WheelGap seconds, and scrolling that runs on without a StreamGap pause is one
    // "turn" of the wheel (the torch lets a turn that starts at full overwind just one notch). Scrolling that carries
    // on from the frame before is always the same turn, however long that frame took (a hitch isn't a pause).
    public const float WheelGap = 0.06f, StreamGap = 0.15f;
    private static int _wheelFrame = -1, _wheelNotches, _scrollFrame = -10;
    private static float _lastScroll = -1f, _lastNotch = -1f;
    private static bool _turnStarted;

    /// <summary>The wheel started turning this frame, after a pause: a new twist, not more of the last one.</summary>
    public static bool WheelTurnStarted { get { ReadWheel(); return _turnStarted; } }

    /// <summary>The wheel counted a notch this frame.</summary>
    public static bool WheelTurned { get { ReadWheel(); return _wheelNotches != 0; } }

    /// <summary>Is the wheel one of this action's controls?</summary>
    public static bool UsesWheel(Act a) => _bound[(int)a, 0].kind == Kind.Wheel || _bound[(int)a, 1].kind == Kind.Wheel;

    /// <summary>Is this control one of the action's (main or spare)?</summary>
    public static bool Uses(Act a, Control c) => _bound[(int)a, 0].Equals(c) || _bound[(int)a, 1].Equals(c);

    /// <summary>This frame's wheel notches (signed: scrolling down is negative), read once a frame however many ask.</summary>
    private static int ReadWheel()
    {
        if (_wheelFrame == Time.frameCount) return _wheelNotches;
        _wheelFrame = Time.frameCount;
        _wheelNotches = 0;
        _turnStarted = false;
        var ms = Mouse.current;
        float y = ms != null ? ms.scroll.ReadValue().y : 0f;
        if (Mathf.Abs(y) <= 0.01f) return 0;
        float now = Time.unscaledTime;
        _turnStarted = now - _lastScroll > StreamGap && Time.frameCount - _scrollFrame > 1;
        _lastScroll = now;
        _scrollFrame = Time.frameCount;
        if (!_turnStarted && now - _lastNotch < WheelGap) return 0;
        _lastNotch = now;
        int notches = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(y) / 120f), 1, 2);
        _wheelNotches = y < 0f ? -notches : notches;
        return _wheelNotches;
    }

    /// <summary>Is this control bound to anything? (Spare mouse buttons keep their old jobs only while free.)</summary>
    public static bool IsBound(Control c)
    {
        for (int a = 0; a < Count; a++)
            for (int s = 0; s < 2; s++)
                if (_bound[a, s].Equals(c)) return true;
        return false;
    }

    private static bool IsHeld(Control c) => c.kind != Kind.Wheel && Button(c) is ButtonControl b && b.isPressed;

    private static ButtonControl Button(Control c)
    {
        if (c.kind == Kind.Key)
        {
            var kb = Keyboard.current;
            if (kb == null) return null;
            try { return kb[(Key)c.code]; }
            catch (ArgumentOutOfRangeException) { return null; }   // a key this keyboard doesn't have
        }
        if (c.kind == Kind.Mouse)
        {
            var ms = Mouse.current;
            if (ms == null) return null;
            return c.code switch { 0 => ms.leftButton, 1 => ms.rightButton, 2 => ms.middleButton, 3 => ms.forwardButton, _ => ms.backButton };
        }
        return null;
    }

    // ---- changing them --------------------------------------------------------------------------------

    /// <summary>Binds a control to an action's slot. If another slot already has it, the two swap, so a
    /// control never does two jobs and nothing goes missing.</summary>
    public static void Set(Act a, int slot, Control c)
    {
        var old = _bound[(int)a, slot];
        if (!c.IsNone)
            for (int i = 0; i < Count; i++)
                for (int s = 0; s < 2; s++)
                    if ((i != (int)a || s != slot) && _bound[i, s].Equals(c)) _bound[i, s] = old;
        _bound[(int)a, slot] = c;
        Save();
    }

    /// <summary>Every slot as text (tests put the player's own bindings back afterwards).</summary>
    public static string[] Snapshot()
    {
        var all = new string[Count * 2];
        for (int a = 0; a < Count; a++)
            for (int s = 0; s < 2; s++)
                all[a * 2 + s] = _bound[a, s].ToString();
        return all;
    }

    public static void Restore(string[] all)
    {
        if (all == null || all.Length != Count * 2) return;
        for (int a = 0; a < Count; a++)
            for (int s = 0; s < 2; s++)
                _bound[a, s] = Control.Parse(all[a * 2 + s]);
        Save();
    }

    public static void ResetDefaults()
    {
        for (int a = 0; a < Count; a++)
            for (int s = 0; s < 2; s++)
                _bound[a, s] = Control.Parse(Defaults[a][s]);
        Save();
    }

    /// <summary>Can this action take the wheel? Only the ones that count turns: the crank and the lens.</summary>
    public static bool TakesWheel(Act a) => a == Act.Crank || a == Act.Lens;

    /// <summary>Keys that keep their own jobs: Esc and P (Bookmark), Backspace and Delete (clear a slot
    /// on the Controls page), 1 and 2 (pick a lens).</summary>
    public static bool Reserved(Key k) =>
        k is Key.Escape or Key.P or Key.Backspace or Key.Delete or Key.Digit1 or Key.Digit2 or Key.None ||
        (int)k == 111;                                   // the IME's dummy "key"

    /// <summary>The control the player is pressing this frame (for the Controls page), if any.</summary>
    public static bool Capture(Act forAct, out Control c)
    {
        c = default;
        var kb = Keyboard.current;
        if (kb != null)
            foreach (var key in kb.allKeys)
                if (key != null && key.wasPressedThisFrame && !Reserved(key.keyCode)) { c = new Control(Kind.Key, (int)key.keyCode); return true; }
        var ms = Mouse.current;
        if (ms == null) return false;
        var buttons = new[] { ms.leftButton, ms.rightButton, ms.middleButton, ms.forwardButton, ms.backButton };
        for (int i = 0; i < buttons.Length; i++)
            if (buttons[i].wasPressedThisFrame) { c = new Control(Kind.Mouse, i); return true; }
        if (TakesWheel(forAct) && Mathf.Abs(ms.scroll.ReadValue().y) > 0.01f) { c = new Control(Kind.Wheel); return true; }
        return false;
    }

    // ---- names --------------------------------------------------------------------------------------------

    private static bool IsHoldAct(Act a) => a is Act.Left or Act.Right or Act.Up or Act.Down or Act.Follow;

    /// <summary>What a control is called on the page: "SPACE", "E", "RIGHT CLICK", "HOLD"-style actions get
    /// "LEFT MOUSE" instead of "LEFT CLICK", the wheel is "SCROLL".</summary>
    public static string Name(Control c, bool hold = false)
    {
        switch (c.kind)
        {
            case Kind.Wheel: return "SCROLL";
            case Kind.Mouse:
                string b = c.code switch { 0 => "LEFT", 1 => "RIGHT", 2 => "MIDDLE", 3 => "MOUSE 5", _ => "MOUSE 4" };
                return c.code >= 3 ? b : b + (hold ? " MOUSE" : " CLICK");
            case Kind.Key:
                var k = (Key)c.code;
                switch (k)
                {
                    case Key.Space: return "SPACE";
                    case Key.LeftShift: return "SHIFT";
                    case Key.RightShift: return "RIGHT SHIFT";
                    case Key.LeftCtrl: return "CTRL";
                    case Key.RightCtrl: return "RIGHT CTRL";
                    case Key.LeftAlt: return "ALT";
                    case Key.RightAlt: return "ALT GR";
                    case Key.LeftArrow: return "LEFT ARROW";
                    case Key.RightArrow: return "RIGHT ARROW";
                    case Key.UpArrow: return "UP ARROW";
                    case Key.DownArrow: return "DOWN ARROW";
                    case Key.Enter: return "ENTER";
                    case Key.NumpadEnter: return "NUM ENTER";
                    case Key.Tab: return "TAB";
                    case Key.CapsLock: return "CAPS LOCK";
                }
                string shown = Button(c)?.displayName;               // the player's own layout (AZERTY's A on Q)
                return (string.IsNullOrWhiteSpace(shown) ? k.ToString() : shown).ToUpperInvariant();
        }
        return "(UNSET)";
    }

    /// <summary>An action's main control by name (its spare if the main slot is empty).</summary>
    public static string Name(Act a)
    {
        var c = _bound[(int)a, 0];
        if (c.IsNone) c = _bound[(int)a, 1];
        return Name(c, IsHoldAct(a));
    }

    /// <summary>One slot by name, for the Controls page ("--" when it's empty).</summary>
    public static string SlotName(Act a, int slot)
    {
        var c = _bound[(int)a, slot];
        return c.IsNone ? "--" : Name(c, IsHoldAct(a));
    }

    /// <summary>Both of an action's controls: "A / LEFT ARROW".</summary>
    public static string Names(Act a)
    {
        var main = _bound[(int)a, 0];
        var spare = _bound[(int)a, 1];
        if (main.IsNone || spare.IsNone) return Name(a);
        return Name(main, IsHoldAct(a)) + " / " + Name(spare, IsHoldAct(a));
    }

    /// <summary>Fills in {JUMP}, {TORCH}, {FOLLOW}... with whatever the player has bound (remembered until
    /// the bindings change, so hints can call it every frame).</summary>
    public static string Format(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
        if (_formatted.TryGetValue(text, out var done)) return done;
        string s = text;
        for (int a = 0; a < Count; a++)
        {
            string token = "{" + ((Act)a).ToString().ToUpperInvariant() + "}";
            if (s.Contains(token)) s = s.Replace(token, Name((Act)a));
        }
        if (_formatted.Count > 256) _formatted.Clear();
        _formatted[text] = s;
        return s;
    }

    private static readonly System.Collections.Generic.Dictionary<string, string> _formatted = new();

    // ---- storage (through Settings: nothing else touches PlayerPrefs) -------------------------------------

    private static string StoreKey(int a, int s) => $"bind.{(Act)a}.{s}";

    private static void Load()
    {
        for (int a = 0; a < Count; a++)
            for (int s = 0; s < 2; s++)
                _bound[a, s] = Control.Parse(Settings.GetText(StoreKey(a, s), Defaults[a][s]));
    }

    private static void Save()
    {
        for (int a = 0; a < Count; a++)
            for (int s = 0; s < 2; s++)
                Settings.SetText(StoreKey(a, s), _bound[a, s].ToString());
        Settings.Save();
        _formatted.Clear();
        Changed?.Invoke();
    }
}
