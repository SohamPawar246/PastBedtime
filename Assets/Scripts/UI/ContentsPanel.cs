using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The comic's contents page (Art/UI/Contents, on the title of a touch screen): the nine pages as caption boxes,
/// each with its title and the stars found on it. A page that's been read opens straight to its start (to find the
/// stars still hiding there); one not reached yet stays "???". START OVER lives here on a phone, out of the way of
/// CONTINUE.
/// </summary>
public class ContentsPanel : MonoBehaviour
{
    [Tooltip("Pages 1 to 9, in order.")]
    [SerializeField] private CaptionButton[] _pages = new CaptionButton[9];
    [SerializeField] private CaptionButton _startOver, _back;
    public CaptionButton Back => _back;

    private Action<int> _onPage;

    /// <summary>A page picked (its number), START OVER, and BACK to whoever opened it.</summary>
    public void Bind(Action<int> onPage, Action onStartOver, Action onBack)
    {
        _onPage = onPage;
        for (int i = 0; i < _pages.Length; i++)
        {
            int page = i + 1;
            _pages[i].Bind(() => _onPage?.Invoke(page));
        }
        // it wipes the save: a second tap within a few seconds is the yes
        _startOver.Bind(() =>
        {
            if (Time.unscaledTime < _confirmUntil) { onStartOver?.Invoke(); return; }
            _confirmUntil = Time.unscaledTime + 3f;
            _startOver.SetLabel("TAP AGAIN TO ERASE");
        });
        _back.Bind(() => onBack?.Invoke());
    }

    private float _confirmUntil = -1f;

    private void OnEnable()
    {
        _confirmUntil = -1f;
        Refresh();
    }

    private void Update()
    {
        if (_confirmUntil > 0f && Time.unscaledTime >= _confirmUntil)
        {
            _confirmUntil = -1f;
            _startOver.SetLabel("START OVER");
        }
    }

    private void Refresh()
    {
        int reached = Mathf.Max(1, Settings.HighestPage);
        for (int i = 0; i < _pages.Length; i++)
        {
            int page = i + 1;
            bool read = page <= reached;
            var def = Resources.Load<PageDef>($"Pages/Page{page:00}");
            int found = 0;
            for (int bits = Settings.StarsFound(page); bits != 0; bits &= bits - 1) found++;
            int drawn = def != null && def.art != null ? def.art.GetComponentsInChildren<StarPickup>(true).Length : 0;
            _pages[i].SetLabel(read
                ? $"{page}. {(def != null ? def.title.ToUpperInvariant() : "")}\n<size=70%>STARS {found} / {drawn}</size>"
                : $"{page}. ???");
            _pages[i].Button.interactable = read;
            if (!_pages[i].TryGetComponent(out CanvasGroup g)) g = _pages[i].gameObject.AddComponent<CanvasGroup>();
            g.alpha = read ? 1f : 0.45f;
        }
    }
}
