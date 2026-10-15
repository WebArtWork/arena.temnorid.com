using System.Collections.Generic;
using System.IO;
using ITKamianets.Engine.Assets;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Development tool: turns the delivered art in Assets/Models into what the game uses: stone
/// floor and wall materials, the asset catalog that maps arena asset ids to the imported GLB
/// models, and the mage's animation controller. Safe to run again. Called by
/// <see cref="ArenaSceneBuilder"/>; also Temnorid → Build Arena Art.
/// </summary>
public static class ArenaArtBuilder
{
    public const string CatalogPath = "Assets/Models/ArenaModels.asset";
    public const string FloorMaterialPath = "Assets/Models/materials/StoneFloor.mat";
    public const string WallMaterialPath = "Assets/Models/materials/StoneWall.mat";
    public const string MagePath = "Assets/Models/characters/mage.glb";
    public const string ControllerPath = "Assets/Models/characters/Mage.controller";

    private const string Materials = "Assets/Models/materials";
    private const string ClipFolder = "Assets/Models/characters/mage-clips";

    // Clips that play once. The importer marks every clip as looping, so these get copies.
    private static readonly string[] OneShots = { "jump-start", "land", "cast-projectile", "cast-nova", "cast-shield", "cast-blink", "hit", "death" };

    [MenuItem("Temnorid/Build Arena Art")]
    public static void Build()
    {
        BuildMaterial(FloorMaterialPath, "stone-floor", 2f);
        BuildMaterial(WallMaterialPath, "stone-wall", 4f);
        BuildCatalog();
        BuildController();
        AssetDatabase.SaveAssets();
    }

    // ---- Materials ----

    // URP Lit with the delivered base color and normal map. Scene surfaces have UVs in meters, so
    // tiling is one over the texture's size in meters.
    private static void BuildMaterial(string path, string texture, float metersPerTile)
    {
        var normalPath = $"{Materials}/{texture}_normal.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Materials}/{texture}_basecolor.png");
        var normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        var tiling = new Vector2(1f / metersPerTile, 1f / metersPerTile);

        material.SetTexture("_BaseMap", baseMap);
        material.SetTextureScale("_BaseMap", tiling);
        material.mainTexture = baseMap;
        material.mainTextureScale = tiling;
        material.SetColor("_BaseColor", Color.white);
        if (normalMap != null)
        {
            material.SetTexture("_BumpMap", normalMap);
            material.EnableKeyword("_NORMALMAP");
        }

