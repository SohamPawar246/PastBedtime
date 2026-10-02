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

    private Button _button;
    private RectTransform _body;
    private Image _face;
    private TMP_Text _label;
    private float _restTilt;
    private bool _selected;
    private Coroutine _anim;

    public Button Button => _button;
    public TMP_Text Label => _label;
    public RectTransform Body => _body;

    /// <summary>Builds the whole button. <paramref name="quiet"/> skips the click blip
    /// (for buttons that play their own sound, like START READING).</summary>
    public static CaptionButton Create(Transform parent, string name, string label, UnityAction onClick,
        Vector2 pos, Vector2 size, float fontSize = 40f, float tilt = 0f, bool quiet = false)
    {
        var root = UIKit.Rect(parent, name);
        UIKit.Place(root, pos, size);

        // Invisible full-rect hit area on the root; the visuals rotate inside it.
        var hit = root.gameObject.AddComponent<Image>();
        hit.color = Color.clear;

        var body = UIKit.Rect(root, "Body");
        UIKit.Stretch(body);
        body.localRotation = Quaternion.Euler(0, 0, tilt);

        var shadow = UIKit.Image(body, "Shadow", Palette.Ink, UIKit.Box, sliced: true);
        UIKit.Stretch(shadow.rectTransform);
        shadow.rectTransform.anchoredPosition = new Vector2(7f, -7f);

        var face = UIKit.Image(body, "Face", Palette.Paper, UIKit.Box, sliced: true);
        UIKit.Stretch(face.rectTransform);

        var text = UIKit.Text(body, "Label", label, UIKit.Display, fontSize, Palette.Ink);
        UIKit.Stretch(text.rectTransform, 6f);
        text.characterSpacing = 4f;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        var button = root.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        if (onClick != null) button.onClick.AddListener(onClick);
        if (!quiet) button.onClick.AddListener(() => AudioDirector.I?.Click());

        var cb = root.gameObject.AddComponent<CaptionButton>();
        cb._button = button;
        cb._body = body;
        cb._face = face;
        cb._label = text;
        cb._restTilt = tilt;
        cb.ApplyInstant(false);
        return cb;
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
