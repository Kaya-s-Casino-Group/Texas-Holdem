using System;
using System.Collections.Generic;
using System.Linq;
using BattlePresentation;
using CardPresentation;
using PokerEngine;
using UnityEngine;

namespace CardInteraction
{
    public sealed partial class CardZonePlayHost
    {
        private PresentationScope projectionScope;
        private IPresentationOperation projectionOperation;
        private string projectionAction;
        private CardZoneDisplayData projectionTarget;
        private readonly List<(PresentationScope scope, IPresentationOperation operation)> departureProjections =
            new List<(PresentationScope, IPresentationOperation)>();
        public ICardZonePresentation Presentation => GetComponents<MonoBehaviour>().OfType<ICardZonePresentation>().SingleOrDefault()
            ?? throw new InvalidOperationException("Bind one ICardZonePresentation on the battle hand.");
        public GameObject CardPrefab => GetComponent<CardZoneView>().CardPrefab;
        public void BindBattleHand(CardMatchHost owner, ZoneId handZone, ZoneId deck, ZoneId discard, int maxSelection)
        {
            var hand = GetComponent<HandZoneView>();
            if (hand == null) throw new InvalidOperationException("This input binding requires a world hand.");
            Bind(owner, handZone, deck, discard, hand.FacingCamera); BeginSession(); hand.SetMaxSelectedCards(maxSelection);
        }
        private static CardZoneDisplayData Display(CardZoneViewData data) => new CardZoneDisplayData(data.ZoneId.Value,
            null, "hand", data.Cards.Select(card => new CardDisplayData(card.ViewId, card.CardId?.Value, null,
                card.FaceArtId, !card.FaceUp, card.IsDisabled ? "disabled" : null, new Dictionary<string, string> {
                    ["effects"] = ((int)card.Effects).ToString(), ["selected"] = card.Interaction.IsSelected.ToString(),
                    ["hovered"] = card.Interaction.IsHovered.ToString() })));
        private sealed class ProjectionSignals : IMarkerSink { public bool Emit(string marker) => true; }
        private static bool SameProjection(CardZoneDisplayData before, CardZoneDisplayData after) =>
            before != null && before.Id == after.Id && before.Cards.Count == after.Cards.Count &&
            before.Cards.Zip(after.Cards, (a, b) => a.ViewId == b.ViewId && a.CardId == b.CardId &&
                a.FaceKey == b.FaceKey && a.ArtKey == b.ArtKey && a.Hidden == b.Hidden && a.DisabledReason == b.DisabledReason &&
                a.Properties.Count == b.Properties.Count && a.Properties.All(pair => b.Properties.TryGetValue(pair.Key, out var value) && value == pair.Value)).All(same => same);
        /// <summary>Commits an already prepared observer update only after the frontend accepts its display projection.</summary>
        public void PresentProjection(CardMatchUpdate update, IEnumerable<string> discarded, bool animate)
        {
            if (update == null || update.Owner != match) throw new ArgumentException("Foreign card projection.");
            var frontend = Presentation;
            var before = (projectionOperation?.Status == PresentationStatus.Pending ? projectionTarget : null) ??
                frontend.Current ?? new CardZoneDisplayData(Zone.Id.Value, null, "hand", null);
            var after = Display(update.Project(Zone.Id));
            // Played -> discard collection and deck recycling can publish an unchanged hand. Keep the
            // current compaction / queued draw alive instead of cancelling its tween and snapping to target.
            if (animate && SameProjection(before, after))
            {
                match.Commit(update); match.Notify(update, GetComponent<CardZoneView>().Data); return;
            }
            var removed = before.Cards.Where(card => after.Cards.All(next => next.ViewId != card.ViewId)).Select(card => card.ViewId).ToArray();
            var discardedIds = new HashSet<string>(discarded ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            foreach (var previous in departureProjections.Where(item => item.operation.Status != PresentationStatus.Pending).ToArray())
            { previous.scope.Dispose(); departureProjections.Remove(previous); }
            bool drawing = removed.Length == 0 && after.Cards.Any(card => before.Cards.All(old => old.ViewId != card.ViewId));
            if (animate && drawing && (projectionAction == CardZoneActions.Discard || projectionAction == CardZoneActions.Play) && projectionOperation?.Status == PresentationStatus.Pending)
                departureProjections.Add((projectionScope, projectionOperation));
            else projectionScope?.Dispose();
            projectionScope = null; projectionOperation = null; projectionAction = null;
            try
            {
                if (!animate) frontend.Synchronize(after);
                else
                {
                    projectionScope = new PresentationScope("projection/" + update.Change.After.Revision);
                    var key = removed.Length > 0 ? removed.Any(discardedIds.Contains) ? CardZoneActions.Discard : CardZoneActions.Play :
                        after.Cards.Any(card => before.Cards.All(old => old.ViewId != card.ViewId)) ? CardZoneActions.Draw : CardZoneActions.Arrange;
                    var ids = removed.Length > 0 ? removed : after.Cards.Where(card => before.Cards.All(old => old.ViewId != card.ViewId)).Select(card => card.ViewId).ToArray();
                    var operation = frontend.Play(new CardZonePresentationRequest(key, projectionScope.Id, projectionScope, before, after, ids), new ProjectionSignals());
                    projectionOperation = operation; projectionAction = key;
                    if (operation.Status == PresentationStatus.Failed) throw new InvalidOperationException(operation.Error.ToString());
                    if (operation.Status == PresentationStatus.Cancelled) throw new InvalidOperationException("Card projection was cancelled before commit.");
                }
                match.Commit(update);
                projectionTarget = after;
            }
            catch { projectionScope?.Dispose(); projectionScope = null; projectionTarget = null; frontend.Synchronize(before); throw; }
            match.Notify(update, GetComponent<CardZoneView>().Data);
        }
        private void StopProjection()
        {
            projectionScope?.Dispose(); projectionScope = null; projectionOperation = null; projectionAction = null;
            projectionTarget = null;
            foreach (var previous in departureProjections) previous.scope.Dispose();
            departureProjections.Clear();
        }
    }
}
