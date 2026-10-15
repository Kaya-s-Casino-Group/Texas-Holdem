using NUnit.Framework;
using UnityEngine;

namespace RetroRendering.Tests
{
    public sealed class HeightFogCurveTests
    {
        [TestCase(HeightFogFalloff.Linear, .5f)]
        [TestCase(HeightFogFalloff.Smooth, .5f)]
        [TestCase(HeightFogFalloff.GroundHugging, .25f)]
        [TestCase(HeightFogFalloff.HighFill, .75f)]
        public void PresetIntegralsMatchKnownAreas(HeightFogFalloff mode, float expected)
        {
            var table = HeightFogCurveLut.Bake(mode, null);
            Assert.That(table[0], Is.EqualTo(new Vector2(1, 0)));
            Assert.That(table[HeightFogCurveLut.Segments].x, Is.Zero);
            Assert.That(table[HeightFogCurveLut.Segments].y, Is.EqualTo(expected).Within(.00001f));
            for (int i = 1; i < table.Length; i++)
                Assert.That(table[i].y, Is.GreaterThanOrEqualTo(table[i - 1].y));
        }

        [Test] public void CustomCurveClampsDensityAndKeepsBoundaryValuesContinuous()
        {
            var curve = AnimationCurve.Linear(0, 3, 1, -2);
            var table = HeightFogCurveLut.Bake(HeightFogFalloff.Custom, curve);
            foreach (var sample in table) Assert.That(sample.x, Is.InRange(0, 1));
            Assert.That(table[0].x, Is.EqualTo(1)); Assert.That(table[table.Length - 1].x, Is.Zero);
            Assert.That(HeightFogCurveLut.Evaluate(HeightFogFalloff.Custom, null, .25f), Is.EqualTo(.75f));
        }

        [Test] public void LutUpdatesOnlyOnChangesAndReusesItsTexture()
        {
            var lut = new HeightFogCurveLut();
            var curve = AnimationCurve.EaseInOut(0, 1, 1, 0);
            try
            {
                Assert.IsTrue(lut.Update(HeightFogFalloff.Custom, curve));
                var texture = lut.Texture;
                Assert.IsFalse(lut.Update(HeightFogFalloff.Custom, curve));
                curve.AddKey(new Keyframe(.5f, .15f));
                Assert.IsTrue(lut.Update(HeightFogFalloff.Custom, curve)); Assert.AreSame(texture, lut.Texture);
                Assert.That(texture.GetPixelData<Vector2>(0)[128].x, Is.EqualTo(.15f).Within(.00001f));
                var key = curve[1]; key.outTangent = 1.25f; curve.MoveKey(1, key);
                Assert.IsTrue(lut.Update(HeightFogFalloff.Custom, curve));
                Assert.IsFalse(lut.Update(HeightFogFalloff.Custom, curve));
                Assert.IsTrue(lut.Update(HeightFogFalloff.Smooth, curve));
            }
            finally { lut.Dispose(); }
            Assert.IsTrue(lut.Texture == null);
        }
    }
}
