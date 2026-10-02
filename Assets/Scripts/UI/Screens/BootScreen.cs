using System.Collections;
using UnityEngine;

/// <summary>
/// First scene. On the web, browsers only allow sound after a user gesture, so
/// the game waits for one click ("turn on the torch") before the studio intro
/// plays with its audio. Everywhere else it passes straight through.
/// </summary>
public class BootScreen : MonoBehaviour
{
    private IEnumerator Start()
    {
        if (!App.IsWeb)
        {
            yield return null;
            App.I.Flow.Load(Scenes.StudioIntro);
            yield break;
        }

        UIKit.EnsureEventSystem();
        var canvas = UIKit.Canvas("BootCanvas", 0);
        var bg = UIKit.Image(canvas.transform, "Night", Palette.Night);
        UIKit.Stretch(bg.rectTransform);

        var prompt = UIKit.Lettering(canvas.transform, "Prompt", "CLICK TO TURN ON THE TORCH", 78f, Palette.Yellow,
            new Vector2(0f, 30f), new Vector2(1500f, 120f), outline: 0.24f, shadow: 7f, tilt: -2f);
        var sub = UIKit.Text(canvas.transform, "Sub", "sound on  ·  best in fullscreen", UIKit.Mono, 26f, Palette.PaperDim);
        UIKit.Place(sub.rectTransform, new Vector2(0f, -70f), new Vector2(1200f, 50f));

        yield return null; // the click that launched the page must not count
        var group = prompt.transform.parent;
        while (!ShellInput.AnyPress())
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * 3f) * 0.025f;
            group.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        App.I.Flow.Load(Scenes.StudioIntro);
    }
}
