#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only: screenshots of whatever is on screen at each phone and tablet shape, for checking layouts
/// (Docs/MOBILE_PLAN.md). The game view is switched to each resolution in turn, given a moment to lay out, captured
/// (<see cref="DevCapture"/>), and put back to 1920 x 1080 at the end. Each shot is logged as "[Shots] path".
/// </summary>
public class MobileShots : MonoBehaviour
{
    /// <summary>Name, width, height: 16:9, 19.5:9 and 20:9 phones, 21:9, a 16:10 and a 4:3 tablet.</summary>
    public static readonly (string name, int w, int h)[] Shapes =
    {
        ("phone_16x9", 1920, 1080), ("phone_19x9", 2340, 1080), ("phone_20x9", 2400, 1080),
        ("phone_21x9", 2520, 1080), ("tablet_16x10", 2560, 1600), ("tablet_4x3", 2048, 1536),
    };

    private string _folder, _tag;
    private string[] _only;

    /// <summary>Shoots every shape (or just those named, comma-separated) into `folder` as tag_shape.png. Each
    /// shot is 1000 px wide, the screen's shape.</summary>
    public static void Run(string folder, string tag, string only = "")
    {
        var go = new GameObject("MobileShots");
        DontDestroyOnLoad(go);
        var s = go.AddComponent<MobileShots>();
        s._folder = folder;
        s._tag = tag;
        s._only = string.IsNullOrEmpty(only) ? null : only.Split(',');
        s.StartCoroutine(s.Shoot());
    }

    private IEnumerator Shoot()
    {
        foreach (var (name, w, h) in Shapes)
        {
            if (_only != null && System.Array.IndexOf(_only, name) < 0) continue;
            PlayModeWindow.SetCustomRenderingResolution((uint)w, (uint)h, name);
            for (int i = 0; i < 4; i++) yield return null;
            yield return new WaitForSecondsRealtime(0.4f);
            string path = Path.Combine(_folder, _tag + "_" + name + ".png");
            Debug.Log("[Shots] " + DevCapture.Capture(path, 1000, Mathf.RoundToInt(1000f * h / w)));
        }
        PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "1920x1080");
        Debug.Log("[Shots] done");
        Destroy(gameObject);
    }
}
#endif
