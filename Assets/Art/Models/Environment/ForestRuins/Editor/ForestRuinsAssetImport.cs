using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ForestRuins.Editor
{
    public static class ForestRuinsAssetImport
    {
        public const string Folder = "Assets/Art/Models/Environment/ForestRuins";
        public const string ShaderName = "Forest Ruins/Hand Painted Deferred";
        const string ReportFolder = "Blender/ForestRuins_UnityExport";
        [Serializable] public class MaterialSpec
        {
            public string name, kind, baseMap, normalMap;
            public float[] baseColor;
            public float cutoff, normalStrength, paintFill, vertexTone, windStrength, windSpeed;
        }
        [Serializable] public class ModelSpec
        {
            public string name, path, category, kind;
            public string[] materials;
            public int vertices, triangles;
            public float[] boundsMinBlender, boundsMaxBlender;
        }
        [Serializable] public class InstanceSpec { public string name, source, category, kind; public int triangles; }
        [Serializable] public class Manifest
        {
            public string assemblyPath;
            public float cameraVerticalFov, cameraAspect;
            public int meshCount;
            public MaterialSpec[] materials;
            public ModelSpec[] models;
            public InstanceSpec[] instances;
        }
        public static Manifest ReadManifest() => JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "/ForestRuins_Manifest.json"));

        [MenuItem("Tools/Art Assets/Forest Ruins/Configure Materials and Prefabs")]
        public static void Configure()
        {
            var before = SceneManager.GetActiveScene();
            bool dirty = before.isDirty;
            int[] roots = before.GetRootGameObjects().Select(o => o.GetInstanceID()).ToArray();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var manifest = ReadManifest();
            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException("Forest Ruins Deferred shader is missing.");
            foreach (var spec in manifest.materials)
            {
                if (!string.IsNullOrEmpty(spec.baseMap)) ConfigureTexture(spec.baseMap, false, spec.cutoff > 0, spec.kind);
                if (!string.IsNullOrEmpty(spec.normalMap)) ConfigureTexture(spec.normalMap, true, false, spec.kind);
                string path = Folder + "/Materials/" + spec.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader) { name = spec.name }; AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                mat.SetColor("_BaseColor", new Color(spec.baseColor[0], spec.baseColor[1], spec.baseColor[2], spec.baseColor[3]));
                mat.SetTexture("_BaseMap", LoadTexture(spec.baseMap));
                mat.SetTexture("_BumpMap", LoadTexture(spec.normalMap));
                mat.SetFloat("_BumpScale", string.IsNullOrEmpty(spec.normalMap) ? 0 : spec.normalStrength);
                mat.SetFloat("_Smoothness", .08f);
                mat.SetFloat("_Cutoff", spec.cutoff);
                mat.SetFloat("_VertexTone", spec.vertexTone);
                mat.SetFloat("_PaintFill", spec.paintFill);
                mat.SetFloat("_BakeEmission", 0);
                mat.SetFloat("_WindStrength", spec.windStrength);
                mat.SetFloat("_WindSpeed", spec.windSpeed);
                mat.SetFloat("_Cull", (float)CullMode.Off);
                mat.doubleSidedGI = true;
                mat.enableInstancing = true;
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                mat.renderQueue = spec.cutoff > 0 ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
                mat.SetOverrideTag("RenderType", spec.cutoff > 0 ? "TransparentCutout" : "Opaque");
                mat.shaderKeywords = Array.Empty<string>();
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            foreach (var path in manifest.models.Select(m => m.path).Concat(new[] { manifest.assemblyPath }))
                ConfigureModel(path, manifest);
            foreach (var spec in manifest.models)
                BuildPrefab(spec.path, Folder + "/Prefabs/PF_FR_" + spec.name.Substring(6) + ".prefab", manifest, spec.kind);
            BuildPrefab(manifest.assemblyPath, Folder + "/Prefabs/PF_FR_EnvironmentLayout.prefab", manifest, null);
            AssetDatabase.SaveAssets();
            Validate();
            if (SceneManager.GetActiveScene().handle != before.handle || dirty != before.isDirty ||
                !before.GetRootGameObjects().Select(o => o.GetInstanceID()).SequenceEqual(roots))
                throw new InvalidOperationException("Active scene changed unexpectedly.");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/PF_FR_EnvironmentLayout.prefab");
            EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log("Forest Ruins: materials, lightmap UVs and prefabs ready. Current scene unchanged.");
        }
        static Texture2D LoadTexture(string path) => string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        static void ConfigureTexture(string path, bool normal, bool alpha, string kind)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing texture: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.convertToNormalmap = false; importer.sRGBTexture = !normal;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = alpha;
            importer.mipmapEnabled = true; importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = kind == "foliage" ? 512 : 4096;
            importer.textureCompression = kind == "foliage" || kind == "backdrop" || kind == "cutout"
                ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.anisoLevel = 4; importer.isReadable = false;
            importer.SaveAndReimport();
        }
        static void ConfigureModel(string path, Manifest manifest)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing model: " + path);
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false; importer.importLights = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.Import;
            importer.generateSecondaryUV = !ForestRuinsPackedUV.HasCache(path);
            bool stoneTile = path.EndsWith("/SM_FR_StoneTile.fbx", StringComparison.Ordinal);
            importer.secondaryUVPackMargin = stoneTile ? 8 : 4;
            if (stoneTile) importer.secondaryUVHardAngle = 60;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var spec in manifest.materials)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + spec.name + ".mat");
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), spec.name), mat);
            }
            importer.SaveAndReimport();
        }
        static void BuildPrefab(string modelPath, string prefabPath, Manifest manifest, string kind)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var root = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                root.name = Path.GetFileNameWithoutExtension(prefabPath);
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                if (kind == null)
                {
                    foreach (string category in new[] { "Ground", "Architecture", "Nature", "Foliage", "Backdrop", "References" })
                    {
                        var group = new GameObject(category);
                        SceneManager.MoveGameObjectToScene(group, preview);
                        group.transform.SetParent(root.transform, false);
                        foreach (var child in root.GetComponentsInChildren<Transform>(true).Where(t => t != root.transform && t != group.transform && t.parent == root.transform).ToArray())
                        {
                            var record = manifest.instances.FirstOrDefault(i => i.name == child.name);
                            if (record?.category == category || (category == "References" && child.name.StartsWith("REF_FR_")))
                                child.SetParent(group.transform, true);
                        }
                    }
                }
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string effectiveKind = kind ?? manifest.instances.FirstOrDefault(i => i.name == r.gameObject.name)?.kind;
                    bool wind = effectiveKind == "foliage";
                    bool backdrop = effectiveKind == "backdrop" || effectiveKind == "cutout";
                    bool bake = !wind && !backdrop;
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, bake ? StaticEditorFlags.ContributeGI : 0);
                    r.receiveGI = bake ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
                    r.lightProbeUsage = backdrop ? LightProbeUsage.Off : LightProbeUsage.BlendProbes;
                    r.shadowCastingMode = backdrop ? ShadowCastingMode.Off : ShadowCastingMode.TwoSided;
                    r.receiveShadows = !backdrop;
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        [MenuItem("Tools/Art Assets/Forest Ruins/Validate Import")]
        public static void Validate()
        {
            var manifest = ReadManifest(); var report = new StringBuilder();
            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException("Missing shader.");
            bool previous = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var keywords in new[] { Array.Empty<string>(), new[] { "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "SHADOWS_SHADOWMASK", "LIGHTMAP_SHADOW_MIXING" }, new[] { "DYNAMICLIGHTMAP_ON" }, new[] { "_GBUFFER_NORMALS_OCT" }, new[] { "EDITOR_VISUALIZATION" } })
                {
                    var test = new Material(shader) { shaderKeywords = keywords };
                    try { for (int i = 0; i < test.passCount; i++) ShaderUtil.CompilePass(test, i, true); }
                    finally { UnityEngine.Object.DestroyImmediate(test); }
                }
            }
            finally { ShaderUtil.allowAsyncCompilation = previous; }
            if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException(string.Join("\n", ShaderUtil.GetShaderMessages(shader).Select(m => m.message)));
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + manifest.materials[0].name + ".mat");
            foreach (var pass in new[] { "GBuffer", "Forward", "ShadowCaster", "DepthOnly", "DepthNormals", "Meta" })
                if (material.FindPass(pass) < 0) throw new InvalidOperationException("Missing pass " + pass);
            int modelMeshes = 0;
            foreach (var path in manifest.models.Select(m => m.path).Concat(new[] { manifest.assemblyPath }))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) throw new InvalidOperationException("Missing model " + path);
                foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = f.sharedMesh;
                    foreach (var a in new[] { VertexAttribute.Position, VertexAttribute.Normal, VertexAttribute.Tangent, VertexAttribute.TexCoord0, VertexAttribute.TexCoord1 })
                        if (!mesh.HasVertexAttribute(a)) throw new InvalidOperationException(path + ": missing " + a);
                    modelMeshes++;
                }
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterials.Any(m => m == null || m.shader != shader)) throw new InvalidOperationException("Material remap failed: " + r.name);
                if (model.GetComponentsInChildren<Camera>(true).Length > 0 || model.GetComponentsInChildren<Light>(true).Length > 0)
                    throw new InvalidOperationException("Unexpected cameras/lights in " + path);
            }
            foreach (var spec in manifest.models)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.path);
                if ((model.transform.localScale - Vector3.one).sqrMagnitude > .000001f || Quaternion.Angle(model.transform.localRotation, Quaternion.identity) > .01f)
                    throw new InvalidOperationException("Non-identity model root: " + spec.name);
                var renderersForModel = model.GetComponentsInChildren<MeshRenderer>();
                Bounds bounds = renderersForModel[0].bounds;
                foreach (var r in renderersForModel.Skip(1)) bounds.Encapsulate(r.bounds);
                var lo=spec.boundsMinBlender;var hi=spec.boundsMaxBlender;
                var expected = new Vector3(hi[0]-lo[0],hi[2]-lo[2],hi[1]-lo[1]);
                if (spec.kind == "foliage") expected += Vector3.one * .6f; // persistent wind culling envelope
                if ((bounds.size-expected).sqrMagnitude > .00001f)
                    throw new InvalidOperationException("Scale or axis mismatch " + spec.name + ": " + bounds.size + " expected " + expected);
                if (spec.kind == "foliage" && !model.GetComponentInChildren<MeshFilter>().sharedMesh.HasVertexAttribute(VertexAttribute.Color))
                    throw new InvalidOperationException("Leaf tone vertex colors lost: " + spec.name);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/PF_FR_EnvironmentLayout.prefab");
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != manifest.meshCount) throw new InvalidOperationException("Wrong environment mesh count: " + renderers.Length);
            if (prefab.GetComponentsInChildren<Transform>(true).Any(t => t.name.IndexOf("Jackal", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("Gargoyle", StringComparison.OrdinalIgnoreCase) >= 0))
                throw new InvalidOperationException("Statue leaked into export.");
            foreach (var spec in manifest.materials)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + spec.name + ".mat");
                if (m.GetFloat("_BakeEmission") != 0 || m.globalIlluminationFlags != MaterialGlobalIlluminationFlags.EmissiveIsBlack)
                    throw new InvalidOperationException("Stylistic fill must not emit baked light.");
            }
            report.AppendLine("PASS: " + manifest.models.Length + " reusable FBX models + environment layout (" + manifest.meshCount + " meshes). Both statues excluded.");
            report.AppendLine("PASS: " + manifest.materials.Length + " external materials; Deferred GBuffer, Forward, ShadowCaster, DepthOnly, DepthNormals, Meta.");
            report.AppendLine("PASS: shader compiled with static/directional lightmap + shadowmask, dynamic lightmap, oct normals and Meta visualization variants.");
            report.AppendLine("PASS: " + modelMeshes + " imported meshes have UV0, lightmap UV1, normals and tangents.");
            report.AppendLine("PASS: all modular model roots are identity; metre dimensions and Y-up conversion match Blender. Foliage vertex colors preserved.");
            report.AppendLine("Static environment: ContributeGI + ReceiveGI.Lightmaps. Wind foliage: LightProbes, static bake pose in Meta. Background cards: no GI contribution or shadows.");
            report.AppendLine("PaintFill is excluded from baked emission. _BakeEmission is an explicit, separate physical-emission control.");
            report.AppendLine("Validation only: existing scene lighting and bake data were not modified.");
            Directory.CreateDirectory(ReportFolder); File.WriteAllText(ReportFolder + "/unity_import_report.txt", report.ToString());
            Debug.Log(report.ToString());
        }
    }
}
