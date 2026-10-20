using System;
using System.IO;
using System.Linq;
using CardArt.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools.Utils;

namespace CardArt.Tests
{
    public sealed class CardPrefabExporterTests
    {
        string folder;
        CardAtlasData atlas;
        Mesh mesh;

        [SetUp]
        public void SetUp()
        {
            folder = "Assets/Scripts/Presentation/CardArt/Tests/PrefabExportTemp_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            atlas = ScriptableObject.CreateInstance<CardAtlasData>();
            var original = AssetDatabase.LoadAssetAtPath<CardAtlasData>(CardRenderingValidation.AtlasPath);
            EditorUtility.CopySerialized(original, atlas);
            AssetDatabase.CreateAsset(atlas, folder + "/Atlas.asset");
            mesh = AssetDatabase.LoadAllAssetsAtPath(CardRenderingValidation.MeshPath).OfType<Mesh>().Single();
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(folder);

        CardPrefabExportSettings Settings() => new CardPrefabExportSettings
        {
            atlas = atlas, shader = Shader.Find(CardShaderProperties.ShaderName), mesh = mesh,
            cardId = "spades_k", outputFolder = folder, outputName = "TestCard"
        };

        Shader TestShader(string name, Func<string, string> mutate)
        {
            var source = File.ReadAllText("Assets/GameAssets/CardArt/Shaders/CardUnlit.shader")
                .Replace("CardArt/Card Unlit Forward", "Hidden/CardArt/Tests/" + name)
                .Replace("\"CardUnlit.hlsl\"", "\"Assets/GameAssets/CardArt/Shaders/CardUnlit.hlsl\"");
            var path = folder + "/" + name + ".shader";
            File.WriteAllText(path, mutate(source));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Shader>(path);
        }

        [Test]
        public void FourBeastsInputsPassAllChecks()
        {
            var check = CardPrefabExporter.Validate(Settings());
            Assert.That(check.errors, Is.Empty, string.Join("\n", check.errors));
            Assert.That(check.faceCount, Is.EqualTo(54));
            Assert.That(check.surfaceBounds.size.y, Is.EqualTo(0.00035f).Within(0.000001f));
        }

        [Test]
        public void ExportPreservesArtProfileAndRejectsAtlasMismatchBeforeWritingAssets()
        {
            var settings = Settings();
            var profile = ScriptableObject.CreateInstance<CardArtProfile>();
            profile.Configure(atlas, CardDeferredShaderCombinations.Create());
            AssetDatabase.CreateAsset(profile,folder + "/Profile.asset"); settings.artProfile = profile;
            var prefab = CardPrefabExporter.Export(settings);
            Assert.AreSame(profile,prefab.GetComponentInChildren<CardView>(true).ArtProfile);
            var original = File.ReadAllText(folder + "/TestCard.prefab");
            profile.Configure(AssetDatabase.LoadAssetAtPath<CardAtlasData>(CardRenderingValidation.AtlasPath),CardDeferredShaderCombinations.Create());
            Assert.IsFalse(CardPrefabExporter.Validate(settings).CanExport);
            Assert.Throws<ArgumentException>(() => CardPrefabExporter.Export(settings));
            Assert.AreEqual(original,File.ReadAllText(folder + "/TestCard.prefab"));
        }

        [Test]
        public void ExportBuildsBoundPrefabAndSharedMaterialWithoutAddingSceneObjects()
        {
            var settings = Settings();
            var scene = SceneManager.GetActiveScene();
            var roots = scene.rootCount;
            var wasDirty = scene.isDirty;
            var prefab = CardPrefabExporter.Export(settings);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            Assert.That(scene.rootCount, Is.EqualTo(roots));
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
            var view = prefab.GetComponentInChildren<CardView>();
            Assert.That(view.CardId, Is.EqualTo("spades_k"));
            Assert.That(view.Atlas, Is.SameAs(atlas));
            Assert.That(view.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
            var material = view.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(AssetDatabase.GetAssetPath(material), Is.EqualTo(folder + "/TestCard.mat"));
            Assert.That(material.GetTexture(CardShaderProperties.BaseMap), Is.SameAs(atlas.texture));
            Assert.That(material.enableInstancing, Is.True);
            atlas.TryGetCard("card_back", out var back);
            Assert.That(material.GetVector(CardShaderProperties.BackUVRect), Is.EqualTo(back.uv));
            Assert.That(prefab.GetComponent<BoxCollider>().size.y, Is.EqualTo(0.001f));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var live = instance.GetComponentInChildren<CardView>();
                Assert.That(live.Tint, Is.EqualTo(Color.white));
                live.SetCard("hearts_q");
                var tint = new Color(0.4f, 0.7f, 1f, 1f);
                live.SetTint(tint);
                var block = new MaterialPropertyBlock();
                live.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                atlas.TryGetCard("hearts_q", out var face);
                Assert.That(block.GetVector(CardShaderProperties.FrontUVRect), Is.EqualTo(face.uv));
                Assert.That(block.GetColor(CardShaderProperties.ColorTint), Is.EqualTo(tint).Using(new ColorEqualityComparer(0.00001f)));
                Assert.That(live.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(material));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        [Test]
        public void SealExportRetainsColorOverlayAndIndependentInstanceSettings()
        {
            var s = Settings();
            s.sealMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GameAssets/CardArt/Textures/WaxSeal_Paw.png");
            s.sealEnabled = true;
            s.sealColor = Color.red;
            s.sealRotation = -17;
            s.sealEmissionStrength = 0.35f;
            var prefab = CardPrefabExporter.Export(s);
            var saved = prefab.GetComponentInChildren<CardView>();
            var mat = saved.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(saved.SealEnabled, Is.True);
            Assert.That(saved.SealColor, Is.EqualTo(Color.red));
            Assert.That(saved.SealEmissionStrength, Is.EqualTo(0.35f));
            Assert.That(mat.GetTexture(CardShaderProperties.SealMap), Is.SameAs(s.sealMap));
            Assert.That(mat.GetVector(CardShaderProperties.SealRect), Is.EqualTo(s.sealRect));
            Assert.That(mat.GetFloat(CardShaderProperties.SealRotation), Is.EqualTo(-17));
            var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var a = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            var b = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            try
            {
                var first = a.GetComponentInChildren<CardView>();
                var second = b.GetComponentInChildren<CardView>();
                first.SetSealColor(Color.blue);
                first.SetSealEnabled(false);
                Assert.Throws<ArgumentOutOfRangeException>(() => first.SetSealEmissionStrength(float.NaN));
                Assert.Throws<ArgumentOutOfRangeException>(() => first.SetSealEmissionStrength(float.PositiveInfinity));
                first.SetSealEmissionStrength(2);
                Assert.That(first.SealEmissionStrength, Is.EqualTo(1));
                first.SetSealEmissionStrength(-1);
                Assert.That(first.SealEmissionStrength, Is.Zero);
                first.SetSealEmissionStrength(0.8f);
                first.SetCard("hearts_q");
                first.SetFoilEnabled(true);
                first.enabled = false;
                first.enabled = true;
                var block = new MaterialPropertyBlock();
                first.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetFloat(CardShaderProperties.SealEnabled), Is.Zero);
                Assert.That(block.GetColor(CardShaderProperties.SealColor), Is.EqualTo(Color.blue));
                Assert.That(block.GetFloat(CardShaderProperties.SealEmissionStrength), Is.EqualTo(0.8f));
                second.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetFloat(CardShaderProperties.SealEnabled), Is.EqualTo(1));
                Assert.That(block.GetColor(CardShaderProperties.SealColor), Is.EqualTo(Color.red));
                Assert.That(block.GetFloat(CardShaderProperties.SealEmissionStrength), Is.EqualTo(0.35f));
                Assert.That(first.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(second.GetComponent<MeshRenderer>().sharedMaterial));
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
            s.sealEnabled = false;
            s.sealEmissionStrength = 0;
            var updated = CardPrefabExporter.Export(s, true);
            Assert.That(updated.GetComponentInChildren<CardView>().SealEnabled, Is.False);
            Assert.That(updated.GetComponentInChildren<CardView>().SealEmissionStrength, Is.Zero);
        }

        [Test]
        public void SealExportRejectsMissingColorMapAndUnsupportedShader()
        {
            var s = Settings();
            s.sealMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GameAssets/CardArt/Textures/WaxSeal_Paw.png");
            s.sealEnabled = true; s.sealMap = null;
            Assert.That(CardPrefabExporter.Validate(s).errors.Any(e => e.Contains("颜色贴图")), Is.True);
            s.sealMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GameAssets/CardArt/Textures/WaxSeal_Paw.png");
            s.shader = TestShader("NoSeal", text => text.Replace("ColorOverlayV1", "Unsupported"));
            Assert.That(CardPrefabExporter.Validate(s).errors.Any(e => e.Contains("CardSealContract")), Is.True);
            s.sealMap = null; s.sealEnabled = false;
            Assert.That(CardPrefabExporter.Validate(s).errors, Is.Empty, "The optional extension must not reject ordinary cards.");
        }

        [Test]
        public void SealEmissionValidatesRangeAndOptionalShaderSupport()
        {
            var s = Settings();
            foreach (float invalid in new[] { -1f, 2f, float.NaN, float.PositiveInfinity })
            {
                s.sealEmissionStrength = invalid;
                Assert.That(CardPrefabExporter.Validate(s).errors.Any(e => e.Contains("自发光强度")), Is.True);
            }
            s.sealEmissionStrength = 0.4f;
            Assert.That(CardPrefabExporter.Validate(s).errors.Any(e => e.Contains("颜色贴图")), Is.True);
            s.sealMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GameAssets/CardArt/Textures/WaxSeal_Paw.png");
            s.shader = TestShader("LegacySeal", source => string.Join("\n", source.Split('\n').Where(line => !line.Contains("_SealEmissionStrength"))));
            Assert.That(CardPrefabExporter.Validate(s).errors.Any(e => e.Contains("_SealEmissionStrength")), Is.True);
            s.sealEmissionStrength = 0;
            Assert.That(CardPrefabExporter.Validate(s).errors, Is.Empty, "Legacy seal shaders remain compatible when emission is zero.");
        }

        [Test]
        public void RedCrossExportKeepsInstancesIndependentAndPreservesLifecycle()
        {
            var s = Settings();
            s.redCrossMap = AssetDatabase.LoadAssetAtPath<Texture2D>(CardRedCrossSetup.TexturePath);
            s.redCrossEnabled = true;
            Assert.That(s.redCrossMap, Is.Not.Null);
            Assert.That(s.redCrossMap.width, Is.EqualTo(128));
            Assert.That(s.redCrossMap.height, Is.EqualTo(192));
            Assert.That(s.redCrossMap.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(s.redCrossMap.mipmapCount, Is.EqualTo(1));
            var prefab = CardPrefabExporter.Export(s);
            Assert.That(prefab.GetComponentInChildren<CardView>().RedCrossEnabled, Is.True);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var first = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponentInChildren<CardView>();
                var second = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponentInChildren<CardView>();
                var material = first.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.That(material.GetTexture(CardShaderProperties.RedCrossMap), Is.SameAs(s.redCrossMap));
                Assert.That(first.SupportsRedCross, Is.True);
                first.SetRedCrossEnabled(false);
                second.SetCard("hearts_q"); second.SetSealEnabled(true); second.SetFoilEnabled(true);
                second.Bind(mesh, material, atlas, "diamonds_a");
                second.enabled = false; second.enabled = true;
                var block = new MaterialPropertyBlock();
                first.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetFloat(CardShaderProperties.RedCrossEnabled), Is.Zero);
                second.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetFloat(CardShaderProperties.RedCrossEnabled), Is.EqualTo(1));
                Assert.That(second.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(material));
                Assert.That(material.enableInstancing, Is.True);
                material.SetTexture(CardShaderProperties.RedCrossMap, null);
                second.SetRedCrossEnabled(true);
                second.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetFloat(CardShaderProperties.RedCrossEnabled), Is.Zero, "Missing texture must not cover the card.");
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            s.redCrossEnabled = false; s.redCrossMap = null;
            var updated = CardPrefabExporter.Export(s, true);
            var view = updated.GetComponentInChildren<CardView>();
            Assert.That(view.RedCrossEnabled, Is.False);
            Assert.That(view.GetComponent<MeshRenderer>().sharedMaterial.GetTexture(CardShaderProperties.RedCrossMap), Is.Null);
        }

