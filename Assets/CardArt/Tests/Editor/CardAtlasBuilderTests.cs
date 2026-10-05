using System;
using System.IO;
using System.Linq;
using CardArt.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CardArt.Tests
{
    public sealed class CardAtlasBuilderTests
    {
        private string source;
        private string output;
        private CardAtlasRecipe recipe;

        [SetUp]
        public void SetUp()
        {
            var id = Guid.NewGuid().ToString("N");
            source = Path.Combine(CardAtlasBuilder.ProjectRoot, "Temp/CardAtlasTests-" + id);
            output = "Assets/__CardAtlasTests-" + id;
            Directory.CreateDirectory(source);
            recipe = ScriptableObject.CreateInstance<CardAtlasRecipe>();
            recipe.sourceFolder = source; recipe.outputFolder = output;
            recipe.settings.cardWidth = 2; recipe.settings.cardHeight = 2;
            recipe.settings.padding = 1; recipe.settings.columns = 2;
            recipe.settings.powerOfTwo = false; recipe.settings.maxSize = 8;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(recipe);
            if (Directory.Exists(source)) Directory.Delete(source, true);
            AssetDatabase.DeleteAsset(output);
        }

        private string Image(string name, params Color32[] colors)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(colors); texture.Apply();
                var path = Path.Combine(source, name + ".png");
                File.WriteAllBytes(path, texture.EncodeToPNG()); return path;
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void PackingPreservesOrientationAndExtrudesAllEdges()
        {
            Image("a", Color.red, Color.green, Color.blue, Color.white);
            Image("b", Color.yellow, Color.yellow, Color.yellow, Color.yellow);
            Image("c", Color.magenta, Color.magenta, Color.magenta, Color.magenta);
            CardAtlasBuilder.Scan(recipe);
            var layout = CardAtlasBuilder.CreateLayout(recipe);
            using (var build = CardAtlasBuilder.Build(recipe))
            {
                CollectionAssert.AreEqual(layout.cards.Select(card => card.pixels), build.cards.Select(card => card.pixels));
                CollectionAssert.AreEqual(layout.cards.Select(card => card.uv), build.cards.Select(card => card.uv));
                Assert.AreEqual(8, build.texture.width); Assert.AreEqual(8, build.texture.height);
                Assert.AreEqual(new RectInt(1, 5, 2, 2), build.cards[0].pixels);
                Assert.AreEqual(new RectInt(1, 1, 2, 2), build.cards[2].pixels);
                var p = build.texture.GetPixels32();
                Assert.AreEqual((Color32)Color.red, p[5 * 8 + 1]);
                Assert.AreEqual((Color32)Color.blue, p[6 * 8 + 1]);
                Assert.AreEqual((Color32)Color.red, p[4 * 8]);
                Assert.AreEqual((Color32)Color.white, p[7 * 8 + 3]);
                Assert.AreEqual(new Vector4(.125f, .625f, .25f, .25f), build.cards[0].uv);
                Assert.AreEqual(0, p[0 * 8 + 7].a);
            }
        }

        [Test]
        public void LayoutUpdatesFromSettingsAndSelectionWithoutReadingImages()
        {
            recipe.cards.Add(new CardAtlasEntry { id = "hearts_a", sourcePath = "missing/hearts_a.png" });
            recipe.cards.Add(new CardAtlasEntry { id = "card_back", sourcePath = "missing/card_back.png" });
            recipe.cards.Add(new CardAtlasEntry { id = "spades_a", sourcePath = "missing/spades_a.png", replacementPath = "also_missing.png" });
            var first = CardAtlasBuilder.CreateLayout(recipe);
            Assert.AreEqual(new Vector2Int(8, 8), first.size);
            CollectionAssert.AreEqual(new[] { "card_back", "spades_a", "hearts_a" }, first.cards.Select(card => card.id));
            Assert.AreEqual(new RectInt(1, 5, 2, 2), first.cards[0].pixels);
            recipe.settings.columns = 1;
            var singleColumn = CardAtlasBuilder.CreateLayout(recipe);
            Assert.AreEqual(new Vector2Int(8, 8), singleColumn.size);
            Assert.AreEqual(new Vector2(4, 12), singleColumn.gridSize);
            Assert.That(singleColumn.scale.x, Is.EqualTo(2f / 3).Within(.0001f));
            Assert.IsTrue(singleColumn.cards.All(card => !card.clipped));
            recipe.cards[0].included = false;
            var partial = CardAtlasBuilder.CreateLayout(recipe);
            Assert.AreEqual(new Vector2Int(8, 8), partial.size);
            Assert.AreEqual(Vector2.one, partial.scale);
            Assert.AreEqual(2, partial.cards.Count);
            recipe.settings.columns = 2; recipe.settings.padding = 2;
            recipe.settings.cardWidth = 4; recipe.settings.cardHeight = 6;
            recipe.settings.powerOfTwo = true; recipe.settings.maxSize = 13;
            var resized = CardAtlasBuilder.CreateLayout(recipe);
            Assert.AreEqual(new Vector2Int(16, 16), resized.size);
            Assert.AreEqual(new RectInt(2, 8, 4, 6), resized.cards[0].pixels);
            recipe.order = CardAtlasOrder.Manual;
            recipe.cards.Reverse();
            Assert.AreEqual("spades_a", CardAtlasBuilder.CreateLayout(recipe).cards[0].id);
            foreach (var entry in recipe.cards) entry.included = false;
            Assert.Throws<InvalidOperationException>(() => CardAtlasBuilder.CreateLayout(recipe));
        }

        [Test]
        public void ReplacementKeepsSlotAndSourceFileAndSurvivesScan()
        {
            var a = Image("a", Color.red, Color.red, Color.red, Color.red);
            var b = Image("b", Color.blue, Color.blue, Color.blue, Color.blue);
            var original = File.ReadAllBytes(a);
            CardAtlasBuilder.Scan(recipe);
            recipe.cards[0].replacementPath = b;
            recipe.cards[1].included = false;
            CardAtlasBuilder.Scan(recipe);
            using (var build = CardAtlasBuilder.Build(recipe))
            {
                Assert.AreEqual(1, build.cards.Count); Assert.AreEqual("a", build.cards[0].id);
                Assert.AreEqual((Color32)Color.blue, (Color32)build.texture.GetPixel(4, 4));
                CollectionAssert.AreEqual(original, File.ReadAllBytes(a));
            }
        }

        [Test]
        public void FitFitsTheWholeGridAndDuplicateIdsOrInvalidSideFail()
        {
            Image("a", Color.red, Color.red, Color.red, Color.red);
            Image("b", Color.green, Color.green, Color.green, Color.green);
            CardAtlasBuilder.Scan(recipe);
            recipe.settings.cardWidth = 4;
            using (var build = CardAtlasBuilder.Build(recipe))
            {
                var rect = build.cards[0].pixels;
                var pixels = build.texture.GetPixels32();
                Assert.AreEqual((Color32)Color.red, pixels[rect.y * build.texture.width + rect.x]);
                Assert.AreEqual(0, pixels[0].a);
                Assert.IsTrue(build.cards.All(card => !card.clipped));
            }
            recipe.cards[1].id = "a";
            Assert.Throws<InvalidOperationException>(() => CardAtlasBuilder.Build(recipe));
            recipe.cards[1].id = "b"; recipe.settings.maxSize = 0;
            Assert.Throws<InvalidOperationException>(() => CardAtlasBuilder.Build(recipe));
        }

        [Test]
        public void ExportImportsChosenSettingsAndPreservesGuidOnUpdate()
        {
            Image("a", Color.red, Color.green, Color.blue, Color.white);
            CardAtlasBuilder.Scan(recipe);
            recipe.settings.filter = FilterMode.Bilinear; recipe.settings.wrap = TextureWrapMode.Mirror;
            recipe.settings.mipmaps = true; recipe.settings.sRGB = false;
            using (var build = CardAtlasBuilder.Build(recipe))
            {
                var path = CardAtlasBuilder.Export(recipe, build);
                var guid = AssetDatabase.AssetPathToGUID(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
                Assert.AreEqual(TextureWrapMode.Mirror, importer.wrapMode);
                Assert.IsTrue(importer.mipmapEnabled); Assert.IsFalse(importer.sRGBTexture);
                Assert.AreEqual(TextureImporterNPOTScale.None, importer.npotScale);
                var data = AssetDatabase.LoadAssetAtPath<CardAtlasData>(Path.ChangeExtension(path, ".asset"));
                Assert.AreEqual(1, data.cards.Count); Assert.AreEqual(build.cards[0].uv, data.cards[0].uv);
                Assert.AreEqual(build.texture.width, data.texture.width);
                var manifest = JsonUtility.FromJson<CardAtlasManifest>(File.ReadAllText(CardAtlasBuilder.Absolute(Path.ChangeExtension(path, ".json"))));
                Assert.AreEqual("Bilinear", manifest.filterMode); Assert.AreEqual("a", manifest.cards.Single().id);
                CardAtlasBuilder.Export(recipe, build);
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
                Assert.IsFalse(File.Exists(CardAtlasBuilder.Absolute(Path.ChangeExtension(path, ".mat"))));
            }
        }

        [Test]
        public void GeneratedAtlasIsNotShrunkByIndividualCardImporter()
        {
            output = "Assets/Art/Cards/FourBeasts/__AtlasTest-" + Guid.NewGuid().ToString("N");
            recipe.outputFolder = output;
            Image("a", Color.red, Color.green, Color.blue, Color.white);
            CardAtlasBuilder.Scan(recipe);
            recipe.settings.cardWidth = 256; recipe.settings.cardHeight = 384; recipe.settings.maxSize = 512;
            recipe.settings.filter = FilterMode.Bilinear;
            using (var build = CardAtlasBuilder.Build(recipe))
            {
                var path = CardAtlasBuilder.Export(recipe, build);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.AreEqual(build.texture.width, texture.width);
                Assert.AreEqual(build.texture.height, texture.height);
                Assert.AreEqual(FilterMode.Bilinear, texture.filterMode);
            }
        }

        [Test]
        public void DefaultOrderPacksBackThenSuitsAndManualOrderStillWorks()
        {
            Image("hearts_a", Color.red, Color.red, Color.red, Color.red);
            Image("spades_k", Color.blue, Color.blue, Color.blue, Color.blue);
            Image("spades_a", Color.green, Color.green, Color.green, Color.green);
            Image("card_back", Color.yellow, Color.yellow, Color.yellow, Color.yellow);
            Image("logo", Color.white, Color.white, Color.white, Color.white);
            Image("spade_01_copy", Color.cyan, Color.cyan, Color.cyan, Color.cyan);
            CardAtlasBuilder.Scan(recipe);
            var ace = recipe.cards.Single(card => card.id == "spades_a");
            ace.id = "custom_ace";
            ace.replacementPath = recipe.cards.Single(card => card.id == "hearts_a").sourcePath;
            recipe.cards.Single(card => card.id == "spades_k").included = false;
            var storedOrder = recipe.cards.ToArray();
            using (var build = CardAtlasBuilder.Build(recipe))
            {
                CollectionAssert.AreEqual(new[] { "card_back", "spade_01_copy", "custom_ace", "hearts_a", "logo" }, build.cards.Select(card => card.id));
                Assert.AreEqual(0, build.cards[0].index);
                Assert.Greater(build.cards[0].pixels.y, build.cards.Last().pixels.y);
                Assert.LessOrEqual(build.cards[0].pixels.x, 1);
                Assert.AreEqual((Color32)Color.yellow, build.texture.GetPixels32()[build.cards[0].pixels.y * build.texture.width + 1]);
                CollectionAssert.AreEqual(storedOrder, recipe.cards);
                Assert.IsFalse(recipe.cards.Single(card => card.id == "spades_k").included);
                Assert.IsNotNull(ace.replacementPath);
            }
            recipe.order = CardAtlasOrder.Manual;
            using (var build = CardAtlasBuilder.Build(recipe))
                CollectionAssert.AreEqual(recipe.cards.Where(card => card.included).Select(card => card.id), build.cards.Select(card => card.id));
            recipe.order = CardAtlasOrder.BySuit;
            recipe.cards.Single(card => card.id == "card_back").included = false;
            using (var build = CardAtlasBuilder.Build(recipe))
                Assert.AreEqual("spade_01_copy", build.cards[0].id);
        }

        [TestCase(false, 13, 13)]
        [TestCase(true, 13, 16)]
        [TestCase(false, 1, 1)]
        public void RequestedSideIsSquareAndIndependentOfGridSize(bool powerOfTwo, int requested, int side)
        {
            recipe.settings.powerOfTwo = powerOfTwo; recipe.settings.maxSize = requested;
            Assert.AreEqual(new Vector2Int(side, side), CardAtlasBuilder.GetSize(recipe.settings, 55));
            recipe.settings.cardWidth = 4096; recipe.settings.cardHeight = 4096;
            Assert.AreEqual(new Vector2Int(side, side), CardAtlasBuilder.GetSize(recipe.settings, 1));
            recipe.settings.maxSize = 8193;
            Assert.Throws<InvalidOperationException>(() => CardAtlasBuilder.GetSize(recipe.settings, 1));
        }

        [TestCase(CardFitMode.Fit)]
        [TestCase(CardFitMode.Fill)]
        [TestCase(CardFitMode.Stretch)]
        public void WholeGridModesMatchPixelsAndPreviewAndReportCroppedCards(CardFitMode mode)
        {
            Image("a", Color.red, Color.red, Color.red, Color.red);
            Image("b", Color.green, Color.green, Color.green, Color.green);
            CardAtlasBuilder.Scan(recipe);
            recipe.settings.cardWidth = 4; recipe.settings.cardHeight = 2;
            recipe.settings.padding = 0; recipe.settings.fit = mode;
            // Grid 8x2 into an 8x8 atlas: Fit uses 1x1, Fill 4x4, Stretch 1x4.
            var layout = CardAtlasBuilder.CreateLayout(recipe);
            Assert.AreEqual(new Vector2(8, 2), layout.gridSize);
            Assert.AreEqual(new Vector2(mode == CardFitMode.Fill ? 4 : 1, mode == CardFitMode.Fit ? 1 : 4), layout.scale);
            Assert.AreEqual(new Vector2(0, 8), new Vector2(layout.cellRects[0].xMin, layout.cellRects[0].yMax));
            using (var build = CardAtlasBuilder.Build(recipe))
            {
                CollectionAssert.AreEqual(layout.cards.Select(card => card.uv), build.cards.Select(card => card.uv));
                Assert.AreEqual((Color32)Color.red, (Color32)build.texture.GetPixel(0, 7));
                Assert.AreEqual(mode == CardFitMode.Fill ? (Color32)Color.red : (Color32)Color.green, (Color32)build.texture.GetPixel(7, 7));
                Assert.AreEqual(mode == CardFitMode.Fit ? 0 : 255, build.texture.GetPixels32()[0].a);
                Assert.AreEqual(mode == CardFitMode.Fill, build.cards.Any(card => card.clipped));
                if (mode == CardFitMode.Fill)
                {
                    Assert.AreEqual(0, build.cards[1].visiblePixels.width);
                    Assert.Greater(build.cards[0].uv.z, 1);
                    var path = CardAtlasBuilder.Export(recipe, build);
                    var manifest = JsonUtility.FromJson<CardAtlasManifest>(File.ReadAllText(CardAtlasBuilder.Absolute(Path.ChangeExtension(path, ".json"))));
                    Assert.IsTrue(manifest.cards[1].clipped);
                    Assert.AreEqual(0, manifest.cards[1].visiblePixels.width);
                }
            }
        }

        [Test]
        public void NonPowerOfTwoExportKeepsTheExactIntegerDimensions()
        {
            Image("a", Color.red, Color.green, Color.blue, Color.white);
            CardAtlasBuilder.Scan(recipe); recipe.settings.maxSize = 37;
            using (var build = CardAtlasBuilder.Build(recipe))
            {
                var path = CardAtlasBuilder.Export(recipe, build);
                var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.AreEqual(37, imported.width); Assert.AreEqual(37, imported.height);
                Assert.AreEqual(FilterMode.Point, imported.filterMode);
                Assert.IsTrue(build.cards.All(card => !card.clipped));
            }
        }

        [Test]
        public void FourBeastsDeckBuildsAllCardsWithoutMutatingSourceAssets()
        {
            recipe.sourceFolder = "Assets/Art/Cards/FourBeasts";
            recipe.settings = new CardAtlasSettings();
            CardAtlasBuilder.Scan(recipe);
            Assert.AreEqual(55, recipe.cards.Count);
            Assert.IsTrue(CardDeckValidator.Analyze(recipe.cards).IsExactDeck);
            using (var build = CardAtlasBuilder.Build(recipe))
            {
                Assert.AreEqual(2048, build.texture.width); Assert.AreEqual(2048, build.texture.height);
                Assert.AreEqual(55, build.cards.Count);
                var ranks = new[] { "a", "02", "03", "04", "05", "06", "07", "08", "09", "10", "j", "q", "k" };
                var expected = new[] { "card_back" }
                    .Concat(new[] { "spades", "hearts", "clubs", "diamonds" }.SelectMany(suit => ranks.Select(rank => suit + "_" + rank)))
                    .Concat(new[] { "joker_big", "joker_small" });
                CollectionAssert.AreEqual(expected, build.cards.Select(card => card.id));
                foreach (var suit in new[] { "spades", "hearts", "clubs", "diamonds" })
                {
                    Assert.IsTrue(build.cards.Any(card => card.id == suit + "_a"), "Missing Ace: " + suit);
                }
                Assert.IsTrue(build.cards.Any(card => card.id == "card_back"));
                Assert.IsTrue(build.cards.Any(card => card.id == "joker_small"));
                foreach (var card in build.cards)
                {
                    Assert.That(card.uv.x, Is.InRange(0f, 1f)); Assert.That(card.uv.y, Is.InRange(0f, 1f));
                    Assert.LessOrEqual(card.uv.x + card.uv.z, 1f); Assert.LessOrEqual(card.uv.y + card.uv.w, 1f);
                }
            }
        }
    }
}
