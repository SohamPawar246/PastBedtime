using TMPro;
using UnityEngine;

/// <summary>A caption's words and its box, fitted to them; re-lettered when the controls change.</summary>
public class CaptionWords : MonoBehaviour
{
    public float Width { get; private set; }
    public float Height { get; private set; }
    [SerializeField] private TextMeshPro _text;
    [SerializeField] private Transform _box, _edge;
    [SerializeField, TextArea] private string _raw;

    private void OnEnable()
    {
        Bindings.Changed += Fit;
        Fit();
    }

    private void OnDisable() => Bindings.Changed -= Fit;

    private void Fit()
    {
        if (_text == null) return;
        string text = Bindings.Format(_raw);
        _text.text = text;
        _text.rectTransform.sizeDelta = new Vector2(Mathf.Min(7f, 0.22f * text.Length + 0.8f), 1f);
        _text.ForceMeshUpdate();
        var size = _text.GetRenderedValues(false) + new Vector2(0.5f, 0.36f);
        var sz = new Vector2(Mathf.Max(size.x, 1f), Mathf.Max(size.y, 0.6f));
        _text.rectTransform.sizeDelta = sz;
        _box.localPosition = new Vector3(sz.x / 2f, -sz.y / 2f, 0.02f);
        _box.localScale = new Vector3(sz.x, sz.y, 1f);
        _edge.localPosition = new Vector3(sz.x / 2f, -sz.y / 2f, 0.04f);
        _edge.localScale = new Vector3(sz.x + 0.12f, sz.y + 0.12f, 1f);
        Width = sz.x;
        Height = sz.y;
    }
}
