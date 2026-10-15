using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

/// <summary>
/// Development tool for Meta Quest 3. Quest runs Android, the same build target as phones, so XR
/// is kept off for Android in the project and switched on only while a Quest build runs:
/// Temnorid → Build Quest APK assigns the OpenXR loader, the Quest quality level, Vulkan and the
/// Quest API level, builds Builds/Quest/TemnoridArena.apk (development, debug key, ready for
/// <c>adb install -r</c>) and puts the phone settings back. A phone APK never starts XR.
///
/// Temnorid → Set Up Quest XR creates what the build needs (XR settings for every target with no
/// loader, OpenXR features for Android, the Quest quality level); the build runs it first, so it
/// is only needed by hand to look at the settings.
/// </summary>
public static class ArenaQuestBuilder
{
    public const string ApkPath = "Builds/Quest/TemnoridArena.apk";
    public const string QualityName = "Quest";
    public const string RenderPipelinePath = "Assets/Settings/Quest_RPAsset.asset";

    private const string OpenXRLoader = "UnityEngine.XR.OpenXR.OpenXRLoader";
    private const string MobileRenderPipelinePath = "Assets/Settings/Mobile_RPAsset.asset";

    // Meta requires Android 12L (API 32) or newer for Quest apps.
    private const AndroidSdkVersions QuestMinSdk = (AndroidSdkVersions)32;

