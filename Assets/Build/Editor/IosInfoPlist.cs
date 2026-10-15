using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

/// <summary>
/// Adds what Temnorid Arena needs to the iOS app's Info.plist after Unity exports the Xcode project:
/// the Local Network prompt text (joining or hosting a game on Wi-Fi), and the export compliance
/// answer (only standard HTTPS encryption), so App Store Connect does not ask on every upload.
///
/// Edits the plist as text rather than with UnityEditor.iOS.Xcode, so it needs no iOS-only code
/// and runs whatever platform the Editor was on when the build started.
/// </summary>
public static class IosInfoPlist
{
    private static readonly (string Key, string Value)[] Entries =
    {
        ("NSLocalNetworkUsageDescription", "<string>Temnorid Arena connects to matches hosted by players on your network.</string>"),
        ("ITSAppUsesNonExemptEncryption", "<false/>"),
    };

    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        var plistPath = Path.Combine(path, "Info.plist");
        var plist = File.ReadAllText(plistPath);
        var end = plist.LastIndexOf("</dict>", System.StringComparison.Ordinal);
        if (end < 0)
        {
            Debug.LogError($"Info.plist has no top-level dict: {plistPath}");
            return;
        }

        var added = string.Empty;
        foreach (var (key, value) in Entries)
        {
            if (!plist.Contains($"<key>{key}</key>"))
            {
                added += $"    <key>{key}</key>\n    {value}\n";
            }
        }

        File.WriteAllText(plistPath, plist.Insert(end, added));
    }
}
