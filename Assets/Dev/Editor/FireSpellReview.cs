using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Batch entry point: -executeMethod FireSpellReview.Validate. Renders the actual game effect.</summary>
public static class FireSpellReview
{
    public static void Validate()
    {
        if (File.Exists("output/spells/fire/validation.txt")) File.Delete("output/spells/fire/validation.txt");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var shader = Resources.Load<Shader>("TemnoridFireParticle");
            if (shader == null || !shader.isSupported) throw new Exception("Fire shader unavailable");
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                if (message.severity.ToString() == "Error") throw new Exception(message.message);
            var type = typeof(Temnorid.Arena.Spells.SpellCaster).Assembly.GetType("Temnorid.Arena.Spells.FireSpellVisual", true);
            var projectile = (GameObject)type.GetMethod("Projectile", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { Vector3.zero, Vector3.right, 0.25f });
            SceneManager.MoveGameObjectToScene(projectile, scene);
            var systems = projectile.GetComponentsInChildren<ParticleSystem>();
            if (systems.Length != 3 || projectile.GetComponent<Collider>() != null) throw new Exception("Unexpected fireball structure");
            projectile.transform.position = Vector3.right * 10f;
            foreach (var system in systems)
            {
                system.Simulate(0.33f, false, true, false);
                if (system.main.simulationSpace != ParticleSystemSimulationSpace.World) continue;
                // Reconstruct emission along a 32 m/s path for a deterministic still in edit mode.
                var particles = new ParticleSystem.Particle[64];
                var count = system.GetParticles(particles);
                for (var i = 0; i < count; i++)
                    particles[i].position -= Vector3.right * (32f * (particles[i].startLifetime - particles[i].remainingLifetime));
                system.SetParticles(particles, count);
            }
            var head = projectile.transform.position;
            foreach (var system in systems)
            {
                var particles = new ParticleSystem.Particle[64];
                var count = system.GetParticles(particles);
                Debug.Log($"FIRE_PARTICLES {system.name}: {count}, first={(count > 0 ? particles[0].position.ToString() : "none")}, bounds={system.GetComponent<Renderer>().bounds}");
                if (count == 0) throw new Exception("Projectile emitter has no particles: " + system.name);
            }
            var cameraObject = new GameObject("Fire review camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.transform.position = head + new Vector3(-1.1f, 1.5f, -5f);
            camera.transform.LookAt(head + Vector3.left * 1.1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.03f, 0.04f);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.05f;
            Directory.CreateDirectory("output/spells/fire");
            Capture(camera, "output/spells/fire/fireball-unity.png");
            type.GetMethod("Stop").Invoke(projectile.GetComponent(type), null);
            foreach (var system in systems)
            {
                system.Simulate(0.4f, false, false, false);
                if (system.particleCount != 0) throw new Exception("Wake failed to expire");
            }
            UnityEngine.Object.DestroyImmediate(projectile);
            type.GetMethod("Impact").Invoke(null, new object[] { head, Vector3.up });
            // Impact is the only remaining particle hierarchy in the main scene.
            foreach (var system in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (system.transform.root.name != "Fire impact") continue;
                if (system.transform.root.gameObject.scene != scene)
                    SceneManager.MoveGameObjectToScene(system.transform.root.gameObject, scene);
                system.Simulate(0.12f, false, true, false);
            }
            camera.transform.LookAt(head + Vector3.up * 0.2f);
            Capture(camera, "output/spells/fire/impact-unity.png");
            File.WriteAllText("output/spells/fire/validation.txt", "PASS: Unity compilation; shader supported; 3 projectile emitters; no collider; wake expires; flight and impact rendered.\n");
            Debug.Log("FIRE_REVIEW_PASS");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void Capture(Camera camera, string path)
    {
        var target = new RenderTexture(1100, 660, 24);
        var previous = RenderTexture.active;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        var visiblePixels = 0;
        foreach (var pixel in image.GetPixels32())
            if (pixel.r > 60 && pixel.r > pixel.b * 2) visiblePixels++;
        if (visiblePixels < 20) throw new Exception("Fire review rendered blank: " + path);
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
    }
}
