using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Batch validation and actual Unity renders for every starter spell.</summary>
public static class ElementalSpellReview
{
    public static void Validate()
    {
        Directory.CreateDirectory("output/spells");
        if (File.Exists("output/spells/validation.txt")) File.Delete("output/spells/validation.txt");
        FireSpellReview.Validate();
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var shader = Resources.Load<Shader>("TemnoridElemental");
            if (shader == null || !shader.isSupported) throw new Exception("Elemental shader unavailable");
            var type = typeof(Temnorid.Arena.Spells.SpellCaster).Assembly.GetType("Temnorid.Arena.Spells.ElementalSpellVisual", true);
            var cameraObject = new GameObject("Spell review camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.03f, 0.04f);
            var owner = new GameObject("Review caster");
            SceneManager.MoveGameObjectToScene(owner, scene);
            foreach (var name in new[] { "frost-nova", "arcane-shield", "blink-departure", "blink-arrival" })
            {
                GameObject root;
                if (name == "frost-nova")
                    root = (GameObject)type.GetMethod("Nova").Invoke(null, new object[] { Vector3.zero, 6f, new Color(0.45f, 0.8f, 1f) });
                else if (name == "arcane-shield")
                    root = (GameObject)type.GetMethod("Shield").Invoke(null, new object[] { owner.transform, new Color(0.7f, 0.4f, 1f) });
                else
                    root = (GameObject)type.GetMethod("Blink").Invoke(null, new object[] { Vector3.zero, name == "blink-arrival" });
                if (root.transform.parent == null) SceneManager.MoveGameObjectToScene(root, scene);
                var component = root.GetComponent(type);
                type.GetMethod("Sample").Invoke(component, new object[] { name == "frost-nova" ? 0.32f : 0.18f });
                foreach (var system in root.GetComponentsInChildren<ParticleSystem>()) system.Simulate(0.18f, false, true, false);
                if (root.GetComponentsInChildren<Collider>().Length != 0) throw new Exception("Cosmetic spell has a collider: " + name);
                if (name == "frost-nova")
                {
                    camera.transform.position = new Vector3(8f, 10f, -10f);
                    camera.transform.LookAt(Vector3.zero);
                    camera.orthographicSize = 7f;
                }
                else
                {
                    camera.transform.position = new Vector3(2.5f, 2f, -4f);
                    camera.transform.LookAt(Vector3.up * 0.95f);
                    camera.orthographicSize = 1.4f;
                }
                Capture(camera, "output/spells/" + name + "-unity.png");
                if (name == "arcane-shield")
                {
                    owner.transform.position = new Vector3(2, 1, 3);
                    if ((root.transform.position - owner.transform.position - Vector3.up * 0.9f).sqrMagnitude > 0.001f)
                        throw new Exception("Shield did not follow its owner");
                    owner.transform.position = Vector3.zero;
                    var membrane = root.transform.Find("Transparent field").GetComponent<Renderer>();
                    var block = new MaterialPropertyBlock(); membrane.GetPropertyBlock(block);
                    if (block.GetFloat("_Membrane") < 0.9f) throw new Exception("Shield membrane flag missing");
                }
                else
                {
                    type.GetMethod("Sample").Invoke(component, new object[] { 1f });
                    var block = new MaterialPropertyBlock();
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                    {
                        renderer.GetPropertyBlock(block);
                        if (block.GetColor("_Tint").a > 0.001f) throw new Exception("Effect failed to fade: " + name);
                    }
                }
                UnityEngine.Object.DestroyImmediate(root);
            }
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                if (message.severity.ToString() == "Error") throw new Exception(message.message);
            File.WriteAllText("output/spells/validation.txt", "PASS: all four spells compiled and rendered; nonblank previews; elemental shader supported; cosmetic effects have no colliders; frost/blink fade; shield follows owner and uses transparent membrane.\n");
            Debug.Log("ELEMENTAL_REVIEW_PASS");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void Capture(Camera camera, string path)
    {
        var target = new RenderTexture(1050, 800, 24);
        var previous = RenderTexture.active;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        var pixels = 0;
        foreach (var pixel in image.GetPixels32()) if (pixel.r + pixel.g + pixel.b > 130) pixels++;
        if (pixels < 50) throw new Exception("Blank spell render: " + path);
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
    }
}
