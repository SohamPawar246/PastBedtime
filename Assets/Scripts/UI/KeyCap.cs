using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One slot: a key cap on the paper. Hover or arrows select it (yellow); a click or Enter
/// picks it up (red, "PRESS...").</summary>
public class KeyCap : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [field: SerializeField] public Button Button { get; private set; }
    [SerializeField] private Image _face;
    [SerializeField] private TMP_Text _label;
    private bool _selected, _live;

    /// <summary>What picking it up does.</summary>
    public void Bind(UnityEngine.Events.UnityAction onPick) => Button.onClick.AddListener(onPick);

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
