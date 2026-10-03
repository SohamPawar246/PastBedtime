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
    private TextMeshProUGUI _wallet;
    private readonly TextMeshProUGUI[] _steps = new TextMeshProUGUI[3];
    private readonly CaptionButton[] _buy = new CaptionButton[3];
    private CanvasGroup _group;

    /// <summary>Shows the Nightstand over the room until the player keeps reading.</summary>
    public static IEnumerator Visit()
    {
        var canvas = UIKit.Canvas("Nightstand", 40);
        var ns = canvas.gameObject.AddComponent<Nightstand>();
        ns.Build(canvas.transform);
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

    private void Build(Transform root)
    {
        _group = root.gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;

        var dim = UIKit.Image(root, "Dim", Palette.Ink.Alpha(0.72f));
        UIKit.Stretch(dim.rectTransform);

        var card = UIKit.Panel(root, "Card", new Vector2(0f, 10f), new Vector2(1240f, 700f), Palette.Paper, shadow: 14f, tilt: -1f);
        var c = card.transform;
        UIKit.Lettering(c, "Title", "THE NIGHTSTAND", 92f, Palette.Yellow, new Vector2(0f, 270f), new Vector2(1100f, 110f));
        var sub = UIKit.Text(c, "Sub", "Between chapters Roshan tinkers with the torch. Spend the stars Max found.", UIKit.Body, 30f, Palette.Ink);
        UIKit.Place(sub.rectTransform, new Vector2(0f, 190f), new Vector2(1100f, 44f));
        _wallet = UIKit.Text(c, "Wallet", "", UIKit.Display, 46f, Palette.Ink);
        UIKit.Place(_wallet.rectTransform, new Vector2(470f, 268f), new Vector2(220f, 60f));

        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            float x = (i - 1) * 390f;
            var col = UIKit.Panel(c, Names[i], new Vector2(x, -20f), new Vector2(350f, 330f), Palette.Yellow, shadow: 7f, tilt: i == 1 ? 0.8f : -0.6f);
            var name = UIKit.Text(col.transform, "Name", Names[i], UIKit.Display, 56f, Palette.Ink);
            UIKit.Place(name.rectTransform, new Vector2(0f, 120f), new Vector2(320f, 66f));
            var what = UIKit.Text(col.transform, "What", What[i], UIKit.Body, 24f, Palette.Ink);
            what.fontStyle = FontStyles.Bold;
            UIKit.Place(what.rectTransform, new Vector2(0f, 62f), new Vector2(310f, 56f));
            _steps[i] = UIKit.Text(col.transform, "Steps", "", UIKit.Display, 40f, Palette.Ink);
            UIKit.Place(_steps[i].rectTransform, new Vector2(0f, -6f), new Vector2(320f, 56f));
            _buy[i] = CaptionButton.Create(col.transform, "Buy", "", () => Buy(idx), new Vector2(0f, -104f), new Vector2(290f, 78f), 34f, tilt: -1f);
        }
        var go = CaptionButton.Create(c, "KeepReading", "KEEP READING", () => _done = true, new Vector2(0f, -280f), new Vector2(420f, 92f), 46f, tilt: 1f);
        UIKit.Select(go.gameObject);
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
