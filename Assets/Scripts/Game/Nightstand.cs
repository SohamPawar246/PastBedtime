using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Nightstand (GDD section 5), between acts (after pages 3 and 6): Roshan tinkers with the torch
/// and spends the stars Max found on the pages.
///   SPRING   more light in the torch: 60 s of charge, then 80, then 100
///   GEAR     more charge per twist: +2 s, then +3, then +4
///   RATCHET  more overwind before a flare: 3 notches, then 5, then 7
/// A first level costs 4 stars, a second 7. KEEP READING turns to the next act.
/// </summary>
public class Nightstand : MonoBehaviour
{
    public static readonly int[] Cost = { 4, 7 };
    public static bool Open { get; private set; }
    public static Nightstand I { get; private set; }

    /// <summary>On to the next act (KEEP READING).</summary>
    public void KeepReading() => _done = true;

    private static readonly string[] Names = { "SPRING", "GEAR", "RATCHET" };
    private static readonly string[] What = { "MORE LIGHT IN THE TORCH", "MORE CHARGE PER TWIST", "MORE OVERWIND BEFORE A FLARE" };
    private static readonly string[][] Steps =
    {
        new[] { "60 s", "80 s", "100 s" },
        new[] { "+2 s", "+3 s", "+4 s" },
        new[] { "3", "5", "7" },
    };

    private bool _done;
    [SerializeField] private TextMeshProUGUI _wallet;
    [Tooltip("SPRING, GEAR, RATCHET: each column's steps and its buy button.")]
    [SerializeField] private TextMeshProUGUI[] _steps = new TextMeshProUGUI[3];
    [SerializeField] private CaptionButton[] _buy = new CaptionButton[3];
    [SerializeField] private CaptionButton _keepReading;
    [SerializeField] private CanvasGroup _group;

    /// <summary>Shows the Nightstand (a copy of UI/Nightstand) over the room until the player keeps reading.</summary>
    public static IEnumerator Visit()
    {
        var ns = Instantiate(Resources.Load<Nightstand>("UI/Nightstand"));
        ns.name = "Nightstand";
        var canvas = ns.GetComponent<Canvas>();
        ns.Bind();
        I = ns;
        Open = true;
        AudioDirector.I?.Duck(0.5f);
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            ns._group.alpha = t / 0.3f;
            yield return null;
        }
        ns._group.alpha = 1f;
        ns._group.interactable = true;
        while (!ns._done) yield return null;
        ns._group.interactable = false;
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            ns._group.alpha = 1f - t / 0.25f;
            yield return null;
        }
        AudioDirector.I?.Duck(1f);
        Open = false;
        I = null;
        Destroy(canvas.gameObject);
    }

    private static int Level(int i)
    {
        var gs = GameState.I;
        if (gs == null) return 0;
        return i switch { 0 => gs.Spring, 1 => gs.Gear, _ => gs.Ratchet };
    }

    /// <summary>The buy buttons and KEEP READING; the stars in hand.</summary>
    private void Bind()
    {
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            _buy[i].Bind(() => Buy(idx));
        }
        _keepReading.Bind(() => _done = true);
        UIKit.Select(_keepReading.gameObject);
        Refresh();
    }

    private void Buy(int i)
    {
        var gs = GameState.I;
        int level = Level(i);
        if (gs == null || level >= 2 || gs.StarsTotal < Cost[level]) return;
        gs.StarsTotal -= Cost[level];
        if (i == 0) gs.Spring++; else if (i == 1) gs.Gear++; else gs.Ratchet++;
        TorchController.I?.Charge.ApplyUpgrades(gs.Spring, gs.Gear, gs.Ratchet);
        if (i == 0 && TorchController.I != null) TorchController.I.Charge.Charge = TorchController.I.Charge.Capacity;   // a fresh spring, fully wound
        Settings.Stars = gs.StarsTotal;
        Settings.Spring = gs.Spring;
        Settings.Gear = gs.Gear;
        Settings.Ratchet = gs.Ratchet;
        Settings.Save();
        gs.Notify();
        GameAudio.Play("star", 0.8f);
        Refresh();
    }

    private void Refresh()
    {
        var gs = GameState.I;
        int stars = gs != null ? gs.StarsTotal : 0;
        _wallet.text = $"{stars} STARS";
        for (int i = 0; i < 3; i++)
        {
            int level = Level(i);
            var s = Steps[i];
            string Step(int k) => k == level ? $"<u>{s[k]}</u>" : $"<alpha=#55>{s[k]}<alpha=#FF>";
            _steps[i].text = $"{Step(0)}  >  {Step(1)}  >  {Step(2)}";
            bool maxed = level >= 2;
            bool afford = !maxed && stars >= Cost[level];
            _buy[i].SetLabel(maxed ? "MAXED!" : $"UPGRADE: {Cost[level]} STARS");
            _buy[i].Button.interactable = afford;
            if (!_buy[i].TryGetComponent(out CanvasGroup cg)) cg = _buy[i].gameObject.AddComponent<CanvasGroup>();
            cg.alpha = maxed || afford ? 1f : 0.45f;
        }
    }
}
