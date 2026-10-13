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
    [TestFixture]
    public sealed class CardMatchHostTests
    {
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
            private CardZonePlayHost Hand(string id)
            {
                var root = new GameObject(id); SceneManager.MoveGameObjectToScene(root, Scene);
                var view = root.AddComponent<HandZoneView>();
                view.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
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

        [Test] public void ValidationWindowReadsCurrentMatchInsteadOfRestoringItsCachedPile()
        {
            using (var f = new Fixture())
            {
                var window = ScriptableObject.CreateInstance<CardZoneValidationWindow>();
                try
                {
                    window.SelectTarget(f.Left.GetComponent<HandZoneView>());
                    var sync = typeof(CardZoneValidationWindow).GetMethod("SyncRuntimeState", BindingFlags.Instance | BindingFlags.NonPublic);
                    var experiment = typeof(CardZoneValidationWindow).GetProperty("CurrentExperiment", BindingFlags.Instance | BindingFlags.NonPublic);
                    f.Right.DrawCards(1, false); sync.Invoke(window, null);
                    // Editor reads the authoring seed in Edit mode; it never overwrites a live session from its cache.
                    Assert.AreEqual(1, ((CardZoneDrawExperiment)experiment.GetValue(window)).DeckCards.Count);
                    Assert.IsEmpty(f.Match.GetCards(new ZoneId("deck"))); Assert.AreEqual(1, f.Right.Cards.Count);
                    window.SelectTarget(f.Right.GetComponent<HandZoneView>()); Assert.IsEmpty(f.Left.DeckCards);
                }
                finally { window.SelectTarget(null); Object.DestroyImmediate(window); }
            }
        }
    }
}
#endif
