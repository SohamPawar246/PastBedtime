using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene names in one place, in boot order.</summary>
public static class Scenes
{
    public const string Boot = "Boot";
    public const string StudioIntro = "StudioIntro";
    public const string Warning = "EpilepsyWarning";
    public const string Title = "Title";
    public const string Game = "Game";
    public const string Credits = "Credits";

    /// <summary>Scenes where Esc opens the Bookmark pause.</summary>
    public static bool IsGameplay(string name) => name == Game || name.StartsWith("Page");
}

/// <summary>
/// The persistent root. It creates itself before the first scene loads (so any
/// scene can be played straight from the editor) and owns the scene transitions,
/// the audio and the loaded settings.
///
/// Boot flow: Boot → StudioIntro → EpilepsyWarning → Title → Game (→ Credits → Title).
/// </summary>
public class App : MonoBehaviour
{
    public static App I { get; private set; }

    /// <summary>The photosensitivity warning shows once per launch.</summary>
    public static bool WarningShown;

    public SceneFlow Flow { get; private set; }
    public AudioDirector Audio { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (I != null) return;
        var go = new GameObject("App (persistent)");
        DontDestroyOnLoad(go);
        go.AddComponent<App>();
    }

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        Settings.Load();
        Audio = gameObject.AddComponent<AudioDirector>();
        Flow = gameObject.AddComponent<SceneFlow>();

#if !UNITY_WEBGL || UNITY_EDITOR
        // On the web the browser owns the canvas; elsewhere honour the saved choice.
        if (!Application.isEditor) Screen.fullScreen = Settings.Fullscreen;
#endif
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (I == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Every gameplay scene gets the Bookmark pause without placing it by hand.
        if (Scenes.IsGameplay(scene.name) && FindFirstObjectByType<BookmarkPause>() == null)
            new GameObject("BookmarkPause").AddComponent<BookmarkPause>();
    }

    /// <summary>Platform-aware quit. Menus hide Quit on WebGL; this is a backstop.</summary>
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif !UNITY_WEBGL
        Application.Quit();
#endif
    }

    public static bool IsWeb =>
#if UNITY_WEBGL && !UNITY_EDITOR
        true;
#else
        false;
#endif
}
