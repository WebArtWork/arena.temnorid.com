using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Development tool: Temnorid → Build Android APK makes a development (debug) APK of the enabled
/// build scenes at Builds/Android/TemnoridArena.apk, signed with the debug key, ready for
/// <c>adb install -r</c> or to send to a phone. Needs the Android module. The Editor goes back to
/// the platform it was on afterwards.
/// </summary>
public static class ArenaAndroidBuilder
{
    public const string ApkPath = "Builds/Android/TemnoridArena.apk";

    [MenuItem("Temnorid/Build Android APK")]
    public static void Build()
    {
        var previousGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        var previousTarget = EditorUserBuildSettings.activeBuildTarget;

        Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
        EditorUserBuildSettings.buildAppBundle = false;
        ArenaQuestBuilder.RemoveAndroidLoader();

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development,
        });

        if (report.summary.result == BuildResult.Succeeded && File.Exists(ApkPath))
        {
            var size = new FileInfo(ApkPath).Length / (1024f * 1024f);
            Debug.Log($"Android APK: {Path.GetFullPath(ApkPath)} ({size:F0} MB).");
        }
        else
        {
            // A module installed while the Editor was open is not loaded until a restart.
            Debug.LogError($"Android build {report.summary.result} ({report.summary.totalErrors} errors). Just installed the Android module? Restart the Editor.");
        }

        if (previousTarget != BuildTarget.Android)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(previousGroup, previousTarget);
        }
    }
}
