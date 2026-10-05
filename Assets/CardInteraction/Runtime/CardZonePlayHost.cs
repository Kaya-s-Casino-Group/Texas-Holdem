using System;
using System.Collections.Generic;
using System.Linq;
using CardPresentation;
using PokerEngine;
using UnityEngine;

namespace CardInteraction
{
    /// <summary>Scene-owned startup snapshot. Play works with the validation window closed and in a Player build.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CardZoneView))]
    public sealed partial class CardZonePlayHost : MonoBehaviour, ICardInteractionSource
    {
        [SerializeField] private bool hasSnapshot;
        [SerializeField] private string zoneId, zoneKind, controllerId;
        [SerializeField] private List<CardZoneCardState> initialCards = new List<CardZoneCardState>();
        [SerializeField] private List<CardZoneCardState> initialDiscardedCards = new List<CardZoneCardState>();
        [SerializeField] private Camera inputCamera;
        private List<CardZoneCardState> liveCards = new List<CardZoneCardState>();
        private List<CardZoneCardState> discardedCards = new List<CardZoneCardState>();
        private CardZoneMouseInput mouse;
        private CardZoneView view;
        public bool HasSnapshot => hasSnapshot;
        public bool IsRunning { get; private set; }
        public IReadOnlyList<CardZoneCardState> InitialCards => initialCards.AsReadOnly();
        public IReadOnlyList<CardZoneCardState> Cards => liveCards.AsReadOnly();
        public IReadOnlyList<CardZoneCardState> InitialDiscardedCards => initialDiscardedCards.AsReadOnly();
        public IReadOnlyList<CardZoneCardState> DiscardedCards => discardedCards.AsReadOnly();
        public CardZoneMouseInput MouseInput => mouse;
        public CardZone Zone => BuildZone(IsRunning ? liveCards : initialCards);
        public CardZone DiscardZone => BuildDiscardZone(zoneId, controllerId, IsRunning ? discardedCards : initialDiscardedCards);

        public void Capture(CardZone zone, IEnumerable<CardZoneCardState> cards, Camera camera = null,
            IEnumerable<CardZoneCardState> discarded = null)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            var copy = cards.Select(card => card != null ? card.Copy(clearHover: true) : throw new ArgumentException("Missing card state.", nameof(cards))).ToList();
            if (!zone.Cards.SequenceEqual(copy.Select(card => card.Card.Id)))
                throw new ArgumentException("Snapshot cards must match the zone's full identity order.", nameof(cards));
            GetComponent<CardZoneView>().ValidateCardCount(copy.Count);
            var discardCopy = CopyDiscarded(discarded ?? Enumerable.Empty<CardZoneCardState>());
            zone.TransferRangeTo(BuildDiscardZone(zone.Id.Value, zone.ControllerId?.Value, discardCopy), new CardInstanceId[0], discardCopy.Count);
            StopSession();
            zoneId = zone.Id.Value; zoneKind = zone.Kind; controllerId = zone.ControllerId?.Value;
            initialCards = copy; initialDiscardedCards = discardCopy; inputCamera = camera; hasSnapshot = true;
        }

        private CardZone BuildZone(IEnumerable<CardZoneCardState> cards) => new CardZone(new ZoneId(zoneId), zoneKind,
            string.IsNullOrEmpty(controllerId) ? (ParticipantId?)null : new ParticipantId(controllerId), cards.Select(card => card.Card.Id));

        public static CardZone BuildDiscardZone(string sourceId, string owner, IEnumerable<CardZoneCardState> cards) =>
            new CardZone(new ZoneId(Guard.Text(sourceId, nameof(sourceId)) + "/discard"), CardZoneKinds.Discard,
                string.IsNullOrEmpty(owner) ? (ParticipantId?)null : new ParticipantId(owner), cards.Select(card => card.Card.Id));

        private static List<CardZoneCardState> CopyDiscarded(IEnumerable<CardZoneCardState> cards) => cards.Select(card => {
            var copy = card != null ? card.Copy() : throw new ArgumentException("Missing discarded card state.");
            copy.SetInteraction(default(CardInteractionState)); return copy;
        }).ToList();

