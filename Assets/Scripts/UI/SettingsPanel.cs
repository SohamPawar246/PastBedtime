using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The settings card: a sheet of paper with typewritten option lines (wireframe 08).
/// It's drawn twice: a modal card with RESET / BACK on the title (UI/SettingsCard) and a sheet without
/// them in the Bookmark pause (UI/SettingsSheet). Every change saves immediately. "Controls" opens the
/// Controls page (<see cref="ControlsPanel"/>) over it, wherever it is.
///
/// On a touch screen the section headings are tabs (SOUND, READING, COMFORT, TOUCH): one section shows at a time,
/// its lines spaced a finger apart, on a shorter card that <see cref="PhoneFit"/> blows up to fill the screen. The
/// key-binding line and Fullscreen go (a phone has neither); TOUCH sets up the on-screen controls.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Tooltip("The option lines, top to bottom: master, music, effects, follow assist, controls, suspicion, " +
             "flat page, screen shake, reduce flashing, captions, fullscreen; then (touch screens) haptics, button " +
             "size, button opacity, left-handed.")]
    public List<OptionRow> Rows = new();
    [field: SerializeField] public CaptionButton ResetButton { get; private set; }
    [field: SerializeField] public CaptionButton BackButton { get; private set; }
    [field: SerializeField] public OptionRow ControlsRow { get; private set; }

    [Header("Touch screens")]
    [Tooltip("The section headings in order (SOUND, READING, COMFORT, TOUCH): a touch screen's tabs.")]
    [SerializeField] private List<TMP_Text> _headers = new();
    [Tooltip("The index into Rows each section starts at (the last runs to the end).")]
    [SerializeField] private int[] _sectionStarts = { 0, 3, 7, 11 };
    [Tooltip("The paper and the title plate (they shrink with the card on a touch screen).")]
    [SerializeField] private RectTransform _card, _titlePlate;
    [Tooltip("The BACK box only a touch screen shows (the Bookmark's sheet has none on a desktop).")]
    [SerializeField] private bool _backOnTouchOnly;

    /// <summary>First thing to select when the panel opens.</summary>
    public Selectable First => Rows.Count > 0 ? Rows[0] : null;

    // the touch layout: line pitch and height, tab width, and the card's height (the longest section, 4 lines)
    private const float Pitch = 80f, LineHeight = 70f, TabWidth = 170f, TabHeight = 70f, TabStep = 180f, TabsBelowTop = 98f,
                        FirstLine = 72f, PlateOverhang = 40f;
    private const int MostLines = 5;
    private int _tab;
    private bool? _touchLaid;
    private readonly Dictionary<RectTransform, (Vector2 pos, Vector2 size)> _drawn = new();

    /// <summary>Every line to its setting; BACK (if it has one) and the Controls line to whoever opened it.</summary>
    public void Bind(Action onBack, Action onControls)
    {
        Rows[0].Bind(() => Settings.MasterVolume, v => { Settings.MasterVolume = v; Settings.Commit(); });
        Rows[1].Bind(() => Settings.MusicVolume, v => { Settings.MusicVolume = v; Settings.Commit(); });
        Rows[2].Bind(() => Settings.SfxVolume, v => { Settings.SfxVolume = v; Settings.Commit(); });
        Rows[3].Bind(() => Settings.FollowAssist, v => { Settings.FollowAssist = v; Settings.Commit(); });
        ControlsRow.Bind(onControls);
        Rows[5].Bind(() => Settings.EasySuspicion ? 1 : 0, i => { Settings.EasySuspicion = i == 1; Settings.Commit(); });
        Rows[6].Bind(() => Settings.FlatPage, v => { Settings.FlatPage = v; Settings.Commit(); });
        Rows[7].Bind(() => Settings.ScreenShake, v => { Settings.ScreenShake = v; Settings.Commit(); });
        Rows[8].Bind(() => Settings.ReduceFlashing, v => { Settings.ReduceFlashing = v; Settings.Commit(); });
        Rows[9].Bind(() => Settings.SoundCaptions, v => { Settings.SoundCaptions = v; Settings.Commit(); });
        Rows[10].Bind(() => Screen.fullScreen, v => { Settings.Fullscreen = v; Screen.fullScreen = v; Settings.Commit(); });
        if (Rows.Count > 14)
        {
            Rows[11].Bind(() => Settings.Haptics, v => { Settings.Haptics = v; Settings.Commit(); if (v) Haptics.Bump(); });
            Rows[12].Bind(() => Settings.ButtonSize, i => { Settings.ButtonSize = i; Settings.Commit(); });
            Rows[13].Bind(() => Settings.ButtonOpacity, v => { Settings.ButtonOpacity = v; Settings.Commit(); });
            Rows[14].Bind(() => Settings.LeftHanded, v => { Settings.LeftHanded = v; Settings.Commit(); });
        }
        if (Rows.Count > 15) Rows[15].Bind(() => Settings.TiltAim, v => { Settings.TiltAim = v; Settings.Commit(); });
        if (ResetButton != null)
            ResetButton.Bind(() =>
            {
                Settings.ResetOptions();
                RefreshAll();
                AudioDirector.I?.Confirm();
            });
        if (BackButton != null) BackButton.Bind(() => onBack?.Invoke());
        for (int i = 0; i < _headers.Count; i++)
        {
            int tab = i;
            var header = _headers[i];
            header.raycastTarget = true;
            if (!header.TryGetComponent<Button>(out var button)) button = header.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => ShowTab(tab));
        }
    }

    private void OnEnable() => RefreshAll();

    public void RefreshAll()
    {
        foreach (var r in Rows) if (r != null) r.Refresh();
    }

    private void Update()
    {
        bool touch = Form.Touch;
        if (_touchLaid != touch) Layout(touch);
    }

    /// <summary>A tab on a touch screen: its section's lines show, the rest wait.</summary>
    public void ShowTab(int tab)
    {
        if (tab == _tab && _touchLaid == true) return;
        _tab = tab;
        AudioDirector.I?.Toggle();
        Haptics.Tick();
        Layout(Form.Touch);
    }

    private int SectionOf(int row)
    {
        int s = 0;
        for (int i = 0; i < _sectionStarts.Length; i++) if (row >= _sectionStarts[i]) s = i;
        return s;
    }

    /// <summary>Lines a touch screen doesn't show: the key bindings and Fullscreen.</summary>
    private bool TouchHides(OptionRow row) => row == ControlsRow || (Rows.Count > 10 && row == Rows[10]);

    private void Remember(RectTransform rt)
    {
        if (rt != null && !_drawn.ContainsKey(rt)) _drawn[rt] = (rt.anchoredPosition, rt.sizeDelta);
    }

    private void PutBack(RectTransform rt)
    {
        if (rt != null && _drawn.TryGetValue(rt, out var d)) { rt.anchoredPosition = d.pos; rt.sizeDelta = d.size; }
    }

    private void Layout(bool touch)
    {
        _touchLaid = touch;
        if (_headers.Count == 0) return;                     // (drawn before the touch tabs existed)
        var root = (RectTransform)transform;
        Remember(root);
        Remember(_card);
        Remember(_titlePlate);
        foreach (var h in _headers) Remember(h.rectTransform);
        foreach (var r in Rows) Remember((RectTransform)r.transform);
        if (ResetButton != null) Remember((RectTransform)ResetButton.transform);
        if (BackButton != null) Remember((RectTransform)BackButton.transform);

        if (!touch)
        {
            // as drawn; the TOUCH section is a touch screen's only
            foreach (var rt in _drawn.Keys) PutBack(rt);
            for (int i = 0; i < _headers.Count; i++)
            {
                _headers[i].gameObject.SetActive(i < 3);
                _headers[i].alpha = 1f;
            }
            for (int i = 0; i < Rows.Count; i++) Rows[i].gameObject.SetActive(SectionOf(i) < 3);
            if (BackButton != null && _backOnTouchOnly) BackButton.gameObject.SetActive(false);
            return;
        }

        bool buttons = ResetButton != null || BackButton != null;
        float height = TabsBelowTop + FirstLine + MostLines * Pitch + (buttons ? 110f : 40f);
        // the card itself, and room above it for the title plate that sticks out of its top (so a fit to the screen
        // keeps the plate on it)
        root.sizeDelta = new Vector2(root.sizeDelta.x, height + PlateOverhang);
        float top = (height + PlateOverhang) / 2f - PlateOverhang;
        if (_card != null)
        {
            _card.sizeDelta = new Vector2(_card.sizeDelta.x, height);
            _card.anchoredPosition = new Vector2(_drawn[_card].pos.x, top - height / 2f);
        }
        if (_titlePlate != null) _titlePlate.anchoredPosition = new Vector2(_drawn[_titlePlate].pos.x, top - 6f);
        float tabsY = top - TabsBelowTop;
        float firstTab = -(_headers.Count - 1) * TabStep / 2f;
        for (int i = 0; i < _headers.Count; i++)
        {
            var h = _headers[i];
            h.gameObject.SetActive(true);
            h.rectTransform.sizeDelta = new Vector2(TabWidth, TabHeight);
            h.rectTransform.anchoredPosition = new Vector2(firstTab + i * TabStep, tabsY);
            h.alpha = i == _tab ? 1f : 0.4f;
        }
        int line = 0;
        for (int i = 0; i < Rows.Count; i++)
        {
            var row = Rows[i];
            bool show = SectionOf(i) == _tab && !TouchHides(row);
            row.gameObject.SetActive(show);
            if (!show) continue;
            var rt = (RectTransform)row.transform;
            rt.sizeDelta = new Vector2(_drawn[rt].size.x, LineHeight);
            rt.anchoredPosition = new Vector2(_drawn[rt].pos.x, tabsY - FirstLine - line * Pitch);
            line++;
        }
        float buttonsY = top - height + 62f;
        if (ResetButton != null) ((RectTransform)ResetButton.transform).anchoredPosition = new Vector2(_drawn[(RectTransform)ResetButton.transform].pos.x, buttonsY);
        if (BackButton != null)
        {
            BackButton.gameObject.SetActive(true);
            var back = (RectTransform)BackButton.transform;
            back.anchoredPosition = new Vector2(ResetButton != null ? _drawn[back].pos.x : 0f, buttonsY);
        }
        RefreshAll();
    }
}
