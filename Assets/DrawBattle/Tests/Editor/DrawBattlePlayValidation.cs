using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CardInteraction;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Poker.DrawBattle.Tests
{
    /// <summary>Real scene button smoke test across asynchronous flights, without saving/replacing the scene.</summary>
    public static class DrawBattlePlayValidation
    {
        private static DrawBattleHost host;
        private static DrawEngine originalEngine;
        private static DrawState originalState;
        private static DrawEngine engine;
        private static int stage;
        private static double deadline;
        private static string sortSnapshot;
        private const string Path = "Library/DrawBattleTests/play.json";

        public static void Run()
        {
            if (!Application.isPlaying || host != null) throw new InvalidOperationException("Enter Play and finish any previous validation first.");
            host = Object.FindObjectsOfType<DrawBattleHost>().Single(item => item.State != null);
            originalState = host.State;
            originalEngine = (DrawEngine)typeof(DrawBattleHost).GetField("engine", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);
            engine = new DrawEngine(originalEngine.Catalog, new DrawDefinition("draw.ui-validation.1", 100000, 8, 2, 1));
            try
            {
                var next = engine.Create("draw/ui-validation", originalState.PlayerId, originalState.Cards, HmacDeckShuffler.FromSeed(new byte[32]));
                host.StartEncounter(engine, next, card => DrawBattleHost.StandardArt(engine.Catalog.Get(card.DefinitionId)));
                Select(); host.Actions.Refresh(); Assert.IsTrue(host.Actions.CanDiscard); host.Actions.Panel.DiscardButton.onClick.Invoke();
                Assert.IsTrue(host.IsPresenting); Assert.AreEqual(0, host.State.DiscardsRemaining); Assert.IsFalse(host.Actions.CanPlay);
                stage = 0; deadline = EditorApplication.timeSinceStartup + 30; EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error); }
        }
        private static void Select()
        {
            var hand = host.Actions.Hand;
            foreach (var card in hand.Cards) hand.SetInteraction(card.Id, default);
            hand.SetInteraction(hand.Cards.First(card => card.Card.CanPlay).Id, new CardInteractionState(isSelected: true)); hand.Publish();
            hand.GetComponent<HandZoneView>().CompleteTransitions(); host.Actions.Refresh();
        }
        private static void Tick()
        {
            try
            {
                if (!Application.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Draw UI validation timed out during animation.");
                if (host.IsPresenting || host.Actions.Hand.GetComponent<HandZoneView>().IsTransitioning) return;
                var hand = host.Actions.Hand;
                if (stage == 0)
                {
                    Assert.AreEqual(8, hand.Cards.Count); Assert.AreEqual(1, hand.DiscardedCards.Count); Select(); Assert.IsFalse(host.Actions.CanDiscard);
                    Assert.IsTrue(host.Actions.CanPlay); host.Actions.Panel.PlayButton.onClick.Invoke();
                    Assert.IsTrue(host.IsPresenting); Assert.AreEqual(1, host.Actions.Played.Cards.Count);
                    Assert.IsTrue(host.Actions.PlayRoute.IsTransferring); Assert.IsFalse(host.Actions.CanPlay);
                    long revision = host.State.Revision; host.Actions.Panel.PlayButton.onClick.Invoke(); Assert.AreEqual(revision, host.State.Revision);
                    stage = 1;
                }
                else if (stage == 1)
                {
                    Assert.AreEqual(DrawPhase.Ready, host.State.Phase); Assert.AreEqual(1, host.State.HandsRemaining); Assert.Greater(host.State.Score, 0);
                    Assert.AreEqual(8, hand.Cards.Count); Assert.AreEqual(2, hand.DiscardedCards.Count); Assert.IsEmpty(host.Actions.Played.Cards);
                    Assert.AreEqual(host.State.Cards.Count - 10, hand.DeckCards.Count);
                    foreach (var zone in host.State.World.Zones) CollectionAssert.AreEquivalent(zone.Cards, host.Match.State.GetZone(zone.Id).Cards);
                    var pile = Object.FindObjectsOfType<DrawPilePresenter>().Single(item => item.Source == hand);
                    pile.Refresh(); Assert.AreEqual(hand.DeckCards.Count, pile.View.RemainingCards); Assert.AreEqual(hand.DeckCards.Count.ToString(), pile.View.RemainingCount.text);
                    ExecuteEvents.Execute(pile.View.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                    Assert.IsTrue(pile.Summary.IsOpen); Assert.AreEqual(hand.DeckCards.Count, pile.Overview.ReadData().RemainingCount);
                    pile.View.SetHovered(false, false); ExecuteEvents.Execute(pile.View.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
                    Assert.IsTrue(pile.Overview.View.IsOpen); Assert.AreEqual(host.State.Cards.Count, pile.Overview.ReadData().Cards.Count);
                    pile.Overview.View.CloseButton.onClick.Invoke(); Assert.IsFalse(pile.Overview.View.IsOpen);
                    sortSnapshot = DrawSnapshotCodec.Serialize(host.State, engine); host.Actions.Panel.SuitSortButton.onClick.Invoke(); stage = 2;
                }
                else
                {
                    Assert.AreEqual(CardZoneSortOrder.SuitFirst, hand.SortOrder); Assert.AreEqual(sortSnapshot, DrawSnapshotCodec.Serialize(host.State, engine)); Finish(null);
                }
            }
            catch (Exception error) { Finish(error); }
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick; Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            File.WriteAllText(Path, JsonUtility.ToJson(new Report { status = error == null ? "Passed" : "Failed", error = error?.ToString(), nativeMouse = false }, true));
            try
            {
                var pile = Object.FindObjectsOfType<DrawPilePresenter>().FirstOrDefault(item => item.Source == host?.Actions.Hand); pile?.SetPinned(false);
                if (host != null && Application.isPlaying && originalState != null) host.StartEncounter(originalEngine, originalState, card => DrawBattleHost.StandardArt(originalEngine.Catalog.Get(card.DefinitionId)));
            }
            finally { host = null; originalState = null; originalEngine = null; }
            if (error != null) Debug.LogException(error); else Debug.Log("Draw UI passed: real discard/play/sort buttons, transfer input lock, score/refill, deck counter, hover summary and clicked overview. Original encounter restored.");
        }
        [Serializable] private sealed class Report { public string status, error; public bool nativeMouse; }
    }
}
