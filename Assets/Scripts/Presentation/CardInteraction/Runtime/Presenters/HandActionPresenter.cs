using System;
using System.Collections.Generic;
using CardPresentation;
using UnityEngine;

namespace CardInteraction
{
    /// <summary>Local hand actions. Buttons call card-data commands, then views consume the resulting lists.</summary>
    [DisallowMultipleComponent]
    public sealed class HandActionPresenter : MonoBehaviour
    {
        [SerializeField] private CardZonePlayHost hand;
        [SerializeField] private HandActionPanelView panel;
        [SerializeField] private CardZoneTransferPresenter playRoute;
        private HandActionPanelView subscribedPanel;
        public CardZonePlayHost Hand => hand;
        public HandActionPanelView Panel => panel;
        public CardZoneTransferPresenter PlayRoute => playRoute;
        public CardZonePlayHost Played => playRoute != null && playRoute.Destination != null ?
            playRoute.Destination.GetComponent<CardZonePlayHost>() : null;
        private bool Ready => isActiveAndEnabled && hand != null && hand.isActiveAndEnabled && hand.IsRunning &&
            hand.GetComponent<HandZoneView>() is HandZoneView view && view.isActiveAndEnabled && (hand.Match.Commands == null || hand.Match.Commands.CanInteract);
        public bool CanPlay
        {
            get
            {
                var request = Ready ? hand.CaptureSelection() : null;
                return HasSelection(request) && CanPlaySelection(request, hand.Cards);
            }
        }
        public bool CanDiscard
        {
            get
            {
                var request = Ready ? hand.CaptureSelection() : null;
                return HasSelection(request) && CanDiscardSelection(request, hand.Cards);
            }
        }
        private static bool HasSelection(CardSelectionRequest request) => request != null && request.Cards.Count > 0;
        private bool CanPlaySelection(CardSelectionRequest request, IReadOnlyList<CardZoneCardState> cards)
        {
            if (!HasSelection(request)) return false;
            if (hand.Match.Commands != null)
            {
                if (!hand.Match.Commands.CanPlay(request)) return false;
            }
            else
            {
                var destination = Played;
                if (playRoute == null || !playRoute.isActiveAndEnabled || playRoute.IsTransferring ||
                    playRoute.Source != hand.GetComponent<HandZoneView>() || destination == null || !destination.IsRunning ||
                    !destination.isActiveAndEnabled || !playRoute.Destination.isActiveAndEnabled ||
                    playRoute.Source.CardPrefab != playRoute.Destination.CardPrefab ||
                    request.Cards.Count > PlayedZoneView.SlotCount - destination.Cards.Count) return false;
            }
            var view = hand.GetComponent<HandZoneView>();
            int selected = 0;
            foreach (var card in cards)
            {
                if (!card.Interaction.IsSelected) continue;
                selected++;
                if (!card.Card.CanPlay || !view.TryGetView(card.Id, out var visual) || !visual.isActiveAndEnabled ||
                    visual.Surface == null || !(visual.Surface.GetComponent<Renderer>() is Renderer renderer) || !renderer.enabled)
                    return false;
            }
            return selected == request.Cards.Count;
        }
        private bool CanDiscardSelection(CardSelectionRequest request, IReadOnlyList<CardZoneCardState> cards)
        {
            if (!HasSelection(request) || hand.Match.Commands != null && !hand.Match.Commands.CanDiscard(request)) return false;
            int selected = 0;
            foreach (var card in cards)
            {
                if (!card.Interaction.IsSelected) continue;
                selected++; if (!card.Card.CanDiscard) return false;
            }
            return selected == request.Cards.Count;
        }

        public void Configure(CardZonePlayHost source, HandActionPanelView view, CardZoneTransferPresenter route = null)
        {
            if (source == null || source.GetComponent<HandZoneView>() == null) throw new ArgumentException("Select a hand zone host.", nameof(source));
            if (view == null || !view.IsConfigured) throw new ArgumentException("Configure the action panel first.", nameof(view));
            if (route != null && route.Source != source.GetComponent<HandZoneView>())
                throw new ArgumentException("The play route must start at this hand.", nameof(route));
            Unsubscribe(); hand = source; panel = view; playRoute = route;
            if (isActiveAndEnabled) Subscribe(); Refresh();
        }
        public void Play()
        {
            if (!CanPlay) return;
            if (hand.Match.Commands != null) hand.PlaySelected();
            else hand.PlaySelected(Played, playRoute);
            Refresh();
        }
        public void Discard() { if (CanDiscard) { hand.DiscardSelected(); Refresh(); } }
        public void SortByRank() => Sort(CardZoneSortOrder.RankFirst);
        public void SortBySuit() => Sort(CardZoneSortOrder.SuitFirst);
        private void Sort(CardZoneSortOrder order)
        {
            if (!Ready) return;
            hand.RequestSortAfterLanding(order); hand.TryCompletePendingSort(); Refresh();
        }
        public void Refresh()
        {
            if (panel == null) return;
            bool ready = Ready;
            // Capture one immutable request for both buttons. An empty hand selection must not
            // enter gameplay legality queries just to rediscover that both actions are disabled.
            var request = ready ? hand.CaptureSelection() : null;
            var cards = HasSelection(request) ? hand.Cards : null;
            panel.SetState(CanPlaySelection(request, cards), CanDiscardSelection(request, cards), ready,
                hand != null && hand.SortOrder == CardZoneSortOrder.SuitFirst);
        }
        private void Subscribe()
        {
            if (panel == null || subscribedPanel != null) return;
            subscribedPanel = panel; panel.PlayRequested += Play; panel.DiscardRequested += Discard;
            panel.RankSortRequested += SortByRank; panel.SuitSortRequested += SortBySuit;
        }
        private void Unsubscribe()
        {
            if (subscribedPanel == null) return;
            subscribedPanel.PlayRequested -= Play; subscribedPanel.DiscardRequested -= Discard;
            subscribedPanel.RankSortRequested -= SortByRank; subscribedPanel.SuitSortRequested -= SortBySuit; subscribedPanel = null;
        }
        private void OnEnable() { Subscribe(); Refresh(); }
        private void OnDisable() { Unsubscribe(); Refresh(); }
        private void LateUpdate() { if (Application.isPlaying) Refresh(); }
    }
}
