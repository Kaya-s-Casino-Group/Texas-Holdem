using System;
using System.Linq;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CardInteraction.Tests
{
    public sealed class RecordingCardZoneView : MonoBehaviour, ICardMatchZoneView
    {
        public CardMatchZoneData Last;
        public int PresentCount;
        public void Present(CardMatchZoneData snapshot) { Last = snapshot; PresentCount++; }
        public void Clear() => Last = null;
    }

    public sealed class CardMatchFrontendTests
    {
        private Scene scene;
        private CardMatchHost host;
        private readonly ZoneId hand = new ZoneId("hand"), deck = new ZoneId("deck"), discard = new ZoneId("discard");
        private CardInstance first, second;
        [SetUp] public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            host = New("Shared match").AddComponent<CardMatchHost>();
            first = new CardInstance(new CardInstanceId("first"), new CardDefinitionId("face/first"));
            second = new CardInstance(new CardInstanceId("second"), new CardDefinitionId("face/second"));
            var state = new CardTableState(new MatchId("frontends"), new[] { first, second }, new[] {
                new CardZone(hand, CardZoneKinds.Hand, null, new[] { first.Id }),
                new CardZone(deck, CardZoneKinds.Deck, null, new[] { second.Id }),
                new CardZone(discard, CardZoneKinds.Discard, null, Array.Empty<CardInstanceId>())
            });
            host.StartMatch(state, new[] { new CardMatchCardState(first), new CardMatchCardState(second) });
        }
        [TearDown] public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);
        private GameObject New(string name, bool rect = false)
        {
            var go = rect ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene); return go;
        }
        private RecordingCardZoneView View(string name, bool rect = false)
        {
            var go = New(name, rect); var view = go.AddComponent<RecordingCardZoneView>();
            go.AddComponent<CardMatchZoneBinding>().Configure(host, hand, view); return view;
        }
        [Test] public void CoreAssemblyCannotReferenceAConcreteFrontend()
        {
            var references = typeof(CardMatchHost).Assembly.GetReferencedAssemblies().Select(item => item.Name).ToArray();
            Assert.AreEqual("CardMatch.Runtime", typeof(CardMatchHost).Assembly.GetName().Name);
            Assert.IsFalse(references.Any(name => name.StartsWith("CardPresentation") || name.StartsWith("CardArt") ||
                name.StartsWith("CardInteraction") || name.Contains("TextMeshPro") || name.Contains("UIFoundation")));
        }
        [Test] public void RectAndWorldObserversShareSelectionAndMovesWithoutOwningTheMatch()
        {
            var canvas = View("Canvas observer", true); var world = View("World observer");
            var old = canvas.Last;
            host.SetInteraction(first.Id, new CardInteractionState(isSelected: true));
            Assert.IsTrue(canvas.Last.Cards.Single().Interaction.IsSelected);
            Assert.IsTrue(world.Last.Cards.Single().Interaction.IsSelected);
            Assert.IsFalse(old.Cards.Single().Interaction.IsSelected, "Published snapshots must remain stable.");
            var update = host.PrepareDraw(deck, hand, 1, 8);
            Assert.AreEqual(1, canvas.Last.Cards.Count, "Preparing must not publish uncommitted state.");
            host.Commit(update); host.Notify(update);
            CollectionAssert.AreEqual(new[] { "first", "second" }, canvas.Last.Cards.Select(card => card.Id));
            CollectionAssert.AreEqual(canvas.Last.Cards.Select(card => card.Id), world.Last.Cards.Select(card => card.Id));
            canvas.gameObject.SetActive(false);
            Assert.IsTrue(host.IsRunning); Assert.AreEqual(2, world.Last.Cards.Count);
            update = host.PrepareDiscard(hand, discard, new[] { first.Id }); host.Commit(update); host.Notify(update);
            Assert.AreEqual("second", world.Last.Cards.Single().Id);
            canvas.gameObject.SetActive(true);
            Assert.AreEqual("second", canvas.Last.Cards.Single().Id);
            host.StopMatch(); Assert.IsNull(canvas.Last); Assert.IsNull(world.Last);
            host.RestartMatch(); Assert.AreEqual("first", canvas.Last.Cards.Single().Id);
        }
        [Test] public void AlreadyPresentedTokenSuppressesOnlyItsConsumer()
        {
            var firstView = View("First"); var secondView = View("Second", true);
            var update = host.PrepareDraw(deck, hand, 1, 8);
            firstView.Present(update.Snapshot(hand)); int count = firstView.PresentCount;
            host.Commit(update); host.Notify(update, firstView);
            Assert.AreEqual(count, firstView.PresentCount);
            Assert.AreEqual(2, secondView.Last.Cards.Count);
        }
        [Test] public void InvalidRestoreDoesNotReplaceNeutralStateOrNotifyViews()
        {
            var view = View("Observer"); var state = host.State; int count = view.PresentCount;
            Assert.Throws<ArgumentException>(() => host.StartMatch(state, new[] { new CardMatchCardState(first) }));
            Assert.AreSame(state, host.State); Assert.AreEqual(count, view.PresentCount);
        }
    }
}
