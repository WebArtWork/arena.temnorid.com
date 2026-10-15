using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

/// <summary>
/// Development tool: Temnorid → VR Simulator in Play Mode turns the XR Interaction Toolkit's
/// XR Interaction Simulator on or off. On, Play mode starts with a simulated headset and two
/// controllers driven by keyboard and mouse, so the game takes its VR layout (rig, wrist HUD,
/// world-space menus) without a headset. Editor only; builds never include it. The first use
/// imports the simulator sample into Assets/Samples.
/// </summary>
public static class ArenaVRSimulator
{
    private const string MenuPath = "Temnorid/VR Simulator in Play Mode";
    private const string SampleName = "XR Interaction Simulator";

    [MenuItem(MenuPath)]
    public static void Toggle()
    {
        Set(!IsOn());
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, IsOn());
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    public static bool IsOn()
    {
        var settings = Settings();
        return settings != null && new SerializedObject(settings).FindProperty("m_AutomaticallyInstantiateSimulatorPrefab").boolValue;
    }

    public static void Set(bool on)
    {
        var settings = new SerializedObject(Settings());
        if (on)
        {
            var prefab = SimulatorPrefab();
            if (prefab == null)
            {
                Debug.LogError($"[VR] Could not import the '{SampleName}' sample of the XR Interaction Toolkit.");
                return;
            }

            settings.FindProperty("m_SimulatorPrefab").objectReferenceValue = prefab;
            settings.FindProperty("m_UseClassic").boolValue = false;
        }

        settings.FindProperty("m_AutomaticallyInstantiateSimulatorPrefab").boolValue = on;
        settings.FindProperty("m_AutomaticallyInstantiateInEditorOnly").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log(on
            ? "[VR] Simulator on: Play mode starts with a simulated headset and controllers (see the simulator's on-screen help for keys)."
            : "[VR] Simulator off.");
    }

    // The toolkit keeps its simulator settings type internal; its Instance creates the asset when missing.
    private static Object Settings()
    {
        var type = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulatorSettings, Unity.XR.Interaction.Toolkit");
        var instance = type?.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
        return instance?.GetValue(null) as Object;
    }

    private static GameObject SimulatorPrefab()
    {
        var sample = Sample.FindByPackage("com.unity.xr.interaction.toolkit", string.Empty).FirstOrDefault(s => s.displayName == SampleName);
        if (string.IsNullOrEmpty(sample.displayName))
        {
            return null;
        }

        if (!sample.isImported)
        {
            sample.Import(Sample.ImportOptions.HideImportWindow | Sample.ImportOptions.OverridePreviousImports);
            AssetDatabase.Refresh();
        }

        var path = Path.Combine(sample.importPath, $"{SampleName}.prefab").Replace('\\', '/');
        var projectPath = "Assets" + path.Substring(Application.dataPath.Length);
        return AssetDatabase.LoadAssetAtPath<GameObject>(projectPath);
    }
}
