using System;
using System.IO;
using System.Linq;
using CardArt.Editor;
using NUnit.Framework;
using UnityEngine;

namespace CardArt.Tests
{
    public sealed class CardImageValidatorTests
    {
        private string folder;
        private CardImageValidator validator;
        private CardImageValidationSettings settings;
        private CardAtlasSettings atlas;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(CardAtlasBuilder.ProjectRoot, "Temp/CardImageTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            validator = new CardImageValidator();
            settings = new CardImageValidationSettings { sourceWidth = 4, sourceHeight = 6 };
            atlas = new CardAtlasSettings { cardWidth = 2, cardHeight = 3, maxSize = 4, padding = 0 };
        }

        [TearDown]
        public void TearDown() { Directory.Delete(folder, true); }

        private string WriteImage(string name, int width, int height)
        {
            var path = Path.Combine(folder, name);
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                File.WriteAllBytes(path, name.EndsWith(".jpg") ? image.EncodeToJPG() : image.EncodeToPNG());
                return path;
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        [Test]
        public void SizeAndRatioAreSeparateAndCacheDetectsChangedFiles()
        {
            var path = WriteImage("spades_a.png", 4, 6);
            Assert.IsFalse(validator.Check(path, settings, atlas).HasIssue);
            WriteImage("spades_a.png", 8, 12);
            // Ensure a different timestamp even on filesystems with coarse timestamp resolution.
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2));
            var resized = validator.Check(path, settings, atlas);
            Assert.AreEqual(new Vector2Int(8, 12), resized.size);
            Assert.IsTrue(resized.sizeMismatch); Assert.IsFalse(resized.ratioMismatch);
            WriteImage("spades_a.png", 4, 8);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(4));
            var distorted = validator.Check(path, settings, atlas);
            Assert.IsTrue(distorted.sizeMismatch); Assert.IsTrue(distorted.ratioMismatch);
            settings.checkSize = false;
            Assert.IsFalse(validator.Check(path, settings, atlas).sizeMismatch);
            Assert.IsTrue(validator.Check(path, settings, atlas).ratioMismatch);
        }

        [Test]
        public void ReplacementUsesItsOwnDimensionsAndBadImagesRemainVisible()
        {
            var recipe = ScriptableObject.CreateInstance<CardAtlasRecipe>();
            try
            {
                recipe.imageValidation = settings; recipe.settings = atlas;
                var entry = new CardAtlasEntry
                {
                    sourcePath = WriteImage("hearts_a.png", 4, 6),
                    replacementPath = WriteImage("replacement.jpg", 6, 6)
                };
                recipe.cards.Add(entry);
                var check = validator.Analyze(recipe).Single();
                Assert.IsFalse(check.original.HasIssue);
                Assert.IsTrue(check.effective.sizeMismatch); Assert.IsTrue(check.effective.ratioMismatch);
                File.Delete(entry.replacementPath);
                Assert.IsNotNull(validator.Analyze(recipe).Single().effective.error);
                File.WriteAllText(entry.replacementPath, "invalid image");
                Assert.IsNotNull(validator.Analyze(recipe).Single().effective.error);
                entry.replacementPath = null;
                Assert.IsFalse(validator.Analyze(recipe).Single().HasIssue);
            }
            finally { UnityEngine.Object.DestroyImmediate(recipe); }
        }

        [TestCase(CardFitMode.Fit, true)]
        [TestCase(CardFitMode.Fill, true)]
        [TestCase(CardFitMode.Stretch, true)]
        public void SingleCardUpscaleDoesNotUseTheWholeGridFitMode(CardFitMode fit, bool expected)
        {
            var path = WriteImage("small.png", 4, 8);
            atlas.cardWidth = 6; atlas.cardHeight = 6; atlas.fit = fit;
            Assert.AreEqual(expected, validator.Check(path, settings, atlas).needsUpscale);
        }

        [TestCase(CardFitMode.Fit, false)]
        [TestCase(CardFitMode.Fill, true)]
        [TestCase(CardFitMode.Stretch, true)]
        public void AnalyzeIncludesTheWholeGridTransformInUpscaleCheck(CardFitMode mode, bool expected)
        {
            var recipe = ScriptableObject.CreateInstance<CardAtlasRecipe>();
            try
            {
                recipe.settings = atlas; recipe.imageValidation = settings;
                atlas.maxSize = 8; atlas.fit = mode; atlas.cardWidth = 2; atlas.cardHeight = 2; atlas.columns = 2;
                recipe.cards.Add(new CardAtlasEntry { id = "a", sourcePath = WriteImage("a.png", 4, 6) });
                recipe.cards.Add(new CardAtlasEntry { id = "b", sourcePath = WriteImage("b.png", 4, 6) });
                Assert.IsTrue(validator.Analyze(recipe).All(check => check.effective.needsUpscale == expected));
            }
            finally { UnityEngine.Object.DestroyImmediate(recipe); }
        }

        [Test]
        public void RatioToleranceSupportsPixelRoundingAndRejectsWrongAtlasAspect()
        {
            Assert.IsTrue(CardImageValidator.MatchesRatio(1024, 1536, settings));
            Assert.IsTrue(CardImageValidator.MatchesRatio(171, 256, settings));
            Assert.IsFalse(CardImageValidator.MatchesRatio(128, 256, settings));
            settings.ratioTolerancePercent = 0;
            Assert.IsFalse(CardImageValidator.MatchesRatio(171, 256, settings));
            Assert.IsFalse(CardImageValidator.MatchesRatio(0, 256, settings));
            settings.ratioWidth = 0;
            Assert.Throws<InvalidOperationException>(() => validator.Check("unused.png", settings, atlas));
        }

        [Test]
        public void FourBeastsChecksOriginalDimensionsRatherThanUnityImportedDimensions()
        {
            var recipe = ScriptableObject.CreateInstance<CardAtlasRecipe>();
            try
            {
                CardAtlasBuilder.Scan(recipe);
                var checks = validator.Analyze(recipe);
                Assert.AreEqual(55, checks.Count);
                Assert.IsTrue(checks.All(check => check.effective.size == new Vector2Int(1024, 1536)));
                Assert.IsTrue(checks.All(check => !check.HasIssue));
            }
            finally { UnityEngine.Object.DestroyImmediate(recipe); }
        }
    }
}
