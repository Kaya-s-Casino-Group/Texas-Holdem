using NUnit.Framework;
using UnityEngine;

namespace RetroRendering.Tests
{
    public sealed class HeightFogNoiseTests
    {
        [Test] public void PhaseWrapsContinuouslyOnEveryAxisEvenAfterLongRunningTime()
        {
            var s = new HeightFogFeature.Settings { noiseWorldSize = new Vector3(2, 4, 8),
                noiseVelocity = new Vector3(.5f, -1, 2), noiseOffset = new Vector3(-3, 2, 9) };
            var phase = s.NoisePhase(123456789.125);
            var next = s.NoisePhase(123456789.125 + .001);
            for (int axis = 0; axis < 3; axis++)
            {
                Assert.That(phase[axis], Is.InRange(0f, 1f));
                float delta = Mathf.DeltaAngle(phase[axis] * 360, next[axis] * 360) / 360;
                Assert.That(delta, Is.EqualTo(-s.noiseVelocity[axis] * .001f / s.NoiseSize[axis]).Within(1e-6));
                var offset = s.noiseOffset; offset[axis] += s.NoiseSize[axis]; s.noiseOffset = offset;
                Assert.That(Vector3.Distance(s.NoisePhase(123456789.125), phase), Is.LessThan(1e-6));
            }
        }

        [Test] public void TextureOptionNeedsTextureAndPositiveStrength()
        {
            var s = new HeightFogFeature.Settings { useNoiseTexture = true };
            Assert.IsFalse(s.NoiseEnabled);
            var texture = new Texture3D(2, 2, 2, TextureFormat.R8, false);
            try
            {
                s.noiseTexture = texture; Assert.IsTrue(s.NoiseEnabled);
                s.noiseStrength = 0; Assert.IsFalse(s.NoiseEnabled);
                s.noiseStrength = 1; s.useNoiseTexture = false; Assert.IsFalse(s.NoiseEnabled);
                s.noiseWorldSize = new Vector3(0, -2, 4);
                Assert.That(s.NoiseSize, Is.EqualTo(new Vector3(.001f, 2, 4)));
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
