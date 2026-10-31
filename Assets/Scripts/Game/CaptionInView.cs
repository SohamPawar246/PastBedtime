using UnityEngine;

/// <summary>A caption stays readable while its panel is on the page: when the page camera has panned so
/// the panel's corner is off the edge, the box slides along the panel's top to stay in view (never out of
/// its panel).</summary>
public class CaptionInView : MonoBehaviour
{
    public Rect panel;
    public CaptionWords words;
    private float _homeX;

    private void Start() => _homeX = transform.position.x;

    private void LateUpdate()
    {
        var cam = PageCamera.I;
        if (cam == null) return;
        float y = transform.position.y, width = words != null ? words.Width : 1f;
        float viewL = cam.ViewportToLane(new Vector2(0f, 0.5f)).x + 0.35f;
        float viewR = cam.ViewportToLane(new Vector2(1f, 0.5f)).x - 0.35f;
        float x = Mathf.Max(_homeX, viewL);                              // pushed in from the left edge
        x = Mathf.Min(x, Mathf.Max(_homeX, viewR - width));             // or the right
        x = Mathf.Clamp(x, panel.xMin + 0.2f, Mathf.Max(panel.xMin + 0.2f, panel.xMax - width - 0.2f));
        var p = transform.position;
        transform.position = new Vector3(x, y, p.z);
    }
}
