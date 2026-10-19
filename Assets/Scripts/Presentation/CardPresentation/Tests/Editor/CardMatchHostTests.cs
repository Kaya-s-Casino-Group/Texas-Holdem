#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using CardInteraction;
using CardPresentation.Editor;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    public sealed class NeutralCardZoneObserver : MonoBehaviour, ICardMatchZoneView
    {
        public CardMatchZoneData Snapshot;
        public void Present(CardMatchZoneData snapshot) => Snapshot = snapshot;
        public void Clear() => Snapshot = null;
    }

    [TestFixture]
    public sealed class CardMatchHostTests
    {

        public static void ValidateLiveWindow()
        {
            CardZoneValidationWindow window = null;
            Exception failure = null;
            try
            {
                if (!Application.isPlaying) throw new InvalidOperationException("Enter a real battle in Play first.");
                var hand = Object.FindObjectsOfType<CardZonePlayHost>().First(host => host.IsRunning && host.Match.Commands != null &&
                    host.Match.Commands.CanInteract && host.GetComponent<HandZoneView>() != null);
                var view = hand.GetComponent<HandZoneView>(); var table = hand.Match.State; var data = view.Data;
                var instances = view.Views.ToDictionary(card => card.ViewId, card => card.gameObject.GetInstanceID());
                var layout = view.Layout;
                window = ScriptableObject.CreateInstance<CardZoneValidationWindow>(); window.SelectTarget(view);
                Assert.That(window.HasLiveBattle, Is.True);
                CollectionAssert.AreEqual(hand.Cards.Select(card => card.Id), window.CurrentCards.Select(card => card.Id));
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var next = layout; next.Size.x += .001f;
                typeof(CardZoneValidationWindow).GetField("layout", flags).SetValue(window, next);
                try { typeof(CardZoneValidationWindow).GetMethod("Apply", flags).Invoke(window, new object[] { false }); }
                finally { view.SetLayout(layout, view.MotionSettings, false); }
                Assert.That(hand.Match.State, Is.SameAs(table)); Assert.That(view.Data, Is.SameAs(data));
                var played = view.GetComponent<CardZoneTransferPresenter>()?.Destination;
                if (played != null)
                {
                    window.SelectTarget(played);
                    CollectionAssert.AreEqual(played.GetComponent<CardZonePlayHost>().Cards.Select(card => card.Id), window.CurrentCards.Select(card => card.Id));
                }
                Object.DestroyImmediate(window); window = null;
                Assert.That(hand.Match.State, Is.SameAs(table)); Assert.That(view.Data, Is.SameAs(data));
                foreach (var item in view.Views) Assert.That(item.gameObject.GetInstanceID(), Is.EqualTo(instances[item.ViewId]));
            }
            catch (Exception exception) { failure = exception; }
            finally { if (window != null) Object.DestroyImmediate(window); }
            System.IO.Directory.CreateDirectory("Library/CardPresentationTests");
            System.IO.File.WriteAllText("Library/CardPresentationTests/live-validator.json", JsonUtility.ToJson(new LiveWindowReport {
                status = failure == null ? "Passed" : "Failed", error = failure?.ToString() }, true));
            if (failure != null) Debug.LogException(failure); else Debug.Log("Live card window passed: real table reads, settings-only edits, target switching and safe close.");
        }
        [Serializable] private sealed class LiveWindowReport { public string status, error; }
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene = EditorSceneManager.NewPreviewScene();
            public readonly CardMatchHost Match;
            public readonly CardZonePlayHost Left, Right;
            public Fixture(string art = "hearts_q")
            {
                var root = new GameObject("Shared match"); SceneManager.MoveGameObjectToScene(root, Scene);
                Match = root.AddComponent<CardMatchHost>();
                var card = new CardInstance(new CardInstanceId("shared-card"), new CardDefinitionId("hearts_q"), "test", CardEffects.Ineffective);
                var seed = new CardTableState(new MatchId("shared"), new[] { card }, new[] {
                    Row("deck", CardZoneKinds.Deck, card.Id), Row("discard", CardZoneKinds.Discard),
                    Row("left", CardZoneKinds.Hand), Row("right", CardZoneKinds.Hand) });
                // Use the Player-available initialization path, including a restored pure-engine snapshot.
                Match.StartMatch(CardTableSnapshotCodec.Deserialize(CardTableSnapshotCodec.Serialize(seed)),
                    new[] { new CardZoneCardState(card, art, true, Color.cyan, default) });
                Left = Hand("left"); Right = Hand("right");
            }
            private static CardZone Row(string id, string kind, params CardInstanceId[] cards) => new CardZone(new ZoneId(id), kind, null, cards);
            public CardZonePlayHost Hand(string id)
            {
                var root = new GameObject(id); SceneManager.MoveGameObjectToScene(root, Scene);
                var view = root.AddComponent<HandZoneView>();
                view.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                var host = root.AddComponent<CardZonePlayHost>();
                host.Bind(Match, new ZoneId(id), new ZoneId("deck"), new ZoneId("discard")); host.BeginSession(); return host;
            }
            public void Dispose() => EditorSceneManager.ClosePreviewScene(Scene);
        }

        [Test] public void SharedDeckAndDiscardFollowMatchAndReattachingCannotResetThem()
        {
            using (var f = new Fixture())
            {
                Assert.AreEqual(1, f.Left.DrawCards(1, false)); Assert.IsEmpty(f.Right.DeckCards);
                Assert.IsFalse(f.Right.CanDraw()); var card = f.Left.Cards[0];
                f.Left.SetInteraction(card.Id, new CardInteractionState(isSelected: true)); f.Left.Publish();
                Assert.AreEqual(1, f.Left.DiscardSelected(false)); Assert.AreEqual(1, f.Right.DiscardedCards.Count);
                Assert.AreEqual(CardEffects.Ineffective, f.Right.DiscardedCards[0].Card.Effects);
                Assert.IsFalse(f.Right.DiscardedCards[0].Interaction.IsSelected);
                long revision = f.Match.State.Revision;
                f.Left.StopSession(); f.Left.BeginSession(); f.Right.BeginSession();
                Assert.AreEqual(revision, f.Match.State.Revision); Assert.IsEmpty(f.Left.Cards); Assert.IsEmpty(f.Left.DeckCards);
                Assert.AreEqual(1, f.Left.DiscardedCards.Count);
                f.Match.RestartMatch(); Assert.AreEqual(1, f.Right.DeckCards.Count);
                Assert.IsEmpty(f.Left.Cards); Assert.IsEmpty(f.Right.DiscardedCards);
            }
        }

        [Test] public void BadArtCannotConsumeSharedDeckOrChangeMatchRevision()
        {
            using (var f = new Fixture("missing-art"))
            {
                var before = f.Match.State; var data = f.Left.GetComponent<HandZoneView>().Data;
                Assert.Throws<ArgumentException>(() => f.Left.DrawCards(1));
                Assert.AreSame(before, f.Match.State); Assert.AreSame(data, f.Left.GetComponent<HandZoneView>().Data);
                Assert.AreEqual(1, f.Right.DeckCards.Count); Assert.IsEmpty(f.Left.Cards);
            }
        }

        [Test] public void SelectionRequestCopiesInputAndDoesNotTrackLaterInteraction()
        {
            using (var f = new Fixture())
            {
                f.Left.DrawCards(1, false);
                var id = f.Left.Cards.Single().Card.Id;
                f.Left.SetInteraction(id.Value, new CardInteractionState(isSelected: true));
                var request = f.Left.CaptureSelection();
                f.Left.SetInteraction(id.Value, default);
                Assert.AreEqual(f.Match.State.MatchId, request.MatchId);
                Assert.AreEqual(f.Left.Zone.Id, request.ZoneId);
                Assert.AreEqual(f.Left.Zone.ControllerId, request.ControllerId);
                CollectionAssert.AreEqual(new[] { id }, request.Cards);
                Assert.IsEmpty(f.Left.CaptureSelection().Cards);

                var input = new[] { id };
                var copied = new CardSelectionRequest(request.MatchId, request.ZoneId, null, input);
                input[0] = new CardInstanceId("replacement");
                CollectionAssert.AreEqual(new[] { id }, copied.Cards);
                Assert.Throws<ArgumentException>(() => new CardSelectionRequest(request.MatchId, request.ZoneId, null, new[] { id, id }));
                Assert.Throws<ArgumentException>(() => new CardSelectionRequest(default, request.ZoneId, null, new[] { id }));
            }
        }

        [Test] public void ExistingWorldHandAndNeutralRectFrontendUseTheSameHostAndInput()
        {
            using (var f = new Fixture())
            {
                var root = new GameObject("Neutral Canvas frontend", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, f.Scene);
                var observer = root.AddComponent<NeutralCardZoneObserver>();
                root.AddComponent<CardMatchZoneBinding>().Configure(f.Match, f.Left.Zone.Id, observer);
                f.Left.DrawCards(1, false);
                var id = f.Left.Cards.Single().Card.Id;
                Assert.AreEqual(id, observer.Snapshot.Cards.Single().Card.Id);
                var state = f.Match.State;
                f.Left.SetInteraction(id.Value, new CardInteractionState(isSelected: true)); f.Left.Publish();
                Assert.IsTrue(observer.Snapshot.Cards.Single().Interaction.IsSelected);
                f.Match.SetInteraction(id, default);
                Assert.IsFalse(f.Left.GetComponent<HandZoneView>().Data.Cards.Single().Interaction.IsSelected);
                Assert.IsFalse(observer.Snapshot.Cards.Single().Interaction.IsSelected);
                Assert.AreSame(state, f.Match.State, "Shared local selection cannot change rule/table state.");
                root.SetActive(false); Assert.IsTrue(f.Left.IsRunning);
            }
        }

        [Test] public void PresentedSnapshotSkipsOnlyItsViewAndStillRefreshesAnotherObserverOfTheSameZone()
        {
            using (var f = new Fixture())
            {
                var observer = f.Hand("left");
                var primary = f.Left.GetComponent<HandZoneView>();
                var secondary = observer.GetComponent<HandZoneView>();
                int notifications = 0;
                f.Match.Changed += (change, presented) => {
                    notifications++;
                    Assert.AreSame(primary.Data, presented.Single(), "The source must not be presented twice.");
                    Assert.AreNotSame(primary.Data, secondary.Data);
                    CollectionAssert.AreEqual(primary.Data.Cards.Select(card => card.ViewId), secondary.Data.Cards.Select(card => card.ViewId));
                };
                f.Left.DrawCards(1, false);
                Assert.AreEqual(1, secondary.Views.Count);
                f.Left.SetInteraction(f.Left.Cards.Single().Id, new CardInteractionState(isSelected: true));
                f.Left.DiscardSelected(false);
                Assert.IsEmpty(secondary.Views);
                Assert.AreEqual(2, notifications);
            }
        }

        [Test] public void RestoreRejectsIncompleteObserverCardsWithoutReplacingRunningMatch()
        {
            using (var f = new Fixture())
            {
                var before = f.Match.State;
                Assert.Throws<ArgumentException>(() => f.Match.StartMatch(before, Array.Empty<CardZoneCardState>()));
                Assert.AreSame(before, f.Match.State); Assert.IsTrue(f.Left.IsRunning);
                f.Match.StopMatch();
                Assert.Throws<InvalidOperationException>(() => f.Match.PrepareDraw(new ZoneId("deck"), new ZoneId("left"), 1, 5));
            }
        }

        [Test] public void WindowNeverImportsEditSeedsOrClearsLiveViewsOnClose()
        {
            using (var f = new Fixture())
            {
                f.Left.DrawCards(1, false); var view = f.Left.GetComponent<HandZoneView>();
                var table = f.Match.State; var data = view.Data; var instance = view.Views[0];
                var window = ScriptableObject.CreateInstance<CardZoneValidationWindow>();
                try
                {
                    window.SelectTarget(view); Assert.That(window.CurrentCards, Is.Empty); Assert.That(window.HasLiveBattle, Is.False);
                    typeof(CardZoneValidationWindow).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { false });
                    CardZoneValidationWindow.RefreshMatchInCurrentWindow();
                    Assert.That(f.Match.State, Is.SameAs(table)); Assert.That(view.Data, Is.SameAs(data)); Assert.That(view.Views[0], Is.SameAs(instance));
                }
                finally { Object.DestroyImmediate(window); }
                Assert.That(f.Match.State, Is.SameAs(table)); Assert.That(view.Data, Is.SameAs(data)); Assert.That(view.Views[0], Is.SameAs(instance));
            }
        }
        [Test] public void OldSerializedWindowCardsCannotPopulateRuntimeManagedBindings()
        {
            using (var f = new Fixture())
            {
                f.Match.ConfigureRuntimeManaged(); f.Left.ConfigureRuntimeBinding(f.Match); f.Right.ConfigureRuntimeBinding(f.Match);
                var window = ScriptableObject.CreateInstance<CardZoneValidationWindow>();
                try
                {
                    // Simulate an old persisted window containing fake cards and deck IDs. Removed fields are ignored.
                    EditorJsonUtility.FromJsonOverwrite("{\"hasEditableSnapshot\":true,\"previewZoneId\":\"editor/old\",\"model\":{\"cards\":[{\"id\":\"fake-card\",\"artId\":\"hearts_q\"}]}}", window);
                    var view = f.Left.GetComponent<HandZoneView>(); window.SelectTarget(view);
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(CardZoneValidationWindow).GetMethod("Apply", flags).Invoke(window, new object[] { false });
                    typeof(CardZoneValidationWindow).GetMethod("OnPlayMode", flags).Invoke(window, new object[] { PlayModeStateChange.EnteredEditMode });
                    typeof(CardZoneValidationWindow).GetMethod("OnPlayMode", flags).Invoke(window, new object[] { PlayModeStateChange.ExitingEditMode });
                    Assert.That(view.Views, Is.Empty); Assert.That(f.Match.InitialState.Cards, Is.Empty); Assert.That(f.Match.InitialState.Zones, Is.Empty);
                    Assert.That(window.CurrentCards, Is.Empty); Assert.That(f.Left.HasSnapshot, Is.False);
                    Assert.Throws<InvalidOperationException>(() => f.Match.ConfigureInitial(Array.Empty<CardMatchZoneSnapshot>()));
                }
                finally { Object.DestroyImmediate(window); }
            }
        }
    }
}
#endif
