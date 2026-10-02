using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A full-screen modal layer: an ink dim plus content that pops in (fade + a small
/// scale-up, the dim itself never scales). Unscaled time so it works while paused.
/// The owning screen decides what Esc does; this only shows and hides.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class Modal : MonoBehaviour
{
    public RectTransform Content { get; private set; }

    private CanvasGroup _group;
    private Coroutine _pop;

    public bool IsOpen => gameObject.activeSelf;

    public static Modal Create(Transform canvas, string name, float dim = 0.72f)
    {
        var root = UIKit.Rect(canvas, name);
        UIKit.Stretch(root);
        root.gameObject.AddComponent<CanvasGroup>();

        var dimImg = UIKit.Image(root, "Dim", Palette.Ink.Alpha(dim));
        UIKit.Stretch(dimImg.rectTransform);
        dimImg.raycastTarget = true; // swallow clicks to whatever is underneath

        var content = UIKit.Rect(root, "Content");
        UIKit.Stretch(content);

        var modal = root.gameObject.AddComponent<Modal>();
        modal.Content = content;
        root.gameObject.SetActive(false);
        return modal;
    }

    private void Awake() => _group = GetComponent<CanvasGroup>();

    public void Open(Selectable select = null)
    {
        gameObject.SetActive(true);
        if (_pop != null) StopCoroutine(_pop);
        _pop = StartCoroutine(Pop());
        if (select != null) UIKit.Select(select.gameObject);
    }

    public void Close() => gameObject.SetActive(false);

    private IEnumerator Pop()
    {
        const float seconds = 0.16f;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = 1f - (1f - t / seconds) * (1f - t / seconds);
            _group.alpha = k;
            Content.localScale = Vector3.one * Mathf.Lerp(0.93f, 1f, k);
            yield return null;
        }
        _group.alpha = 1f;
        Content.localScale = Vector3.one;
        _pop = null;
    }
}
