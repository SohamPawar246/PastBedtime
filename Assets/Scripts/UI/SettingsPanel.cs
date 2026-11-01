using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The settings card: a sheet of paper with typewritten option lines (wireframe 08).
/// It's drawn twice: a modal card with RESET / BACK on the title (UI/SettingsCard) and a sheet without
/// them in the Bookmark pause (UI/SettingsSheet). Every change saves immediately. "Controls" opens the
/// Controls page (<see cref="ControlsPanel"/>) over it, wherever it is.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Tooltip("The option lines, top to bottom: master, music, effects, follow assist, controls, suspicion, " +
             "flat page, screen shake, reduce flashing, captions, fullscreen.")]
    public List<OptionRow> Rows = new();
    [field: SerializeField] public CaptionButton ResetButton { get; private set; }
    [field: SerializeField] public CaptionButton BackButton { get; private set; }
    [field: SerializeField] public OptionRow ControlsRow { get; private set; }

    /// <summary>First thing to select when the panel opens.</summary>
    public Selectable First => Rows.Count > 0 ? Rows[0] : null;

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
        if (ResetButton != null)
            ResetButton.Bind(() =>
            {
                Settings.ResetOptions();
                RefreshAll();
                AudioDirector.I?.Confirm();
            });
        if (BackButton != null) BackButton.Bind(() => onBack?.Invoke());
    }

    private void OnEnable() => RefreshAll();

    public void RefreshAll()
    {
        foreach (var r in Rows) if (r != null) r.Refresh();
    }
}
