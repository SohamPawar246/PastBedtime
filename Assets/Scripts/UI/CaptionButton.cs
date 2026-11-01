using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A menu entry drawn as a comic caption box (wireframe 08): paper face, ink
/// border, hard ink shadow, Bangers lettering. Selected = the box turns process
/// yellow, grows a little and tips off its rest angle, like a box someone just
/// pointed the torch at. Hover selects, so mouse and keyboard never disagree.
/// </summary>
[RequireComponent(typeof(Button))]
public class CaptionButton : MonoBehaviour,
    ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float selectedScale = 1.07f;
    [SerializeField] private float selectedTilt = -2.2f;
    [SerializeField] private float animSeconds = 0.09f;

    [SerializeField] private RectTransform _body;
    [SerializeField] private Image _face;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private float _restTilt;
    [Tooltip("No click blip (it plays its own sound, like START READING).")]
    [SerializeField] private bool _quiet;
    private Button _button;
    private bool _selected;
    private Coroutine _anim;

    public Button Button => _button != null ? _button : _button = GetComponent<Button>();
    public TMP_Text Label => _label;
    public RectTransform Body => _body;

    /// <summary>What a click does (and the click blip after it, unless the button is quiet).</summary>
    public void Bind(UnityAction onClick)
    {
        var button = GetComponent<Button>();
        if (onClick != null) button.onClick.AddListener(onClick);
        if (!_quiet) button.onClick.AddListener(() => AudioDirector.I?.Click());
    }

    private void Awake() => _button = GetComponent<Button>();

    private void OnDisable()
    {
        _selected = false;
        ApplyInstant(false);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (_button.IsInteractable() && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject, e);
    }

    public void OnSelect(BaseEventData e)
    {
        _selected = true;
        if (e is PointerEventData || e is AxisEventData) AudioDirector.I?.Hover(); // silent when selected by code
        Animate(true);
    }

    public void OnDeselect(BaseEventData e)
    {
        _selected = false;
        Animate(false);
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (_button.IsInteractable() && _body != null) _body.localScale = Vector3.one * 0.95f;
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (_body != null) _body.localScale = Vector3.one * (_selected ? selectedScale : 1f);
    }

    public void SetLabel(string text) { if (_label != null) _label.text = text; }

    /// <summary>The angle it rests at, tipped like a box stuck on the page.</summary>
    public void Rest(float tilt)
    {
        _restTilt = tilt;
        ApplyInstant(_selected);
    }

    private void Animate(bool on)
    {
        if (!isActiveAndEnabled) { ApplyInstant(on); return; }
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimRoutine(on));
    }

    private void ApplyInstant(bool on)
    {
        if (_body == null) return;
        _face.color = on ? Palette.Yellow : Palette.Paper;
        _body.localScale = Vector3.one * (on ? selectedScale : 1f);
        _body.localRotation = Quaternion.Euler(0, 0, _restTilt + (on ? selectedTilt : 0f));
    }

    private IEnumerator AnimRoutine(bool on)
    {
        Color c0 = _face.color, c1 = on ? Palette.Yellow : Palette.Paper;
        float s0 = _body.localScale.x, s1 = on ? selectedScale : 1f;
        float a0 = _body.localEulerAngles.z, a1 = _restTilt + (on ? selectedTilt : 0f);
        for (float t = 0f; t < animSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / animSeconds;
            float pop = on ? 1f + Mathf.Sin(k * Mathf.PI) * 0.04f : 1f; // tiny overshoot on select
            _face.color = Color.Lerp(c0, c1, k);
            _body.localScale = Vector3.one * Mathf.Lerp(s0, s1, k) * pop;
            _body.localRotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(a0, a1, k));
            yield return null;
        }
        ApplyInstant(_selected);
        _anim = null;
    }

    /// <summary>Simple vertical navigation through a list of buttons/rows (wraps around).</summary>
    public static void ChainVertical(params Selectable[] items)
    {
        var list = new System.Collections.Generic.List<Selectable>();
        foreach (var s in items) if (s != null && s.gameObject.activeSelf) list.Add(s);
        for (int i = 0; i < list.Count; i++)
        {
            var nav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = list[(i - 1 + list.Count) % list.Count],
                selectOnDown = list[(i + 1) % list.Count],
            };
            list[i].navigation = nav;
        }
    }
}
