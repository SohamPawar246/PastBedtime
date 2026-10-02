using UnityEngine;

/// <summary>
/// Keeps a fixed 1920 × 1080 layer covering its parent, like CSS background-size:
/// cover, by uniform scaling. Everything placed inside it in 1920 × 1080 units
/// stays registered with a full-frame render whatever the screen's aspect ratio.
/// </summary>
[ExecuteAlways, RequireComponent(typeof(RectTransform))]
public class CoverScaler : MonoBehaviour
{
    public Vector2 designSize = new Vector2(1920f, 1080f);

    private RectTransform _rt;

    /// <summary>Creates a centred, cover-scaled 1920 × 1080 layer under <paramref name="parent"/>.</summary>
    public static RectTransform Create(Transform parent, string name)
    {
        var rt = UIKit.Rect(parent, name);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1920f, 1080f);
        rt.anchoredPosition = Vector2.zero;
        rt.gameObject.AddComponent<CoverScaler>().Fit();
        return rt;
    }

    private void OnEnable() => Fit();
    private void Update() => Fit();

    public void Fit()
    {
        if (_rt == null) _rt = (RectTransform)transform;
        var parent = _rt.parent as RectTransform;
        if (parent == null) return;
        var size = parent.rect.size;
        float s = Mathf.Max(size.x / designSize.x, size.y / designSize.y);
        if (s > 0f && !Mathf.Approximately(_rt.localScale.x, s)) _rt.localScale = new Vector3(s, s, 1f);
        _rt.sizeDelta = designSize;
    }
}
