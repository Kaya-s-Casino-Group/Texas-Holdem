using System;
using System.Collections.Generic;
using System.Linq;
using BattlePresentation;
using CardInteraction;
using CardPresentation;
using PokerEngine;
using UnityEngine;

namespace Poker.BattlePresentation
{
    /// <summary>World input adapter. Candidate selection belongs to the battle bridge; hover is local.</summary>
    [DefaultExecutionOrder(50), DisallowMultipleComponent, RequireComponent(typeof(HandZoneView), typeof(WorldCardZonePresentation), typeof(CardZoneMouseInput))]
    public sealed class BattleHandInteraction : MonoBehaviour, ICardInteractionSource, ICardInteractionGate, ICardReorderSource
    {
        [SerializeField] private BattleTimelineSceneAdapter adapter;
        [SerializeField] private Camera inputCamera;
        private HandZoneView hand;
        private WorldCardZonePresentation presentation;
        private CardZoneMouseInput input;
        private string hovered;
        private bool connected;
        // Pointer gestures share the bridge's player-turn and playback gate.
        public bool CanInteract => isActiveAndEnabled && connected && hand != null && hand.isActiveAndEnabled &&
            presentation.isActiveAndEnabled && adapter != null && adapter.CanAct;
        private bool CanChoose => CanInteract && adapter.CanAct &&
            (adapter.Host.AuthorityState.FlowFor(adapter.ObserverId).Window == BattlePlayerWindow.Action ||
             adapter.Host.AuthorityState.FlowFor(adapter.ObserverId).Window == BattlePlayerWindow.ForcedDiscard);

        public void Configure(BattleTimelineSceneAdapter source, Camera camera)
        {
            if (source == null || source.gameObject.scene != gameObject.scene || !transform.IsChildOf(source.transform))
                throw new ArgumentException("Bind the battle adapter that owns this hand.");
            Disconnect(); adapter = source; inputCamera = camera;
            if (isActiveAndEnabled) Connect();
        }
        private void OnEnable() => Connect();
        private void Connect()
        {
            if (connected || adapter == null) return;
            if (adapter.gameObject.scene != gameObject.scene || !transform.IsChildOf(adapter.transform))
                throw new InvalidOperationException("Hand input must belong to its battle adapter.");
            hand = GetComponent<HandZoneView>(); presentation = GetComponent<WorldCardZonePresentation>();
            input = GetComponent<CardZoneMouseInput>();
            connected = true;
            input.Bind(hand, this, inputCamera); input.StateChanged += RefreshFeedback;
            adapter.CandidateChanged += OnCandidate;
            adapter.Director.Display.Changed += OnDisplay;
            RefreshFeedback();
        }
        private CardDisplayData Card(string viewId) => presentation?.Current?.Cards.FirstOrDefault(card => card.ViewId == viewId);
        public bool TryGetInteraction(string viewId, out CardInteractionState state)
        {
            var card = Card(viewId);
            state = new CardInteractionState(hovered == viewId,
                card?.CardId != null && adapter?.Candidate?.SelectedIds.Contains(card.CardId) == true);
            return card != null && !card.Hidden && card.CardId != null;
        }
        public bool CanSelect(string viewId)
        {
            var card = Card(viewId);
            return CanChoose && card != null && !card.Hidden && card.CardId != null && card.DisabledReason == null &&
                hand.TryGetView(viewId, out var visual) && !visual.IsEntering && !visual.IsExiting;
        }
        public void SetInteraction(string viewId, CardInteractionState next)
        {
            if (!TryGetInteraction(viewId, out var previous)) return;
            if (!next.IsHovered && hovered == viewId) hovered = null;
            else if (next.IsHovered && CanInteract) hovered = viewId;
            if (next.IsSelected == previous.IsSelected || !CanChoose) return;
            if (next.IsSelected && (!CanSelect(viewId) || adapter.Candidate.SelectedIds.Count >= hand.MaxSelectedCards)) return;
            var ids = new HashSet<string>(adapter.Candidate.SelectedIds, StringComparer.Ordinal);
            var card = Card(viewId);
            if (next.IsSelected) ids.Add(card.CardId); else ids.Remove(card.CardId);
            adapter.SelectCards(ids);
        }
        private void OnCandidate(CandidatePresentationModel candidate)
        {
            if (!CanInteract || candidate == null || candidate.State == CandidateState.Closed || candidate.State == CandidateState.Committing)
            { hovered = null; input.Controller?.CancelPointer(); presentation.EndReorder(true); }
            RefreshFeedback();
        }
        private void OnDisplay(DisplayUpdate update)
        {
            if (update.IsSynchronization || !CanInteract) { hovered = null; input.Controller?.CancelPointer(); presentation.EndReorder(true); }
            RefreshFeedback();
        }
        public bool BeginReorder(string viewId, Ray pointer) => CanInteract && presentation.BeginReorder(viewId, pointer);
        public bool UpdateReorder(Ray pointer)
        {
            if (CanInteract) return presentation.UpdateReorder(pointer);
            presentation.EndReorder(true); return false;
        }
        public void EndReorder(bool cancel) => presentation.EndReorder(cancel || !CanInteract);
        private void Update()
        {
            // Also invalidate a held gesture when the host/gate changes without a display publication.
            if (connected && !CanInteract) { input.Controller?.CancelPointer(); presentation.EndReorder(true); }
        }
        private void RefreshFeedback()
        {
            if (!connected || presentation == null) return;
            if (!CanInteract) hovered = null;
            var selected = new HashSet<string>(adapter.Candidate?.SelectedIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            presentation.PresentInteraction((presentation.Current?.Cards ?? Array.Empty<CardDisplayData>()).Select(card =>
                new KeyValuePair<string, CardInteractionState>(card.ViewId,
                    new CardInteractionState(!card.Hidden && card.ViewId == hovered,
                        !card.Hidden && card.CardId != null && selected.Contains(card.CardId)))));
        }
        private void Disconnect()
        {
            if (!connected) return;
            connected = false;
            input.StateChanged -= RefreshFeedback; input.Unbind();
            adapter.CandidateChanged -= OnCandidate; adapter.Director.Display.Changed -= OnDisplay;
            hovered = null; presentation.PresentInteraction(null, false);
            if (adapter.CanAct) adapter.SelectCards(Array.Empty<string>());
        }
        private void OnDisable() => Disconnect();
        private void OnDestroy() => Disconnect();
    }
}
