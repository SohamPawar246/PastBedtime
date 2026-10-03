using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gameplay controls (GDD section 3), polled from the keyboard and mouse. The left hand
/// plays Max on the keyboard, all within reach of W A S D; the right hand is the reader's,
/// on the mouse: aim, hold to follow Max, click the torch on and off, scroll the crank,
/// middle-click the lens wheel.
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

    private static Keyboard Kb => Keyboard.current;
    private static Mouse Ms => Mouse.current;

    private static bool Held(Key a, Key b = Key.None)
    {
        var kb = Kb;
        return kb != null && (kb[a].isPressed || (b != Key.None && kb[b].isPressed));
    }

    private static bool Pressed(Key a, Key b = Key.None)
    {
        var kb = Kb;
        return kb != null && (kb[a].wasPressedThisFrame || (b != Key.None && kb[b].wasPressedThisFrame));
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
            if (Held(Key.A, Key.LeftArrow)) x -= 1f;
            if (Held(Key.D, Key.RightArrow)) x += 1f;
            return x;
        }
    }

    public static bool Up => Virtual != null ? Virtual.up : Held(Key.W, Key.UpArrow);
    public static bool Down => Virtual != null ? Virtual.down : Held(Key.S, Key.DownArrow);
    public static bool JumpHeld => Virtual != null ? Virtual.jumpHeld : Held(Key.Space);
    public static bool JumpPressed => Virtual != null ? Take(ref Virtual.jump) : Pressed(Key.Space);
    // Max is all left hand round W A S D (the right hand is on the mouse, holding the torch):
    // E punch (the middle finger, next to W), Q kick, F splash page, Shift dodge, Space jump.
    public static bool PunchPressed => Virtual != null ? Take(ref Virtual.punch) : Pressed(Key.E);
    public static bool KickPressed => Virtual != null ? Take(ref Virtual.kick) : Pressed(Key.Q);
    public static bool GrabPressed => Virtual != null ? Take(ref Virtual.grab) : Pressed(Key.G);
    public static bool DodgePressed => Virtual != null ? Take(ref Virtual.dodge) : Pressed(Key.LeftShift, Key.RightShift);
    public static bool SplashPressed => Virtual != null ? Take(ref Virtual.splash) : Pressed(Key.F);

    /// <summary>Hold left mouse: the beam glides to Max (follow assist).</summary>
    public static bool FollowHeld => Virtual != null ? Virtual.followHeld : Ms != null && Ms.leftButton.isPressed;

    /// <summary>Right click toggles the torch.</summary>
    public static bool TorchTogglePressed => Virtual != null ? Take(ref Virtual.torchToggle) : Ms != null && Ms.rightButton.wasPressedThisFrame;

    /// <summary>Crank twists this frame: one scroll notch (either way) is one twist, or one tap of R
    /// (the "mash" option).</summary>
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
            int n = 0;
            if (Ms != null)
            {
                float s = Mathf.Abs(Ms.scroll.ReadValue().y);
                // Desktop reports 120 per notch; browsers vary (3 to 120): count at least one.
                if (s > 0.01f) n += Mathf.Max(1, Mathf.RoundToInt(s / 120f));
            }
            if (Pressed(Key.R)) n += 1;
            return n;
        }
    }

    /// <summary>The lens wheel is on the torch, so it's on the mouse: the middle button (or the
    /// forward side button) twists it on a step, the back side button back; Tab for a mouse without.</summary>
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
            int step = 0;
            var ms = Ms;
            if (ms != null)
            {
                if (ms.middleButton.wasPressedThisFrame || ms.forwardButton.wasPressedThisFrame) step++;
                if (ms.backButton.wasPressedThisFrame) step--;
            }
            if (Pressed(Key.Tab)) step++;
            return step;
        }
    }

    /// <summary>1 to 4 jump straight to a lens (0-based), or -1.</summary>
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
            if (Pressed(Key.Digit1)) return 0;
            if (Pressed(Key.Digit2)) return 1;
            if (Pressed(Key.Digit3)) return 2;
            if (Pressed(Key.Digit4)) return 3;
            return -1;
        }
    }

    public static Vector2? Pointer
    {
        get
        {
            if (Virtual != null) return Virtual.pointer;
            return Ms != null ? Ms.position.ReadValue() : (Vector2?)null;
        }
    }
}
