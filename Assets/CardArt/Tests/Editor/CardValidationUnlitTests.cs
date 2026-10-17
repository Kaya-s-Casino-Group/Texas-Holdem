using System;
using System.IO;
using System.Linq;
using CardArt.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CardArt.Tests
{
    public sealed class CardValidationUnlitTests
    {
        private const string ShaderPath = "Assets/CardArt/Shaders/CardValidationUnlit.shader";
        private string folder;
        private CardPrefabExportSettings settings;

        [SetUp]
        public void SetUp()
        {
            // Register only this temporary folder; a full refresh can import unrelated edits
            // while the test runner owns the current assembly callbacks.
            folder = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder("Assets/CardArt/Tests",
                "UnlitTemp_" + Guid.NewGuid().ToString("N")));
            var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab")
                .GetComponentInChildren<CardView>(true);
            var profile = ScriptableObject.CreateInstance<CardArtProfile>();
            profile.Configure(original.Atlas, new[]
            {
                new CardShaderCombination("base", CardShaderParameter.Color("_ColorTint", Color.white)),
                new CardShaderCombination("selected", CardShaderParameter.Color("_ColorTint", new Color(1, .8f, .35f, 1))),
                new CardShaderCombination("disabled", CardShaderParameter.Color("_ColorTint", new Color(.45f, .45f, .45f, 1)))
            });
            AssetDatabase.CreateAsset(profile, folder + "/Profile.asset");
            settings = new CardPrefabExportSettings
            {
                atlas = original.Atlas,
                mesh = original.GetComponent<MeshFilter>().sharedMesh,
                shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath),
                artProfile = profile,
                cardId = "spades_a",
                outputFolder = folder,
                outputName = "UnlitCard"
            };
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(folder);

        [Test]
        public void ExportPreservesAtlasMeshAndIndependentTintCombinations()
        {
            var check = CardPrefabExporter.Validate(settings);
            Assert.That(check.errors, Is.Empty, string.Join("\n", check.errors));
            var prefab = CardPrefabExporter.Export(settings);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var first = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponentInChildren<CardView>();
                var second = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponentInChildren<CardView>();
                var material = first.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.That(material.FindPass("Unlit2D"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.FindPass("ForwardUnlit"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.FindPass("DepthOnly"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.FindPass("GBuffer"), Is.EqualTo(-1));
                Assert.That(material.FindPass("ShadowCaster"), Is.EqualTo(-1));
                Assert.That(first.Atlas, Is.SameAs(settings.atlas));
                Assert.That(first.ArtProfile, Is.SameAs(settings.artProfile));
                Assert.That(first.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(settings.mesh));
                Assert.That(second.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(material));
                Assert.That(material.GetTexture(CardShaderProperties.BaseMap), Is.SameAs(settings.atlas.texture));
                Assert.That(material.enableInstancing, Is.True);
                settings.atlas.TryGetCard("card_back", out var back);
                Assert.That(material.GetVector(CardShaderProperties.BackUVRect), Is.EqualTo(back.uv));
                var before = EditorJsonUtility.ToJson(material);
                first.PresentArtwork(new[] { "selected" }, Color.white);
                second.PresentArtwork(Array.Empty<string>(), Color.white);
                var block = new MaterialPropertyBlock();
                first.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(Vector4.Distance(block.GetColor(CardShaderProperties.ColorTint), new Color(1, .8f, .35f, 1)), Is.LessThan(.00001f));
                second.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetColor(CardShaderProperties.ColorTint), Is.EqualTo(Color.white));
                first.SetCard("hearts_q");
                settings.atlas.TryGetCard("hearts_q", out var front);
                first.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetVector(CardShaderProperties.FrontUVRect), Is.EqualTo(front.uv));
                Assert.That(Vector4.Distance(block.GetColor(CardShaderProperties.ColorTint), new Color(1, .8f, .35f, 1)), Is.LessThan(.00001f));
                Assert.That(first.SupportsFoil || first.SupportsSeal || first.SupportsRedCross, Is.False);
                Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(before));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ArtworkPreviewRendersUnlitCardsIndependentlyOfLighting()
        {
            var prefab = CardPrefabExporter.Export(settings);
            using (var preview = new CardArtworkPreviewRenderer(prefab))
            {
                preview.Present(settings.artProfile, "spades_a", new[] { "selected" });
                preview.ConfigureLighting(0, 0, 0, new Vector2(45, -35));
                var dark = ReadPixels(preview.Render(new Rect(0, 0, 480, 300), 0));
                preview.ConfigureLighting(4, 2, 1, new Vector2(20, 60));
                var lit = ReadPixels(preview.Render(new Rect(0, 0, 480, 300), 0));
                Assert.That(dark.Count(pixel => pixel.grayscale > .3f), Is.GreaterThan(500), "Cards must remain visible with no preview lights.");
                var difference = dark.Zip(lit, (a, b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)).Average();
                Assert.That(difference, Is.LessThan(.002f), "Unlit card pixels must not depend on lights or ambient intensity.");
                preview.Present(settings.artProfile, "spades_a", new[] { "selected" }, false);
                Assert.That(preview.Current.CardId, Is.EqualTo("card_back"));
                Assert.That(preview.Current.Tint, Is.EqualTo(Color.white));
            }
        }

        [TestCase("MissingForward", "缺少 LightMode = UniversalForwardOnly")]
        [TestCase("NoInstancing", "缺少 multi_compile_instancing")]
        public void UnlitStillRequiresForwardAndInstanceContracts(string variant, string expected)
        {
            var source = File.ReadAllText(ShaderPath)
                .Replace("CardArt/Validation Unlit", "Hidden/CardArt/Tests/" + Guid.NewGuid().ToString("N"))
                .Replace("\"CardSurfaceCommon.hlsl\"", "\"Assets/CardArt/Shaders/CardSurfaceCommon.hlsl\"");
            source = variant == "MissingForward"
                ? source.Replace("\"LightMode\"=\"UniversalForwardOnly\"", "\"LightMode\"=\"Unsupported\"")
                : source.Replace("#pragma multi_compile_instancing", "");
            string path = folder + "/Invalid.shader";
            File.WriteAllText(path, source);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            var check = CardPrefabExporter.Validate(settings);
            Assert.That(check.errors.Any(error => error.Contains(expected)), Is.True, string.Join("\n", check.errors));
            Assert.Throws<ArgumentException>(() => CardPrefabExporter.Export(settings));
            Assert.That(File.Exists(folder + "/UnlitCard.mat"), Is.False);
        }

        [Test]
        public void UnsupportedEffectCombinationsAreRejectedBeforeExport()
        {
            settings.artProfile.Configure(settings.atlas, new[]
            {
                new CardShaderCombination("base", CardShaderParameter.Color("_ColorTint", Color.white),
                    CardShaderParameter.Float("_FoilEnabled", 0))
            });
            var check = CardPrefabExporter.Validate(settings);
            Assert.That(check.errors.Any(error => error.Contains("_FoilEnabled")), Is.True);
            Assert.Throws<ArgumentException>(() => CardPrefabExporter.Export(settings));
            Assert.That(File.Exists(folder + "/UnlitCard.prefab"), Is.False);
        }

        private static Color[] ReadPixels(Texture texture)
        {
            var previous = RenderTexture.active;
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = (RenderTexture)texture;
                pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                pixels.Apply();
                return pixels.GetPixels();
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(pixels); }
        }
    }
}
