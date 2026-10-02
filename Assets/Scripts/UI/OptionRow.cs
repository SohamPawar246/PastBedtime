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
/// toggles/cycles; clicking a slider sets it where you click. The selected line
/// gets a process-yellow highlighter stripe. The dot leaders are measured so they
/// always run right up to the value column.
/// </summary>
public class OptionRow : Selectable, IPointerClickHandler, ISubmitHandler, IScrollHandler
{
    private enum Kind { Toggle, Choice, Slider }

    private const int Cells = 10;
    private const float ValueColumn = 0.56f;   // value column starts at this fraction of the row width

    private Kind _kind;
    private Func<bool> _getBool; private Action<bool> _setBool;
    private Func<int> _getIndex; private Action<int> _setIndex; private string[] _choices;
    private Func<float> _getValue; private Action<float> _setValue;

    private Image _highlight;
    private TMP_Text _value;
    private RectTransform _bar;
    private Image[] _cells;

    // ---- Factories ----------------------------------------------------------------------

    public static OptionRow Toggle(Transform parent, string label, Func<bool> get, Action<bool> set, Vector2 pos, float width)
    {
        var row = Build(parent, label, pos, width);
        row._kind = Kind.Toggle; row._getBool = get; row._setBool = set;
        row.Refresh();
        return row;
    }

    public static OptionRow Choice(Transform parent, string label, string[] choices, Func<int> get, Action<int> set, Vector2 pos, float width)
    {
        var row = Build(parent, label, pos, width);
        row._kind = Kind.Choice; row._choices = choices; row._getIndex = get; row._setIndex = set;
        row.Refresh();
        return row;
    }

    public static OptionRow Slider(Transform parent, string label, Func<float> get, Action<float> set, Vector2 pos, float width)
    {
        var row = Build(parent, label, pos, width);
        row._kind = Kind.Slider; row._getValue = get; row._setValue = set;
        row._value.gameObject.SetActive(false);

        row._bar = UIKit.Rect(row.transform, "Bar");
        row._bar.anchorMin = new Vector2(ValueColumn, 0.5f);
        row._bar.anchorMax = new Vector2(1f, 0.5f);
        row._bar.pivot = new Vector2(0f, 0.5f);
        row._bar.sizeDelta = new Vector2(-14f, 26f);
        row._bar.anchoredPosition = Vector2.zero;
        row._cells = new Image[Cells];
        for (int i = 0; i < Cells; i++)
        {
            var cell = UIKit.Image(row._bar, "Cell" + i, Palette.Paper, UIKit.Box, sliced: true);
            cell.pixelsPerUnitMultiplier = 2f; // thinner border on small cells
            var rt = cell.rectTransform;
            rt.anchorMin = new Vector2((float)i / Cells, 0f);
            rt.anchorMax = new Vector2((float)(i + 1) / Cells, 1f);
            rt.offsetMin = new Vector2(2f, 0f);
            rt.offsetMax = new Vector2(-2f, 0f);
            row._cells[i] = cell;
        }
        row.Refresh();
        return row;
    }

    private static OptionRow Build(Transform parent, string label, Vector2 pos, float width)
    {
        var rt = UIKit.Rect(parent, "Row " + label);
        UIKit.Place(rt, pos, new Vector2(width, 46f));

        var hit = rt.gameObject.AddComponent<Image>(); // pointer target
        hit.color = Color.clear;

        var highlight = UIKit.Image(rt, "Highlight", Palette.Yellow);
        UIKit.Stretch(highlight.rectTransform);
        highlight.rectTransform.offsetMin = new Vector2(-12f, 2f);
        highlight.rectTransform.offsetMax = new Vector2(12f, -2f);
        highlight.enabled = false;

        var labelText = UIKit.Text(rt, "Label", label, UIKit.Mono, 27f, Palette.Ink, TextAlignmentOptions.Left);
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        labelText.rectTransform.anchorMin = new Vector2(0f, 0f);
        labelText.rectTransform.anchorMax = new Vector2(ValueColumn, 1f);
        labelText.rectTransform.offsetMin = labelText.rectTransform.offsetMax = Vector2.zero;
        labelText.text = WithLeaders(labelText, label, width * ValueColumn - 14f);

        var value = UIKit.Text(rt, "Value", "", UIKit.Mono, 27f, Palette.Ink, TextAlignmentOptions.Left);
        value.textWrappingMode = TextWrappingModes.NoWrap;
        value.richText = true;
        value.rectTransform.anchorMin = new Vector2(ValueColumn, 0f);
        value.rectTransform.anchorMax = new Vector2(1f, 1f);
        value.rectTransform.offsetMin = value.rectTransform.offsetMax = Vector2.zero;

        var row = rt.gameObject.AddComponent<OptionRow>();
        row.transition = Transition.None;
        row.targetGraphic = hit;
        row._highlight = highlight;
        row._value = value;
        return row;
    }

    /// <summary>"Label ........" with as many dots as fit before <paramref name="maxWidth"/>,
    /// measured with the real font so every row's leaders end at the value column.</summary>
    public static string WithLeaders(TMP_Text measure, string label, float maxWidth)
    {
        float labelW = measure.GetPreferredValues(label + " ", 4000f, 100f).x;
        float dotW = measure.GetPreferredValues("..........", 4000f, 100f).x / 10f;
        int n = Mathf.Max(2, Mathf.FloorToInt((maxWidth - labelW) / Mathf.Max(1f, dotW)));
        return label + " " + new string('.', n);
    }

    // ---- Value plumbing -----------------------------------------------------------------------

    public void Refresh()
    {
        switch (_kind)
        {
            case Kind.Toggle:
                _value.text = _getBool() ? "[ ON ]" : "[ OFF ]";
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
        if (e.moveDir == MoveDirection.Left) { Step(-1); e.Use(); return; }
        if (e.moveDir == MoveDirection.Right) { Step(1); e.Use(); return; }
        base.OnMove(e);
    }

    public void OnSubmit(BaseEventData e)
    {
        if (_kind != Kind.Slider) Step(1);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (!IsInteractable() || e.button != PointerEventData.InputButton.Left) return;
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
        if (!IsInteractable() || Mathf.Approximately(e.scrollDelta.y, 0f)) return;
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
