using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Import, material, mesh budget and actual URP render review for the shared model set.</summary>
public static class ModelRealismReview
{
    public static void Validate()
    {
        const string output = "output/realism";
        Directory.CreateDirectory(output);
        var reportPath = output + "/unity-validation.txt";
        if (File.Exists(reportPath)) File.Delete(reportPath);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var buildMaterial = typeof(ArenaArtBuilder).GetMethod("BuildMaterial", BindingFlags.Static | BindingFlags.NonPublic);
        buildMaterial.Invoke(null, new object[] { ArenaArtBuilder.FloorMaterialPath, "stone-floor", 2f });
        buildMaterial.Invoke(null, new object[] { ArenaArtBuilder.WallMaterialPath, "stone-wall", 4f });
        AssetDatabase.SaveAssets();
        var messages = new List<string>();
        foreach (var path in new[] { ArenaArtBuilder.FloorMaterialPath, ArenaArtBuilder.WallMaterialPath })
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material.GetTexture("_MetallicGlossMap") == null || material.GetTexture("_OcclusionMap") == null)
                throw new Exception("Stone PBR maps missing: " + path);
        }
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var cameraObject = new GameObject("Model review camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
            camera.orthographic = true;
            foreach (var info in new[] { new Vector3(35, -30, 0), new Vector3(25, 140, 0) })
            {
                var lightObject = new GameObject("Review directional");
                SceneManager.MoveGameObjectToScene(lightObject, scene);
                lightObject.transform.rotation = Quaternion.Euler(info);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = info.y < 0 ? 1.1f : 0.55f;
                light.shadows = LightShadows.None;
            }
            var paths = new List<string>(Directory.GetFiles("Assets/Models/arena", "*.glb"));
            paths.Add(ArenaArtBuilder.MagePath);
            foreach (var path in paths)
            {
                if (path == ArenaArtBuilder.MagePath || path.EndsWith("arena-banner.glb"))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) throw new Exception("GLB failed to import: " + path);
                var instance = UnityEngine.Object.Instantiate(asset);
                SceneManager.MoveGameObjectToScene(instance, scene);
                var renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) throw new Exception("No imported renderers: " + path);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null || material.shader == null || !material.shader.isSupported)
                            throw new Exception("Unsupported model material: " + path);
                        if (material.name.StartsWith("accent"))
                            Debug.Log($"MODEL_ACCENT {path}: {material.name}, factor={(material.HasProperty("baseColorFactor") ? material.GetColor("baseColorFactor").ToString() : "absent")}, base={(material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor").ToString() : "absent")}");
                    }
                }
                var triangles = 0;
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>()) triangles += filter.sharedMesh.triangles.Length / 3;
                foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>()) triangles += skin.sharedMesh.triangles.Length / 3;
                if (path == ArenaArtBuilder.MagePath)
                {
                    if (triangles > 25000) throw new Exception("Mage exceeds shared mesh budget");
                    var clips = 0;
                    foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
                        if (subAsset is AnimationClip clip && !clip.name.StartsWith("__")) clips++;
                    if (clips != 17) throw new Exception("Mage animation clips lost: " + clips);
                    var accent = false;
                    foreach (var renderer in renderers)
                        foreach (var material in renderer.sharedMaterials) if (material.name.StartsWith("accent")) accent = true;
                    if (!accent) throw new Exception("Recolorable mage accent missing");
                }
                if (triangles <= 0) throw new Exception("Empty model mesh: " + path);
                var span = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                camera.orthographicSize = Mathf.Max(bounds.size.y * 0.65f, (bounds.size.x + bounds.size.z * 0.6f) * 0.6f);
                camera.transform.position = bounds.center + new Vector3(span * 1.4f, span * 0.9f, span * 2.3f);
                camera.transform.LookAt(bounds.center);
                Capture(camera, output + "/" + Path.GetFileNameWithoutExtension(path) + "-unity.png");
                if (path == ArenaArtBuilder.MagePath)
                {
                    foreach (var renderer in renderers)
                    {
                        var materials = renderer.sharedMaterials;
                        for (var i = 0; i < materials.Length; i++)
                        {
                            if (!materials[i].name.StartsWith("accent")) continue;
                            var block = new MaterialPropertyBlock();
                            var cyan = new Color(0.25f, 0.85f, 1f);
                            block.SetColor("_BaseColor", cyan);block.SetColor("baseColorFactor", cyan);
                            renderer.SetPropertyBlock(block, i);
                        }
                    }
                    Capture(camera, output + "/mage-team-cyan-unity.png", true);
                    messages.Add("PASS mage accent recolors through the runtime material property block");
                }
                messages.Add($"PASS {Path.GetFileName(path)}: {triangles} triangles; imported materials and nonblank URP render");
                UnityEngine.Object.DestroyImmediate(instance);
            }
            foreach (var surface in new[] { "stone-floor", "stone-wall" })
            {
                var meters = surface == "stone-floor" ? 2f : 4f;
                var go = new GameObject("PBR surface", typeof(MeshFilter), typeof(MeshRenderer));
                SceneManager.MoveGameObjectToScene(go, scene);
                var mesh = new Mesh
                {
                    vertices = new[] { new Vector3(-1,0,-1), new Vector3(-1,0,1), new Vector3(1,0,1), new Vector3(1,0,-1) },
                    uv = new[] { Vector2.zero, new Vector2(0,meters), new Vector2(meters,meters), new Vector2(meters,0) },
                    triangles = new[] { 0,1,2,0,2,3 }
                };
                mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(surface == "stone-floor" ? ArenaArtBuilder.FloorMaterialPath : ArenaArtBuilder.WallMaterialPath);
                camera.orthographicSize = 1.7f;
                camera.transform.position = new Vector3(0.1f, 3f, 1.7f);camera.transform.LookAt(Vector3.zero);
                Capture(camera, output + "/" + surface + "-unity.png");
                messages.Add("PASS " + surface + ": normal, linear occlusion and smoothness maps; nonblank URP render");
                UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(mesh);
            }
            File.WriteAllLines(reportPath, messages);
            Debug.Log("MODEL_REALISM_REVIEW_PASS");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void Capture(Camera camera, string path, bool requireCyan = false)
    {
        var target = new RenderTexture(900, 900, 24);
        var previous = RenderTexture.active;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);image.Apply();
        var pixels = image.GetPixels32();
        var reference = pixels[0];var changed = 0;var cyanPixels = 0;
        foreach (var pixel in pixels)
        {
            if (Math.Abs(pixel.r - reference.r) + Math.Abs(pixel.g - reference.g) + Math.Abs(pixel.b - reference.b) > 30) changed++;
            if (pixel.b > pixel.r * 1.5f && pixel.g > pixel.r * 1.3f && pixel.g > 65) cyanPixels++;
            if (pixel.r > 240 && pixel.b > 240 && pixel.g < 30) throw new Exception("Missing shader (magenta) in " + path);
        }
        if (changed < 500) throw new Exception("Blank model render: " + path);
        if (requireCyan && cyanPixels < 50) throw new Exception("Mage accent did not recolor: " + path);
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = null;RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);
    }
}
