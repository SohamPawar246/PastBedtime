#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Editor-only: renders the current play-mode frame, overlay canvases included, into a PNG at a
/// fixed resolution, independent of the Game view's size or focus. Overlay canvases are briefly
/// switched to Screen Space - Camera on the main camera (whose culling mask briefly takes their
/// layers), rendered into a RenderTexture, then put back. The playtest driver calls it at exact
/// moments; the editor's ShellCapture calls it on demand.
/// </summary>
public static class DevCapture
{
    /// <summary>Where <see cref="Shot"/> saves; empty means shots are skipped.</summary>
    public static string Folder = "";

    public static void Shot(string name)
    {
        if (string.IsNullOrEmpty(Folder)) return;
        Debug.Log("[Capture] " + Capture(Path.Combine(Folder, name + ".png"), 1600, 900));
    }

    public static string Capture(string path, int width = 1920, int height = 1080)
    {
        var cam = Camera.main;
        if (cam == null) return "No main camera.";

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var restore = new List<(Canvas c, RenderMode mode, Camera cam, float dist)>();
        int prevMask = cam.cullingMask;
        foreach (var c in canvases)
        {
            if (!c.isRootCanvas) continue;
            restore.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f + 0.0001f * (32767 - c.sortingOrder); // keep sort order
                cam.cullingMask |= 1 << c.gameObject.layer;                 // overlays ignore the mask; cameras don't
            }
        }

        var prevTarget = cam.targetTexture;
        cam.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        cam.Render();

        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        cam.targetTexture = prevTarget;
        cam.cullingMask = prevMask;
        foreach (var (c, mode, wc, dist) in restore)
        {
            c.renderMode = mode;
            c.worldCamera = wc;
            c.planeDistance = dist;
        }
        Canvas.ForceUpdateCanvases();

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        return "Saved " + path;
    }
}
#endif
