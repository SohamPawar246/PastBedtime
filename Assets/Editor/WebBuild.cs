using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// The itch.io build in one go:  Past Bedtime > Build Web (itch.io)
///
///  - the release web settings: Gzip with the decompression fallback (itch.io serves gzip natively and the
///    loader copes anywhere that doesn't), data caching, the PastBedtime page template, wasm built for
///    runtime speed (not with link-time optimisation: its single-threaded link takes well over an hour)
///  - builds the scenes in the build list to Builds/Web, and logs the biggest assets it packed
///  - zips the folder's contents to Builds/PastBedtime-web.zip, index.html at the root and forward-slash
///    paths (itch.io's servers are case-sensitive Linux), ready to upload as an HTML5 game
/// </summary>
public static class WebBuild
{
    public const string Folder = "Builds/Web", Zip = "Builds/PastBedtime-web.zip";
    private const string PendingKey = "PastBedtime.WebBuildPending";

    [MenuItem("Past Bedtime/Build Web (itch.io)")]
    public static void Build()
    {
        ApplyReleaseSettings();
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = Folder,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };
        Debug.Log("[WebBuild] building " + string.Join(", ", options.scenes));
        var report = BuildPipeline.BuildPlayer(options);
        var s = report.summary;
        Debug.Log($"[WebBuild] {s.result}: {s.totalSize / (1024f * 1024f):0.0} MB in {s.totalTime.TotalMinutes:0.0} min, {s.totalErrors} errors, {s.totalWarnings} warnings");
        if (s.result != BuildResult.Succeeded) return;
        foreach (var (path, bytes) in report.packedAssets.SelectMany(p => p.contents)
                     .GroupBy(c => c.sourceAssetPath)
                     .Select(g => (g.Key, g.Sum(c => (long)c.packedSize)))
                     .OrderByDescending(a => a.Item2).Take(15))
            Debug.Log($"[WebBuild]   {bytes / (1024f * 1024f),5:0.0} MB  {path}");
        ZipFolder(Folder, Zip);
        Debug.Log($"[WebBuild] zipped: {Path.GetFullPath(Zip)} ({new FileInfo(Zip).Length / (1024f * 1024f):0.0} MB)");
    }

    /// <summary>Starts the build on the editor's next tick and returns at once (for scripted runs); an
    /// assembly reload in between doesn't lose it.</summary>
    public static void BuildSoon()
    {
        SessionState.SetBool(PendingKey, true);
        EditorApplication.delayCall += RunPending;
    }

    [InitializeOnLoadMethod]
    private static void ResumeAfterReload()
    {
        if (SessionState.GetBool(PendingKey, false)) EditorApplication.delayCall += RunPending;
    }

    private static void RunPending()
    {
        if (!SessionState.GetBool(PendingKey, false) || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer) return;
        SessionState.SetBool(PendingKey, false);
        Build();
    }

    public static void ApplyReleaseSettings()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.template = "PROJECT:PastBedtime";
        EditorUserBuildSettings.development = false;
        // Build Profile > Code Optimization: Runtime Speed (looked up by name: the type only exists with the
        // Web module installed)
        var settings = Type.GetType("UnityEditor.WebGL.UserBuildSettings, UnityEditor.WebGL.Extensions");
        var code = settings?.GetProperty("codeOptimization");
        if (code != null) code.SetValue(null, Enum.Parse(code.PropertyType, "RuntimeSpeed"));
    }

    /// <summary>The folder's contents, not the folder: itch.io looks for index.html at the top of the zip.</summary>
    public static void ZipFolder(string folder, string zipPath)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        string root = Path.GetFullPath(folder);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            string entry = file.Substring(root.Length + 1).Replace('\\', '/');
            zip.CreateEntryFromFile(file, entry, System.IO.Compression.CompressionLevel.Optimal);
        }
    }
}
