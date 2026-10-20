using System;
using System.Collections.Generic;
using System.Linq;
using CardPresentation;
using PokerEngine;
using UnityEngine;

namespace CardInteraction
{
    /// <summary>Binds one view to an observer zone, translates input into data requests and coordinates local
    /// presentation. CardMatchHost owns the table/projection; gameplay owns command validation and execution.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CardZoneView))]
    public sealed partial class CardZonePlayHost : MonoBehaviour, ICardInteractionSource, ICardHoverSource
    {
        [SerializeField] private CardMatchHost match;
        [SerializeField] private string zoneId, deckZoneId, discardZoneId;
        [SerializeField] private Camera inputCamera;
        private CardZoneMouseInput mouse;
        private CardZoneView view;
        private bool attached;
        public CardMatchHost Match => match;
        public bool HasSnapshot => match != null && !string.IsNullOrEmpty(zoneId) && match.ContainsZone(new ZoneId(zoneId));
        public bool IsRunning => attached && match != null && match.IsRunning;
        public IReadOnlyList<CardZoneCardState> InitialCards => Read(zoneId, true);
        public IReadOnlyList<CardZoneCardState> Cards => Read(zoneId, false);
        public IReadOnlyList<CardZoneCardState> InitialDiscardedCards => Read(discardZoneId, true);
        public IReadOnlyList<CardZoneCardState> DiscardedCards => Read(discardZoneId, false);
        public CardZoneMouseInput MouseInput => mouse;
        public CardZone Zone => match.GetZone(new ZoneId(zoneId));
        public CardZone DiscardZone => match.GetZone(new ZoneId(discardZoneId));
        private IReadOnlyList<CardZoneCardState> Read(string id, bool initial) => match != null && !string.IsNullOrEmpty(id) &&
            match.ContainsZone(new ZoneId(id)) ? match.GetCards(new ZoneId(id), initial) : Array.Empty<CardZoneCardState>();

