using UnityEngine;
using UnityEngine.InputSystem;
using Act = Bindings.Act;

/// <summary>
/// Gameplay controls (GDD section 3), polled from the keyboard and mouse through the player's
/// <see cref="Bindings"/> (rebindable under Settings > Controls). By default the left hand plays Max
/// on the keyboard, all within reach of W A S D; the right hand is the reader's, on the mouse: aim,
/// hold to follow Max, click the torch on and off, scroll the crank, middle-click the lens wheel.
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
            return x;
        }
    }

    public static bool Up => Virtual != null ? Virtual.up : Bindings.Held(Act.Up);
    public static bool Down => Virtual != null ? Virtual.down : Bindings.Held(Act.Down);
    public static bool JumpHeld => Virtual != null ? Virtual.jumpHeld : Bindings.Held(Act.Jump);
    public static bool JumpPressed => Virtual != null ? Take(ref Virtual.jump) : Bindings.Pressed(Act.Jump);
    // by default Max is all left hand round W A S D (the right hand is on the mouse, holding the torch):
    // E punch (the middle finger, next to W), Q kick, F splash page, Shift dodge, Space jump
    public static bool PunchPressed => Virtual != null ? Take(ref Virtual.punch) : Bindings.Pressed(Act.Punch);
    public static bool KickPressed => Virtual != null ? Take(ref Virtual.kick) : Bindings.Pressed(Act.Kick);
    public static bool GrabPressed => Virtual != null ? Take(ref Virtual.grab) : Pressed(Key.G);
    public static bool DodgePressed => Virtual != null ? Take(ref Virtual.dodge) : Bindings.Pressed(Act.Dodge);
    public static bool SplashPressed => Virtual != null ? Take(ref Virtual.splash) : Bindings.Pressed(Act.Splash);

    /// <summary>Hold (left mouse): the beam glides to Max (follow assist).</summary>
    public static bool FollowHeld => Virtual != null ? Virtual.followHeld : Bindings.Held(Act.Follow);

    /// <summary>The torch on and off (right click).</summary>
    public static bool TorchTogglePressed => Virtual != null ? Take(ref Virtual.torchToggle) : Bindings.Pressed(Act.Torch);

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
            return Bindings.Pulses(Act.Crank);
        }
    }

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
            var ms = Ms;
            if (ms != null)
            {
                if (ms.forwardButton.wasPressedThisFrame && !Bindings.IsBound(new Bindings.Control(Bindings.Kind.Mouse, 3))) step++;
                if (ms.backButton.wasPressedThisFrame && !Bindings.IsBound(new Bindings.Control(Bindings.Kind.Mouse, 4))) step--;
            }
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
