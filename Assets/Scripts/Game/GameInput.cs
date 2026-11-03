using UnityEngine;
using UnityEngine.InputSystem;
using Act = Bindings.Act;

/// <summary>
/// Gameplay controls (GDD section 3), polled from the keyboard and mouse through the player's
/// <see cref="Bindings"/> (rebindable under Settings > Controls). By default the left hand plays Max
/// on the keyboard, all within reach of W A S D; the right hand is the reader's, on the mouse: aim,
/// hold to follow Max, click the torch on and off, scroll the crank, middle-click the lens wheel.
/// On a phone the same questions are answered by the on-screen controls (<see cref="Touch"/>).
///
/// Tests (and any scripted demo) can drive the game through <see cref="Virtual"/>: while
/// it is set, every query reads it instead of the devices.
/// </summary>
public static class GameInput
{
    /// <summary>A scriptable stand-in for the devices. "Pressed" flags are consumed when read.</summary>
    public class Pad
    {
        public float moveX;
        public bool up, down, jumpHeld, followHeld, rHeld;
        public bool jump, punch, kick, grab, dodge, splash, torchToggle;
        public int twists, lensStep, lensPick = -1;
        public Vector2? pointer;
    }

    public static Pad Virtual;

    /// <summary>The on-screen controls (<see cref="TouchControls"/>), read alongside the keyboard and mouse.</summary>
    public static Pad Touch;

    /// <summary>A phone or tablet (also the editor's Device Simulator, and while Unity Remote is connected).</summary>
    public static bool OnTouchDevice => ForceTouch || UnityEngine.Device.Application.isMobilePlatform || RemoteConnected;

    /// <summary>The game is being played on the screen: a touch device, unless a keyboard or mouse plugged into it
    /// was the last thing used (a Chromebook, a phone with a keyboard); touching the screen brings touch back.</summary>
    public static bool TouchMode => OnTouchDevice && !_keysInUse;

#if UNITY_EDITOR
    private static bool RemoteConnected => UnityEditor.EditorApplication.isRemoteConnected;
#else
    private const bool RemoteConnected = false;
#endif

    /// <summary>Tests and the editor: play as on a phone anyway (set before the scene loads).</summary>
    public static bool ForceTouch;

    private static bool _keysInUse;

