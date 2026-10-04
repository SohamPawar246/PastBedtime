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
    private const float Width = 1160f, Height = 930f, RowH = 46f;
    private static readonly float[] SlotX = { 150f, 430f };

    private Action _onBack;
    private CanvasGroup _group;
    private KeyCap[,] _caps;
    private TMP_Text _note;
    private int _act = -1, _slot;
    private bool _waitRelease;
    private float _blink, _noteFor;

    public Selectable First => _caps[0, 0].Button;

    public static ControlsPanel Build(Transform parent, Action onBack)
    {
        var root = UIKit.Rect(parent, "Controls");
        UIKit.Place(root, Vector2.zero, new Vector2(Width, Height));
        var panel = root.gameObject.AddComponent<ControlsPanel>();
        panel._onBack = onBack;
        panel._group = root.gameObject.AddComponent<CanvasGroup>();

        UIKit.Panel(root, "Card", Vector2.zero, new Vector2(Width, Height), Palette.Paper, shadow: 12f);
        var plate = UIKit.Panel(root, "TitlePlate", new Vector2(-Width * 0.5f + 175f, Height * 0.5f - 6f),
            new Vector2(310f, 70f), Palette.Yellow, shadow: 6f, tilt: -3f);
        var title = UIKit.Text(plate.transform, "Title", "CONTROLS", UIKit.Display, 46f, Palette.Ink);
        UIKit.Stretch(title.rectTransform);
        title.characterSpacing = 6f;

        float labelW = 560f, labelX = -Width * 0.5f + 55f + labelW * 0.5f;
        float y = Height * 0.5f - 88f;
        Header(root, "MAIN", new Vector2(SlotX[0], y));
        Header(root, "SPARE", new Vector2(SlotX[1], y));
        y -= 50f;

        panel._caps = new KeyCap[Bindings.Count, 2];
        for (int a = 0; a < Bindings.Count; a++)
        {
            var label = UIKit.Text(root, "Label " + (Act)a, "", UIKit.Mono, 27f, Palette.Ink, TextAlignmentOptions.Left);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UIKit.Place(label.rectTransform, new Vector2(labelX, y), new Vector2(labelW, 40f));
            label.text = OptionRow.WithLeaders(label, Bindings.Labels[a], labelW - 10f);
            for (int s = 0; s < 2; s++)
            {
                int act = a, slot = s;
                panel._caps[a, s] = KeyCap.Create(root, $"{(Act)a} {s}", new Vector2(SlotX[s], y), () => panel.Begin(act, slot));
            }
            y -= RowH;
        }

        panel._note = UIKit.Text(root, "Note", "", UIKit.Mono, 21f, Palette.Ink.Alpha(0.75f));
        UIKit.Place(panel._note.rectTransform, new Vector2(0f, -Height * 0.5f + 150f), new Vector2(Width - 100f, 60f));

        float by = -Height * 0.5f + 62f;
        var defaults = CaptionButton.Create(root, "DefaultsButton", "DEFAULTS", panel.Defaults,
            new Vector2(-170f, by), new Vector2(280f, 64f), 34f, tilt: 1.2f, quiet: true);
        var back = CaptionButton.Create(root, "BackButton", "BACK", () => panel._onBack?.Invoke(),
            new Vector2(170f, by), new Vector2(240f, 64f), 34f, tilt: -1.4f, quiet: true);
        panel.Chain(defaults.Button, back.Button);
        panel.RefreshAll();
        return panel;
    }

    private static void Header(Transform root, string text, Vector2 at)
    {
        var h = UIKit.Text(root, "Header " + text, text, UIKit.Display, 30f, Palette.Magenta);
        UIKit.Place(h.rectTransform, at, new Vector2(250f, 44f));
        h.characterSpacing = 5f;
    }

    /// <summary>Arrow keys: up and down a column, across between main and spare, down onto the buttons.</summary>
    private void Chain(Selectable defaults, Selectable back)
    {
        int last = Bindings.Count - 1;
        for (int a = 0; a <= last; a++)
            for (int s = 0; s < 2; s++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = a > 0 ? _caps[a - 1, s].Button : null;
                nav.selectOnDown = a < last ? _caps[a + 1, s].Button : (s == 0 ? defaults : back);
                nav.selectOnLeft = s == 1 ? _caps[a, 0].Button : null;
                nav.selectOnRight = s == 0 ? _caps[a, 1].Button : null;
                _caps[a, s].Button.navigation = nav;
            }
        defaults.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = _caps[last, 0].Button, selectOnRight = back };
        back.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = _caps[last, 1].Button, selectOnLeft = defaults };
    }

    private void OnEnable()
    {
        Stop(select: false);
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (_caps == null) return;
        for (int a = 0; a < Bindings.Count; a++)
            for (int s = 0; s < 2; s++)
                _caps[a, s].Show(Bindings.SlotName((Act)a, s), false);
        if (_noteFor <= 0f)
            _note.text = "PICK A SLOT, THEN PRESS THE NEW KEY OR MOUSE BUTTON (SCROLL FOR THE CRANK OR LENS).\n" +
                         "ESC CANCELS, DELETE CLEARS.  KEPT AS THEY ARE: ESC / P BOOKMARK, 1-4 PICK A LENS.";
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
        if (select && act >= 0 && _caps != null) UIKit.Select(_caps[act, slot].gameObject);
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
        _caps[_act, _slot].Show(Mathf.Repeat(_blink, 0.7f) < 0.45f ? "PRESS..." : "", true);
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

    /// <summary>One slot: a key cap on the paper. Hover or arrows select it (yellow); a click or Enter
    /// picks it up (red, "PRESS...").</summary>
    private class KeyCap : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        public Button Button { get; private set; }
        private Image _face;
        private TMP_Text _label;
        private bool _selected, _live;

        public static KeyCap Create(Transform parent, string name, Vector2 pos, UnityEngine.Events.UnityAction onPick)
        {
            var root = UIKit.Rect(parent, "Cap " + name);
            UIKit.Place(root, pos, new Vector2(256f, 40f));
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var shadow = UIKit.Image(root, "Shadow", Palette.Ink, UIKit.Box, sliced: true);
            UIKit.Stretch(shadow.rectTransform);
            shadow.rectTransform.anchoredPosition = new Vector2(4f, -4f);
            var face = UIKit.Image(root, "Face", Palette.Paper, UIKit.Box, sliced: true);
            UIKit.Stretch(face.rectTransform);
            var label = UIKit.Text(root, "Label", "", UIKit.Mono, 23f, Palette.Ink);
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 23f;
            UIKit.Stretch(label.rectTransform, 4f);

            var cap = root.gameObject.AddComponent<KeyCap>();
            cap.Button = root.gameObject.AddComponent<Button>();
            cap.Button.transition = Selectable.Transition.None;
            cap.Button.targetGraphic = hit;
            cap.Button.onClick.AddListener(onPick);
            cap._face = face;
            cap._label = label;
            return cap;
        }

        public void Show(string text, bool live)
        {
            _label.text = text;
            _live = live;
            Paint();
        }

        private void Paint()
        {
            _face.color = _live ? Palette.HeroRed : _selected ? Palette.Yellow : Palette.Paper;
            _label.color = _live ? Palette.Paper : Palette.Ink;
        }

        public void OnSelect(BaseEventData e)
        {
            _selected = true;
            Paint();
            if (e is PointerEventData || e is AxisEventData) AudioDirector.I?.Hover();
        }

        public void OnDeselect(BaseEventData e)
        {
            _selected = false;
            Paint();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (Button.IsInteractable() && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject, e);
        }
    }
}
