using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CardArt;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    public sealed class CardSuitReadabilityTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly Scene scene = EditorSceneManager.NewPreviewScene();
            public readonly CardView World;
            public readonly CardUIItemView Item;

            public Fixture()
            {
                var world = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"));
                SceneManager.MoveGameObjectToScene(world, scene);
                World = world.GetComponentInChildren<CardView>();
                var item = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardPresentation/Prefabs/CardUIItem.prefab"));
                SceneManager.MoveGameObjectToScene(item, scene);
                Item = item.GetComponent<CardUIItemView>();
                ((RectTransform)Item.transform).sizeDelta = new Vector2(80, 120);
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose() => EditorSceneManager.ClosePreviewScene(scene);
        }

        // Catches a missing world corner layer, the wrong face's glyph, a mirrored
        // lower corner, and a badge enlarged into the central beast illustration.
        [TestCase("spades_j")]
        [TestCase("spades_q")]
        [TestCase("spades_k")]
        [TestCase("clubs_j")]
        [TestCase("clubs_q")]
        [TestCase("clubs_k")]
        [TestCase("hearts_q")]
        [TestCase("diamonds_k")]
        public void WorldFaceCornersEnlargeTheirOwnSuitWithoutCoveringTheIllustration(string face)
        {
            using (var fixture = new Fixture())
            {
                fixture.World.SetCard(face);
                var renderers = ExtraWorldRenderers(fixture.World);
                Assert.That(renderers, Is.Not.Empty, "Small world cards need a visible suit enhancement.");
                var vertices = new List<Vector3>();
                var uv = new List<Vector2>();
                var original = fixture.World.GetComponent<MeshFilter>().sharedMesh;
                foreach (var renderer in renderers)
                {
                    Assert.That(renderer.sharedMaterial.mainTexture, Is.SameAs(fixture.World.Atlas.texture));
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh, Is.Not.Null);
                    vertices.AddRange(mesh.vertices.Select(vertex => fixture.World.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex))));
                    uv.AddRange(mesh.uv);
                }
                Assert.That(vertices.Count, Is.EqualTo(8), "One readable suit is required at each opposing corner.");
                var frame = new FaceFrame(original);
                var normalized = vertices.Select(frame.Normalize).ToArray();
                AssertCorners(normalized.Select(point => new Vector2(point.x, point.y)).ToArray());
                Assert.That(normalized.All(point => point.z > 0), Is.True, "Corner symbols must sit on the visible front, above its surface.");
                AssertOwnCornerSampling(fixture.World.Atlas, face, uv.ToArray());
            }
        }

        // Catches a stale suit leaking when a pooled world card becomes hidden.
        [Test] public void WorldRebindingClearsSuitsForBacksAndJokersAndRestoresTheNextFace()
        {
            using (var fixture = new Fixture())
            {
                fixture.World.SetCard("clubs_k");
                Assert.That(ExtraWorldRenderers(fixture.World), Is.Not.Empty);
                fixture.World.SetCard("card_back");
                Assert.That(ExtraWorldRenderers(fixture.World), Is.Empty, "A back must not reveal its previous suit.");
                fixture.World.SetCard("joker_small");
                Assert.That(ExtraWorldRenderers(fixture.World), Is.Empty, "Jokers have no standard suit.");
                fixture.World.SetCard("spades_q");
                fixture.World.enabled = false;
                fixture.World.enabled = true;
                var renderers = ExtraWorldRenderers(fixture.World);
                Assert.That(renderers, Is.Not.Empty);
                AssertOwnCornerSampling(fixture.World.Atlas, "spades_q", renderers.SelectMany(renderer => renderer.GetComponent<MeshFilter>().sharedMesh.uv).ToArray());
            }
        }

        // Catches thumbnails retaining tiny glyphs, swallowing pointer events,
        // or leaving a corner behind while their parent card scales/rotates.
        [TestCase("spades_j")]
        [TestCase("clubs_k")]
        [TestCase("hearts_q")]
        [TestCase("diamonds_j")]
        public void CanvasFaceCornersRemainLegibleAndMoveWithTheCard(string face)
        {
            using (var fixture = new Fixture())
            {
                fixture.Item.Present(new CardVisualData("thumbnail", face), true, CardZoneMotionSettings.Default);
                var corners = ExtraUiGraphics(fixture.Item);
                Assert.That(corners, Has.Length.EqualTo(1), "Deck and guide thumbnails need the same two suit corners.");
                var corner = corners.Single();
                Assert.That(corner.raycastTarget, Is.False);
                Assert.That(corner.transform.IsChildOf(fixture.Item.Graphic.transform), Is.True);
                Assert.That(corner.mainTexture, Is.SameAs(fixture.Item.Graphic.Atlas.texture));
                var vertices = UiVertices(corner);
                Assert.That(vertices, Has.Length.EqualTo(8));
                var rect = fixture.Item.Graphic.rectTransform.rect;
                var normalized = vertices.Select(vertex => new Vector2((vertex.position.x - rect.xMin) / rect.width,
                    (vertex.position.y - rect.yMin) / rect.height)).ToArray();
                AssertCorners(normalized);
                AssertOwnCornerSampling(fixture.Item.Graphic.Atlas, face, vertices.Select(vertex => (Vector2)vertex.uv0).ToArray());
                fixture.Item.SetHovered(true, false);
                Assert.That(corner.transform.lossyScale.x, Is.EqualTo(fixture.Item.Graphic.transform.lossyScale.x).Within(.00001f));
            }
        }

        // Catches a pooled thumbnail showing the previous suit or ignoring the
        // deck's used-card opacity, and catches duplicate overlays after refresh.
        [Test] public void CanvasReuseResetsSuitAndFollowsUsedCardOpacity()
        {
            using (var fixture = new Fixture())
            {
                fixture.Item.Present(new CardVisualData("same-slot", "clubs_k"), true, CardZoneMotionSettings.Default);
                Assert.That(ExtraUiGraphics(fixture.Item), Is.Not.Empty);
                fixture.Item.Present(new CardVisualData("same-slot"), false, CardZoneMotionSettings.Default);
                Assert.That(ExtraUiGraphics(fixture.Item), Is.Empty, "A hidden face must clear the previously visible suit.");
                for (int index = 0; index < 3; index++)
                    fixture.Item.Present(new CardVisualData("replacement", "spades_q"), false, CardZoneMotionSettings.Default);
                var corners = ExtraUiGraphics(fixture.Item);
                Assert.That(corners, Has.Length.EqualTo(1));
                var vertices = UiVertices(corners.Single());
                AssertOwnCornerSampling(fixture.Item.Graphic.Atlas, "spades_q", vertices.Select(vertex => (Vector2)vertex.uv0).ToArray());
                Assert.That(vertices.All(vertex => Mathf.Abs(vertex.color.a / 255f - .28f) <= 1f / 255), Is.True,
                    "Suit corners must fade with an already drawn card.");
            }
        }

        // The hand guide owns CardUIGraphic directly; an item-wrapper-only fix
        // would improve the deck while leaving guide examples unreadable.
        [Test] public void DirectGuideGraphicPresentationRefreshesAndClearsItsSuit()
        {
            using (var fixture = new Fixture())
            {
                fixture.Item.Graphic.Present(new CardVisualData("guide-example", "clubs_q"));
                var corners = ExtraUiGraphics(fixture.Item);
                Assert.That(corners, Has.Length.EqualTo(1));
                AssertOwnCornerSampling(fixture.Item.Graphic.Atlas, "clubs_q", UiVertices(corners.Single()).Select(vertex => (Vector2)vertex.uv0).ToArray());
                fixture.Item.Graphic.Present(new CardVisualData("guide-example"));
                Assert.That(ExtraUiGraphics(fixture.Item), Is.Empty);
            }
        }

        [Test] public void DisablingTheCanvasSurfaceHidesItsSuitUntilTheSurfaceReturns()
        {
            using (var fixture = new Fixture())
            {
                fixture.Item.Graphic.Present(new CardVisualData("guide-example", "spades_k"));
                Assert.That(ExtraUiGraphics(fixture.Item), Is.Not.Empty);
                fixture.Item.Graphic.enabled = false;
                Assert.That(ExtraUiGraphics(fixture.Item), Is.Empty, "Disabling the card surface must also hide its suit.");
                fixture.Item.Graphic.enabled = true;
                Assert.That(ExtraUiGraphics(fixture.Item), Has.Length.EqualTo(1));
            }
        }

        [Test] public void AThumbnailDiscardedBeforeActivationDoesNotRetainTheSharedMaterial()
        {
            Material material;
            using (var fixture = new Fixture())
            {
                fixture.Item.Graphic.Present(new CardVisualData("visible", "clubs_k"));
                material = ExtraUiGraphics(fixture.Item).Single().material;
                var dormant = new GameObject("Discarded before activation", typeof(RectTransform));
                dormant.SetActive(false);
                try
                {
                    var surface = dormant.AddComponent<CardUIGraphic>();
                    surface.Bind(fixture.Item.Graphic.Atlas, fixture.Item.Graphic.material);
                    surface.SetArtProfile(fixture.Item.Graphic.ArtProfile);
                    surface.Present(new CardVisualData("dormant", "clubs_k"));
                }
                finally { Object.DestroyImmediate(dormant); }
            }
            Assert.That(material == null, Is.True, "A never activated thumbnail must not keep a shared corner material alive after the visible card closes.");
        }

        [UnityTest] public IEnumerator WorldCornersFollowDealDelayAndProjectileSourceVisibility()
        {
            using (var fixture = new Fixture())
            {
                fixture.World.SetCard("clubs_k");
                var surface = fixture.World.GetComponent<MeshRenderer>();
                // CardInstanceView's delayed deal toggles this renderer, while
                // BattleCardProjectileAction temporarily forces its source off.
                surface.enabled = false;
                EditorApplication.QueuePlayerLoopUpdate(); yield return null;
                Assert.That(ExtraWorldRenderers(fixture.World), Is.Empty, "A delayed deal must not leave floating suit corners.");
                surface.enabled = true;
                EditorApplication.QueuePlayerLoopUpdate(); yield return null;
                Assert.That(ExtraWorldRenderers(fixture.World), Is.Not.Empty);
                surface.forceRenderingOff = true;
                EditorApplication.QueuePlayerLoopUpdate(); yield return null;
                Assert.That(ExtraWorldRenderers(fixture.World), Is.Empty, "A projectile must hide its source's suit with its source card.");
                surface.forceRenderingOff = false;
                EditorApplication.QueuePlayerLoopUpdate(); yield return null;
                Assert.That(ExtraWorldRenderers(fixture.World), Is.Not.Empty);
            }
        }

        private static MeshRenderer[] ExtraWorldRenderers(CardView view) => view.GetComponentsInChildren<MeshRenderer>(true)
            .Where(renderer => renderer != view.GetComponent<MeshRenderer>() && renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy).ToArray();

        private static Graphic[] ExtraUiGraphics(CardUIItemView item) => item.Graphic.GetComponentsInChildren<Graphic>(true)
            .Where(graphic => graphic != item.Graphic && graphic.isActiveAndEnabled).ToArray();

        private static UIVertex[] UiVertices(Graphic graphic)
        {
            using (var helper = new VertexHelper())
            {
                graphic.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(graphic, new object[] { helper });
                var vertices = new UIVertex[helper.currentVertCount];
                for (int index = 0; index < vertices.Length; index++) helper.PopulateUIVertex(ref vertices[index], index);
                return vertices;
            }
        }

        private static void AssertCorners(Vector2[] points)
        {
            var upper = points.Where(point => point.x < .5f).ToArray();
            var lower = points.Where(point => point.x > .5f).ToArray();
            Assert.That(upper, Has.Length.EqualTo(4));
            Assert.That(lower, Has.Length.EqualTo(4));
            Assert.That(upper.All(point => point.x >= .03f && point.x <= .20f && point.y >= .76f && point.y <= .88f), Is.True,
                "The enhancement must stay below the rank and outside the central illustration.");
            Assert.That(lower.All(point => point.x >= .80f && point.x <= .97f && point.y >= .12f && point.y <= .24f), Is.True);
            Assert.That(upper.Max(point => point.x) - upper.Min(point => point.x), Is.GreaterThanOrEqualTo(.125f),
                "An 80-pixel thumbnail needs a suit wider than the original seven-pixel glyph.");
        }

        private static void AssertOwnCornerSampling(CardAtlasData atlas, string face, Vector2[] uv)
        {
            Assert.That(atlas.TryGetCard(face, out var region), Is.True);
            Assert.That(uv, Has.Length.EqualTo(8));
            var local = uv.Select(point => new Vector2((point.x - region.uv.x) / region.uv.z,
                (point.y - region.uv.y) / region.uv.w)).ToArray();
            Assert.That(local.All(point => point.x >= .045f && point.x <= .18f && point.y >= .77f && point.y <= .88f), Is.True,
                "Both corners must reuse this face's upper suit, preserving its existing silhouette.");
            Assert.That(local.Take(4).First().x, Is.LessThan(local.Take(4).Last().x));
            Assert.That(local.Skip(4).First().x, Is.GreaterThan(local.Skip(4).Last().x), "The opposing suit must turn with the original lower rank.");
        }

        private sealed class FaceFrame
        {
            private readonly Vector3 origin, right, up, normal;
            public FaceFrame(Mesh mesh)
            {
                var front = Enumerable.Range(0, mesh.vertexCount).Where(index => mesh.uv2[index] == Vector2.zero).ToArray();
                var vertices = mesh.vertices;
                var uv = mesh.uv;
                int first = front[0];
                int second = front.First(index => Mathf.Abs(uv[index].x - uv[first].x) > .25f);
                int third = front.First(index => Mathf.Abs((uv[second].x - uv[first].x) * (uv[index].y - uv[first].y)
                    - (uv[second].y - uv[first].y) * (uv[index].x - uv[first].x)) > .1f);
                var a = uv[second] - uv[first]; var b = uv[third] - uv[first];
                var p = vertices[second] - vertices[first]; var q = vertices[third] - vertices[first];
                float determinant = a.x * b.y - a.y * b.x;
                right = (p * b.y - q * a.y) / determinant;
                up = (q * a.x - p * b.x) / determinant;
                origin = vertices[first] - right * uv[first].x - up * uv[first].y;
                normal = mesh.normals[first];
            }
            public Vector3 Normalize(Vector3 point)
            {
                var delta = point - origin;
                return new Vector3(Vector3.Dot(delta, right) / right.sqrMagnitude, Vector3.Dot(delta, up) / up.sqrMagnitude,
                    Vector3.Dot(delta, normal));
            }
        }
    }
}
