using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Development tool: Temnorid → Build Windows Release makes a release (non-development) Windows
/// build of the enabled build scenes into Builds/Release/TemnoridArena and zips it as
/// Builds/TemnoridArena-&lt;version&gt;-windows.zip, ready to share. Players unzip it and run
/// TemnoridArena.exe. The version is Player Settings → Version.
/// </summary>
public static class ArenaReleaseBuilder
{
    private const string BuildFolder = "Builds/Release/TemnoridArena";
    private const string ExeName = "TemnoridArena.exe";

    [MenuItem("Temnorid/Build Windows Release")]
    public static void Build()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (Directory.Exists(BuildFolder))
        {
            Directory.Delete(BuildFolder, true);
        }

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = $"{BuildFolder}/{ExeName}",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"Windows release build {report.summary.result} ({report.summary.totalErrors} errors).");
            return;
        }

        var zip = $"Builds/TemnoridArena-{PlayerSettings.bundleVersion}-windows.zip";
        File.Delete(zip);

        // Files sit in a TemnoridArena folder inside the zip, so unzipping does not scatter them.
        // Folders Unity marks as not for shipping (debug symbols, sources for crash analysis) stay
        // in the build folder for us but are left out of the zip.
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            var root = Path.GetFullPath(BuildFolder);
            var parent = Path.GetDirectoryName(root);
            foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                if (file.Contains("DoNotShip") || file.Contains("DontShip"))
                {
                    continue;
                }

                var entry = file.Substring(parent.Length + 1).Replace('\\', '/');
                archive.CreateEntryFromFile(file, entry, System.IO.Compression.CompressionLevel.Optimal);
            }
        }

        var size = new FileInfo(zip).Length / (1024f * 1024f);
        Debug.Log($"Windows release: {Path.GetFullPath(zip)} ({size:F0} MB).");
        EditorUtility.RevealInFinder(zip);
    }
}
