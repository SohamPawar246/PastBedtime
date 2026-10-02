using UnityEngine;

/// <summary>
/// Renders the cover of Max Voltage #1 from a posed comic set
/// (Resources/Comic/CoverSet, built by Past Bedtime > Build Comic Look) into a
/// RenderTexture, once. The pose is static, so the set is drawn a single time and
/// then switched off: the cover costs nothing per frame.
/// </summary>
public static class ComicCover
{
    private const string SetPath = "Comic/CoverSet";
    private static readonly Vector3 Offstage = new Vector3(0f, -500f, 0f);

    private static RenderTexture _shared;

    /// <summary>The cover, rendered on first use and shared by every screen that shows
    /// the comic (title, Bookmark pause). Survives scene loads.</summary>
    public static RenderTexture Shared
    {
        get
        {
            if (_shared == null || !_shared.IsCreated())
            {
                _shared = Render(944, 1324);
                if (_shared != null) _shared.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            return _shared;
        }
    }

    /// <summary>Returns a new RenderTexture with the cover art, or null if the set is missing.</summary>
    public static RenderTexture Render(int width, int height)
    {
        var prefab = Resources.Load<GameObject>(SetPath);
        if (prefab == null)
        {
            Debug.LogWarning("[ComicCover] Resources/Comic/CoverSet is missing. Run Past Bedtime > Build Comic Look.");
            return null;
        }

        var set = Object.Instantiate(prefab, Offstage, Quaternion.identity);
        set.name = "CoverSet (render once)";
        var cam = set.GetComponentInChildren<Camera>(true);
        var light = set.GetComponentInChildren<ComicLight>(true);
        if (light != null) light.Apply();

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "ComicCover", antiAliasing = 4 };
        rt.Create();
        if (cam != null)
        {
            cam.aspect = (float)width / height;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
        }
        if (Application.isPlaying) Object.Destroy(set); else Object.DestroyImmediate(set);
        return rt;
    }
}
