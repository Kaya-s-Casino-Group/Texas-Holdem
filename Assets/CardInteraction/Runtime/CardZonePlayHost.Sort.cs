using System;
using System.Linq;
using CardPresentation;
using PokerEngine;
using UnityEngine;

namespace CardInteraction
{
    public enum CardZoneSortOrder { RankFirst, SuitFirst }

    public sealed partial class CardZonePlayHost
    {
        [SerializeField] private CardZoneSortOrder sortOrder = CardZoneSortOrder.RankFirst;
        private bool pendingSort;
        public CardZoneSortOrder SortOrder => sortOrder;
        public bool HasPendingSort => pendingSort;

        public void SetSortOrder(CardZoneSortOrder order)
        {
            if (!Enum.IsDefined(typeof(CardZoneSortOrder), order)) throw new ArgumentOutOfRangeException(nameof(order));
            sortOrder = order;
        }

        /// <summary>Keep the submitted draw order through flight and settling; reorder the card data afterwards.</summary>
        public void RequestSortAfterLanding(CardZoneSortOrder order)
        {
            if (!IsRunning || !(view is HandZoneView))
                throw new InvalidOperationException("Start a hand zone session before requesting a deferred sort.");
            SetSortOrder(order); pendingSort = true;
        }

        /// <summary>Returns true once the queued request is completed, including an already sorted or empty hand.</summary>
        public bool TryCompletePendingSort()
        {
            if (!pendingSort || !IsRunning || !isActiveAndEnabled || !(view is HandZoneView hand) ||
                !hand.isActiveAndEnabled || hand.IsTransitioning) return false;
            var byId = Cards.ToDictionary(card => card.Card.Id);
            CardDefinition Resolve(CardInstanceId id) =>
                standardDefinitions.TryGetValue(byId[id].Card.DefinitionId, out var definition) ? definition : null;
            var zone = Zone;
            var sorted = sortOrder == CardZoneSortOrder.SuitFirst ? zone.SortBySuit(Resolve) : zone.SortByRank(Resolve);
            if (!sorted.Cards.SequenceEqual(zone.Cards))
                {
                    var update = match.PrepareReorder(zone.Id, sorted.Cards);
                    view.PresentCommitted(update.Project(zone.Id), () => match.Commit(update)); match.Notify(update, this);
                }
            pendingSort = false;
            return true;
        }

        private void LateUpdate() { if (Application.isPlaying) TryCompletePendingSort(); }
    }
}
