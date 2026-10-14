using System.Collections.Generic;
using ITKamianets.Engine.Audio;
using ITKamianets.Engine.Character;
using ITKamianets.Engine.CharacterView;
using ITKamianets.Engine.Network;
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

        Set(combatant, "disableWhenDead", new Object[] { player.GetComponent<CharacterViewControls>(), caster });

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

        const string materialPath = "Assets/Prefabs/ArenaBot.mat";
        var red = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (red == null)
        {
            red = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.75f, 0.2f, 0.2f) };
            red.SetColor("_BaseColor", red.color);
            AssetDatabase.CreateAsset(red, materialPath);
        }

        Part(bot, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), red);
        Part(bot, PrimitiveType.Cube, "Nose", new Vector3(0f, 1.5f, 0.35f), new Vector3(0.15f, 0.15f, 0.3f), red);

        PrefabUtility.SaveAsPrefabAsset(bot, BotPath);
        Object.DestroyImmediate(bot);
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

        var session = new GameObject("NetworkSession").AddComponent<NetworkSession>();
        Set(session, "playerPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
        Set(session, "networkPrefabs", new Object[] { AssetDatabase.LoadAssetAtPath<GameObject>(BotPath), AssetDatabase.LoadAssetAtPath<GameObject>(MatchPath) });
        Set(session, "mode", ArenaEvents.Mode);

        var map = new GameObject("Arena map").AddComponent<ArenaMap>();
        Set(map, "arena", AssetDatabase.LoadAssetAtPath<TextAsset>(ArenaJson));
        Set(map, "assetLibraries", new Object[] { AssetDatabase.LoadAssetAtPath<TextAsset>(PropsJson) });

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

    private static void Part(GameObject parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent.transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
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
