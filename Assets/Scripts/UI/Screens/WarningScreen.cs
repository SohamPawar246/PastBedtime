using TMPro;
using UnityEngine;

/// <summary>
/// Epilepsy warning, shown once per launch. Deliberately static and high-contrast:
/// nothing on it moves except the prompt, which fades in only after the minimum
/// read time. It names this game's real triggers (the torch flare, sudden light
/// changes, dot patterns). Reduce Flashing lives in Settings.
/// </summary>
public class WarningScreen : MonoBehaviour
{
    [SerializeField] private float minSeconds = 4f;

    [Tooltip("The warning, drawn in the scene (switched off until it's shown).")]
    [SerializeField] private GameObject _card;
    [SerializeField] private CanvasGroup _prompt;
    private float _elapsed;
    private bool _advancing, _showing;

    private void Start()
    {
        if (App.WarningShown) { Advance(); return; }

        UIKit.EnsureEventSystem();
        _card.SetActive(true);
        _showing = true;
    }

    private void Update()
    {
        if (_advancing || !_showing) return;
        _elapsed += Time.unscaledDeltaTime;
        bool ready = _elapsed >= minSeconds;
        if (ready) _prompt.alpha = Mathf.MoveTowards(_prompt.alpha, 1f, Time.unscaledDeltaTime * 2f);
        if (ready && ShellInput.AnyPress()) Advance();
    }

    private void Advance()
    {
        if (_advancing) return;
        _advancing = true;
        App.WarningShown = true;
        App.I.Flow.Load(Scenes.MouseKeyboard);                 // then the mouse-and-keyboard card, then the title
    }
}
