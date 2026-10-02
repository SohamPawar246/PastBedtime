using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Device polling for the menus' simple questions ("any key?", "back?"). Gameplay
/// uses proper InputActions; this keeps the front-end screens from each
/// re-implementing it.
/// </summary>
public static class ShellInput
{
    /// <summary>Any keyboard key, mouse button, touch or gamepad button this frame.</summary>
    public static bool AnyPress()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;

        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame ||
                              mouse.rightButton.wasPressedThisFrame ||
                              mouse.middleButton.wasPressedThisFrame)) return true;

        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;

        var pad = Gamepad.current;
        if (pad != null)
        {
            foreach (var control in pad.allControls)
                if (control is ButtonControl b && !b.synthetic && b.wasPressedThisFrame) return true;
        }
        return false;
    }

    /// <summary>Esc, P or gamepad Start. P is there because browsers can swallow
    /// Esc (it also leaves fullscreen and pointer lock).</summary>
    public static bool PausePressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) return true;
        var pad = Gamepad.current;
        return pad != null && pad.startButton.wasPressedThisFrame;
    }

    /// <summary>Esc, Backspace or gamepad B/East: close the open panel.</summary>
    public static bool BackPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) return true;
        var pad = Gamepad.current;
        return pad != null && pad.buttonEast.wasPressedThisFrame;
    }

    public static bool KeyPressed(Key key)
    {
        var kb = Keyboard.current;
        return kb != null && kb[key].wasPressedThisFrame;
    }

    /// <summary>Mouse position in screen pixels, or null when there is no mouse.</summary>
    public static Vector2? PointerPosition()
    {
        var mouse = Mouse.current;
        return mouse != null ? mouse.position.ReadValue() : (Vector2?)null;
    }
}
