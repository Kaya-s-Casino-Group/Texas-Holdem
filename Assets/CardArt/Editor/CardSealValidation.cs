using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CardArt.Editor
{
    public static class CardSealValidation
    {
        const string MapPath = "Assets/CardArt/Textures/WaxSeal_Paw.png";
        const string NormalPath = "Assets/CardArt/Textures/WaxSeal_Paw_Normal.png";
        const string Output = "ArtSource/Cards/RenderingValidation/Seal";

        [MenuItem("工具/卡牌美术/素材准备/蜡封材质")]
        public static void PrepareMaterials()
        {
            var shader = Shader.Find(CardShaderProperties.ShaderName);
            var map = AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            CardRenderingValidation.SetTextureDefaults(shader, new[] { "_SealMap", "_SealNormalMap" }, new Texture[] { map, normal });
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material.shader != shader) continue;
                foreach (var entry in new[] { (CardShaderProperties.SealMap, map), (CardShaderProperties.SealNormalMap, normal) })
                {
                    var current = material.GetTexture(entry.Item1);
                    if (current != null && AssetDatabase.Contains(current)) continue;
                    material.SetTexture(entry.Item1, entry.Item2);
                    EditorUtility.SetDirty(material);
                }
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("工具/卡牌美术/渲染验证/验证蜡封导出与渲染")]
        public static void Validate()
        {
            Directory.CreateDirectory(Output);
            var originalScene = SceneManager.GetActiveScene();
            var originalDirty = originalScene.isDirty;
            var originalRoots = originalScene.rootCount;
            var fixtureType = Type.GetType("CardArt.Tests.CardPrefabExporterTests, CardArt.Tests.Editor", true);
            foreach (string method in new[] { "FourBeastsInputsPassAllChecks", "SealExportRetainsNormalMapAndIndependentInstanceSettings", "SealExportRejectsWrongNormalImportAndUnsupportedShader", "SealEmissionValidatesRangeAndOptionalShaderSupport", "RedCrossExportKeepsInstancesIndependentAndPreservesLifecycle", "RedCrossValidationRejectsMissingOrIncompatibleInputs" })
            {
                var fixture = Activator.CreateInstance(fixtureType);
                try { fixtureType.GetMethod("SetUp").Invoke(fixture, null); fixtureType.GetMethod(method).Invoke(fixture, null); }
                finally { fixtureType.GetMethod("TearDown").Invoke(fixture, null); }
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            Material material = null;
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Color.gray * 0.3f;
                RenderSettings.skybox = null;
                var mesh = AssetDatabase.LoadAllAssetsAtPath(CardRenderingValidation.MeshPath).OfType<Mesh>().Single();
                material = new Material(Shader.Find(CardShaderProperties.ShaderName));
                var atlas = AssetDatabase.LoadAssetAtPath<CardAtlasData>(CardRenderingValidation.AtlasPath);
                CardShaderProperties.ConfigureSkin(material, atlas);
                material.SetTexture(CardShaderProperties.SealMap, AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath));
                material.SetTexture(CardShaderProperties.SealNormalMap, AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath));
                material.SetFloat("_FoilSpeed", 0);
                material.SetTexture(CardShaderProperties.RedCrossMap, AssetDatabase.LoadAssetAtPath<Texture2D>(CardRedCrossSetup.TexturePath));
                var card = new GameObject("Temporary Seal Validation").AddComponent<CardView>();
                card.gameObject.layer = 30;
                card.transform.rotation = CardRenderingValidation.SurfaceOrientation(mesh);
                card.Bind(mesh, material, atlas, "spades_k");
                var camera = new GameObject("Temporary Seal Camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 0.18f, -0.09f);
                camera.transform.LookAt(Vector3.zero);
                camera.orthographic = true;
                camera.orthographicSize = 0.062f;
                camera.nearClipPlane = 0.01f; camera.farClipPlane = 1;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.04f, 0.045f);
                camera.cullingMask = 1 << 30;
                var light = new GameObject("Temporary Seal Light").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 0.65f;
                light.transform.rotation = Quaternion.Euler(45, -35, 0);
                light.cullingMask = 1 << 30;
                RenderSettings.sun = light;
                byte[] Shot(string name)
                {
                    var target = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32);
                    var pixels = new Texture2D(640, 640, TextureFormat.RGBA32, false);
                    var previous = RenderTexture.active;
                    try
                    {
                        target.Create(); camera.aspect = 1;
                        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                        RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); pixels.Apply();
                        var bytes = pixels.EncodeToPNG(); File.WriteAllBytes(Output + "/" + name + ".png", bytes); return bytes;
                    }
                    finally { RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); }
                }
                void Different(byte[] a, byte[] b, string message) { if (a.SequenceEqual(b)) throw new InvalidOperationException(message); }
                var off = Shot("off");
                card.SetRedCrossEnabled(true);
                Different(off, Shot("red_cross"), "Red cross has no effect.");
                card.SetRedCrossEnabled(false);
                if (!off.SequenceEqual(Shot("red_cross_disabled"))) throw new InvalidOperationException("Disabling red cross must restore original card.");
                card.SetSealEnabled(true);
                material.SetFloat(CardShaderProperties.SealNormalScale, 0);
                var flat = Shot("flat"); Different(off, flat, "Seal missing.");
                material.SetFloat(CardShaderProperties.SealNormalScale, 1);
                var normal = Shot("normal"); Different(flat, normal, "Normal map has no effect.");
                var rotation = material.GetFloat(CardShaderProperties.SealRotation);
                material.SetFloat(CardShaderProperties.SealRotation, 0);
                Different(normal, Shot("unrotated"), "Seal rotation has no effect.");
                material.SetFloat(CardShaderProperties.SealRotation, rotation);
                card.SetSealEmissionStrength(0.4f);
                var emission = Shot("emission"); Different(normal, emission, "Seal emission has no effect.");
                material.SetFloat(CardShaderProperties.SealNormalScale, 0);
                Different(emission, Shot("emission_flat"), "Emission must preserve normal lighting.");
                material.SetFloat(CardShaderProperties.SealNormalScale, 1);
                card.SetSealEmissionStrength(0);
                if (!normal.SequenceEqual(Shot("emission_zero"))) throw new InvalidOperationException("Zero emission must restore lit wax.");
                light.enabled = false; RenderSettings.ambientLight = Color.black;
                var dark = Shot("dark"); card.SetSealEmissionStrength(0.4f);
                Different(dark, Shot("dark_emission"), "Emission must remain visible without light.");
                light.enabled = true; RenderSettings.ambientLight = Color.gray * 0.3f;
                card.SetSealColor(new Color(0.08f, 0.5f, 0.65f));
                Different(normal, Shot("blue"), "Instance seal color has no effect.");
                card.SetFoilEnabled(true);
                var effects = Shot("foil_and_seal");
                card.SetRedCrossEnabled(true);
                Different(effects, Shot("red_cross_foil_and_seal"), "Red cross must coexist with foil and seal.");
                card.SetRedCrossEnabled(false); card.SetFoilEnabled(false);
                card.SetSealEnabled(false);
                if (!off.SequenceEqual(Shot("disabled"))) throw new InvalidOperationException("Disabled seal must restore ordinary card.");
                card.transform.rotation = Quaternion.AngleAxis(180, Vector3.forward) * card.transform.rotation;
                var back = Shot("back_off"); card.SetSealEnabled(true); card.SetRedCrossEnabled(true);
                if (!back.SequenceEqual(Shot("back_on"))) throw new InvalidOperationException("Seal and red cross must not affect the card back.");
                var errors = ShaderUtil.GetShaderMessages(material.shader).Where(m => m.severity.ToString() == "Error").ToArray();
                if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors.Select(e => e.message)));
                var renderer = camera.GetUniversalAdditionalCameraData().scriptableRenderer;
                var mode = renderer.GetType().GetProperty("renderingModeActual", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(renderer)?.ToString();
                if (mode != "Deferred") throw new InvalidOperationException("Expected Deferred rendering.");
            }
            finally
            {
                SceneManager.SetActiveScene(originalScene);
                EditorSceneManager.CloseScene(scene, true);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            }
            if (originalScene.rootCount != originalRoots || originalScene.isDirty != originalDirty)
                throw new InvalidOperationException("Validation changed the original scene.");
            File.WriteAllText(Output + "/validation.json", "{\"passed\":true,\"exportTestGroups\":6,\"redCross\":true,\"redCrossWithEffects\":true,\"sealEmission\":true,\"emissionPreservesNormalLighting\":true,\"emissionWithoutLight\":true,\"rendering\":\"Deferred\",\"normalMapChangesLighting\":true,\"sealRotation\":true,\"backUnaffected\":true,\"originalScenePreserved\":true}");
            Debug.Log("Wax seal validation passed: exported textures and instances, partial emission, normal lighting, back exclusion, and original scene preservation.");
        }
    }
}
