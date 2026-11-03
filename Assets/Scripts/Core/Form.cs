using UnityEngine;

/// <summary>What the game is played on: the keyboard-and-mouse game, or a touch screen of a phone's or a tablet's
/// size. Phones and tablets lay the game and the menus out differently (Docs/MOBILE_PLAN.md).</summary>
public enum FormFactor { Desktop, Phone, Tablet }

/// <summary>
/// The screen the game is on: its form factor and its real size, so touch targets can be set in millimetres. A
/// phone, a tablet, the editor's Device Simulator and Unity Remote are touch screens (<see cref="GameInput.TouchMode"/>
/// says whether touch is what's being used right now; a phone with a keyboard plugged in is still a phone).
/// </summary>
public static class Form
{
    /// <summary>Tests and the editor: lay out as this form factor whatever the screen is.</summary>
    public static FormFactor? Forced;

    /// <summary>Screens smaller than this (inches, corner to corner) are phones.</summary>
    public const float PhoneInches = 7.5f;

    public static FormFactor Current
    {
        get
        {
            if (Forced.HasValue) return Forced.Value;
            if (!GameInput.OnTouchDevice) return FormFactor.Desktop;
            return Inches < PhoneInches ? FormFactor.Phone : FormFactor.Tablet;
        }
    }

    public static bool Phone => Current == FormFactor.Phone;
    public static bool Touch => Current != FormFactor.Desktop;

    /// <summary>Screen pixels per inch. A real device (and the Device Simulator) says; otherwise (Unity Remote, a
    /// device that reports 0, a forced form factor) it's guessed from the screen's shape: a phone-shaped screen is
    /// taken as a 6.5" phone, a squarer one as a 10" tablet.</summary>
    public static float Dpi
    {
        get
        {
            var s = UnityEngine.Device.Screen.dpi;
            bool real = UnityEngine.Device.Application.isMobilePlatform && s > 20f;
            if (real && !Forced.HasValue) return s;
            float w = Mathf.Max(1, UnityEngine.Device.Screen.width), h = Mathf.Max(1, UnityEngine.Device.Screen.height);
            bool phoneShaped = Forced.HasValue ? Forced.Value == FormFactor.Phone : Mathf.Max(w, h) / Mathf.Min(w, h) > 1.7f;
            float inches = phoneShaped ? 6.5f : 10f;
            return Mathf.Sqrt(w * w + h * h) / inches;
        }
    }

    /// <summary>The screen's size corner to corner, in inches.</summary>
    public static float Inches
    {
        get
        {
            float w = UnityEngine.Device.Screen.width, h = UnityEngine.Device.Screen.height;
            return Mathf.Sqrt(w * w + h * h) / Dpi;
        }
    }

    /// <summary>Millimetres on the screen to screen pixels.</summary>
    public static float MmToPixels(float mm) => mm / 25.4f * Dpi;

    /// <summary>Millimetres on the screen to a canvas's own units (what a RectTransform's size is in).</summary>
    public static float Mm(Canvas canvas, float mm) => MmToPixels(mm) / Mathf.Max(0.01f, canvas.rootCanvas.scaleFactor);
}