    [MenuItem("Temnorid/Set Up Quest XR")]
    public static void SetUp()
    {
        foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android, BuildTargetGroup.iOS })
        {
            var settings = GeneralSettings(group);
            settings.InitManagerOnStart = true;
            foreach (var loader in settings.AssignedSettings.activeLoaders.ToArray())
            {
                XRPackageMetadataStore.RemoveLoader(settings.AssignedSettings, loader.GetType().FullName, group);
            }
        }

        SetUpOpenXR();
        SetUpQuality();
        AssetDatabase.SaveAssets();
        Debug.Log("[Quest] XR settings ready: no loader on any target (the Quest build adds OpenXR), OpenXR features for Android, quality level Quest.");
    }

    [MenuItem("Temnorid/Build Quest APK")]
    public static void Build()
    {
        SetUp();

        var previousGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        var previousTarget = EditorUserBuildSettings.activeBuildTarget;
        var phone = PhoneSettings.Read();

        Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
        EditorUserBuildSettings.buildAppBundle = false;

        BuildReport report;
        try
        {
            // OpenXR's build validation reads the platform selected in Build Settings.
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
            ApplyQuestSettings();
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            });
        }
        finally
        {
            phone.Apply();
            AssetDatabase.SaveAssets();
        }

        if (report.summary.result == BuildResult.Succeeded && File.Exists(ApkPath))
        {
            var size = new FileInfo(ApkPath).Length / (1024f * 1024f);
            Debug.Log($"Quest APK: {Path.GetFullPath(ApkPath)} ({size:F0} MB). Install: adb install -r \"{ApkPath}\"");
        }
        else
        {
            Debug.LogError($"Quest build {report.summary.result} ({report.summary.totalErrors} errors). Just installed the Android module? Restart the Editor.");
        }

        if (previousTarget != BuildTarget.Android)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(previousGroup, previousTarget);
        }
    }

    /// <summary>Takes OpenXR off Android again, e.g. before a phone build if a Quest build was interrupted.</summary>
    public static void RemoveAndroidLoader()
    {
        var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        if (settings != null && settings.AssignedSettings != null && settings.AssignedSettings.activeLoaders.Count > 0)
        {
            XRPackageMetadataStore.RemoveLoader(settings.AssignedSettings, OpenXRLoader, BuildTargetGroup.Android);
            AssetDatabase.SaveAssets();
        }
    }

    private static void ApplyQuestSettings()
    {
        var settings = GeneralSettings(BuildTargetGroup.Android);
        XRPackageMetadataStore.AssignLoader(settings.AssignedSettings, OpenXRLoader, BuildTargetGroup.Android);
        settings.InitManagerOnStart = true;

        PlayerSettings.Android.minSdkVersion = QuestMinSdk;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        SetDefaultQuality("Android", QualityName);
        AssetDatabase.SaveAssets();
    }

    private static XRGeneralSettings GeneralSettings(BuildTargetGroup group)
    {
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
        {
            Directory.CreateDirectory("Assets/XR");
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        }

        if (!perTarget.HasManagerSettingsForBuildTarget(group))
        {
            perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
        }

        return XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
    }

    private static void SetUpOpenXR()
    {
        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (openXR == null)
        {
            Debug.LogError("[Quest] No OpenXR settings for Android.");
            return;
        }

        openXR.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
        openXR.foveatedRenderingApi = OpenXRSettings.BackendFovationApi.SRPFoveation;

        foreach (var feature in openXR.GetFeatures())
        {
            switch (feature)
            {
                case MetaQuestFeature _:
                case OculusTouchControllerProfile _:
                case MetaQuestTouchPlusControllerProfile _:
                case FoveatedRenderingFeature _:
                    feature.enabled = true;
                    break;
            }

            // The name and address fields open the headset's keyboard.
            if (feature is MetaQuestFeature quest)
            {
                quest.enableSystemKeyboard = true;
                EditorUtility.SetDirty(quest);
            }
        }

        EditorUtility.SetDirty(openXR);
    }

    // A "Quest" quality level like Mobile, with its own URP asset: MSAA 4x, no HDR, short shadows.
    // Included only for Android; the Quest build makes it Android's default while it runs.
    private static void SetUpQuality()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(RenderPipelinePath);
        if (pipeline == null)
        {
            AssetDatabase.CopyAsset(MobileRenderPipelinePath, RenderPipelinePath);
            pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(RenderPipelinePath);
        }

        pipeline.msaaSampleCount = 4;
        pipeline.supportsHDR = false;
        pipeline.shadowDistance = 20f;
        pipeline.renderScale = 1f;
        EditorUtility.SetDirty(pipeline);

        var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels = quality.FindProperty("m_QualitySettings");
        var index = FindLevel(levels, QualityName);
        if (index < 0)
        {
            var mobile = FindLevel(levels, "Mobile");
            levels.InsertArrayElementAtIndex(mobile);
            levels.MoveArrayElement(mobile, levels.arraySize - 1);
            index = levels.arraySize - 1;
            levels.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue = QualityName;
        }

        var level = levels.GetArrayElementAtIndex(index);
        level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
        level.FindPropertyRelative("vSyncCount").intValue = 0;
        level.FindPropertyRelative("antiAliasing").intValue = 4;

        var excluded = level.FindPropertyRelative("excludedTargetPlatforms");
        var platforms = new[] { "Standalone", "iPhone", "WebGL" };
        excluded.arraySize = platforms.Length;
        for (var i = 0; i < platforms.Length; i++)
        {
            excluded.GetArrayElementAtIndex(i).stringValue = platforms[i];
        }

        quality.ApplyModifiedPropertiesWithoutUndo();
    }

    private static int FindLevel(SerializedProperty levels, string name)
    {
        for (var i = 0; i < levels.arraySize; i++)
        {
            if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name)
            {
                return i;
            }
        }

        return -1;
    }

    private static string DefaultQuality(string platform)
    {
        var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var defaults = quality.FindProperty("m_PerPlatformDefaultQuality");
        var levels = quality.FindProperty("m_QualitySettings");
        for (var i = 0; i < defaults.arraySize; i++)
        {
            var entry = defaults.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == platform)
            {
                var level = entry.FindPropertyRelative("second").intValue;
                return level < levels.arraySize ? levels.GetArrayElementAtIndex(level).FindPropertyRelative("name").stringValue : null;
            }
        }

        return null;
    }

    private static void SetDefaultQuality(string platform, string levelName)
    {
        var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var index = FindLevel(quality.FindProperty("m_QualitySettings"), levelName);
        var defaults = quality.FindProperty("m_PerPlatformDefaultQuality");
        for (var i = 0; i < defaults.arraySize; i++)
        {
            var entry = defaults.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == platform && index >= 0)
            {
                entry.FindPropertyRelative("second").intValue = index;
            }
        }

        quality.ApplyModifiedPropertiesWithoutUndo();
    }

    // The Android settings phones use, put back after a Quest build.
    private sealed class PhoneSettings
    {
        private AndroidSdkVersions _minSdk;
        private bool _defaultGraphics;
        private GraphicsDeviceType[] _graphics;
        private UIOrientation _orientation;
        private string _quality;

        public static PhoneSettings Read()
        {
            return new PhoneSettings
            {
                _minSdk = PlayerSettings.Android.minSdkVersion,
                _defaultGraphics = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android),
                _graphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android),
                _orientation = PlayerSettings.defaultInterfaceOrientation,
                _quality = DefaultQuality("Android") ?? "Mobile",
            };
        }

        public void Apply()
        {
            RemoveAndroidLoader();
            PlayerSettings.Android.minSdkVersion = _minSdk;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, _defaultGraphics);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, _graphics);
            PlayerSettings.defaultInterfaceOrientation = _orientation;
            SetDefaultQuality("Android", _quality);
        }
    }
}
