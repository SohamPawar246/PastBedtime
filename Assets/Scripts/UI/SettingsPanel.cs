using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The settings card: a sheet of paper with typewritten option lines (wireframe 08).
/// The same builder serves the title menu (as a modal with RESET / BACK) and the
/// Bookmark pause (embedded, no nested menus). Every change saves immediately.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    public readonly List<OptionRow> Rows = new();
    public CaptionButton ResetButton { get; private set; }
    public CaptionButton BackButton { get; private set; }

    /// <summary>First thing to select when the panel opens.</summary>
    public Selectable First => Rows.Count > 0 ? Rows[0] : null;

    public static SettingsPanel Build(Transform parent, Vector2 pos, bool withButtons, Action onBack)
    {
        const float width = 820f, rowH = 44f, headH = 52f;
        float height = withButtons ? 870f : 790f;

        var root = UIKit.Rect(parent, "Settings");
        UIKit.Place(root, pos, new Vector2(width, height));
        var panel = root.gameObject.AddComponent<SettingsPanel>();

        UIKit.Panel(root, "Card", Vector2.zero, new Vector2(width, height), Palette.Paper, shadow: 12f);

        // Title plate, pinned to the top-left corner and tipped like a pasted caption.
        var plate = UIKit.Panel(root, "TitlePlate", new Vector2(-width * 0.5f + 170f, height * 0.5f - 6f),
            new Vector2(300f, 70f), Palette.Yellow, shadow: 6f, tilt: -3f);
        var title = UIKit.Text(plate.transform, "Title", "SETTINGS", UIKit.Display, 46f, Palette.Ink);
        UIKit.Stretch(title.rectTransform);
        title.characterSpacing = 6f;

        float rowW = width - 110f;
        float y = height * 0.5f - 92f;

        void Header(string text)
        {
            var h = UIKit.Text(root, "Header " + text, text, UIKit.Display, 30f, Palette.Magenta, TMPro.TextAlignmentOptions.Left);
            UIKit.Place(h.rectTransform, new Vector2(0f, y - 6f), new Vector2(rowW, headH));
            h.characterSpacing = 5f;
            y -= headH;
        }

        void Add(OptionRow r)
        {
            panel.Rows.Add(r);
            y -= rowH;
        }

        Vector2 P() => new Vector2(0f, y);

        Header("SOUND");
        Add(OptionRow.Slider(root, "Master volume", () => Settings.MasterVolume, v => { Settings.MasterVolume = v; Settings.Commit(); }, P(), rowW));
        Add(OptionRow.Slider(root, "Music", () => Settings.MusicVolume, v => { Settings.MusicVolume = v; Settings.Commit(); }, P(), rowW));
        Add(OptionRow.Slider(root, "Effects", () => Settings.SfxVolume, v => { Settings.SfxVolume = v; Settings.Commit(); }, P(), rowW));

        Header("READING");
        Add(OptionRow.Toggle(root, "Follow assist", () => Settings.FollowAssist, v => { Settings.FollowAssist = v; Settings.Commit(); }, P(), rowW));
        Add(OptionRow.Choice(root, "Twist", new[] { "scroll", "circle", "mash" },
            () => (int)Settings.Twist, i => { Settings.Twist = (TwistInput)i; Settings.Commit(); }, P(), rowW));
        Add(OptionRow.Choice(root, "Suspicion", new[] { "normal", "easy" },
            () => Settings.EasySuspicion ? 1 : 0, i => { Settings.EasySuspicion = i == 1; Settings.Commit(); }, P(), rowW));
        Add(OptionRow.Toggle(root, "Flat page view", () => Settings.FlatPage, v => { Settings.FlatPage = v; Settings.Commit(); }, P(), rowW));

        Header("COMFORT");
        Add(OptionRow.Toggle(root, "Screen shake", () => Settings.ScreenShake, v => { Settings.ScreenShake = v; Settings.Commit(); }, P(), rowW));
        Add(OptionRow.Toggle(root, "Reduce flashing", () => Settings.ReduceFlashing, v => { Settings.ReduceFlashing = v; Settings.Commit(); }, P(), rowW));
        Add(OptionRow.Toggle(root, "Captions for sound", () => Settings.SoundCaptions, v => { Settings.SoundCaptions = v; Settings.Commit(); }, P(), rowW));
        Add(OptionRow.Toggle(root, "Fullscreen", () => Screen.fullScreen,
            v => { Settings.Fullscreen = v; Screen.fullScreen = v; Settings.Commit(); }, P(), rowW));

        var nav = new List<Selectable>(panel.Rows);
        if (withButtons)
        {
            float by = -height * 0.5f + 62f;
            panel.ResetButton = CaptionButton.Create(root, "ResetButton", "RESET", () =>
            {
                Settings.ResetOptions();
                panel.RefreshAll();
                AudioDirector.I?.Confirm();
            }, new Vector2(-150f, by), new Vector2(240f, 64f), 34f, tilt: 1.2f, quiet: true);
            panel.BackButton = CaptionButton.Create(root, "BackButton", "BACK", () => onBack?.Invoke(),
                new Vector2(150f, by), new Vector2(240f, 64f), 34f, tilt: -1.4f, quiet: true);
            nav.Add(panel.ResetButton.Button);
            nav.Add(panel.BackButton.Button);
        }
        CaptionButton.ChainVertical(nav.ToArray());
        return panel;
    }

    private void OnEnable() => RefreshAll();

    public void RefreshAll()
    {
        foreach (var r in Rows) if (r != null) r.Refresh();
    }
}
