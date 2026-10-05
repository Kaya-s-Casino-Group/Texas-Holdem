using System.IO;
using System.Linq;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardArt.Editor
{
    public static class CardFoilSetup
    {
        public const string ScenePath = "Assets/Scenes/CardFoilValidation.unity";
        public const string TexturePath = "Assets/CardArt/Textures/FoilPattern.png";

        [MenuItem("Tools/Card Art/Prepare Foil Texture")]
        public static void PrepareTexture()
        {
            if (!File.Exists(TexturePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGB24, false, true);
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        // Seamless engraved diagonal grooves with coarse, diamond-shaped facets.
                        int u = (x + y) % 16, v = (x - y + size) % 16;
                        float facet = Mathf.Abs(u - 8) + Mathf.Abs(v - 8);
                        float groove = (x + y * 3) % 8 < 2 ? 0.12f : 0;
                        float value = Mathf.Clamp01(0.2f + facet / 22f + groove);
                        value = Mathf.Round(value * 7) / 7;
                        pixels[y * size + x] = new Color(value, value, value, 1);
                    }
                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.sRGBTexture = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var map = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var shader = Shader.Find(CardShaderProperties.ShaderName);
            CardRenderingValidation.SetTextureDefaults(shader, new[] { "_FoilMap" }, new Texture[] { map });
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material.shader != shader || material.GetTexture("_FoilMap") != null &&
                    AssetDatabase.Contains(material.GetTexture("_FoilMap"))) continue;
                material.SetTexture("_FoilMap", map);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Foil texture prepared: " + TexturePath);
        }

        [MenuItem("Tools/Card Art/Preview Foil Cards")]
        public static void PreviewCards()
        {
            // Keep the user's current scene loaded, including unsaved changes.
            var existing = SceneManager.GetSceneByPath(ScenePath);
            if (existing.IsValid() && existing.isLoaded) { SceneManager.SetActiveScene(existing); return; }
            if (File.Exists(ScenePath))
            {
                var loaded = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(loaded);
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.25f, 0.25f);
            RenderSettings.skybox = null;
            var mesh = AssetDatabase.LoadAllAssetsAtPath(CardRenderingValidation.MeshPath).OfType<Mesh>().Single();
            var atlas = AssetDatabase.LoadAssetAtPath<CardAtlasData>(CardRenderingValidation.AtlasPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(CardRenderingValidation.MaterialPath);
            var orientation = CardRenderingValidation.SurfaceOrientation(mesh);
            for (int i = 0; i < 3; i++)
            {
                var root = new GameObject(new[] { "普通牌", "动态彩虹镭射", "粉色镭射" }[i]);
                root.layer = 31;
                root.transform.position = new Vector3((i - 1) * 0.085f, 0.004f, 0);
                root.transform.rotation = orientation;
                var card = root.AddComponent<CardView>();
                card.Bind(mesh, material, atlas, "spades_k");
                card.SetFoilStrength(1.5f);
                card.SetFoilColor(i == 2 ? new Color(1, 0.45f, 0.65f) : Color.white);
                card.SetFoilEnabled(i != 0);
            }
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Foil Preview Table";
            table.layer = 31;
            table.transform.position = new Vector3(0, -0.01f, 0);
            table.transform.localScale = new Vector3(0.6f, 0.02f, 0.4f);
            table.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Cards/ValidationTable.mat");
            var light = new GameObject("Foil Preview Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.8f;
            light.shadows = LightShadows.Hard;
            light.cullingMask = 1 << 31;
            light.transform.rotation = Quaternion.Euler(55, -30, 0);
            RenderSettings.sun = light;
            var camera = new GameObject("Foil Preview Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0.22f, -0.19f);
            camera.transform.LookAt(Vector3.zero, Vector3.up);
            camera.fieldOfView = 43;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 2;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.025f, 0.02f);
            camera.depth = 10;
            camera.GetUniversalAdditionalCameraData().SetRenderer(0);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = scene.GetRootGameObjects().First(g => g.name == "动态彩虹镭射");
            SceneView.RepaintAll();
        }
    }
}
