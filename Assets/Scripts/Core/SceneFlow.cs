using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// THE way to change scenes: App.I.Flow.Load("Title").
///
/// The transition is a cartoon iris out: a circle of light closes to a point,
/// its edge breaking into Ben-Day dots like a printed page,
/// the scene swaps in the dark, and the iris opens again. It is silent:
/// screens play their own sounds. A screen can hand over its own torch beam (position and radius) so
/// the closing light continues the beam already on screen instead of a second,
/// unrelated circle. Unscaled time, so it works from the pause menu.
/// </summary>
public class SceneFlow : MonoBehaviour
{
    [SerializeField] private float closeSeconds = 0.38f;
    [SerializeField] private float openSeconds = 0.42f;

    private RawImage _overlay;
    private Material _mat;
    private bool _loading;

    public bool IsLoading => _loading;

    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int SoftId = Shader.PropertyToID("_Softness");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int HeightId = Shader.PropertyToID("_HeightPx");
    private static readonly int DotId = Shader.PropertyToID("_DotSize");
    private static readonly int DarkId = Shader.PropertyToID("_DarkColor");
    private static readonly int LitId = Shader.PropertyToID("_LitColor");

    private const float Soft = 0.12f;
    private float OpenRadius => 0.5f * Mathf.Sqrt(Aspect * Aspect + 1f) + Soft + 0.25f; // past every corner
    private static float Aspect => Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;

    private void Awake()
    {
        var canvas = UIKit.Canvas("TransitionCanvas", 32000, transform);
        _overlay = UIKit.Raw(canvas.transform, "Iris");
        UIKit.Stretch(_overlay.rectTransform);
        _overlay.raycastTarget = true;

        _mat = UIKit.NewLightMaskMaterial();
        if (_mat != null)
        {
            _mat.SetColor(DarkId, Color.black);
            _mat.SetColor(LitId, new Color(0, 0, 0, 0));
            _mat.SetFloat(SoftId, Soft);
            _mat.SetFloat(Shader.PropertyToID("_Halftone"), 1f);   // a comic "iris out": the edge breaks into Ben-Day dots
            _overlay.material = _mat;
        }
        _overlay.gameObject.SetActive(false);
    }

    /// <summary>Close the light, load, open it again. Ignored while already loading.</summary>
    /// <param name="focusScreenPx">Where the light closes onto (screen pixels); null = centre.</param>
    /// <param name="startRadius">Radius (in screen heights) the closing light starts from, to continue
    /// a beam that is already on screen; negative = start from fully open.</param>
    public void Load(string scene, Vector2? focusScreenPx = null, float startRadius = -1f)
    {
        if (_loading) return;
        StartCoroutine(LoadRoutine(scene, focusScreenPx, startRadius));
    }

    /// <summary>The old cartoon ending: the light closes slowly onto one spot, holds there on a small circle
    /// for a moment, then shuts, and the next scene opens.</summary>
    /// <param name="holdRadius">The circle it holds on, in screen heights.</param>
    public void IrisOut(string scene, Vector2 focusScreenPx, float holdRadius, float closeOver, float hold)
    {
        if (_loading) return;
        StartCoroutine(LoadRoutine(scene, focusScreenPx, -1f, holdRadius, closeOver, hold));
    }

    public void Reload() => Load(SceneManager.GetActiveScene().name);

    /// <summary>Change scenes with no visible transition: the last frame holds on screen while the
    /// next scene loads and draws, for a scene that picks up exactly where this one leaves off (the
    /// title handing its room, comic and torch to the game).</summary>
    public void Cut(string scene)
    {
        if (_loading) return;
        StartCoroutine(CutRoutine(scene));
    }

    private IEnumerator CutRoutine(string scene)
    {
        _loading = true;
        yield return new WaitForEndOfFrame();
        var still = ScreenCapture.CaptureScreenshotAsTexture();
        var mat = _overlay.material;
        _overlay.material = null;
        _overlay.texture = still;
        _overlay.color = Color.white;
        _overlay.gameObject.SetActive(true);

        Time.timeScale = 1f;
        var op = SceneManager.LoadSceneAsync(scene);
        while (op != null && !op.isDone) yield return null;
        for (int i = 0; i < 3; i++) yield return null;                 // the new scene draws its first frames underneath
        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
        {
            _overlay.color = new Color(1f, 1f, 1f, 1f - t / 0.2f);
            yield return null;
        }
        _overlay.gameObject.SetActive(false);
        _overlay.texture = null;
        _overlay.material = mat;
        _overlay.color = Color.white;
        Destroy(still);
        _loading = false;
    }

    private IEnumerator LoadRoutine(string scene, Vector2? focusPx, float startRadius, float holdRadius = 0f, float closeOver = 0f, float hold = 0f)
    {
        _loading = true;
        Vector2 focus = focusPx.HasValue
            ? new Vector2(focusPx.Value.x / Mathf.Max(1, Screen.width), focusPx.Value.y / Mathf.Max(1, Screen.height))
            : new Vector2(0.5f, 0.5f);

        _overlay.gameObject.SetActive(true);
        float from = startRadius > 0f ? startRadius : OpenRadius;
        if (holdRadius > 0f)
        {
            yield return Iris(focus, from, holdRadius, closeOver, easeIn: false);
            yield return new WaitForSecondsRealtime(hold);
            from = holdRadius;
        }
        yield return Iris(focus, from, -Soft, closeSeconds, easeIn: true);

        Time.timeScale = 1f; // a pause must never leak into the next scene
        var op = SceneManager.LoadSceneAsync(scene);
        while (op != null && !op.isDone) yield return null;
        yield return new WaitForSecondsRealtime(0.12f);

        yield return Iris(new Vector2(0.5f, 0.5f), -Soft, OpenRadius, openSeconds, easeIn: false);
        _overlay.gameObject.SetActive(false);
        _loading = false;
    }

    private IEnumerator Iris(Vector2 center, float from, float to, float seconds, bool easeIn)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / seconds);
            k = easeIn ? k * k : 1f - (1f - k) * (1f - k);
            Apply(center, Mathf.Lerp(from, to, k));
            yield return null;
        }
        Apply(center, to);
    }

    private void Apply(Vector2 center, float radius)
    {
        if (_mat == null) { _overlay.color = radius <= 0f ? Color.black : Color.clear; return; }
        _mat.SetVector(CenterId, center);
        _mat.SetFloat(RadiusId, radius);
        _mat.SetFloat(AspectId, Aspect);
        _mat.SetFloat(HeightId, Screen.height);
        _mat.SetFloat(DotId, 13f * Screen.height / 1080f);
    }
}
