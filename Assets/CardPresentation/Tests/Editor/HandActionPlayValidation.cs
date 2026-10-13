using System;
using System.IO;
using System.Linq;
using CardInteraction;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    /// <summary>Smoke-check deserialized Play listeners without requiring OS mouse injection. Restores startup data.</summary>
    public static class HandActionPlayValidation
    {

        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play before validating runtime button listeners.");
            var presenter = Object.FindObjectsOfType<HandActionPresenter>().FirstOrDefault(item => item.Hand.IsRunning && item.Played != null && item.Played.IsRunning);
            if (presenter == null) throw new InvalidOperationException("Configure an active hand and its played route.");
            var host = presenter.Hand; var panel = presenter.Panel; var order = host.SortOrder;
            var played = presenter.Played; var route = presenter.PlayRoute;
            string path = "Library/CardPresentationTests/hand-actions-play.json";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            try
            {
                int beforeHand = host.Cards.Count, beforeDeck = host.DeckCards.Count;
                Assert.Less(played.Cards.Count, CardPresentation.PlayedZoneView.SlotCount);
                foreach (var card in host.Cards) host.SetInteraction(card.Id, default);
                var playedCard = host.Cards.First(card => card.Card.CanPlay);
                host.SetInteraction(playedCard.Id, new CardPresentation.CardInteractionState(isSelected: true)); host.Publish(); presenter.Refresh();
                Assert.IsTrue(presenter.CanPlay); panel.PlayButton.onClick.Invoke();
                Assert.AreEqual(beforeHand - 1, host.Cards.Count); Assert.AreEqual(beforeDeck, host.DeckCards.Count);
                Assert.IsTrue(played.Cards.Any(card => card.Id == playedCard.Id)); Assert.IsTrue(route.IsTransferring);
                panel.SuitSortButton.onClick.Invoke(); Assert.AreEqual(CardZoneSortOrder.SuitFirst, host.SortOrder);
                panel.RankSortButton.onClick.Invoke(); Assert.AreEqual(CardZoneSortOrder.RankFirst, host.SortOrder);
                // Select via the card source, then consume it through the serialized UI bindings.
                foreach (var card in host.Cards) host.SetInteraction(card.Id, default);
                var selected = host.Cards.First(card => card.Card.CanDiscard);
                host.SetInteraction(selected.Id, new CardPresentation.CardInteractionState(isSelected: true)); host.Publish(); presenter.Refresh();
                int beforeDiscard = host.DiscardedCards.Count;
                panel.DiscardButton.onClick.Invoke();
                Assert.AreEqual(beforeHand - 2, host.Cards.Count);
                Assert.AreEqual(beforeDiscard + 1, host.DiscardedCards.Count);
                Assert.IsFalse(host.Cards.Any(card => card.Id == selected.Id));
                File.WriteAllText(path, "{\"status\":\"Passed\",\"buttons\":4,\"nativeMouse\":false}");
                Debug.Log("Play button listeners passed: play / discard / rank sort / suit sort. Restoring startup zones and deck.");
            }
            catch (Exception error)
            {
                File.WriteAllText(path, JsonUtility.ToJson(new Failure { status = "Failed", error = error.ToString() }, true));
                throw;
            }
            finally
            {
                route.CompleteTransfers(); host.Match.RestartMatch(); host.BeginSession(); played.BeginSession();
                host.SetSortOrder(order); presenter.Refresh();
            }
        }
        [Serializable] private sealed class Failure { public string status, error; }
    }
}
