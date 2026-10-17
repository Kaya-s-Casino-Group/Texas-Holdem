using System;
using System.Linq;
using NUnit.Framework;
using RetroRendering.Editor;
using UnityEditor;
using UnityEngine;

namespace RetroRendering.Tests
{
    public sealed class VolumeNoiseGeneratorTests
    {
        [Test]
        public void BakeIsDeterministicAndSeedChangesTheVolume()
        {
            var settings = new VolumeNoiseGenerator.Settings();
            var first = VolumeNoiseGenerator.Bake(settings);
            Assert.AreEqual(32 * 32 * 32, first.Length);
            CollectionAssert.AreEqual(first, VolumeNoiseGenerator.Bake(settings));
            settings.seed++;
            Assert.IsFalse(first.SequenceEqual(VolumeNoiseGenerator.Bake(settings)));
            Assert.Greater(first.Distinct().Count(), 100, "Noise must contain useful variation.");
        }

        [Test]
        public void NoiseRepeatsAcrossAllAxesIncludingNegativeCoordinates()
        {
            var settings = new VolumeNoiseGenerator.Settings();
            foreach (var p in new[] { new Vector3(.125f, .375f, .75f), new Vector3(-.25f, 0, .5f), Vector3.zero })
            foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                Assert.AreEqual(VolumeNoiseGenerator.Sample(p, settings), VolumeNoiseGenerator.Sample(p + axis, settings), 1e-6);
                float left = VolumeNoiseGenerator.Sample(p - axis * .0001f, settings);
                float right = VolumeNoiseGenerator.Sample(p + axis * .0001f, settings);
                Assert.Less(Mathf.Abs(left - right), .01f, "Tile boundary must be continuous.");
            }
        }

        [Test]
        public void ExcessiveFrequenciesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => VolumeNoiseGenerator.Bake(new VolumeNoiseGenerator.Settings { baseCells = 8, octaves = 4 }));
            Assert.Throws<ArgumentException>(() => VolumeNoiseGenerator.Bake(new VolumeNoiseGenerator.Settings { baseCells = 1, octaves = 1 }));
        }

        [Test]
        public void TextureAssetRetainsGuidAndDataWhenRebaked()
        {
            string path = "Assets/Scripts/Infrastructure/Rendering/Tests/Editor/NoiseTest_" + Guid.NewGuid().ToString("N") + ".asset";
            try
            {
                var settings = new VolumeNoiseGenerator.Settings();
                var texture = VolumeNoiseGenerator.Save(path, settings);
                string guid = AssetDatabase.AssetPathToGUID(path);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture, out string unused, out long id);
                settings.seed = 42;
                texture = VolumeNoiseGenerator.Save(path, settings);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture, out unused, out long updatedId);
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path)); Assert.AreEqual(id, updatedId);
                Assert.AreEqual(TextureFormat.R8, texture.format);
                Assert.AreEqual(1, texture.mipmapCount);
                Assert.AreEqual(32, texture.width); Assert.AreEqual(32, texture.height); Assert.AreEqual(32, texture.depth);
                Assert.AreEqual(TextureWrapMode.Repeat, texture.wrapMode);
                Assert.AreEqual(FilterMode.Bilinear, texture.filterMode);
                Assert.IsFalse(UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat));
                CollectionAssert.AreEqual(VolumeNoiseGenerator.Bake(settings), texture.GetPixelData<byte>(0).ToArray());
                Assert.AreEqual(42, JsonUtility.FromJson<VolumeNoiseGenerator.Settings>(AssetImporter.GetAtPath(path).userData).seed);
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
    }
}
