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

    private CanvasGroup _prompt;
    private float _elapsed;
    private bool _advancing;

    private void Start()
    {
        if (App.WarningShown) { Advance(); return; }

        UIKit.EnsureEventSystem();
        var canvas = UIKit.Canvas("WarningCanvas", 0);
        var root = canvas.transform;

        var bg = UIKit.Image(root, "Black", Color.black);
        UIKit.Stretch(bg.rectTransform);

        var icon = UIKit.Image(root, "Icon", Palette.Yellow, UIKit.Triangle);
        UIKit.Place(icon.rectTransform, new Vector2(0f, 315f), new Vector2(92f, 80f));
        var bang = UIKit.Text(icon.transform, "Bang", "!", UIKit.Display, 58f, Palette.Ink);
        UIKit.Place(bang.rectTransform, new Vector2(0f, -8f), new Vector2(60f, 70f));

        UIKit.Lettering(root, "Title", "EPILEPSY WARNING", 64f, Palette.Yellow,
            new Vector2(0f, 222f), new Vector2(1400f, 84f), outline: 0.2f, shadow: 0f);

        var body = UIKit.Text(root, "Body",
            "A very small percentage of people may have a seizure when exposed to certain " +
            "lights, flashing images or patterns, even with no history of epilepsy.\n\n" +
            "This game contains <b>a bright full-screen flash</b> (when the torch is overwound), " +
            "<b>sudden light changes</b> and <b>high-contrast dot patterns</b>.\n\n" +
            "If you feel dizzy, notice altered vision, eye or muscle twitching, involuntary " +
            "movement, disorientation or any loss of awareness, stop playing at once and see a doctor.",
            UIKit.Body, 31f, Palette.Paper, TextAlignmentOptions.Top);
        body.lineSpacing = 6f;
        UIKit.Place(body.rectTransform, new Vector2(0f, -20f), new Vector2(1180f, 380f));

        var promptGo = UIKit.Rect(root, "Prompt");
        UIKit.Place(promptGo, new Vector2(0f, -330f), new Vector2(1000f, 50f));
        _prompt = promptGo.gameObject.AddComponent<CanvasGroup>();
        _prompt.alpha = 0f;
        var promptText = UIKit.Text(promptGo, "Text", "PRESS ANY KEY TO CONTINUE", UIKit.Mono, 26f, Palette.PaperDim);
        UIKit.Stretch(promptText.rectTransform);
        promptText.characterSpacing = 8f;
    }

    private void Update()
    {
        if (_advancing || _prompt == null) return;
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
        App.I.Flow.Load(Scenes.Title);
    }
}
