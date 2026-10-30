using System.Collections.Generic;
using ITKamianets.Engine.AI;
using ITKamianets.Engine.Character;
using ITKamianets.Engine.CharacterView;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Feedback;
using ITKamianets.Engine.Graybox;
using ITKamianets.Engine.Graybox.EditorTools;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Temnorid.Arena.EditorTools
{
	/// <summary>
	/// Builds the whole game: the stone courtyard from the delivered GLB props (walls, pillars, crates, a raised
	/// platform with stairs, braziers, banners, spawn circles), six fighters on the mage model, camera, light and
	/// screens. Run it from the menu (Temnorid > Arena > Build Scene) or the command line
	/// (<c>-executeMethod Temnorid.Arena.EditorTools.ArenaSceneBuilder.Build</c>); it rebuilds
	/// <c>Assets/Scenes/Main.unity</c> from scratch each time, so do not edit that scene by hand.
	/// Every prop is placed by its asset id (<c>arena/pillar</c>); the colliders are plain boxes, and the NavMesh is baked
	/// from them when the game starts.
	/// </summary>
	public static class ArenaSceneBuilder
	{
		const string ScenePath = "Assets/Scenes/Main.unity";
		const string MaterialDir = "Assets/Materials";
		const string ModelDir = "Assets/Models/arena/";
		const string MagePath = "Assets/Models/characters/mage.glb";
		const string ControllerPath = "Assets/Models/characters/Mage.controller";
		const string FloorMaterialPath = "Assets/Models/materials/StoneFloor.mat";
		const float Half = 20f; // the courtyard is 40 by 40 metres

		[MenuItem("Temnorid/Arena/Build Scene")]
		public static void Build()
		{
			PlayerSettings.productName = "Temnorid Arena";
			PlayerSettings.companyName = "Temnorid";
			PlayerSettings.bundleVersion = "0.2.0";
			PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.itkamianets.arena");
			PlayerSettings.runInBackground = true;
			PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
			RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.6f);

			var courtyard = BuildCourtyard(out var surface, out var spawns);
			var fighters = BuildFighters(spawns);
			var camera = BuildCameraAndLight();

			var gameObject = new GameObject("Game");
			var game = gameObject.AddComponent<ArenaGame>();
			game.Fighters = fighters;
			game.Spawns = spawns;
			game.Surface = surface;
			game.ViewCamera = camera;
			gameObject.AddComponent<ArenaHud>().Game = game;

			EditorSceneManager.SaveScene(scene, ScenePath);
			EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
			AssetDatabase.SaveAssets();
			Debug.Log("[Arena] Scene built: " + ScenePath + " (" + courtyard.name + ", " + fighters.Length + " fighters, " + spawns.Length + " spawns)");
		}

		// ---- the courtyard ----

		static Transform BuildCourtyard(out NavMeshSurface surface, out Transform[] spawns)
		{
			var root = new GameObject("Courtyard");
			surface = root.AddComponent<NavMeshSurface>();
			surface.collectObjects = CollectObjects.Children;
			surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
			var t = root.transform;

			// The floor: the delivered stone material, tiled so one tile is two metres.
			var floorMaterial = TiledFloorMaterial();
			GrayboxBuilder.Box(t, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(Half * 2f + 1f, 0.5f, Half * 2f + 1f), floorMaterial);

			// The wall: segments side by side along each edge, front towards the middle.
			var wallPrefab = Load("arena-wall-segment");
			var wallSize = MeasureSize(wallPrefab);
			var count = Mathf.Max(1, Mathf.RoundToInt(Half * 2f / wallSize.x));
			var width = Half * 2f / count;
			var walls = new GameObject("Walls").transform;
			walls.SetParent(t, false);
			for (var i = 0; i < count; i++)
			{
				var along = -Half + width * (i + 0.5f);
				Prop(walls, wallPrefab, "arena/wall-segment", new Vector3(along, 0f, -Half), Quaternion.identity, true);
				Prop(walls, wallPrefab, "arena/wall-segment", new Vector3(along, 0f, Half), Quaternion.Euler(0f, 180f, 0f), true);
				Prop(walls, wallPrefab, "arena/wall-segment", new Vector3(-Half, 0f, along), Quaternion.Euler(0f, 90f, 0f), true);
				Prop(walls, wallPrefab, "arena/wall-segment", new Vector3(Half, 0f, along), Quaternion.Euler(0f, -90f, 0f), true);
			}

			var props = new GameObject("Props").transform;
			props.SetParent(t, false);

			// The raised platform in the middle, with a stair on two sides. The stairs look like stairs; the collider is a smooth ramp.
			var platform = Load("arena-platform");
			Prop(props, platform, "arena/platform", Vector3.zero, Quaternion.identity, true);
			var platformSize = MeasureSize(platform);
			var stairs = Load("arena-stairs");
			var stairSize = MeasureSize(stairs);
			foreach (var side in new[] { 1f, -1f })
			{
				var stair = Prop(props, stairs, "arena/stairs", new Vector3(0f, 0f, side * (platformSize.z * 0.5f + stairSize.z * 0.5f)), Quaternion.Euler(0f, side > 0f ? 0f : 180f, 0f), false);
				var ramp = new GameObject("Ramp");
				ramp.transform.SetParent(stair.transform, false);
				var run = Mathf.Sqrt(stairSize.z * stairSize.z + stairSize.y * stairSize.y);
				ramp.transform.localPosition = new Vector3(0f, stairSize.y * 0.5f - 0.05f, 0f);
				ramp.transform.localRotation = Quaternion.Euler(Mathf.Atan2(stairSize.y, stairSize.z) * Mathf.Rad2Deg, 0f, 0f);
				var box = ramp.AddComponent<BoxCollider>();
				box.size = new Vector3(stairSize.x, 0.1f, run);
			}

			var pillar = Load("arena-pillar");
			foreach (var x in new[] { -10f, 10f })
				foreach (var z in new[] { -10f, 10f })
					Prop(props, pillar, "arena/pillar", new Vector3(x, 0f, z), Quaternion.identity, true);

			var crate = Load("arena-crate");
			foreach (var spot in new[] { new Vector3(-13f, 0f, -3f), new Vector3(-13f, 0f, -1.7f), new Vector3(13f, 0f, 3f), new Vector3(13f, 0f, 1.7f), new Vector3(3f, 0f, -13f), new Vector3(1.7f, 0f, -13f), new Vector3(-3f, 0f, 13f), new Vector3(-1.7f, 0f, 13f) })
				Prop(props, crate, "arena/crate", spot, Quaternion.Euler(0f, spot.x * 7f, 0f), true);

			var lowWall = Load("arena-low-wall");
			foreach (var x in new[] { -7f, 7f })
				foreach (var z in new[] { -13f, 13f })
					Prop(props, lowWall, "arena/low-wall", new Vector3(x, 0f, z), Quaternion.identity, true);
			foreach (var x in new[] { -13f, 13f })
				foreach (var z in new[] { -7f, 7f })
					Prop(props, lowWall, "arena/low-wall", new Vector3(x, 0f, z), Quaternion.Euler(0f, 90f, 0f), true);

			var broken = Load("arena-broken-pillar");
			foreach (var x in new[] { -16f, 16f })
				foreach (var z in new[] { -16f, 16f })
					Prop(props, broken, "arena/broken-pillar", new Vector3(x, 0f, z), Quaternion.Euler(0f, x * z, 0f), true);

			var rubble = Load("arena-rubble");
			foreach (var spot in new[] { new Vector3(-5f, 0f, -17f), new Vector3(5f, 0f, 17f), new Vector3(-17f, 0f, 5f), new Vector3(17f, 0f, -5f) })
				Prop(props, rubble, "arena/rubble", spot, Quaternion.Euler(0f, spot.x * 11f, 0f), true);

			// Braziers in the corners, each with a warm light.
			var brazier = Load("arena-brazier");
			foreach (var x in new[] { -18f, 18f })
				foreach (var z in new[] { -18f, 18f })
				{
					var lit = Prop(props, brazier, "arena/brazier", new Vector3(x, 0f, z), Quaternion.identity, true);
					var light = new GameObject("Flame");
					light.transform.SetParent(lit.transform, false);
					light.transform.localPosition = new Vector3(0f, 1.6f, 0f);
					var lamp = light.AddComponent<Light>();
					lamp.type = LightType.Point;
					lamp.range = 16f;
					lamp.intensity = 2.2f;
					lamp.color = new Color(1f, 0.65f, 0.35f);
					lamp.shadows = LightShadows.None;
				}

			// Banners hanging on the middle of each wall.
			var banner = Load("arena-banner");
			Prop(props, banner, "arena/banner", new Vector3(0f, 2.2f, -Half + 0.45f), Quaternion.identity, false);
			Prop(props, banner, "arena/banner", new Vector3(0f, 2.2f, Half - 0.45f), Quaternion.Euler(0f, 180f, 0f), false);
			Prop(props, banner, "arena/banner", new Vector3(-Half + 0.45f, 2.2f, 0f), Quaternion.Euler(0f, 90f, 0f), false);
			Prop(props, banner, "arena/banner", new Vector3(Half - 0.45f, 2.2f, 0f), Quaternion.Euler(0f, -90f, 0f), false);

			// Where fighters start and come back: eight circles on a ring, each facing the middle.
			var circle = Load("arena-spawn-circle");
			var spawnRoot = new GameObject("Spawns").transform;
			spawnRoot.SetParent(t, false);
			var list = new List<Transform>();
			for (var i = 0; i < 8; i++)
			{
				var angle = (22.5f + 45f * i) * Mathf.Deg2Rad;
				var position = new Vector3(Mathf.Cos(angle) * 15f, 0f, Mathf.Sin(angle) * 15f);
				var rotation = Quaternion.LookRotation(-position.normalized);
				Prop(spawnRoot, circle, "arena/spawn-circle", position + Vector3.up * 0.02f, rotation, false);
				var spawn = new GameObject("Spawn_" + i).transform;
				spawn.SetParent(spawnRoot, false);
				spawn.SetPositionAndRotation(position, rotation);
				list.Add(spawn);
			}
			spawns = list.ToArray();
			return t;
		}

		static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + name + ".glb");

		/// <summary>A prop from its delivered model, tagged with its asset id; with a box collider the size of the model when <paramref name="solid"/>.</summary>
		static GameObject Prop(Transform parent, GameObject model, string assetId, Vector3 position, Quaternion rotation, bool solid)
		{
			var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
			instance.transform.SetLocalPositionAndRotation(position, rotation);
			instance.AddComponent<AssetIdTag>().Id = assetId;
			if (solid)
			{
				var size = MeasureSize(model, out var center);
				var box = instance.AddComponent<BoxCollider>();
				box.center = center;
				box.size = size;
			}
			return instance;
		}

		static Vector3 MeasureSize(GameObject model) => MeasureSize(model, out _);

		/// <summary>The size of a model's shape in metres, and the middle of it relative to its feet.</summary>
		static Vector3 MeasureSize(GameObject model, out Vector3 center)
		{
			var temp = (GameObject)PrefabUtility.InstantiatePrefab(model);
			var bounds = new Bounds();
			var first = true;
			foreach (var renderer in temp.GetComponentsInChildren<Renderer>())
			{
				if (first) bounds = renderer.bounds;
				else bounds.Encapsulate(renderer.bounds);
				first = false;
			}
			Object.DestroyImmediate(temp);
			center = bounds.center;
			return bounds.size;
		}

		static Material TiledFloorMaterial()
		{
			if (!AssetDatabase.IsValidFolder(MaterialDir)) AssetDatabase.CreateFolder("Assets", "Materials");
			var path = MaterialDir + "/ArenaFloor.mat";
			var source = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
			var material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material == null)
			{
				material = new Material(source);
				AssetDatabase.CreateAsset(material, path);
			}
			material.mainTextureScale = new Vector2(Half, Half); // 40 metres / 2 metres per tile
			EditorUtility.SetDirty(material);
			return material;
		}

		// ---- the fighters ----

		static Fighter[] BuildFighters(Transform[] spawns)
		{
			var root = new GameObject("Fighters").transform;
			var roster = new[]
			{
				(HeroClass.Warrior, "Gorm"), (HeroClass.Warrior, "Brann"),
				(HeroClass.Archer, "Sylva"), (HeroClass.Archer, "Fenn"),
				(HeroClass.Mage, "Ilyra"), (HeroClass.Mage, "Morven"),
			};
			var fighters = new Fighter[roster.Length];
			for (var i = 0; i < roster.Length; i++)
				fighters[i] = BuildFighter(root, roster[i].Item1, roster[i].Item2, i, spawns[(i * 3) % spawns.Length]);
			return fighters;
		}

		static Fighter BuildFighter(Transform parent, HeroClass fighterClass, string name, int index, Transform spawn)
		{
			var definition = Classes.Get(fighterClass);
			var root = new GameObject("Fighter_" + name);
			root.transform.SetParent(parent, false);
			root.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
			root.AddComponent<AssetIdTag>().Id = "characters/" + definition.Name.ToLowerInvariant();

			var controller = root.AddComponent<CharacterController>();
			controller.height = 1.8f;
			controller.radius = 0.4f;
			controller.center = new Vector3(0f, 0.9f, 0f);

			var agent = root.AddComponent<NavMeshAgent>();
			agent.speed = definition.BotSpeed;
			agent.acceleration = 16f;
			agent.angularSpeed = 540f;
			agent.radius = 0.4f;
			agent.height = 1.8f;
			agent.stoppingDistance = 1.2f;
			agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

			var health = root.AddComponent<Health>();
			health.MaxHealth = definition.Health;
			health.Team = index + 1; // everyone fights everyone: each fighter is a team of one
			health.Restore();

			root.AddComponent<AiTarget>().Team = index + 1;

			var ai = root.AddComponent<AiAgent>();
			ai.Team = index + 1;
			ai.SenseRadius = 80f;
			ai.LoseRadius = 120f;
			ai.AttackRange = definition.Kind == AttackKind.Melee ? 2.3f : definition.Range * 0.8f;
			ai.AttackCooldown = definition.Cooldown;
			ai.AttackNeedsSight = definition.Kind != AttackKind.Melee;
			ai.MinDistance = definition.MinDistance;
			ai.TickInterval = 0.15f;

			// The Fighter comes first: the player's control requires one, and a second would be added.
			var fighter = root.AddComponent<Fighter>();
			fighter.Class = fighterClass;
			fighter.DisplayName = name;

			root.AddComponent<CharacterMotor>();
			root.AddComponent<CharacterViewControls>();
			root.AddComponent<PlayerControl>();
			root.AddComponent<HeroBadge>().Height = 3.0f;
			root.AddComponent<HealthBar3D>().Height = 2.4f;

			// The body: the delivered mage, driven by its animator, with a class colour and a weapon in primitives.
			var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MagePath), root.transform);
			model.name = "Model";
			model.transform.localPosition = Vector3.zero;
			model.transform.localRotation = Quaternion.identity;
			var animator = model.GetComponent<Animator>();
			animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
			animator.applyRootMotion = false;
			animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
			var motion = root.AddComponent<FighterAnimation>();
			motion.Animator = animator;

			var tint = GrayboxBuilder.Mat("Class_" + definition.Name, definition.Tint, MaterialDir, 0.2f);
			var steel = GrayboxBuilder.Mat("Steel", new Color(0.78f, 0.8f, 0.86f), MaterialDir, 0.5f);
			var wood = GrayboxBuilder.Mat("Wood", new Color(0.45f, 0.3f, 0.18f), MaterialDir, 0.1f);
			var body = new GameObject("ClassGear").transform;
			body.SetParent(root.transform, false);
			GrayboxBuilder.Prim(PrimitiveType.Cube, "Sash", body, new Vector3(0f, 1.15f, 0.2f), new Vector3(0.62f, 0.14f, 0.38f), tint);
			switch (definition.Kind)
			{
				case AttackKind.Melee:
					GrayboxBuilder.Prim(PrimitiveType.Cube, "Blade", body, new Vector3(0.5f, 1.05f, 0.45f), new Vector3(0.08f, 0.08f, 1.2f), steel);
					GrayboxBuilder.Prim(PrimitiveType.Cube, "Guard", body, new Vector3(0.5f, 1.05f, -0.1f), new Vector3(0.28f, 0.08f, 0.1f), wood);
					GrayboxBuilder.Prim(PrimitiveType.Cube, "ShoulderL", body, new Vector3(-0.36f, 1.5f, 0f), new Vector3(0.28f, 0.16f, 0.34f), tint);
					GrayboxBuilder.Prim(PrimitiveType.Cube, "ShoulderR", body, new Vector3(0.36f, 1.5f, 0f), new Vector3(0.28f, 0.16f, 0.34f), tint);
					break;
				case AttackKind.Arrow:
					GrayboxBuilder.Prim(PrimitiveType.Cube, "BowGrip", body, new Vector3(-0.5f, 1.1f, 0.35f), new Vector3(0.05f, 0.9f, 0.05f), wood);
					var top = GrayboxBuilder.Prim(PrimitiveType.Cube, "BowTop", body, new Vector3(-0.5f, 1.6f, 0.3f), new Vector3(0.05f, 0.5f, 0.05f), wood);
					top.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
					var bottom = GrayboxBuilder.Prim(PrimitiveType.Cube, "BowBottom", body, new Vector3(-0.5f, 0.6f, 0.3f), new Vector3(0.05f, 0.5f, 0.05f), wood);
					bottom.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
					GrayboxBuilder.Prim(PrimitiveType.Cube, "Quiver", body, new Vector3(0.1f, 1.2f, -0.28f), new Vector3(0.14f, 0.5f, 0.14f), tint);
					break;
				default:
					GrayboxBuilder.Prim(PrimitiveType.Cylinder, "Staff", body, new Vector3(0.5f, 0.95f, 0.35f), new Vector3(0.05f, 0.95f, 0.05f), wood);
					GrayboxBuilder.Prim(PrimitiveType.Sphere, "Orb", body, new Vector3(0.5f, 1.95f, 0.35f), Vector3.one * 0.22f, tint);
					break;
			}

			root.SetActive(false); // woken by the game once the NavMesh exists
			return fighter;
		}

		// ---- camera and light ----

		static CharacterViewCamera BuildCameraAndLight()
		{
			var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
			var camera = cameraObject.AddComponent<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = new Color(0.12f, 0.13f, 0.18f);
			camera.fieldOfView = 68f;
			camera.nearClipPlane = 0.1f;
			camera.farClipPlane = 200f;
			cameraObject.AddComponent<AudioListener>();
			var view = cameraObject.AddComponent<CharacterViewCamera>();

			var serialized = new SerializedObject(view);
			serialized.FindProperty("settings.StartView").enumValueIndex = (int)CameraView.ThirdPerson;
			serialized.FindProperty("settings.Distance").floatValue = 4.6f;
			serialized.ApplyModifiedPropertiesWithoutUndo();

			// Before a class is chosen the camera looks down on the courtyard from above one wall.
			cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 17f, -30f), Quaternion.Euler(30f, 0f, 0f));

			var sun = new GameObject("Moonlight");
			sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
			var light = sun.AddComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = 0.8f;
			light.color = new Color(0.8f, 0.85f, 1f);
			light.shadows = LightShadows.Soft;
			return view;
		}
	}
}
