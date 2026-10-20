using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CardArt.Editor
{
    public static class CardRenderingValidation
    {
        public const string MeshPath = "Assets/Art/Models/Cards/Card_Rounded.fbx";
        public const string AtlasPath = "Assets/Art/CardAtlases/FourBeastsAtlas.asset";
        public const string MaterialPath = "Assets/Art/Materials/Cards/FourBeastsCards.mat";
        public const string PrefabPath = "Assets/GameAssets/CardArt/Prefabs/Card.prefab";
        public const string ScenePath = "Assets/Scenes/CardRenderingValidation.unity";
        const string OutputPath = "ArtSource/Cards/RenderingValidation";

        internal static void SetTextureDefaults(Shader shader, string[] names, Texture[] textures)
        {
            // ShaderImporter replaces the whole list; preserve other optional effects' defaults.
            var defaults = new System.Collections.Generic.Dictionary<string, Texture>();
            var probe = new Material(shader);
            try
            {
                for (int i = 0; i < shader.GetPropertyCount(); i++)
                {
                    if (shader.GetPropertyType(i) != ShaderPropertyType.Texture) continue;
                    string name = shader.GetPropertyName(i);
                    var texture = probe.GetTexture(name);
                    if (texture != null && AssetDatabase.Contains(texture)) defaults[name] = texture;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
            for (int i = 0; i < names.Length; i++) defaults[names[i]] = textures[i];
            var importer = (ShaderImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(shader));
            importer.SetDefaultTextures(defaults.Keys.ToArray(), defaults.Values.ToArray());
            importer.SaveAndReimport();
        }

        [MenuItem("工具/卡牌美术/渲染验证/创建验证场景")]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) throw new InvalidOperationException("An active URP pipeline is required.");
            var importer = (ModelImporter)AssetImporter.GetAtPath(MeshPath);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.generateSecondaryUV = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.isReadable = true; // Small mesh; retain UV/normal data for contract validation.
            importer.SaveAndReimport();
            var mesh = AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>().Single();
            var orientation = SurfaceOrientation(mesh);
            var atlas = AssetDatabase.LoadAssetAtPath<CardAtlasData>(AtlasPath);
            if (atlas == null || atlas.cards.Count != 55 || atlas.cards.Any(c => c.clipped))
                throw new InvalidOperationException("Use the complete, uncropped 55-region atlas.");
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(atlas.texture));
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.mipmapEnabled = false;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.SaveAndReimport();
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find(CardShaderProperties.ShaderName));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            CardShaderProperties.ConfigureSkin(material, atlas);
            material.SetColor(CardShaderProperties.BaseColor, Color.white);
            material.SetColor(CardShaderProperties.EdgeColor, new Color(0.76f, 0.65f, 0.44f));
            EditorUtility.SetDirty(material);
            var template = new GameObject("Card");
            var surface = new GameObject("Surface");
            surface.transform.SetParent(template.transform, false);
            surface.transform.localRotation = orientation;
            surface.AddComponent<CardView>().Bind(mesh, material, atlas, "spades_a");
            var collider = template.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.064f, 0.001f, 0.096f);
            var prefab = PrefabUtility.SaveAsPrefabAsset(template, PrefabPath);
            UnityEngine.Object.DestroyImmediate(template);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.24f, 0.24f);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            var cards = new GameObject("Card Showcase — shared Mesh + Material");
            string[] ids = { "spades_k", "hearts_q", "clubs_j", "diamonds_a", "joker_big", "spades_a" };
            for (int i = 0; i < ids.Length; i++)
            {
                var card = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                card.name = i == 5 ? "Card Back" : ids[i];
                card.transform.SetParent(cards.transform, false);
                card.transform.localPosition = new Vector3((i % 3 - 1) * 0.079f, 0.004f, (i < 3 ? 1 : -1) * 0.057f);
                card.GetComponentInChildren<CardView>().SetCard(ids[i]);
                if (i == 5) card.transform.localRotation = Quaternion.AngleAxis(180, Vector3.forward);
            }
            // A separated tilted card makes the small thickness and edge visible up close.
            var tilted = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            tilted.name = "Tilted Card — edge and shadow";
            tilted.transform.position = new Vector3(0.17f, 0.046f, 0);
            tilted.transform.rotation = Quaternion.Euler(0, -15, -65);
            tilted.GetComponentInChildren<CardView>().SetCard("joker_small");

            var floorMaterialPath = "Assets/Art/Materials/Cards/ValidationTable.mat";
            var floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(floorMaterialPath);
            if (floorMaterial == null)
            {
                floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(floorMaterial, floorMaterialPath);
            }
            floorMaterial.SetColor("_BaseColor", new Color(0.055f, 0.095f, 0.088f));
            floorMaterial.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(floorMaterial);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Matte Table";
            floor.transform.position = new Vector3(0, -0.008f, 0);
            floor.transform.localScale = new Vector3(3, 0.016f, 3);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;

            var light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.7f;
            light.color = Color.white;
            light.shadows = LightShadows.Hard;
            light.shadowBias = 0.02f;
            light.shadowNormalBias = 0.01f;
            light.transform.rotation = Quaternion.Euler(60, -35, 0);
            RenderSettings.sun = light;
            var point = new GameObject("Deferred Fill Light").AddComponent<Light>();
            point.type = LightType.Point;
            point.transform.position = new Vector3(0.1f, 0.14f, -0.02f);
            point.range = 0.5f;
            point.intensity = 0.025f;
            point.color = new Color(0.82f, 0.9f, 1);

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 0.135f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 4;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.backgroundColor = new Color(0.055f, 0.095f, 0.088f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(0.024f, 0.42f, -0.19f);
            camera.transform.LookAt(new Vector3(0.024f, 0, 0));
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.SetRenderer(0);
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            camera.gameObject.AddComponent<AudioListener>();
            CreatePixelPresentation(camera);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = cards;
            SceneView.lastActiveSceneView?.LookAt(new Vector3(0.024f, 0, 0), camera.transform.rotation, 0.3f);
            Debug.Log("Card validation scene created: " + ScenePath);
        }

        static void CreatePixelPresentation(Camera source)
        {

            source.targetTexture = null;
            source.depth = 0;
        }

        public static Quaternion SurfaceOrientation(Mesh mesh)
        {
            var flags = mesh.uv2;
            var uv = mesh.uv;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var front = Enumerable.Range(0, mesh.vertexCount).Where(i => flags[i].x < 0.5f && flags[i].y < 0.5f).ToArray();
            var top = front.Where(i => uv[i].y > 0.9f).ToArray();
            var bottom = front.Where(i => uv[i].y < 0.1f).ToArray();
            Vector3 Average(int[] indices) => indices.Aggregate(Vector3.zero, (sum, i) => sum + vertices[i]) / indices.Length;
            var normal = front.Aggregate(Vector3.zero, (sum, i) => sum + normals[i]).normalized;
            var up = (Average(top) - Average(bottom)).normalized;
            return Quaternion.Inverse(Quaternion.LookRotation(up, normal));
        }

        [MenuItem("工具/卡牌美术/渲染验证/捕获验证画面")]
        public static void Capture()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open " + ScenePath + " before capturing.");
            Directory.CreateDirectory(OutputPath);
            var camera = Camera.main;
            CaptureCamera(camera, OutputPath + "/cards_960x540.png", 960, 540);
            // Point upscale preserves the same 960x540 edge pattern in a normal-sized output.
            var low = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                low.LoadImage(File.ReadAllBytes(OutputPath + "/cards_960x540.png"));
                var source = low.GetPixels32();
                var pixels = new Color32[1920 * 1080];
                for (int y = 0; y < 1080; y++)
                    for (int x = 0; x < 1920; x++) pixels[y * 1920 + x] = source[(y / 2) * 960 + x / 2];
                var high = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
                try { high.SetPixels32(pixels); high.Apply(); File.WriteAllBytes(OutputPath + "/cards_point_1920x1080.png", high.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(high); }
            }
            finally { UnityEngine.Object.DestroyImmediate(low); }
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            var size = camera.orthographicSize;
            try
            {
                camera.transform.position = new Vector3(0.205f, 0.068f, -0.095f);
                camera.transform.LookAt(new Vector3(0.17f, 0.046f, 0));
                camera.orthographicSize = 0.06f;
                CaptureCamera(camera, OutputPath + "/edge_detail.png", 960, 540);
                var tilted = GameObject.Find("Tilted Card — edge and shadow").transform;
                var corner = tilted.TransformPoint(new Vector3(0.0315f, 0, 0.0475f));
                camera.transform.position = corner + tilted.rotation * new Vector3(0.02f, 0.025f, 0.03f);
                camera.transform.LookAt(corner, tilted.forward);
                camera.orthographicSize = 0.0065f;
                CaptureCamera(camera, OutputPath + "/corner_detail.png", 960, 540);
            }
            finally { camera.transform.SetPositionAndRotation(position, rotation); camera.orthographicSize = size; }
            WriteReport(camera);
            Debug.Log("Card rendering validation captured in " + OutputPath);
        }

        [MenuItem("工具/卡牌美术/渲染验证/验证闪卡渲染")]
        public static void ValidateFoilRendering()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != CardFoilSetup.ScenePath)
                throw new InvalidOperationException("Use Preview Foil Cards before validating foil.");
            var cards = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CardView>()).ToArray();
            if (cards.Length == 0) throw new InvalidOperationException("No cards available for foil validation.");
            var previous = cards.Select(c => (c.FoilEnabled, c.FoilColor, c.FoilStrength)).ToArray();
            var material = cards[0].GetComponent<MeshRenderer>().sharedMaterial;
            float speed = material.GetFloat("_FoilSpeed");
            var pattern = material.GetTexture("_FoilMap");
            var camera = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.GetComponent<Camera>()).First(c => c != null);
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            var folder = OutputPath + "/Foil";
            Directory.CreateDirectory(folder);
            byte[] Shot(string name)
            {
                string path = folder + "/" + name + ".png";
                CaptureCamera(camera, path, 1920, 1080);
                return File.ReadAllBytes(path);
            }
            try
            {
                material.SetFloat("_FoilSpeed", 0);
                foreach (var c in cards) c.SetFoilEnabled(false);
                var off = Shot("off");
                foreach (var c in cards) { c.SetFoilColor(Color.white); c.SetFoilStrength(0); c.SetFoilEnabled(true); }
                if (!off.SequenceEqual(Shot("zero"))) throw new InvalidOperationException("Zero foil must match disabled foil.");
                foreach (var c in cards) c.SetFoilStrength(2);
                var on = Shot("rainbow");
                if (off.SequenceEqual(on)) throw new InvalidOperationException("Foil did not affect the rendered image.");
                foreach (var c in cards) c.SetFoilColor(new Color(0.15f, 0.8f, 1));
                var cyan = Shot("cyan");
                if (on.SequenceEqual(cyan)) throw new InvalidOperationException("Foil color did not affect the rendered image.");
                foreach (var c in cards) c.SetFoilStrength(0.5f);
                if (cyan.SequenceEqual(Shot("subtle"))) throw new InvalidOperationException("Foil strength did not affect the rendered image.");
                camera.transform.RotateAround(Vector3.zero, Vector3.up, 12);
                Shot("angled");
                camera.transform.SetPositionAndRotation(position, rotation);
                foreach (var c in cards) c.SetFoilEnabled(false);
                if (!off.SequenceEqual(Shot("disabled_again"))) throw new InvalidOperationException("Disabling foil must restore ordinary cards.");
                // Mixed enabled states with one shared material are the normal gameplay configuration.
                var sample = cards.First(c => c.CardId == "spades_k");
                sample.SetFoilColor(Color.white);
                sample.SetFoilStrength(2);
                sample.SetFoilEnabled(true);
                var mixed = Shot("mixed");
                material.SetTexture("_FoilMap", Texture2D.grayTexture);
                if (mixed.SequenceEqual(Shot("without_pattern"))) throw new InvalidOperationException("Foil texture must affect the pattern.");
                material.SetTexture("_FoilMap", pattern);
                material.SetFloat("_FoilSpeed", 0.25f);
                var dynamicFrame = Shot("dynamic_a");
                material.SetFloat("_FoilSpeed", 0.73f);
                if (dynamicFrame.SequenceEqual(Shot("dynamic_b"))) throw new InvalidOperationException("Time-driven foil must change at a fixed camera angle.");
                WriteReport(camera);
                File.WriteAllText(folder + "/validation.json", "{\"passed\":true,\"checks\":[\"zero_equals_off\",\"enabled\",\"color\",\"strength\",\"disabled_restores\",\"mixed_shared_material\",\"texture\",\"dynamic_phase\",\"deferred_shader\"]}");
                Debug.Log("Foil rendering validation passed; captures in " + folder);
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                material.SetFloat("_FoilSpeed", speed);
                material.SetTexture("_FoilMap", pattern);
                for (int i = 0; i < cards.Length; i++)
                {
                    cards[i].SetFoilColor(previous[i].FoilColor);
                    cards[i].SetFoilStrength(previous[i].FoilStrength);
                    cards[i].SetFoilEnabled(previous[i].FoilEnabled);
                }
            }
        }

        [MenuItem("工具/卡牌美术/渲染验证/验证卡牌接口（不重载场景）")]
        public static void ValidateCardAPI()
        {
            // Run these synchronous assertions without Test Runner closing unsaved user scenes.
            var type = Type.GetType("CardArt.Tests.CardRenderingTests, CardArt.Tests.Editor", true);
            var instance = Activator.CreateInstance(type);
            string[] methods = { "ImportedMeshPreservesSurfaceFlagsAndPhysicalSize",
                "CardBindingKeepsResourcesSharedAndFaceParametersIndependent",
                "FoilParametersAreIndependentAndSurviveRebinding",
                "ShaderHasDeferredShadowAndDepthPassesAndPointAtlas" };
            foreach (string name in methods) type.GetMethod(name).Invoke(instance, null);
            Directory.CreateDirectory(OutputPath + "/Foil");
            File.WriteAllText(OutputPath + "/Foil/api_validation.json", "{\"passed\":true,\"assertionGroups\":4}");
            Debug.Log("Card API assertions passed: mesh, binding, per-card foil lifecycle and shader passes.");
        }

        [MenuItem("工具/卡牌美术/渲染验证/捕获闪卡动画")]
        public static void CaptureFoilAnimation()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != CardFoilSetup.ScenePath)
                throw new InvalidOperationException("Use Preview Foil Cards first.");
            var camera = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                .Select(g => g.GetComponent<Camera>()).First(c => c != null);
            string folder = OutputPath + "/Foil/Animation";
            Directory.CreateDirectory(folder);
            int frame = 0;
            double next = EditorApplication.timeSinceStartup;
            byte[] first = null;
            bool changed = false;
            EditorApplication.CallbackFunction capture = null;
            capture = () =>
            {
                if (EditorApplication.timeSinceStartup < next) return;
                try
                {
                    if (camera == null) throw new InvalidOperationException("Preview camera was removed.");
                    string path = folder + "/frame_" + frame.ToString("D2") + ".png";
                    CaptureCamera(camera, path, 960, 540);
                    var bytes = File.ReadAllBytes(path);
                    if (first == null) first = bytes;
                    else changed |= !first.SequenceEqual(bytes);
                    frame++;
                    next = EditorApplication.timeSinceStartup + 0.12;
                    if (frame < 12) return;
                    EditorApplication.update -= capture;
                    if (!changed) throw new InvalidOperationException("Foil did not animate with a stationary camera.");
                    File.WriteAllText(folder + "/validation.json", "{\"passed\":true,\"stationaryCamera\":true,\"frames\":12}");
                    Debug.Log("Foil animation capture passed with a stationary camera.");
                }
                catch (Exception e) { EditorApplication.update -= capture; Debug.LogException(e); }
            };
            EditorApplication.update += capture;
        }

        [MenuItem("工具/卡牌美术/渲染验证/捕获游戏输出")]
        public static void CaptureGameOutput()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode to capture the final screen output.");
            Directory.CreateDirectory(OutputPath);
            EditorApplication.delayCall += () => ScreenCapture.CaptureScreenshot(OutputPath + "/game_output.png");
        }

        static void CaptureCamera(Camera camera, string path, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, antiAliasing = 1 };
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            var previousAspect = camera.aspect;
            try
            {
                rt.Create();
                camera.aspect = (float)width / height;
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("URP render requests are unavailable.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.aspect = previousAspect;
                RenderTexture.active = previous;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        static void WriteReport(Camera camera)
        {
            var views = UnityEngine.Object.FindObjectsOfType<CardView>();
            var mesh = views[0].GetComponent<MeshFilter>().sharedMesh;
            var material = views[0].GetComponent<MeshRenderer>().sharedMaterial;
            var renderer = camera.GetUniversalAdditionalCameraData().scriptableRenderer;
            var mode = renderer.GetType().GetProperty("renderingModeActual", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(renderer)?.ToString();
            var errors = ShaderUtil.GetShaderMessages(material.shader).Where(m => m.severity.ToString() == "Error").ToArray();
            var report = new RenderingReport
            {
                scene = camera.gameObject.scene.path, graphicsDevice = SystemInfo.graphicsDeviceType.ToString(), actualRenderingMode = mode,
                cardCount = views.Length, triangleCount = (int)mesh.GetIndexCount(0) / 3, meshBounds = mesh.bounds.size,
                oneSharedMesh = views.All(v => v.GetComponent<MeshFilter>().sharedMesh == mesh),
                oneSharedMaterial = views.All(v => v.GetComponent<MeshRenderer>().sharedMaterial == material),
                instancingEnabled = material.enableInstancing, atlasFilter = material.GetTexture(CardShaderProperties.BaseMap).filterMode.ToString(),
                shaderErrors = errors.Select(m => m.message).ToArray()
            };
            File.WriteAllText(OutputPath + "/validation.json", JsonUtility.ToJson(report, true));
            if (mode != "Deferred" || errors.Length != 0 || !report.oneSharedMesh || !report.oneSharedMaterial)
                throw new InvalidOperationException("Rendering validation failed; see validation.json.");
        }

        [Serializable]
        sealed class RenderingReport
        {
            public string scene, graphicsDevice, actualRenderingMode, atlasFilter;
            public int cardCount, triangleCount;
            public Vector3 meshBounds;
            public bool oneSharedMesh, oneSharedMaterial, instancingEnabled;
            public string[] shaderErrors;
        }
    }
}
