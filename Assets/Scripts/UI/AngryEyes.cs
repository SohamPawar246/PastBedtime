using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Two angry eyes glaring out of Mom's silhouette: almond whites with a soft glow,
/// small pupils, and brows slanted down toward the middle. Exaggerated on purpose,
/// cartoon-style, so they read at a glance. <see cref="Visible"/> fades them in;
/// <see cref="Blink"/> and <see cref="Look"/> give them life.
/// </summary>
public class AngryEyes : MonoBehaviour
{
    [SerializeField] private RectTransform[] _whites = new RectTransform[2];
    [SerializeField] private RectTransform[] _pupils = new RectTransform[2];
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _root;
    [SerializeField] private float _visible = -1f;
    private float _blink, _look;

    /// <summary>0 hidden .. 1 fully there (they also swell slightly as they appear).</summary>
    public float Visible
    {
        get => _visible;
        set
        {
            if (Mathf.Approximately(value, _visible)) return;
            _visible = Mathf.Clamp01(value);
            _group.alpha = _visible;
            _root.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, Mathf.SmoothStep(0f, 1f, _visible));
        }
    }

    /// <summary>0 open .. 1 shut.</summary>
    public float Blink
    {
        get => _blink;
        set
        {
            _blink = Mathf.Clamp01(value);
            foreach (var e in _whites) e.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.08f, _blink), 1f);
        }
    }

    /// <summary>-1 .. 1: where the pupils point, left to right (0 = straight at Roshan).</summary>
    public float Look
    {
        get => _look;
        set
        {
            _look = Mathf.Clamp(value, -1f, 1f);
            for (int i = 0; i < 2; i++)
            {
                var parent = (RectTransform)_pupils[i].parent;
                _pupils[i].anchoredPosition = new Vector2(_look * parent.sizeDelta.x * 0.22f, -parent.sizeDelta.y * 0.06f);
            }
        }
    }
}