        public void BeginSession()
        {
            if (!hasSnapshot) return;
            var copy = initialCards.Select(card => card.Copy(clearHover: true)).ToList();
            GetComponent<CardZoneView>().ValidateCardCount(copy.Count);
            var discardCopy = CopyDiscarded(initialDiscardedCards);
            // Validate both ownership lists before replacing a session, including deserialized startup data.
            BuildZone(copy).TransferRangeTo(BuildDiscardZone(zoneId, controllerId, discardCopy), new CardInstanceId[0], discardCopy.Count);
            StopSession(); view = GetComponent<CardZoneView>(); mouse = GetComponent<CardZoneMouseInput>();
            liveCards = copy; discardedCards = discardCopy; view.Clear();
            view.Present(BuildZone(liveCards), id => Find(id.Value).Project(), animate: false);
            if (view is HandZoneView)
            {
                if (mouse == null) mouse = gameObject.AddComponent<CardZoneMouseInput>();
                mouse.Bind(view, this, inputCamera); mouse.StateChanged += Publish;
            }
            IsRunning = true;
        }

        public void StopSession()
        {
            // Unsubscribe first: clearing hover during teardown must not recreate views being destroyed.
            if (mouse != null) { mouse.StateChanged -= Publish; mouse.Unbind(); }
            IsRunning = false;
        }

        /// <summary>Replace this running zone's ordered cards, preserving the startup snapshot.</summary>
        public void ReplaceCards(IEnumerable<CardZoneCardState> cards, bool animate = true)
        {
            if (!IsRunning) throw new InvalidOperationException("Start the card zone session before replacing live cards.");
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            var copy = cards.Select(card => card != null ? card.Copy() : throw new ArgumentException("Missing card state.")).ToList();
            var zone = BuildZone(copy);
            zone.TransferRangeTo(DiscardZone, new CardInstanceId[0], discardedCards.Count);
            // Validate the projection and presentation before committing the interaction source.
            var byId = copy.ToDictionary(card => card.Id, StringComparer.Ordinal);
            view.Present(zone, id => byId[id.Value].Project(), animate);
            liveCards = copy;
        }

        /// <summary>Commit all selected cards in hand order. Disabled selection rejects the whole command.</summary>
        public int DiscardSelected(bool animate = true)
        {
            if (!IsRunning) throw new InvalidOperationException("Start the card zone session before discarding.");
            if (!(view is HandZoneView hand)) throw new InvalidOperationException("DiscardSelected requires a HandZoneView.");
            var batch = new CardZoneDiscardBatch(Zone, liveCards, DiscardZone);
            if (batch.Discarded.Count == 0) return 0;
            var byId = batch.Remaining.ToDictionary(card => card.Id, StringComparer.Ordinal);
            hand.Present(batch.Source, id => byId[id.Value].Project(), animate, batch.Discarded.Select(card => card.Id));
            liveCards = batch.Remaining.ToList(); discardedCards.AddRange(batch.Discarded);
            return batch.Discarded.Count;
        }

        private CardZoneCardState Find(string id) => liveCards.FirstOrDefault(card => card.Id == id);
        public bool TryGetInteraction(string viewId, out CardInteractionState state)
        {
            var card = Find(viewId); state = card == null ? default(CardInteractionState) : card.Interaction;
            return card != null;
        }
        public bool CanSelect(string viewId) { var card = Find(viewId); return card != null && card.Card.CanPlay; }
        public void SetInteraction(string viewId, CardInteractionState state)
        {
            var card = Find(viewId) ?? throw new ArgumentException("Card is not in this local zone.", nameof(viewId));
            card.SetInteraction(state);
        }
        public void Publish()
        {
            if (IsRunning && view != null) view.Present(BuildZone(liveCards), id => Find(id.Value).Project());
        }

        private void OnEnable() { if (Application.isPlaying) BeginSession(); }
        private void OnDisable() => StopSession();
        private void OnDestroy() => StopSession();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreSceneHosts()
        {
            // Also restart from the saved snapshot when Enter Play Mode disables domain/scene reload.
            foreach (var host in FindObjectsOfType<CardZonePlayHost>())
                if (host.isActiveAndEnabled) host.BeginSession();
        }
    }
}
