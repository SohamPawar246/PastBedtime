using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Act = Bindings.Act;

/// <summary>
/// The Controls page (Settings > Controls, on the title and in the Bookmark): every action Max and the
/// torch have, each with two slots, a main control and a spare. Pick a slot and press the new key or
/// mouse button (or turn the wheel, for the crank and the lens); Esc cancels, Delete clears the slot.
/// A control that's already in use swaps over, so nothing ends up doing two jobs, and an action never
/// loses its last control. DEFAULTS puts the jam layout back. Typed on the settings card's paper.
/// </summary>
public class ControlsPanel : MonoBehaviour
{

    private Action _onBack;
    [SerializeField] private CanvasGroup _group;
    [Tooltip("Every action's main and spare slots, in pairs (action 0 main, action 0 spare, action 1 main...).")]
    [SerializeField] private KeyCap[] _caps;
    [SerializeField] private TMP_Text _note;
    [SerializeField] private CaptionButton _defaults, _back;
    private int _act = -1, _slot;
    private bool _waitRelease;
    private float _blink, _noteFor;

    public Selectable First => Cap(0, 0).Button;

    private KeyCap Cap(int act, int slot) => _caps[act * 2 + slot];

    /// <summary>Each slot picks itself up; DEFAULTS; BACK to whoever opened the page.</summary>
    public void Bind(Action onBack)
    {
        _onBack = onBack;
        for (int a = 0; a < Bindings.Count; a++)
            for (int s = 0; s < 2; s++)
            {
                int act = a, slot = s;
                Cap(a, s).Bind(() => Begin(act, slot));
            }
        _defaults.Bind(Defaults);
        _back.Bind(() => _onBack?.Invoke());
    }

    private void OnEnable()
    {
        Stop(select: false);
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (_caps == null || _caps.Length == 0) return;
        for (int a = 0; a < Bindings.Count; a++)
            for (int s = 0; s < 2; s++)
                Cap(a, s).Show(Bindings.SlotName((Act)a, s), false);
        if (_noteFor <= 0f)
            _note.text = "PICK A SLOT, THEN PRESS THE NEW KEY OR MOUSE BUTTON (SCROLL FOR THE CRANK OR LENS).\n" +
                         "ESC CANCELS, DELETE CLEARS.  KEPT AS THEY ARE: ESC / P BOOKMARK, 1 / 2 CLEAR / GHOST LENS.";
    }

    private void Say(string text)
    {
        _note.text = text;
        _noteFor = 2.2f;
    }

    private void Defaults()
    {
        Bindings.ResetDefaults();
        AudioDirector.I?.Confirm();
        Say("BACK TO THE DEFAULTS.");
        RefreshAll();
    }

    // ---- picking up a new control ------------------------------------------------------------------

    private void Begin(int act, int slot)
    {
        if (_act >= 0) return;
        _act = act;
        _slot = slot;
        _blink = 0f;
        _waitRelease = true;                         // the click or Enter that picked the slot isn't the answer
        _group.interactable = false;
        AudioDirector.I?.Click();
        _note.text = $"PRESS THE NEW CONTROL FOR \"{Bindings.Labels[act].ToUpperInvariant()}\"...\n" +
                     (Bindings.TakesWheel((Act)act) ? "A KEY, A MOUSE BUTTON OR THE SCROLL WHEEL.  " : "A KEY OR A MOUSE BUTTON.  ") +
                     "ESC CANCELS, DELETE CLEARS.";
        _noteFor = 0f;
    }

    private void Stop(bool select = true)
    {
        int act = _act, slot = _slot;
        _act = -1;
        if (_group != null) _group.interactable = true;
        if (select && act >= 0 && _caps != null) UIKit.Select(Cap(act, slot).gameObject);
    }

    private void Update()
    {
        if (_noteFor > 0f && (_noteFor -= Time.unscaledDeltaTime) <= 0f) RefreshAll();
        if (_act < 0)
        {
            if (ShellInput.BackPressed() || ShellInput.PausePressed()) _onBack?.Invoke();
            return;
        }

        _blink += Time.unscaledDeltaTime;
        Cap(_act, _slot).Show(Mathf.Repeat(_blink, 0.7f) < 0.45f ? "PRESS..." : "", true);
        var kb = Keyboard.current;
        var ms = Mouse.current;
        if (_waitRelease)
        {
            bool held = (kb != null && kb.anyKey.isPressed) ||
                        (ms != null && (ms.leftButton.isPressed || ms.rightButton.isPressed || ms.middleButton.isPressed ||
                                        ms.forwardButton.isPressed || ms.backButton.isPressed));
            if (!held) _waitRelease = false;
            return;
        }
        var act = (Act)_act;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            AudioDirector.I?.Back();
            Stop();
            RefreshAll();
            return;
        }
        if (kb != null && (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
        {
            if (Bindings.Get(act, 1 - _slot).IsNone) Say("EVERY ACTION NEEDS ONE CONTROL: PICK A NEW ONE INSTEAD.");
            else
            {
                Bindings.Set(act, _slot, default);
                AudioDirector.I?.Toggle();
            }
            Stop();
            RefreshAll();
            return;
        }
        if (Bindings.Capture(act, out var c))
        {
            string was = OwnerOf(c, act, _slot);
            Bindings.Set(act, _slot, c);
            AudioDirector.I?.Confirm();
            if (was != null) Say($"{Bindings.Name(c)} WAS ON \"{was.ToUpperInvariant()}\": THEY SWAPPED.");
            Stop();
            RefreshAll();
        }
    }

    /// <summary>Which other action already has this control (it's about to swap), if any.</summary>
    private static string OwnerOf(Bindings.Control c, Act act, int slot)
    {
        for (int a = 0; a < Bindings.Count; a++)
            for (int s = 0; s < 2; s++)
                if ((a != (int)act || s != slot) && Bindings.Get((Act)a, s).Equals(c)) return Bindings.Labels[a];
        return null;
    }
}
