using UnityEngine;

/// <summary>
/// On a touch screen a card (the settings sheet, the Nightstand, How to Read) grows to fill the screen as far as it
/// fits, so its lines and buttons are big enough for a finger; on a desktop it stays as drawn. The size is a
/// multiple of what it was drawn at, so anything that animates the card's own scale (a pop-in) still works on its
/// parent.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PhoneFit : MonoBehaviour
{
    [Tooltip("How much of the safe area's height and width the card may take on a touch screen.")]
    [Range(0.5f, 1f)] [SerializeField] private float _height = 0.94f, _width = 0.96f;
    [Tooltip("Never more than this times its drawn size.")]
    [SerializeField] private float _maxScale = 2.2f;

    private RectTransform _rt;
    private Vector3 _drawn;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        _drawn = _rt.localScale;
    }

    private void LateUpdate()
    {
        if (!Form.Touch)
        {
            _rt.localScale = _drawn;
            return;
        }
        var canvas = _rt.GetComponentInParent<Canvas>().rootCanvas;
        var size = ((RectTransform)canvas.transform).rect.size;
        var sa = UnityEngine.Device.Screen.safeArea;
        float sw = Mathf.Max(1, UnityEngine.Device.Screen.width), sh = Mathf.Max(1, UnityEngine.Device.Screen.height);
        float w = size.x * sa.width / sw, h = size.y * sa.height / sh;
        // the card's size in the canvas's units, as drawn (its parents may scale it already)
        float parent = _rt.parent != null ? _rt.parent.lossyScale.x / canvas.transform.lossyScale.x : 1f;
        Vector2 drawn = _rt.rect.size * _drawn.x * parent;
        float s = Mathf.Min(w * _width / Mathf.Max(1f, drawn.x), h * _height / Mathf.Max(1f, drawn.y));
        s = Mathf.Clamp(s, 1f, _maxScale);
        _rt.localScale = _drawn * s;
    }
}
