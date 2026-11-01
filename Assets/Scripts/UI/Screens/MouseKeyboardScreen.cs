using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Act = Bindings.Act;

/// <summary>
/// "Best with a mouse and keyboard": once per launch, after the epilepsy warning and before the title. The game's
/// one big ask is both hands at once, so before anything else it shows where they go: the keyboard (left hand) is
/// Max, the mouse (right hand) is Roshan's torch. Two comic cards, with the player's own controls (Settings >
/// Controls); the mouse's buttons light up one by one with what they do. Any key or click goes on (after a moment
/// to read it), and it goes on by itself if nobody's there.
/// </summary>
public class MouseKeyboardScreen : MonoBehaviour
{
    [SerializeField] private float minSeconds = 1.2f, autoSeconds = 14f;
    private const float TourStep = 1.3f;                   // how long each part of the mouse stays lit

    // the keyboard's caps, in this order, and the callouts on the mouse's tour: they're lettered with the player's own
    // controls each time the card shows
    private static readonly Act[] CapActs = { Act.Kick, Act.Up, Act.Punch, Act.Left, Act.Down, Act.Right, Act.Splash, Act.Dodge, Act.Jump };

    [Tooltip("The two cards, drawn in the scene (switched off till they show).")]
    [SerializeField] private GameObject _canvas;
    [SerializeField] private CanvasGroup _prompt;
    [Tooltip("The pieces that land like rubber stamps, in order.")]
    [SerializeField] private RectTransform[] _stampRects;
    [SerializeField] private CanvasGroup[] _stampGroups;
    [Tooltip("The mouse's tour, a step each: the part lit (none for aiming), its callout's key and its box.")]
    [SerializeField] private Image[] _tourParts;
    [SerializeField] private TMP_Text[] _tourKeys;
    [SerializeField] private RectTransform[] _tourBoxes;
    [SerializeField] private Image[] _parts;
    [SerializeField] private TMP_Text[] _capKeys;
    [SerializeField] private TMP_Text _combos;
    [Tooltip("The pointers from the callouts to the mouse (line and dot each): follow, aim, torch, crank, lens. Each " +
             "shows only while that control really is that part of the mouse.")]
    [SerializeField] private GameObject[] _leaders;
    private float _t;
    private bool _advancing, _showing;

    private void Start()
    {
        if (App.MouseKeyboardShown) { Advance(); return; }
        App.MouseKeyboardShown = true;

        UIKit.EnsureEventSystem();
        Letter();
        _canvas.SetActive(true);
        _showing = true;
        StartCoroutine(StampIn());
    }

    /// <summary>The player's own controls on the caps and callouts.</summary>
    private void Letter()
    {
        for (int i = 0; i < CapActs.Length; i++) _capKeys[i].text = Bindings.Name(CapActs[i]);
        _combos.text = $"<b>{Bindings.Name(Act.Punch)} {Bindings.Name(Act.Punch)} {Bindings.Name(Act.Punch)}</b> a 3-hit combo   " +
                       $"<b>{Bindings.Name(Act.Up)} + {Bindings.Name(Act.Kick)}</b> launcher";
        _tourKeys[0].text = "HOLD " + Bindings.Name(Act.Follow);
        _tourKeys[1].text = Bindings.Name(Act.Torch);
        _tourKeys[2].text = Bindings.Name(Act.Crank);
        _tourKeys[3].text = Bindings.Name(Act.Lens);
        var M = Bindings.Kind.Mouse;
        bool[] shown =
        {
            Bindings.Uses(Act.Follow, new Bindings.Control(M, 0)), true, Bindings.Uses(Act.Torch, new Bindings.Control(M, 1)),
            Bindings.UsesWheel(Act.Crank), Bindings.Uses(Act.Lens, new Bindings.Control(M, 2)),
        };
        for (int i = 0; i < shown.Length; i++)
        {
            _leaders[i * 2].SetActive(shown[i]);
            _leaders[i * 2 + 1].SetActive(shown[i]);
        }
    }

    /// <summary>Each piece lands like a rubber stamp, one after another.</summary>
    private IEnumerator StampIn()
    {
        for (int i = 0; i < _stampRects.Length; i++)
        {
            StartCoroutine(Stamp(_stampRects[i], _stampGroups[i]));
            yield return new WaitForSecondsRealtime(0.12f);
        }
    }

    private static IEnumerator Stamp(RectTransform rt, CanvasGroup group)
    {
        Vector3 rest = rt.localScale;
        for (float t = 0f; t < 0.24f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.24f;
            float s = k < 0.65f ? Mathf.Lerp(1.18f, 0.97f, k / 0.65f) : Mathf.Lerp(0.97f, 1f, (k - 0.65f) / 0.35f);
            rt.localScale = rest * s;
            group.alpha = Mathf.Clamp01(k * 3f);
            yield return null;
        }
        rt.localScale = rest;
        group.alpha = 1f;
    }

    private void Update()
    {
        if (_advancing || !_showing) return;
        _t += Time.unscaledDeltaTime;

        // the mouse's tour: one part lit at a time, and what it does in red
        if (_t > 0.8f)
        {
            int on = (int)((_t - 0.8f) / TourStep) % _tourKeys.Length;
            foreach (var p in _parts)
            {
                bool lit = _tourParts[on] == p;
                p.color = Palette.Yellow.Alpha(Mathf.MoveTowards(p.color.a, lit ? 0.95f : 0f, Time.unscaledDeltaTime * 6f));
            }
            for (int i = 0; i < _tourKeys.Length; i++)
            {
                bool lit = i == on;
                _tourKeys[i].color = lit ? Palette.HeroRed : Palette.Ink;
                float s = Mathf.MoveTowards(_tourBoxes[i].localScale.x, lit ? 1.08f : 1f, Time.unscaledDeltaTime * 1.5f);
                _tourBoxes[i].localScale = new Vector3(s, s, 1f);
            }
        }

        bool ready = _t >= minSeconds;
        if (ready) _prompt.alpha = Mathf.MoveTowards(_prompt.alpha, 0.65f + 0.35f * Mathf.Sin((_t - minSeconds) * 3f), Time.unscaledDeltaTime * 2f);
        if ((ready && ShellInput.AnyPress()) || _t >= autoSeconds) Advance();
    }

    private void Advance()
    {
        if (_advancing) return;
        _advancing = true;
        App.MouseKeyboardShown = true;
        App.I.Flow.Load(Scenes.Title);
    }
}