        public void Bind(CardMatchHost owner, ZoneId zone, ZoneId deck, ZoneId discard, Camera camera = null)
        {
            if (owner == null || owner.gameObject.scene != gameObject.scene) throw new ArgumentException("Bind a match in the same scene.", nameof(owner));
            var bound = owner.GetZone(zone);
            if (owner.GetZone(deck).Kind != CardZoneKinds.Deck || owner.GetZone(discard).Kind != CardZoneKinds.Discard)
                throw new ArgumentException("Bind a deck and discard zone of the correct kind.");
            GetComponent<CardZoneView>().ValidateCardCount(bound.Cards.Count);
            StopSession(); match = owner; zoneId = zone.Value; deckZoneId = deck.Value; discardZoneId = discard.Value; inputCamera = camera;
        }
        public void BeginSession()
        {
            if (!HasSnapshot) return;
            var nextView = GetComponent<CardZoneView>(); nextView.ValidateCardCount(Zone.Cards.Count);
            StopSession(); match.StartMatch(); view = nextView; mouse = GetComponent<CardZoneMouseInput>();
            view.Clear(); view.Present(Project(), false);
            if (view is HandZoneView)
            {
                if (mouse == null) mouse = gameObject.AddComponent<CardZoneMouseInput>();
                mouse.Bind(view, this, inputCamera); mouse.StateChanged += Publish;
            }
            attached = true; match.Restarted += OnMatchRestarted; match.Changed += OnMatchChanged; match.InteractionChanged += OnInteractionChanged;
        }
        public void StopSession()
        {
            StopProjection();
            pendingSort = false;
            if (match != null) { match.Restarted -= OnMatchRestarted; match.Changed -= OnMatchChanged; match.InteractionChanged -= OnInteractionChanged; }
            if (mouse != null) { mouse.StateChanged -= Publish; mouse.Unbind(); }
            attached = false;
        }
        private void OnMatchRestarted()
        {
            if (!attached || !isActiveAndEnabled) return;
            pendingSort = false; GetComponent<CardZoneTransferPresenter>()?.CompleteTransfers();
            view.Clear(); view.Present(Project(), false);
        }
        private void OnMatchChanged(CardTableChange change, IReadOnlyList<object> presented)
        {
            if (!IsRunning || presented.Any(data => ReferenceEquals(data, view.Data))) return;
            var before = change.Before.Zones.FirstOrDefault(zone => zone.Id.Value == zoneId);
            var after = change.After.GetZone(new ZoneId(zoneId));
            if (!ReferenceEquals(before, after) || after.Cards.Any(id => !ReferenceEquals(change.Before.GetCard(id), change.After.GetCard(id))))
                view.Present(Project()); // A table notification must not echo back as a local input event.
        }
        private void OnInteractionChanged(ZoneId zone, object presented)
        { if (zone.Value == zoneId && IsRunning && view != null && !ReferenceEquals(presented, view)) view.Present(Project()); }
        private CardZoneViewData Project() => match.Project(Zone.Id);
        public CardSelectionRequest CaptureSelection()
        {
            if (!IsRunning) throw new InvalidOperationException("Start a zone binding before capturing input.");
            return match.CaptureSelection(Zone.Id);
        }
        public static CardZone BuildDiscardZone(string sourceId, string owner, IEnumerable<CardZoneCardState> cards) =>
            new CardZone(new ZoneId(Guard.Text(sourceId, nameof(sourceId)) + "/discard"), CardZoneKinds.Discard,
                string.IsNullOrEmpty(owner) ? (ParticipantId?)null : new ParticipantId(owner), cards.Select(card => card.Card.Id));
        public int DiscardSelected(bool animate = true)
        {
            if (!IsRunning || !(view is HandZoneView hand)) throw new InvalidOperationException("Start a hand zone before discarding.");
            var request = CaptureSelection();
            if (match.Commands != null) return match.Commands.Discard(request, animate);
            var update = match.PrepareDiscard(request.ZoneId, DiscardZone.Id, request.Cards);
            if (update.Change.Transferred.Count == 0) return 0;
            hand.PresentDiscardCommitted(update.Project(Zone.Id), update.Change.Transferred.Select(id => id.Value), () => match.Commit(update), animate);
            match.Notify(update, view.Data); return update.Change.Transferred.Count;
        }
        private CardZoneCardState Find(string id) => Cards.FirstOrDefault(card => card.Id == id);
        public bool TryGetInteraction(string viewId, out CardInteractionState state)
        { var card = Find(viewId); state = card == null ? default : card.Interaction; return card != null; }
        public bool CanSelect(string viewId)
        {
            var card = Find(viewId); var hand = GetComponent<HandZoneView>();
            return card != null && !card.Card.IsDisabled && (card.Card.CanPlay || card.Card.CanDiscard) &&
                (match.Commands == null || match.Commands.CanInteract) &&
                (hand == null || card.Interaction.IsSelected || Cards.Count(item => item.Interaction.IsSelected) < hand.MaxSelectedCards);
        }
        public void SetInteraction(string viewId, CardInteractionState state)
        {
            var card = Find(viewId) ?? throw new ArgumentException("Card is outside this match zone.", nameof(viewId));
            if (state.IsSelected && !card.Interaction.IsSelected && GetComponent<HandZoneView>() is HandZoneView hand &&
                Cards.Count(item => item.Interaction.IsSelected) >= hand.MaxSelectedCards)
            { hand.PlaySelectionDenied(viewId); return; }
            match.SetInteraction(card.Card.Id, state, notify: false);
        }
        public void Publish()
        {
            if (!IsRunning || view == null) return;
            view.Present(Project()); match.NotifyInteraction(Zone.Id, view);
        }
        // A gameplay host may bind and deal before this component's first OnEnable.
        // Keep that live session, its draw animation and its queued post-landing sort.
        private void OnEnable() { if (Application.isPlaying && !IsRunning) BeginSession(); }
        private void OnDisable() => StopSession();
        private void OnDestroy() => StopSession();
    }
}