    /// <summary>Once a frame (App): whichever was used last, the screen or the keys, is how the game is played.
    /// Esc doesn't count: it's also Android's Back button.</summary>
    public static void Poll()
    {
        if (!OnTouchDevice) return;
        var ts = UnityEngine.InputSystem.Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) { _keysInUse = false; return; }
        var kb = Kb;
        if (kb != null && kb.anyKey.wasPressedThisFrame && !kb.escapeKey.wasPressedThisFrame) _keysInUse = true;
        var ms = Ms;
        if (ms != null && (ms.leftButton.wasPressedThisFrame || ms.rightButton.wasPressedThisFrame) && !MouseIsTouch) _keysInUse = true;
    }

    // the Device Simulator turns mouse clicks into touches: there the mouse isn't a mouse
    private static bool MouseIsTouch => UnityEngine.Device.Application.isMobilePlatform && Application.isEditor;

    /// <summary>The on-screen controls' crank: the finger circling the torch, like the scroll wheel (a turn past full
    /// may overwind one notch; going round again overwinds the next).</summary>
    public static bool TouchCranking, TouchCrankStarted;

    private static bool Touched(System.Func<Pad, bool> held) => Touch != null && held(Touch);

    private static Keyboard Kb => Keyboard.current;
    private static Mouse Ms => Mouse.current;

    private static bool Pressed(Key a)
    {
        var kb = Kb;
        return kb != null && kb[a].wasPressedThisFrame;
    }

    private static bool Take(ref bool flag)
    {
        bool v = flag;
        flag = false;
        return v;
    }

    public static float MoveX
    {
        get
        {
            if (Virtual != null) return Mathf.Clamp(Virtual.moveX, -1f, 1f);
            float x = 0f;
            if (Bindings.Held(Act.Left)) x -= 1f;
            if (Bindings.Held(Act.Right)) x += 1f;
            if (Touch != null) x += Touch.moveX;
            return Mathf.Clamp(x, -1f, 1f);
        }
    }

    public static bool Up => Virtual != null ? Virtual.up : Bindings.Held(Act.Up) || Touched(t => t.up);
    public static bool Down => Virtual != null ? Virtual.down : Bindings.Held(Act.Down) || Touched(t => t.down);
    public static bool JumpHeld => Virtual != null ? Virtual.jumpHeld : Bindings.Held(Act.Jump) || Touched(t => t.jumpHeld);
    // (both are read, so a press on one never leaves the other's waiting for the next frame)
    public static bool JumpPressed => Virtual != null ? Take(ref Virtual.jump) : Bindings.Pressed(Act.Jump) | (Touch != null && Take(ref Touch.jump));
    // by default Max is all left hand round W A S D (the right hand is on the mouse, holding the torch):
    // E punch (the middle finger, next to W), Q kick, F splash page, Shift dodge, Space jump
    public static bool PunchPressed => Virtual != null ? Take(ref Virtual.punch) : Bindings.Pressed(Act.Punch) | (Touch != null && Take(ref Touch.punch));
    public static bool KickPressed => Virtual != null ? Take(ref Virtual.kick) : Bindings.Pressed(Act.Kick) | (Touch != null && Take(ref Touch.kick));
    public static bool GrabPressed => Virtual != null ? Take(ref Virtual.grab) : Pressed(Key.G);
    public static bool DodgePressed => Virtual != null ? Take(ref Virtual.dodge) : Bindings.Pressed(Act.Dodge) | (Touch != null && Take(ref Touch.dodge));
    public static bool SplashPressed => Virtual != null ? Take(ref Virtual.splash) : Bindings.Pressed(Act.Splash) | (Touch != null && Take(ref Touch.splash));

    /// <summary>Hold (left mouse): the beam glides to Max (follow assist). On the screen it follows him
    /// whenever no finger is aiming it.</summary>
    public static bool FollowHeld => Virtual != null ? Virtual.followHeld : Bindings.Held(Act.Follow) || Touched(t => t.followHeld);

    /// <summary>The torch on and off (right click; a tap on the torch in Roshan's hand).</summary>
    public static bool TorchTogglePressed => Virtual != null ? Take(ref Virtual.torchToggle) : Bindings.Pressed(Act.Torch) | (Touch != null && Take(ref Touch.torchToggle));

    /// <summary>Crank twists this frame: one scroll notch (either way) is one twist, and so is one tap
    /// of its key (R, the "mash" option).</summary>
    public static int Twists
    {
        get
        {
            if (Virtual != null)
            {
                int t = Virtual.twists;
                Virtual.twists = 0;
                return t;
            }
            int twists = Bindings.Pulses(Act.Crank);
            if (Touch != null)
            {
                twists += Touch.twists;
                Touch.twists = 0;
            }
            return twists;
        }
    }

    /// <summary>This frame's twists came off the scroll wheel, or a finger circling the torch (a scripted pad's twists
    /// are taps, like R).</summary>
    public static bool CrankByWheel => Virtual == null && (Bindings.UsesWheel(Act.Crank) && Bindings.WheelTurned || TouchCranking);

    /// <summary>The wheel (as the crank) started a new turn this frame, after a pause; or a finger started circling.</summary>
    public static bool CrankStreamStarted => Virtual == null && (Bindings.UsesWheel(Act.Crank) && Bindings.WheelTurnStarted || TouchCrankStarted);

    /// <summary>The lens wheel is on the torch, so it's on the mouse: the middle button twists it on a
    /// step (Tab for a mouse without). The side buttons step it on and back while nothing else has them.</summary>
    public static int LensStep
    {
        get
        {
            if (Virtual != null)
            {
                int s = Virtual.lensStep;
                Virtual.lensStep = 0;
                return s;
            }
            int step = Bindings.Pulses(Act.Lens, signedWheel: true);
            if (Touch != null)
            {
                step += Touch.lensStep;
                Touch.lensStep = 0;
            }
            var ms = Ms;
            if (ms != null)
            {
                if (ms.forwardButton.wasPressedThisFrame && !Bindings.IsBound(new Bindings.Control(Bindings.Kind.Mouse, 3))) step++;
                if (ms.backButton.wasPressedThisFrame && !Bindings.IsBound(new Bindings.Control(Bindings.Kind.Mouse, 4))) step--;
            }
            return step;
        }
    }

    /// <summary>1 picks the Clear lens, 2 the Ghost lens (as a lens index), or -1.</summary>
    public static int LensPick
    {
        get
        {
            if (Virtual != null)
            {
                int p = Virtual.lensPick;
                Virtual.lensPick = -1;
                return p;
            }
            if (Pressed(Key.Digit1)) return (int)Lens.Clear;
            if (Pressed(Key.Digit2)) return (int)Lens.Ghost;
            return -1;
        }
    }

    public static Vector2? Pointer
    {
        get
        {
            if (Virtual != null) return Virtual.pointer;
            if (Touch != null) return Touch.pointer;                  // the finger aiming the beam (none: it follows Max)
            return Ms != null ? Ms.position.ReadValue() : (Vector2?)null;
        }
    }
}
