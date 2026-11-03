using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// The phone builds (Docs/MOBILE_PLAN.md):
///   Past Bedtime > Build Android (test APK)   Builds/Android/PastBedtime.apk, to copy to a phone and install by hand
///                                              (or adb install); a development build, so the Profiler can attach
///   Past Bedtime > Build Android (Play AAB)   Builds/Android/PastBedtime.aab for the Play Console (signing comes
///                                              with the release step)
/// Both build the scenes in the build list and log the biggest assets they packed. The project's own setting
/// (app bundle or not) is put back afterwards.
/// </summary>
public static class AndroidBuild
{
    public const string Folder = "Builds/Android";
    private const string PendingKey = "PastBedtime.AndroidBuildPending";

    [MenuItem("Past Bedtime/Build Android (test APK)")]
    public static void BuildApk() => Build(bundle: false, development: true);

    [MenuItem("Past Bedtime/Build Android (Play AAB)")]
    public static void BuildAab() => Build(bundle: true, development: false);

    private static void Build(bool bundle, bool development)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.LogError("[AndroidBuild] switch the build profile to Android first");
            return;
        }
        Directory.CreateDirectory(Folder);
        bool wasBundle = EditorUserBuildSettings.buildAppBundle;
        EditorUserBuildSettings.buildAppBundle = bundle;
        try
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Path.Combine(Folder, bundle ? "PastBedtime.aab" : "PastBedtime.apk"),
                target = BuildTarget.Android,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            Debug.Log("[AndroidBuild] building " + options.locationPathName + ": " + string.Join(", ", options.scenes));
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[AndroidBuild] {s.result}: {s.totalSize / (1024f * 1024f):0.0} MB in {s.totalTime.TotalMinutes:0.0} min, " +
                      $"{s.totalErrors} errors, {s.totalWarnings} warnings -> {Path.GetFullPath(options.locationPathName)}");
            if (s.result != BuildResult.Succeeded) return;
            foreach (var (path, bytes) in report.packedAssets.SelectMany(p => p.contents)
                         .GroupBy(c => c.sourceAssetPath)
                         .Select(g => (g.Key, g.Sum(c => (long)c.packedSize)))
                         .OrderByDescending(a => a.Item2).Take(12))
                Debug.Log($"[AndroidBuild]   {bytes / (1024f * 1024f),5:0.0} MB  {path}");
        }
        finally
        {
            EditorUserBuildSettings.buildAppBundle = wasBundle;
        }
    }

    /// <summary>Starts the test APK on an editor tick once the editor's free, and returns at once (for scripted runs);
    /// an assembly reload in between doesn't lose it. (An editor update, not delayCall: a delayCall queued from a
    /// scripted run in an unfocused editor never came.)</summary>
    public static void BuildApkSoon()
    {
        SessionState.SetBool(PendingKey, true);
        EditorApplication.update -= RunPending;
        EditorApplication.update += RunPending;
    }

    [InitializeOnLoadMethod]
    private static void ResumeAfterReload()
    {
        if (SessionState.GetBool(PendingKey, false)) EditorApplication.update += RunPending;
    }

    private static void RunPending()
    {
        if (!SessionState.GetBool(PendingKey, false))
        {
            EditorApplication.update -= RunPending;
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= RunPending;
        SessionState.SetBool(PendingKey, false);
        BuildApk();
    }
}
