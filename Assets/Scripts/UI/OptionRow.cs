using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One typewritten settings line, as drawn in wireframe 08:
///     Follow assist ........ [ ON ]
///     Twist ................ [SCROLL]  circle  mash
///     Music ................ ■■■■■■■□□□
/// Left/right (keys, d-pad, scroll wheel) changes the value; Enter or a click
/// toggles/cycles; clicking a slider sets it where you click. A link line
/// ("Controls ...... [ CHANGE ]") opens its page on Enter or a click. The selected line
/// gets a process-yellow highlighter stripe. The dot leaders are measured so they
/// always run right up to the value column.
/// </summary>
public class OptionRow : Selectable, IPointerClickHandler, ISubmitHandler, IScrollHandler
{
    private enum Kind { Toggle, Choice, Slider, Link }

    private const int Cells = 10;

    [SerializeField] private Kind _kind;
    [SerializeField] private string[] _choices;
    [SerializeField] private string _linkText;
    // what it reads and changes (its panel binds them when it starts)
    private Func<bool> _getBool; private Action<bool> _setBool;
    private Func<int> _getIndex; private Action<int> _setIndex;
    private Func<float> _getValue; private Action<float> _setValue;
    private Action _open;

    [SerializeField] private Image _highlight;
    [SerializeField] private TMP_Text _value;
    [SerializeField] private RectTransform _bar;
    [SerializeField] private Image[] _cells;

    // ---- Value plumbing -----------------------------------------------------------------------

    /// <summary>A toggle's value.</summary>
    public void Bind(Func<bool> get, Action<bool> set) { _getBool = get; _setBool = set; Refresh(); }
    /// <summary>A choice's value (an index into its choices).</summary>
    public void Bind(Func<int> get, Action<int> set) { _getIndex = get; _setIndex = set; Refresh(); }
    /// <summary>A slider's value (0..1).</summary>
    public void Bind(Func<float> get, Action<float> set) { _getValue = get; _setValue = set; Refresh(); }
    /// <summary>What a link line opens.</summary>
    public void Bind(Action open) { _open = open; Refresh(); }

    private bool Bound => _kind switch
    {
        Kind.Toggle => _getBool != null,
        Kind.Choice => _getIndex != null,
        Kind.Slider => _getValue != null,
        _ => true,
    };

    public void Refresh()
    {
        if (!Bound) return;                      // its panel hasn't bound it yet
        switch (_kind)
        {
            case Kind.Toggle:
                _value.text = _getBool() ? "[ ON ]" : "[ OFF ]";
                break;
            case Kind.Link:
                _value.text = "<b>[ " + _linkText + " ]</b>";
                break;
            case Kind.Choice:
            {
                int sel = _getIndex();
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < _choices.Length; i++)
                {
                    if (i > 0) sb.Append(' ');
                    sb.Append(i == sel ? "<b>[" + _choices[i].ToUpperInvariant() + "]</b>"
                                       : "<color=#00000080>" + _choices[i].ToLowerInvariant() + "</color>");
                }
                _value.text = sb.ToString();
                break;
            }
            case Kind.Slider:
            {
                int filled = Mathf.RoundToInt(Mathf.Clamp01(_getValue()) * Cells);
                for (int i = 0; i < Cells; i++) _cells[i].color = i < filled ? Palette.Ink : Palette.Paper;
                break;
            }
        }
    }

    private void Step(int dir)
    {
        switch (_kind)
        {
            case Kind.Toggle:
                _setBool(!_getBool());
                AudioDirector.I?.Toggle();
                break;
            case Kind.Choice:
                _setIndex(((_getIndex() + dir) % _choices.Length + _choices.Length) % _choices.Length);
                AudioDirector.I?.Toggle();
                break;
            case Kind.Slider:
                _setValue(Mathf.Clamp01(Mathf.Round(_getValue() * Cells + dir) / Cells));
                AudioDirector.I?.Hover();
                break;
        }
        Refresh();
    }

    // ---- Input ------------------------------------------------------------------------------------

    public override void OnMove(AxisEventData e)
    {
        if (_kind == Kind.Link) { base.OnMove(e); return; }
        if (e.moveDir == MoveDirection.Left) { Step(-1); e.Use(); return; }
        if (e.moveDir == MoveDirection.Right) { Step(1); e.Use(); return; }
        base.OnMove(e);
    }

    public void OnSubmit(BaseEventData e)
    {
        if (_kind == Kind.Link) { Open(); return; }
        if (_kind != Kind.Slider) Step(1);
    }

    private void Open()
    {
        if (!IsInteractable()) return;
        AudioDirector.I?.Confirm();
        _open?.Invoke();
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (!IsInteractable() || e.button != PointerEventData.InputButton.Left) return;
        if (_kind == Kind.Link) { Open(); return; }
        if (_kind != Kind.Slider) { Step(1); return; }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_bar, e.position, e.pressEventCamera, out var local))
        {
            float k = Mathf.Clamp01(local.x / Mathf.Max(1f, _bar.rect.width));
            _setValue(Mathf.Clamp01(Mathf.Ceil(k * Cells) / Cells));
            AudioDirector.I?.Hover();
            Refresh();
        }
    }

    public void OnScroll(PointerEventData e)
    {
        if (!IsInteractable() || _kind == Kind.Link || Mathf.Approximately(e.scrollDelta.y, 0f)) return;
        Step(e.scrollDelta.y > 0f ? 1 : -1);
    }

    public override void OnPointerEnter(PointerEventData e)
    {
        base.OnPointerEnter(e);
        if (IsInteractable() && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject, e);
    }

    public override void OnSelect(BaseEventData e)
    {
        base.OnSelect(e);
        if (_highlight != null) _highlight.enabled = true;
        if (e is PointerEventData || e is AxisEventData) AudioDirector.I?.Hover(); // silent when selected by code
    }

    public override void OnDeselect(BaseEventData e)
    {
        base.OnDeselect(e);
        if (_highlight != null) _highlight.enabled = false;
    }
}
