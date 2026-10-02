using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Dev tool: renders the current play-mode frame, overlay canvases included, into
/// a PNG at a fixed resolution, independent of the Game view's size or focus.
/// Overlay canvases are briefly switched to Screen Space - Camera on the main
/// camera, rendered into a RenderTexture, then put back.
/// </summary>
public static class ShellCapture
{
    public static string Capture(string path, int width = 1920, int height = 1080)
    {
        var cam = Camera.main;
        if (cam == null) return "No main camera.";

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var restore = new List<(Canvas c, RenderMode mode, Camera cam, float dist)>();
        foreach (var c in canvases)
        {
            if (!c.isRootCanvas) continue;
            restore.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f + 0.0001f * (32767 - c.sortingOrder); // keep sort order
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
