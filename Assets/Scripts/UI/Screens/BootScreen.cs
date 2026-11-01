using System.Collections;
using UnityEngine;

/// <summary>
/// First scene. On the web, browsers only allow sound after a user gesture, so
/// the game waits for one click ("turn on the torch") before the studio intro
/// plays with its audio. Everywhere else it passes straight through.
/// </summary>
public class BootScreen : MonoBehaviour
{
    [Tooltip("The click-to-start card, drawn in the scene (switched off: only the web shows it).")]
    [SerializeField] private GameObject _card;
    [SerializeField] private RectTransform _prompt;

    private IEnumerator Start()
    {
        if (!App.IsWeb)
        {
            yield return null;
            App.I.Flow.Load(Scenes.StudioIntro);
            yield break;
        }

        UIKit.EnsureEventSystem();
        _card.SetActive(true);

        yield return null; // the click that launched the page must not count
        while (!ShellInput.AnyPress())
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * 3f) * 0.025f;
            _prompt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        App.I.Flow.Load(Scenes.StudioIntro);
    }
}