        [Test]
        public void RedCrossValidationRejectsMissingOrIncompatibleInputs()
        {
            var s = Settings();
            s.redCrossEnabled = true;
            Assert.That(CardPrefabExporter.Validate(s).errors.Any(e => e.Contains("请选择红叉贴图")), Is.True);
            s.redCrossMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GameAssets/CardArt/Textures/WaxSeal_Paw.png");
            Assert.That(CardPrefabExporter.Validate(s).errors.Any(e => e.Contains("红叉贴图宽高比例")), Is.True);
            s.redCrossMap = AssetDatabase.LoadAssetAtPath<Texture2D>(CardRedCrossSetup.TexturePath);
            s.shader = TestShader("NoRedCross", source => source.Replace("OverlayV1", "Unsupported"));
            Assert.That(CardPrefabExporter.Validate(s).errors.Any(e => e.Contains("CardRedCrossContract")), Is.True);
            s.redCrossEnabled = false; s.redCrossMap = null;
            Assert.That(CardPrefabExporter.Validate(s).errors, Is.Empty);
        }

        [Test]
        public void CompatibleCustomShaderCanBeExportedAndBound()
        {
            var settings = Settings();
            settings.shader = TestShader("Renamed", s => s);
            settings.addCollider = false;
            var check = CardPrefabExporter.Validate(settings);
            Assert.That(check.errors, Is.Empty, string.Join("\n", check.errors));
            var prefab = CardPrefabExporter.Export(settings);
            Assert.That(prefab.GetComponent<BoxCollider>(), Is.Null);
            var material = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Assert.That(material.shader, Is.SameAs(settings.shader));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try { Assert.DoesNotThrow(() => instance.GetComponentInChildren<CardView>().SetCard("joker_big")); }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        [TestCase("WrongType", "_FrontUVRect 必须为 Vector")]
        [TestCase("MissingTint", "缺少属性 _ColorTint")]
        [TestCase("MissingForward", "缺少 LightMode = UniversalForwardOnly")]
        [TestCase("NoInstancing", "缺少 multi_compile_instancing")]
        [TestCase("WrongUVContract", "CardUVRectContract")]
        public void IncompatibleShaderIsRejectedBeforeWriting(string variant, string expected)
        {
            var settings = Settings();
            settings.shader = TestShader(variant, s => variant == "WrongType"
                ? s.Replace("\"Front UV Rect (offset XY, scale ZW)\", Vector", "\"Front UV Rect (offset XY, scale ZW)\", Color")
                : variant == "MissingTint" ? s.Replace("[PerRendererData] _ColorTint (", "[PerRendererData] _UnsupportedColorTint (")
                : variant == "MissingForward" ? s.Replace("\"LightMode\"=\"UniversalForwardOnly\"", "\"LightMode\"=\"Unsupported\"")
                : variant == "NoInstancing" ? s.Replace("#pragma multi_compile_instancing", "")
                : s.Replace("\"CardUVRectContract\"=\"OffsetXYScaleZW\"", "\"CardUVRectContract\"=\"ScaleXYOffsetZW\""));
            var check = CardPrefabExporter.Validate(settings);
            Assert.That(check.errors.Any(e => e.Contains(expected)), Is.True, string.Join("\n", check.errors));
            Assert.Throws<ArgumentException>(() => CardPrefabExporter.Export(settings));
            Assert.That(File.Exists(folder + "/TestCard.mat"), Is.False);
            Assert.That(File.Exists(folder + "/TestCard.prefab"), Is.False);
        }

        [Test]
        public void SharedTintUniformCannotPassAsPerCardInstancing()
        {
            var input = File.ReadAllText("Assets/GameAssets/CardArt/Shaders/CardUnlit.hlsl")
                .Replace("\"CardSurfaceCommon.hlsl\"", "\"Assets/GameAssets/CardArt/Shaders/CardSurfaceCommon.hlsl\"")
                .Replace("UNITY_DEFINE_INSTANCED_PROP(float4, _ColorTint)", "")
                .Replace("half4 _BaseColor, _EdgeColor;", "half4 _BaseColor, _EdgeColor; float4 _ColorTint;")
                .Replace("UNITY_ACCESS_INSTANCED_PROP(CardInstances,_ColorTint)", "_ColorTint");
            File.WriteAllText(folder + "/SharedTint.hlsl", input);
            var settings = Settings();
            settings.shader = TestShader("SharedTint", s => s.Replace("\"Assets/GameAssets/CardArt/Shaders/CardUnlit.hlsl\"",
                "\"" + folder + "/SharedTint.hlsl\""));
            var check = CardPrefabExporter.Validate(settings);
            Assert.That(check.errors.Any(e => e.Contains("_ColorTint 必须声明为 UNITY_DEFINE_INSTANCED_PROP")), Is.True,
                string.Join("\n", check.errors));
            Assert.That(check.errors.Any(e => e.Contains("读取实例 _ColorTint")), Is.True);
            Assert.Throws<ArgumentException>(() => CardPrefabExporter.Export(settings));
            Assert.That(File.Exists(folder + "/TestCard.mat"), Is.False);
        }

        [Test]
        public void StandardUrpShaderDoesNotPassCardContract()
        {
            var settings = Settings();
            settings.shader = Shader.Find("Universal Render Pipeline/Lit");
            var check = CardPrefabExporter.Validate(settings);
            Assert.That(check.errors.Any(e => e.Contains("缺少属性 _FrontUVRect")), Is.True);
        }

        [TestCase("MissingBack")]
        [TestCase("CroppedFace")]
        [TestCase("DuplicateId")]
        [TestCase("InvalidRect")]
        [TestCase("WrongRatio")]
        [TestCase("OutsideAssets")]
        public void InvalidAtlasOrOutputCannotProduceAssets(string variant)
        {
            var settings = Settings();
            if (variant == "MissingBack") atlas.cards.RemoveAll(c => c.id == "card_back");
            if (variant == "CroppedFace") atlas.cards.First(c => c.id == "spades_k").clipped = true;
            if (variant == "DuplicateId") atlas.cards.Add(new CardAtlasRegion { id = "spades_k", uv = new Vector4(0, 0, 0.1f, 0.1f) });
            if (variant == "InvalidRect") atlas.cards.First(c => c.id == "spades_k").uv = new Vector4(0, 0, float.NaN, 0.1f);
            if (variant == "WrongRatio") atlas.cardSize = new Vector2Int(128, 256);
            if (variant == "OutsideAssets") settings.outputFolder = "Assets/../../CardExportEscape";
            Assert.That(CardPrefabExporter.Validate(settings).CanExport, Is.False);
            Assert.Throws<ArgumentException>(() => CardPrefabExporter.Export(settings));
            Assert.That(File.Exists(folder + "/TestCard.mat"), Is.False);
            Assert.That(File.Exists(folder + "/TestCard.prefab"), Is.False);
        }

        [Test]
        public void InvalidSurfaceFlagsAreRejected()
        {
            var invalid = UnityEngine.Object.Instantiate(mesh);
            var flags = invalid.uv2;
            flags[0] = new Vector2(0.5f, 0.5f);
            invalid.uv2 = flags;
            AssetDatabase.CreateAsset(invalid, folder + "/InvalidMesh.asset");
            var settings = Settings(); settings.mesh = invalid;
            Assert.That(CardPrefabExporter.Validate(settings).errors.Any(e => e.Contains("UV1 不符合")), Is.True);
        }

        [Test]
        public void NonReadableImportedMeshCanBeValidatedWithoutChangingImporter()
        {
            var path = folder + "/NonReadable.fbx";
            Assert.That(AssetDatabase.CopyAsset(CardRenderingValidation.MeshPath, path), Is.True);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = false;
            importer.SaveAndReimport();
            var settings = Settings();
            settings.mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Single();
            Assert.That(settings.mesh.isReadable, Is.False);
            var check = CardPrefabExporter.Validate(settings);
            Assert.That(check.errors, Is.Empty, string.Join("\n", check.errors));
            Assert.That(((ModelImporter)AssetImporter.GetAtPath(path)).isReadable, Is.False);
        }

        [Test]
        public void ReexportRequiresExplicitOverwriteAndKeepsAssetGuids()
        {
            var settings = Settings();
            var prefab = CardPrefabExporter.Export(settings);
            var prefabGuid = AssetDatabase.AssetPathToGUID(folder + "/TestCard.prefab");
            var materialGuid = AssetDatabase.AssetPathToGUID(folder + "/TestCard.mat");
            Assert.Throws<InvalidOperationException>(() => CardPrefabExporter.Export(settings));
            settings.cardId = "hearts_q";
            prefab = CardPrefabExporter.Export(settings, true);
            Assert.That(prefab.GetComponentInChildren<CardView>().CardId, Is.EqualTo("hearts_q"));
            Assert.That(AssetDatabase.AssetPathToGUID(folder + "/TestCard.prefab"), Is.EqualTo(prefabGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(folder + "/TestCard.mat"), Is.EqualTo(materialGuid));
        }
    }
}
