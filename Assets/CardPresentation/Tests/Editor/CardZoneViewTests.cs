using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CardArt;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    [TestFixture]
    public sealed class CardZoneViewTests
    {
        private static CardVisualData Card(string id, string art = "spades_a") =>
            new CardVisualData(id, art, new CardInstanceId(id));
        private static CardZoneViewData Data(params CardVisualData[] cards) => new CardZoneViewData(new ZoneId("zone"), cards);

        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene;
            public readonly GameObject Root;
            public readonly GameObject Prefab;
            public readonly HandZoneView Zone;
            public Fixture()
            {
                Scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    Root = new GameObject("Zone Test") { hideFlags = HideFlags.HideAndDontSave };
                    SceneManager.MoveGameObjectToScene(Root, Scene);
                    Zone = Root.AddComponent<HandZoneView>();
                var lift = CardZoneLiftSettings.Default; lift.Enabled = false; Zone.SetLiftSettings(lift);
                    var transitions = CardZoneTransitionSettings.Default; transitions.Enabled = false; Zone.SetTransitionSettings(transitions);
                    Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab");
                    Zone.Configure(Prefab, CardZoneLayoutSettings.Default);
                }
                catch { EditorSceneManager.ClosePreviewScene(Scene); throw; }
            }
            public void Dispose() => EditorSceneManager.ClosePreviewScene(Scene);
        }

        [Test] public void ProjectionPreservesZoneOrderAndCopiesOnlyDisclosedData()
        {
            var zone = new CardZone(new ZoneId("hand"), CardZoneKinds.Hand, new ParticipantId("owner"),
                new[] { new CardInstanceId("b"), new CardInstanceId("a") });
            var projected = CardZoneViewData.FromZone(zone, id => Card(id.Value));
            CollectionAssert.AreEqual(new[] { "b", "a" }, projected.Cards.Select(c => c.ViewId));
            Assert.AreEqual(zone.Id, projected.ZoneId);
            var hidden = Data(new CardVisualData("opaque-slot"));
            Assert.IsNull(hidden.Cards[0].CardId); Assert.IsNull(hidden.Cards[0].FaceArtId);
            Assert.Throws<NotSupportedException>(() => ((IList<CardVisualData>)projected.Cards).Clear());
            Assert.Throws<ArgumentException>(() => CardZoneViewData.FromZone(zone, _ => Card("wrong")));
            Assert.Throws<ArgumentException>(() => Data(Card("same"), Card("same")));
            var input = new[] { Card("a") }; var data = Data(input); input[0] = Card("b"); Assert.AreEqual("a", data.Cards[0].ViewId);
        }

        [Test] public void PresentCreatesDistinctInstancesAndSharesExistingRenderAssets()
        {
            using (var f = new Fixture())
            {
                f.Zone.Present(Data(Card("a"), Card("b")));
                Assert.AreEqual(2, f.Zone.Views.Count);
                var a = f.Zone.Views[0]; var b = f.Zone.Views[1];
                Assert.AreNotSame(a, b); Assert.AreNotEqual(a.CardId, b.CardId);
                Assert.AreEqual("spades_a", a.Surface.CardId); Assert.AreEqual("spades_a", b.Surface.CardId);
                var original = f.Prefab.GetComponentInChildren<CardView>();
                Assert.AreSame(original.GetComponent<MeshFilter>().sharedMesh, a.Surface.GetComponent<MeshFilter>().sharedMesh);
                Assert.AreSame(original.GetComponent<MeshRenderer>().sharedMaterial, a.Surface.GetComponent<MeshRenderer>().sharedMaterial);
                Assert.AreSame(a.Surface.GetComponent<MeshRenderer>().sharedMaterial, b.Surface.GetComponent<MeshRenderer>().sharedMaterial);
                Assert.IsFalse(a.GetComponentsInChildren<Collider>(true).Any(c => c.enabled));
                Assert.IsTrue(f.Prefab.GetComponent<Collider>().enabled);
            }
        }

        [Test] public void ReorderAndFaceUpdatesReuseInstancesByDisplayIdentity()
        {
            using (var f = new Fixture())
            {
                f.Zone.Present(Data(Card("a"), Card("b"))); var a = f.Zone.Views[0]; var b = f.Zone.Views[1];
                f.Zone.Present(Data(Card("b", "hearts_q"), Card("a")));
                Assert.AreSame(b, f.Zone.Views[0]); Assert.AreSame(a, f.Zone.Views[1]);
                Assert.AreEqual("hearts_q", b.Surface.CardId);
                Assert.Less(b.transform.localPosition.x, a.transform.localPosition.x);
                Assert.IsTrue(f.Zone.TryGetView("a", out var found)); Assert.AreSame(a, found);
                Assert.IsFalse(f.Zone.TryGetView("missing", out _));
            }
        }

        [Test] public void InsertRemoveAndEmptyUpdatesOnlyReleaseOwnedMissingViews()
        {
            using (var f = new Fixture())
            {
                var unrelated = new GameObject("Unrelated child"); unrelated.transform.SetParent(f.Root.transform, false);
                f.Zone.Present(Data(Card("a"), Card("b"))); var a = f.Zone.Views[0]; var removed = f.Zone.Views[1];
                f.Zone.Present(Data(Card("new"), Card("a")));
                Assert.IsTrue(removed == null); Assert.AreSame(a, f.Zone.Views[1]); Assert.IsTrue(unrelated != null);
                f.Zone.Present(Data()); Assert.AreEqual(0, f.Zone.Views.Count); Assert.IsTrue(a == null);
                Assert.AreEqual(new ZoneId("zone"), f.Zone.Data.ZoneId); Assert.IsTrue(unrelated != null);
            }
        }

        [Test] public void UnknownRevealAndHideReuseTheViewWithoutKeepingASecretFace()
        {
            using (var f = new Fixture())
            {
                f.Zone.Present(Data(new CardVisualData("slot"))); var view = f.Zone.Views[0];
                Assert.IsNull(view.CardId); Assert.AreEqual("card_back", view.Surface.CardId);
                Assert.Less((view.FaceRoot.localRotation * Vector3.up).y, -0.99f);
                f.Zone.Present(Data(new CardVisualData("slot", "hearts_q", new CardInstanceId("revealed"), Color.red)));
                Assert.AreSame(view, f.Zone.Views[0]); Assert.AreEqual("hearts_q", view.Surface.CardId);
                Assert.AreEqual(Color.red, view.Surface.Tint); Assert.Greater((view.FaceRoot.localRotation * Vector3.up).y, 0.99f);
                f.Zone.Present(Data(new CardVisualData("slot")));
                Assert.IsNull(view.CardId); Assert.AreEqual("card_back", view.Surface.CardId); Assert.AreEqual(Color.white, view.Surface.Tint);
            }
        }

        [Test] public void ChangingZoneDoesNotReuseAnUnrelatedContextsSameKey()
        {
            using (var f = new Fixture())
            {
                f.Zone.Present(Data(Card("a"))); var old = f.Zone.Views[0];
                f.Zone.Present(new CardZoneViewData(new ZoneId("different"), new[] { Card("a") }));
                Assert.IsTrue(old == null); Assert.AreEqual("different", f.Zone.Data.ZoneId.Value);
            }
        }

        [Test] public void InvalidArtworkAndLayoutLeaveTheCurrentDisplayIntact()
        {
            using (var f = new Fixture())
            {
                var data = Data(Card("a")); f.Zone.Present(data); var view = f.Zone.Views[0]; var position = view.transform.localPosition;
                Assert.Throws<ArgumentException>(() => f.Zone.Present(Data(Card("new"), Card("bad", "missing-art"))));
                Assert.AreSame(data, f.Zone.Data); Assert.AreSame(view, f.Zone.Views.Single());
                var settings = f.Zone.Layout; settings.Size = Vector2.zero;
                Assert.Throws<ArgumentException>(() => f.Zone.SetLayout(settings));
                Assert.AreEqual(position, view.transform.localPosition); Assert.AreEqual(CardZoneLayoutSettings.Default.Size, f.Zone.Layout.Size);
                Assert.Throws<InvalidOperationException>(() => f.Zone.Configure(f.Prefab, CardZoneLayoutSettings.Default));
            }
        }

        [Test] public void ClearCanReconfigureAndDestroyedVisualsAreRecreatedOnNextPresent()
        {
            using (var f = new Fixture())
            {
                var data = Data(Card("a")); f.Zone.Present(data); Object.DestroyImmediate(f.Zone.Views[0].gameObject);
                f.Zone.Present(data); Assert.IsTrue(f.Zone.Views[0] != null);
                f.Root.SetActive(false); var kept = f.Zone.Views[0]; f.Root.SetActive(true); Assert.AreSame(kept, f.Zone.Views[0]);
                f.Zone.Clear(); Assert.IsTrue(kept == null); Assert.IsNull(f.Zone.Data); Assert.IsFalse(f.Zone.TryGetView("a", out _));
                f.Zone.Configure(f.Prefab, CardZoneLayoutSettings.Default); f.Zone.Present(data); Assert.AreEqual(1, f.Zone.Views.Count);
            }
        }

        [Test] public void ZoneTransformPositionsTheGroupWithoutChangingEngineData()
        {
            using (var f = new Fixture())
            {
                var zone = new CardZone(new ZoneId("zone"), "custom", null, new[] { new CardInstanceId("a"), new CardInstanceId("b") });
                f.Root.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(20, 40, 10));
                f.Root.transform.localScale = Vector3.one * 2;
                f.Zone.Present(zone, id => Card(id.Value));
                foreach (var view in f.Zone.Views)
                {
                    Assert.Less(Vector3.Distance(f.Root.transform.TransformPoint(view.transform.localPosition), view.transform.position), 0.00001f);
                    Assert.AreEqual(Vector3.one, view.transform.localScale);
                }
                var centered = f.Zone.Views.Select(v => v.transform.localPosition.x).ToArray();
                var settings = f.Zone.Layout; settings.Alignment = CardZoneAlignment.Left; f.Zone.SetLayout(settings);
                var left = f.Zone.Views.Select(v => v.transform.localPosition.x).ToArray();
                settings.Alignment = CardZoneAlignment.Right; f.Zone.SetLayout(settings);
                for (int i = 0; i < centered.Length; i++)
                {
                    Assert.Less(left[i], centered[i]);
                    Assert.That((left[i] + f.Zone.Views[i].transform.localPosition.x) / 2, Is.EqualTo(centered[i]).Within(0.00001f));
                }
                CollectionAssert.AreEqual(new[] { "a", "b" }, zone.Cards.Select(c => c.Value));
            }
        }

        [Test] public void LayoutFitsWholeCardsAndOverlapsWithinThePaddedRectangle()
        {
            var settings = CardZoneLayoutSettings.Default; var size = new Vector3(0.064f, 0.00035f, 0.096f);
            foreach (var count in new[] { 0, 1, 6, 20 })
            {
                var positions = CardZoneRowLayout.Calculate(settings, size, count); Assert.AreEqual(count, positions.Length);
                foreach (var p in positions)
                {
                    Assert.LessOrEqual(Mathf.Abs(p.x) + size.x / 2, settings.Size.x / 2 - settings.Padding.x + 0.00001f);
                    Assert.LessOrEqual(Mathf.Abs(p.z) + size.z / 2, settings.Size.y / 2 - settings.Padding.y + 0.00001f);
                }
                if (count > 1) for (int i = 1; i < count; i++) Assert.Greater(positions[i].x, positions[i - 1].x);
            }
            var overlapped = CardZoneRowLayout.Calculate(settings, size, 20);
            Assert.Less(overlapped[1].x - overlapped[0].x, size.x);
            Assert.Greater(overlapped[1].y - overlapped[0].y, size.y);
            foreach (var alignment in new[] { CardZoneAlignment.Left, CardZoneAlignment.Center, CardZoneAlignment.Right })
            {
                settings.Alignment = alignment; var x = CardZoneRowLayout.Calculate(settings, size, 1)[0].x;
                Assert.That(x, Is.EqualTo(alignment == CardZoneAlignment.Left ? -0.213f : alignment == CardZoneAlignment.Right ? 0.213f : 0).Within(0.00001f));
            }
        }

        [Test] public void LayoutRejectsNonfiniteNegativeAndUndersizedConfiguration()
        {
            var size = new Vector3(0.064f, 0.00035f, 0.096f);
            var settings = CardZoneLayoutSettings.Default;
            settings.Size.x = float.NaN; Assert.Throws<ArgumentException>(() => CardZoneRowLayout.Calculate(settings, size, 1));
            settings = CardZoneLayoutSettings.Default; settings.PreferredGap = -1;
            Assert.Throws<ArgumentException>(() => CardZoneRowLayout.Calculate(settings, size, 1));
            settings = CardZoneLayoutSettings.Default; settings.Padding.y = 1;
            Assert.Throws<ArgumentException>(() => CardZoneRowLayout.Calculate(settings, size, 1));
            Assert.Throws<ArgumentException>(() => CardZoneRowLayout.Calculate(CardZoneLayoutSettings.Default, new Vector3(2, 1, 2), 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CardZoneRowLayout.Calculate(CardZoneLayoutSettings.Default, size, -1));
        }

        [Test] public void CardProjectionCarriesAuthorizedAttributesAndKeepsClientStateOutsideCore()
        {
            var card = new CardInstance(new CardInstanceId("secret"), new CardDefinitionId("definition"), effects: (CardEffects)7, isDisabled: true);
            var interaction = new CardInteractionState(isHovered: true, isSelected: true);
            var shown = CardVisualData.FromCard(card, "opaque-view", true, definition => "hearts_q", interaction);
            Assert.AreEqual(card.Effects, shown.Effects); Assert.IsTrue(shown.IsDisabled); Assert.IsTrue(shown.FaceUp);
            Assert.AreEqual(card.Id, shown.CardId); Assert.IsTrue(shown.Interaction.IsRaised);
            var hidden = CardVisualData.FromCard(card, "opaque-view", false, definition => { Assert.Fail("Hidden artwork must not be resolved."); return null; }, interaction);
            Assert.IsNull(hidden.CardId); Assert.IsNull(hidden.FaceArtId); Assert.IsFalse(hidden.FaceUp);
            Assert.AreEqual(CardEffects.None, hidden.Effects); Assert.IsFalse(hidden.IsDisabled); Assert.IsTrue(hidden.Interaction.IsSelected);
            Assert.AreEqual(card.Effects, shown.Effects); Assert.IsTrue(card.IsDisabled);
            Assert.Throws<ArgumentException>(() => CardVisualData.FromCard(card, "view", true, definition => null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CardVisualData("view", effects: (CardEffects)8));
        }

        [Test] public void HoverAndSelectionShareOneOffsetAcrossHideRevealAndRelayout()
        {
            using (var f = new Fixture())
            {
                var offset = new Vector3(0, 0.02f, 0.01f); f.Zone.SetRaisedOffset(offset);
                CardInstanceView kept = null; var state = new CardInteractionState();
                // Enter, select, exit while selected, deselect; also deselect while still hovered.
                var states = new[] { state, state.WithHovered(true), new CardInteractionState(true, true),
                    state.WithSelected(true), state, new CardInteractionState(true, true).WithSelected(false), state };
                for (int i = 0; i < states.Length; i++)
                {
                    f.Zone.Present(Data(new CardVisualData("slot", i % 2 == 0 ? "hearts_q" : null, interaction: states[i])));
                    var view = f.Zone.Views.Single();
                    if (kept == null) kept = view; else Assert.AreSame(kept, view);
                    Assert.AreEqual(states[i].IsSelected ? offset : Vector3.zero, view.FaceRoot.localPosition);
                    var before = view.transform.localPosition;
                    f.Zone.RefreshLayout(); f.Zone.RefreshLayout();
                    Assert.AreEqual(before, view.transform.localPosition);
                    Assert.AreEqual(states[i].IsSelected ? offset : Vector3.zero, view.FaceRoot.localPosition);
                }
                f.Zone.Present(Data(new CardVisualData("slot", "hearts_q", interaction: state.WithSelected(true))));
                f.Zone.SetRaisedOffset(offset * 2); Assert.AreEqual(offset * 2, kept.FaceRoot.localPosition);
                Assert.Throws<ArgumentException>(() => f.Zone.SetRaisedOffset(new Vector3(float.NaN, 0, 0)));
                Assert.AreEqual(offset * 2, kept.FaceRoot.localPosition); Assert.AreEqual(offset * 2, f.Zone.RaisedOffset);
                Assert.Throws<ArgumentException>(() => f.Zone.SetInteractionOffsets(Vector3.zero, new Vector3(0, float.NaN, 0)));
                Assert.AreEqual(Vector3.zero, f.Zone.HoveredOffset); Assert.AreEqual(offset * 2, f.Zone.SelectedOffset);
                Assert.AreEqual(offset * 2, kept.FaceRoot.localPosition);
                f.Zone.Clear(); Assert.IsTrue(kept == null);
            }
        }

        [Test] public void CardAttributesDriveShaderAppearanceWithoutChangingZoneOrder()
        {
            using (var f = new Fixture())
            {
                var source = new CardInstance(new CardInstanceId("a"), new CardDefinitionId("definition"), effects: (CardEffects)7, isDisabled: true);
                var data = CardVisualData.FromCard(source, "a", true, definition => "spades_a", new CardInteractionState(isSelected: true));
                f.Zone.Present(Data(data, Card("b")));
                var view = f.Zone.Views[0];
                Assert.AreEqual((CardEffects)7, view.Data.Effects); Assert.IsTrue(view.Data.IsDisabled);
                var brightness = f.Zone.CardPrefab.GetComponentInChildren<CardArt.CardView>(true).ArtProfile.Resolve(new[]{"dim"},Color.white).Tint.r;
                Assert.AreEqual(new Color(brightness, brightness, brightness, 1), view.Surface.Tint);
                Assert.AreEqual("spades_a", view.Surface.CardId);
                Assert.IsTrue(view.Surface.SealEnabled); Assert.IsTrue(view.Surface.FoilEnabled); Assert.IsTrue(view.Surface.RedCrossEnabled);
                Assert.AreEqual(0, view.Surface.SealEmissionStrength);
                f.Zone.Present(Data(Card("b"), data));
                Assert.AreSame(view, f.Zone.Views[1]);
                Assert.Less(Vector3.Distance(f.Zone.RaisedOffset,
                    f.Zone.transform.InverseTransformVector(view.FaceRoot.position - view.transform.position)), 0.00001f);
                f.Zone.Present(Data(Card("b"), Card("a")));
                Assert.AreSame(view, f.Zone.Views[1]); Assert.AreEqual(Vector3.zero, view.FaceRoot.localPosition);
                Assert.AreEqual(CardEffects.None, view.Data.Effects); Assert.IsFalse(view.Data.IsDisabled);
                Assert.IsFalse(view.Surface.SealEnabled); Assert.IsFalse(view.Surface.FoilEnabled); Assert.IsFalse(view.Surface.RedCrossEnabled);
                Assert.AreEqual(Color.white, view.Surface.Tint);
                CollectionAssert.AreEqual(new[] { "b", "a" }, f.Zone.Data.Cards.Select(c => c.ViewId));
            }
        }

        [Test] public void PresentationIsInPlayerWithoutDependingOnSpecificModes()
        {
            var references = typeof(HandZoneView).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            CollectionAssert.AreEqual(new[] { "PokerEngine.Core" }, references.Where(n => n.StartsWith("PokerEngine.")).ToArray());
            var player = CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(a => a.name).ToArray();
            CollectionAssert.Contains(player, "CardPresentation.Runtime"); CollectionAssert.DoesNotContain(player, "CardPresentation.Tests.Editor");
            CollectionAssert.Contains(player, "CardInteraction.Runtime"); CollectionAssert.DoesNotContain(references, "CardInteraction.Runtime");
            CollectionAssert.DoesNotContain(player, "CardPresentation.Editor");
            var linker = XDocument.Load("Assets/CardPresentation/Runtime/link.xml");
            Assert.IsTrue(linker.Root.Elements("assembly").Any(e => (string)e.Attribute("fullname") == "CardPresentation.Runtime" && (string)e.Attribute("preserve") == "all"));
            var inputLinker = XDocument.Load("Assets/CardInteraction/Runtime/link.xml");
            Assert.IsTrue(inputLinker.Root.Elements("assembly").Any(e => (string)e.Attribute("fullname") == "CardInteraction.Runtime" && (string)e.Attribute("preserve") == "all"));
        }

        [Test] public void PresentationAssetsHaveUniqueImportedMetadata()
        {
            var ids = new HashSet<string>();
            var paths = new[] { "Assets/CardPresentation", "Assets/CardInteraction" }.SelectMany(root =>
                new[] { root }.Concat(Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
                    .Concat(Directory.GetFiles(root, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta"))));
            foreach (var path in paths)
            {
                Assert.IsTrue(File.Exists(path + ".meta"), path);
                var guid = AssetDatabase.AssetPathToGUID(path.Replace('\\', '/'));
                Assert.IsNotEmpty(guid, path); Assert.IsTrue(ids.Add(guid), path);
            }
        }
    }
}