        // Rough stone; the delivered roughness map is not in URP's packing (smoothness in alpha).
        material.SetFloat("_Smoothness", 0.15f);
        material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);
    }

    // ---- Asset catalog ----

    private static void BuildCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AssetCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<AssetCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.Entries.Clear();
        foreach (var file in Directory.GetFiles("Assets/Models/arena", "*.glb"))
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
            var name = Path.GetFileNameWithoutExtension(file);

            // arena-pillar.glb -> arena/pillar
            var id = "arena/" + (name.StartsWith("arena-") ? name.Substring("arena-".Length) : name);
            catalog.Entries.Add(new AssetCatalog.Entry { Id = id, Asset = model });
        }

        catalog.Entries.Add(new AssetCatalog.Entry { Id = "characters/mage", Asset = AssetDatabase.LoadAssetAtPath<GameObject>(MagePath) });
        EditorUtility.SetDirty(catalog);
    }

    // ---- Mage animation ----

    private static Dictionary<string, AnimationClip> MageClips()
    {
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(MagePath))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
            {
                clips[clip.name] = clip;
            }
        }

        if (!AssetDatabase.IsValidFolder(ClipFolder))
        {
            AssetDatabase.CreateFolder("Assets/Models/characters", "mage-clips");
        }

        foreach (var name in OneShots)
        {
            var copyPath = $"{ClipFolder}/{name}.anim";
            AssetDatabase.DeleteAsset(copyPath);
            var copy = Object.Instantiate(clips[name]);
            copy.name = name;
            var settings = AnimationUtility.GetAnimationClipSettings(copy);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            AssetDatabase.CreateAsset(copy, copyPath);
            clips[name] = copy;
        }

        return clips;
    }

    private static void BuildController()
    {
        var clips = MageClips();
        AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        foreach (var name in new[] { "Forward", "Right", "Speed", "VerticalSpeed" })
        {
            controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }

        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Crouching", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        foreach (var name in new[] { "CastProjectile", "CastNova", "CastShield", "CastBlink", "Hit" })
        {
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        }

        // Grounded starts true, so a character standing still does not begin in the air.
        var parameters = controller.parameters;
        foreach (var parameter in parameters)
        {
            if (parameter.name == "Grounded")
            {
                parameter.defaultBool = true;
            }
        }

        controller.parameters = parameters;

        BuildMovementLayer(controller, clips);
        BuildActionLayer(controller, clips);
        EditorUtility.SetDirty(controller);
    }

    private static void BuildMovementLayer(AnimatorController controller, Dictionary<string, AnimationClip> clips)
    {
        var machine = controller.layers[0].stateMachine;

        // Walking and running in any direction, by velocity relative to the facing.
        var locomotion = controller.CreateBlendTreeInController("Locomotion", out var move, 0);
        move.blendType = BlendTreeType.FreeformDirectional2D;
        move.blendParameter = "Right";
        move.blendParameterY = "Forward";
        move.AddChild(clips["idle"], new Vector2(0f, 0f));
        move.AddChild(clips["walk"], new Vector2(0f, 4f));
        move.AddChild(clips["run"], new Vector2(0f, 6.5f));
        move.AddChild(clips["walk-back"], new Vector2(0f, -3f));
        move.AddChild(clips["strafe-left"], new Vector2(-4f, 0f));
        move.AddChild(clips["strafe-right"], new Vector2(4f, 0f));

        var crouch = controller.CreateBlendTreeInController("Crouch", out var crouchTree, 0);
        crouchTree.blendType = BlendTreeType.Simple1D;
        crouchTree.blendParameter = "Speed";
        crouchTree.AddChild(clips["crouch-idle"], 0f);
        crouchTree.AddChild(clips["crouch-walk"], 2f);

        var jump = machine.AddState("JumpStart");
        jump.motion = clips["jump-start"];
        var fall = machine.AddState("Fall");
        fall.motion = clips["fall"];
        var land = machine.AddState("Land");
        land.motion = clips["land"];
        var death = machine.AddState("Death");
        death.motion = clips["death"];

        machine.defaultState = locomotion;

        Transition(locomotion, crouch, 0.15f).AddCondition(AnimatorConditionMode.If, 0f, "Crouching");
        Transition(crouch, locomotion, 0.15f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Crouching");

        foreach (var ground in new[] { locomotion, crouch })
        {
            Transition(ground, jump, 0.05f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
        }

        var toFall = Transition(jump, fall, 0.1f);
        toFall.hasExitTime = true;
        toFall.exitTime = 0.9f;
        Transition(jump, land, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        Transition(fall, land, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        var landed = Transition(land, locomotion, 0.15f);
        landed.hasExitTime = true;
        landed.exitTime = 0.7f;

        var die = machine.AddAnyStateTransition(death);
        die.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
        die.duration = 0.1f;
        die.canTransitionToSelf = false;
        Transition(death, locomotion, 0f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
    }

    // Casts and the hit reaction on the upper body only, so legs keep walking.
    private static void BuildActionLayer(AnimatorController controller, Dictionary<string, AnimationClip> clips)
    {
        controller.AddLayer("Actions");
        var layers = controller.layers;
        layers[1].defaultWeight = 1f;
        layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
        layers[1].avatarMask = UpperBodyMask();
        var machine = layers[1].stateMachine;

        var empty = machine.AddState("Empty");
        machine.defaultState = empty;

        foreach (var (trigger, clip) in new[] { ("CastProjectile", "cast-projectile"), ("CastNova", "cast-nova"), ("CastShield", "cast-shield"), ("CastBlink", "cast-blink"), ("Hit", "hit") })
        {
            var state = machine.AddState(trigger);
            state.motion = clips[clip];
            var start = machine.AddAnyStateTransition(state);
            start.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            start.duration = 0.05f;
            start.canTransitionToSelf = true;
            var done = Transition(state, empty, 0.15f);
            done.hasExitTime = true;
            done.exitTime = 0.85f;
        }

        controller.layers = layers;
    }

    // The spine and everything above it (arms, head) of the mage's skeleton.
    private static AvatarMask UpperBodyMask()
    {
        var maskPath = "Assets/Models/characters/MageUpperBody.mask";
        AssetDatabase.DeleteAsset(maskPath);
        var mask = new AvatarMask();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(MagePath);
        mask.AddTransformPath(model.transform, true);
        for (var i = 0; i < mask.transformCount; i++)
        {
            mask.SetTransformActive(i, mask.GetTransformPath(i).Contains("Spine"));
        }

        AssetDatabase.CreateAsset(mask, maskPath);
        return mask;
    }

    private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        return transition;
    }
}
