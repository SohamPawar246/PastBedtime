using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click setup for the front-end shell:  Past Bedtime > Build Shell
///
///  - TMP font assets for Bangers / Comic Neue / Courier Prime (Resources/Fonts)
///  - the LightMask material the torch beam and transitions use (Resources/Materials)
///  - the shell scenes, each just a camera + its screen component (the screens
///    build their UI in code, see UIKit), and the build order
///  - product name, company and WebGL compression settings
///
/// Safe to re-run. The Game scene is only created if it does not exist, so
/// gameplay work there is never overwritten.
/// </summary>
public static class ShellSetup
{
    private const string FontSrcDir = "Assets/Art/Fonts";
    private const string FontDir = "Assets/Resources/Fonts";
    private const string MatDir = "Assets/Resources/Materials";
    private const string SceneDir = "Assets/Scenes";

    private static readonly (string file, string asset)[] Fonts =
    {
        ("Bangers-Regular.ttf", "Bangers SDF"),        // lettering, titles, SFX
        ("ComicNeue-Bold.ttf", "ComicNeue SDF"),       // captions, body text
        ("CourierPrime-Bold.ttf", "CourierPrime SDF"), // typewriter settings, hints
    };

    [MenuItem("Past Bedtime/Build Shell")]
    public static void BuildShell()
    {
        EnsureFolder(FontDir);
        EnsureFolder(MatDir);
        EnsureFolder(SceneDir);

        CreateFonts();
        CreateLightMaskMaterial();
        CreateScenes();
        ApplyPlayerSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene($"{SceneDir}/{Scenes.Boot}.unity");
        Debug.Log("[ShellSetup] Shell built. Press Play in Boot (or any shell scene).");
    }

    // ---- Fonts -------------------------------------------------------------------------------

    private static void CreateFonts()
    {
        foreach (var (file, assetName) in Fonts)
        {
            string target = $"{FontDir}/{assetName}.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(target) != null) continue;

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontSrcDir}/{file}");
            if (font == null) { Debug.LogError($"[ShellSetup] Missing font {FontSrcDir}/{file}"); continue; }

            // TMP's own "Create > TextMeshPro > Font Asset > SDF" makes a dynamic asset
            // with its atlas and material as sub-assets, next to the source font.
            Selection.activeObject = font;
            EditorApplication.ExecuteMenuItem("Assets/Create/TextMeshPro/Font Asset/SDF");
            string made = $"{FontSrcDir}/{Path.GetFileNameWithoutExtension(file)} SDF.asset";
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(made) == null)
            {
                Debug.LogError($"[ShellSetup] TMP did not create {made}");
                continue;
            }
            string err = AssetDatabase.MoveAsset(made, target);
            if (!string.IsNullOrEmpty(err)) Debug.LogError($"[ShellSetup] Move failed: {err}");
        }
    }

    // ---- Material -----------------------------------------------------------------------------

    private static void CreateLightMaskMaterial()
    {
        string path = $"{MatDir}/LightMask.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        var shader = Shader.Find("PastBedtime/UI/LightMask");
        if (shader == null) { Debug.LogError("[ShellSetup] LightMask shader not found."); return; }
        AssetDatabase.CreateAsset(new Material(shader) { name = "LightMask" }, path);
    }

    // ---- Scenes ------------------------------------------------------------------------------------

    private static void CreateScenes()
    {
        MakeScene(Scenes.Boot, typeof(BootScreen), Palette.Night, overwrite: true);
        MakeScene(Scenes.StudioIntro, typeof(StudioIntroScreen), Color.white, overwrite: true);
        MakeScene(Scenes.Warning, typeof(WarningScreen), Color.black, overwrite: true);
        MakeScene(Scenes.Title, typeof(TitleScreen), Palette.Bedroom, overwrite: true);
        MakeScene(Scenes.Credits, typeof(CreditsScreen), Palette.Night, overwrite: true);
        MakeScene(Scenes.Game, typeof(GameDirector), Palette.Bedroom, overwrite: false);

        var order = new[] { Scenes.Boot, Scenes.StudioIntro, Scenes.Warning, Scenes.Title, Scenes.Game, Scenes.Credits };
        var list = new List<EditorBuildSettingsScene>();
        foreach (var s in order) list.Add(new EditorBuildSettingsScene($"{SceneDir}/{s}.unity", true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    private static void MakeScene(string name, System.Type screen, Color background, bool overwrite)
    {
        string path = $"{SceneDir}/{name}.unity";
        if (!overwrite && File.Exists(path)) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        cam.orthographic = true;
        camGo.AddComponent<AudioListener>();

        new GameObject(screen.Name).AddComponent(screen);

        EditorSceneManager.SaveScene(scene, path);
    }

    // ---- Player settings -------------------------------------------------------------------------------

    private static void ApplyPlayerSettings()
    {
        PlayerSettings.productName = "Past Bedtime";
        PlayerSettings.companyName = "Studio Kamikaze";
        PlayerSettings.runInBackground = true;
        // itch.io serves Brotli correctly only with the decompression fallback on.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
