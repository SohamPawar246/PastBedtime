using System;
using TMPro;
using UnityEngine;

/// <summary>The How to Read page (UI/HowToRead): re-letters its lines from the bindings whenever the page is shown
/// or they change, and sends BACK to whoever opened it.</summary>
public class HowToReadLive : MonoBehaviour
{
    [Tooltip("The keyboard column's lines (Max), then the mouse column's (the torch).")]
    [SerializeField] private TMP_Text[] _maxLines, _torchLines;
    [field: SerializeField] public CaptionButton Back { get; private set; }

    private static string N(Bindings.Act a) => Bindings.Name(a);

    // left hand only: everything Max does is in reach of W A S D (the right hand holds the mouse)
    private static (string key, string what)[] MaxRows() => new[]
    {
        (N(Bindings.Act.Left) + " / " + N(Bindings.Act.Right), "run"),
        (N(Bindings.Act.Jump), "jump (hold = higher)"),
        (N(Bindings.Act.Punch), "punch, x3 = haymaker"),
        (N(Bindings.Act.Kick), $"kick ({N(Bindings.Act.Up)}+{N(Bindings.Act.Kick)} launches)"),
        (N(Bindings.Act.Dodge), "dodge"),
        (N(Bindings.Act.Up) + " / " + N(Bindings.Act.Down), "up / slam (with kick)"),
        (N(Bindings.Act.Splash), "splash page (meter full)"),
    };

    private static (string key, string what)[] TorchRows() => new[]
    {
        ("MOVE", "aim the beam"),
        ("HOLD " + N(Bindings.Act.Follow), "beam follows Max"),
        (N(Bindings.Act.Torch), "torch on / off"),
        (N(Bindings.Act.Crank), "twist the crank"),
        (N(Bindings.Act.Lens), "ghost lens on / off (page 7 on)"),
        ("ESC", "bookmark (pause)"),
    };

    public void Bind(Action onBack) => Back.Bind(() => onBack?.Invoke());

    private void OnEnable()
    {
        Bindings.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() => Bindings.Changed -= Refresh;

    private void Refresh()
    {
        Letter(_maxLines, MaxRows());
        Letter(_torchLines, TorchRows());
    }

    private static void Letter(TMP_Text[] lines, (string key, string what)[] rows)
    {
        if (lines == null) return;
        for (int i = 0; i < lines.Length && i < rows.Length; i++) lines[i].text = HowToReadPanel.Line(rows[i].key, rows[i].what);
    }
}
