using System;
using NUnit.Framework;
using PokerEngine;
using RetroRendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    [TestFixture]
    public sealed class HealthCrackTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly Scene scene = EditorSceneManager.NewPreviewScene();
            public readonly Material Material;
            public readonly MeshRenderer Renderer;
            public readonly HealthCrackPresenter Cracks;
            public readonly RigidVATPlayer Vat;
            public Fixture()
            {
                var go = new GameObject("Health cracks"); SceneManager.MoveGameObjectToScene(go, scene);
                Renderer = go.AddComponent<MeshRenderer>();
                Material = new Material(Shader.Find("Rendering/Gargoyle Cracks Deferred"));
                Material.SetFloat("_CrackDepth", .01f); Material.SetFloat("_CrackProgress", .15f); Renderer.sharedMaterial = Material;
                Vat = go.AddComponent<RigidVATPlayer>(); Vat.SetProgress(.42f);
                Cracks = go.AddComponent<HealthCrackPresenter>(); Cracks.Configure(go.transform);
            }
            public void Health(int health) => Cracks.Synchronize(new CombatantState(new ParticipantId("unit"), "test", "team", 100, health));
            public float Property(string name) { var block = new MaterialPropertyBlock(); Renderer.GetPropertyBlock(block); return block.GetFloat(name); }
            public void Dispose() { EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(Material); }
        }
        [Test] public void HealthThresholdsChangeInStepsAndRecoverWhenHealthIncreases()
        {
            using (var f = new Fixture())
            {
                var health = new[] { 100, 76, 75, 51, 50, 26, 25, 0, 100 };
                var stages = new[] { 0, 0, 1, 1, 2, 2, 3, 3, 0 };
                var progress = new[] { 0f, 0f, .35f, .35f, .7f, .7f, 1f, 1f, 0f };
                for (int i = 0; i < health.Length; ++i)
                {
                    f.Health(health[i]); Assert.That(f.Cracks.CurrentStage, Is.EqualTo(stages[i]), "Health " + health[i]);
                    Assert.That(f.Property("_CrackProgress"), Is.EqualTo(progress[i]).Within(.00001f));
                    Assert.That(f.Property("_CrackDepth"), Is.EqualTo(.01f * progress[i]).Within(.00001f));
                }
                f.Health(25); f.Cracks.ResetVisual(); Assert.That(f.Property("_CrackProgress"), Is.Zero);
                Assert.That(f.Property("_CrackDepth"), Is.Zero);
            }
        }
        [Test] public void HealthAndVatPreserveEachOthersPropertiesWithoutMutatingTheMaterial()
        {
            using (var f = new Fixture())
            {
                f.Health(50); Assert.That(f.Property("_VATProgress"), Is.EqualTo(.42f));
                f.Vat.SetProgress(.8f); Assert.That(f.Property("_CrackProgress"), Is.EqualTo(.7f));
                Assert.That(f.Property("_CrackDepth"), Is.EqualTo(.007f).Within(.00001f));
                f.Health(25); Assert.That(f.Property("_VATProgress"), Is.EqualTo(.8f));
                f.Cracks.ResetVisual(); Assert.That(f.Property("_VATProgress"), Is.EqualTo(.8f));
                Assert.That(f.Renderer.sharedMaterial, Is.SameAs(f.Material));
                Assert.That(f.Material.GetFloat("_CrackProgress"), Is.EqualTo(.15f));
                Assert.That(f.Material.GetFloat("_CrackDepth"), Is.EqualTo(.01f));
            }
        }
    }
}
