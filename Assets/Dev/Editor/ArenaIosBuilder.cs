using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Development tool: Temnorid → Build iOS Xcode Project exports the enabled build scenes as an
/// Xcode project in Builds/iOS. Works on Windows too (it checks the iOS build and exports the
/// project); compiling, signing and running need a Mac: open Builds/iOS/Unity-iPhone.xcodeproj,
/// pick your team under Signing &amp; Capabilities, run on an iPhone or Archive. Needs the iOS
/// module. The Editor goes back to the platform it was on afterwards.
/// </summary>
public static class ArenaIosBuilder
{
    public const string ProjectFolder = "Builds/iOS";

    [MenuItem("Temnorid/Build iOS Xcode Project")]
    public static void Build()
    {
        var previousGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        var previousTarget = EditorUserBuildSettings.activeBuildTarget;

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = ProjectFolder,
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,
            options = BuildOptions.Development,
        });

        if (report.summary.result == BuildResult.Succeeded && Directory.Exists(Path.Combine(ProjectFolder, "Unity-iPhone.xcodeproj")))
        {
            Debug.Log($"iOS Xcode project: {Path.GetFullPath(ProjectFolder)}. On a Mac: open Unity-iPhone.xcodeproj, set the team, run.");
        }
        else
        {
            // A module installed while the Editor was open is not loaded until a restart.
            Debug.LogError($"iOS build {report.summary.result} ({report.summary.totalErrors} errors). Just installed the iOS module? Restart the Editor.");
        }

        if (previousTarget != BuildTarget.iOS)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(previousGroup, previousTarget);
        }
    }
}
