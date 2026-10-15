using System.Collections.Generic;
using ITKamianets.Engine.Audio;
using ITKamianets.Engine.Character;
using ITKamianets.Engine.CharacterView;
using ITKamianets.Engine.Network;
using ITKamianets.Engine.VR;
using ITKamianets.Engine.XR;
using Temnorid.Arena.Core;
using Temnorid.Arena.Hud;
using Temnorid.Arena.Maps;
using Temnorid.Arena.Menus;
using Temnorid.Arena.Spells;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Development tool: builds the playable Temnorid Arena setup from the packages: the player and
/// bot prefabs and the Arena scene (made the first build scene). Safe to run again; it replaces
/// what it made before. Temnorid → Build Arena Scene.
/// </summary>
public static class ArenaSceneBuilder
{
    private const string PlayerPath = "Assets/Prefabs/ArenaPlayer.prefab";
    private const string BotPath = "Assets/Prefabs/ArenaBot.prefab";
    private const string MatchPath = "Assets/Prefabs/ArenaMatch.prefab";
    private const string ScenePath = "Assets/Scenes/Arena.unity";
    private const string ArenaJson = "Packages/com.itkamianets.temnorid-arena-maps/Runtime/Arenas/first-arena.scene.json";
    private const string PropsJson = "Packages/com.itkamianets.temnorid-arena-maps/Runtime/Arenas/arena-props.library.json";
    private const string FirebaseConfig = "Assets/Firebase/google-services.json";

    [MenuItem("Temnorid/Build Arena Scene")]
    public static void Build()
    {
        ArenaArtBuilder.Build();
        BuildPlayer();
        BuildBot();
        BuildMatch();
        BuildScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[Arena] Built ArenaPlayer, ArenaBot and the Arena scene.");
    }

    [MenuItem("Temnorid/Build Arena Prefabs")]
    public static void BuildPrefabs()
    {
        BuildPlayer();
        BuildBot();
        AssetDatabase.SaveAssets();
    }

    private static void BuildPlayer()
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        var player = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        player.name = "ArenaPlayer";

        var combatant = player.AddComponent<Combatant>();
        var caster = player.AddComponent<SpellCaster>();
        player.AddComponent<FootstepPlayer>();

        // The mage replaces the base prefab's capsule body.
        player.transform.Find("Body").gameObject.SetActive(false);
        player.transform.Find("Nose").gameObject.SetActive(false);
        AddMage(player);

        // Flat screens: the camera-relative controls. VR: the XR rig, VR controls and the casting
        // hand. The switch picks one set on the owner's copy; other copies run neither.
        var flat = player.GetComponent<CharacterViewControls>();
        var rig = player.AddComponent<XRCharacterRig>();
        var vr = player.AddComponent<VRControls>();
        var aim = player.AddComponent<HandAim>();
        player.AddComponent<SpellAimVisual>();
        var mode = player.AddComponent<XRModeSwitch>();
        flat.enabled = rig.enabled = vr.enabled = aim.enabled = mode.enabled = false;
        Set(mode, "vrOnly", new Object[] { rig, vr, aim });
        Set(mode, "flatOnly", new Object[] { flat });
        Set(player.GetComponent<NetworkCharacter>(), "ownerOnly", new Object[] { mode });

        Set(combatant, "disableWhenDead", new Object[] { flat, vr, caster });
        Set(combatant, "hideBodyWhenDead", false);

        PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
        Object.DestroyImmediate(player);
    }

    private static void BuildBot()
    {
        var bot = new GameObject("ArenaBot");
        bot.AddComponent<CharacterController>();
        bot.AddComponent<CharacterMotor>();
        bot.AddComponent<NetworkObject>();
        bot.AddComponent<NetworkCharacter>();
        var combatant = bot.AddComponent<Combatant>();
        var caster = bot.AddComponent<SpellCaster>();
        var brain = bot.AddComponent<SpellBot>();
        bot.AddComponent<FootstepPlayer>();

        Set(combatant, "isBot", true);
        Set(combatant, "disableWhenDead", new Object[] { brain });
        Set(caster, "playerControlled", false);
        Set(combatant, "hideBodyWhenDead", false);
        AddMage(bot);

        PrefabUtility.SaveAsPrefabAsset(bot, BotPath);
        Object.DestroyImmediate(bot);
    }

    // The mage model with its animation controller, driven by CharacterAnimator (movement) and
    // ArenaCharacterVisuals (casts, hits, death, accent color).
    private static void AddMage(GameObject character)
    {
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ArenaArtBuilder.MagePath));
        model.name = "Model";
        model.transform.SetParent(character.transform, false);
        var animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ArenaArtBuilder.ControllerPath);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        character.AddComponent<CharacterAnimator>();
        character.AddComponent<ArenaCharacterVisuals>();
    }

    // Spawned at runtime by ArenaMatchSpawner: a scene-placed NetworkObject needs editor-made ids.
    private static void BuildMatch()
    {
        var match = new GameObject("ArenaMatch");
        match.AddComponent<NetworkObject>();
        var arenaMatch = match.AddComponent<ArenaMatch>();
        Set(arenaMatch, "botPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(BotPath));
        Set(arenaMatch, "sceneId", "first-arena");
        PrefabUtility.SaveAsPrefabAsset(match, MatchPath);
        Object.DestroyImmediate(match);
    }

    private static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var camera = Camera.main;
        camera.nearClipPlane = 0.05f;
        camera.gameObject.AddComponent<CharacterViewCamera>();
        camera.gameObject.AddComponent<XRHeadDriver>();

        var session = new GameObject("NetworkSession").AddComponent<NetworkSession>();
        Set(session, "playerPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
        Set(session, "networkPrefabs", new Object[] { AssetDatabase.LoadAssetAtPath<GameObject>(BotPath), AssetDatabase.LoadAssetAtPath<GameObject>(MatchPath) });
        Set(session, "mode", ArenaEvents.Mode);

        var map = new GameObject("Arena map").AddComponent<ArenaMap>();
        Set(map, "arena", AssetDatabase.LoadAssetAtPath<TextAsset>(ArenaJson));
        Set(map, "assetLibraries", new Object[] { AssetDatabase.LoadAssetAtPath<TextAsset>(PropsJson) });
        Set(map, "catalogs", new Object[] { AssetDatabase.LoadAssetAtPath<ITKamianets.Engine.Assets.AssetCatalog>(ArenaArtBuilder.CatalogPath) });
        Set(map, "floorMaterial", AssetDatabase.LoadAssetAtPath<Material>(ArenaArtBuilder.FloorMaterialPath));
        Set(map, "wallMaterial", AssetDatabase.LoadAssetAtPath<Material>(ArenaArtBuilder.WallMaterialPath));

        var spawner = new GameObject("Arena match").AddComponent<ArenaMatchSpawner>();
        Set(spawner, "matchPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(MatchPath));

        new GameObject("HUD").AddComponent<ArenaHud>();

        var menus = new GameObject("Menus").AddComponent<ArenaMenus>();
        Set(menus, "firebaseConfig", AssetDatabase.LoadAssetAtPath<TextAsset>(FirebaseConfig));

        EditorSceneManager.SaveScene(scene, ScenePath);

        var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        foreach (var existing in EditorBuildSettings.scenes)
        {
            if (existing.path != ScenePath)
            {
                scenes.Add(existing);
            }
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void Set(Object target, string field, object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(field);
        switch (value)
        {
            case Object[] list:
                property.arraySize = list.Length;
                for (var i = 0; i < list.Length; i++)
                {
                    property.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
                }

                break;
            case Object reference:
                property.objectReferenceValue = reference;
                break;
            case bool flag:
                property.boolValue = flag;
                break;
            case string text:
                property.stringValue = text;
                break;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
