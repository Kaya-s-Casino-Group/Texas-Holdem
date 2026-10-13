using System;
using System.Linq;
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
                var destination = Played;
                if (!Ready || playRoute == null || !playRoute.isActiveAndEnabled || playRoute.IsTransferring ||
                    playRoute.Source != hand.GetComponent<HandZoneView>() || destination == null || !destination.IsRunning ||
                    !destination.isActiveAndEnabled || !playRoute.Destination.isActiveAndEnabled ||
                    playRoute.Source.CardPrefab != playRoute.Destination.CardPrefab) return false;
                var selected = hand.Cards.Where(card => card.Interaction.IsSelected).ToArray();
                if (hand.Match.Commands != null && !hand.Match.Commands.CanPlay(hand)) return false;
                return selected.Length > 0 && selected.Length <= PlayedZoneView.SlotCount - destination.Cards.Count &&
                    selected.All(card => card.Card.CanPlay && playRoute.Source.TryGetView(card.Id, out var visual) &&
                        visual.isActiveAndEnabled && visual.Surface != null && visual.Surface.GetComponent<Renderer>() is Renderer renderer && renderer.enabled);
            }
        }
        public bool CanDiscard => Ready && (hand.Match.Commands == null || hand.Match.Commands.CanDiscard(hand)) && hand.Cards.Any(card => card.Interaction.IsSelected) &&
            hand.Cards.Where(card => card.Interaction.IsSelected).All(card => card.Card.CanDiscard);

        public void Configure(CardZonePlayHost source, HandActionPanelView view, CardZoneTransferPresenter route = null)
        {
            if (source == null || source.GetComponent<HandZoneView>() == null) throw new ArgumentException("Select a hand zone host.", nameof(source));
            if (view == null || !view.IsConfigured) throw new ArgumentException("Configure the action panel first.", nameof(view));
            if (route != null && route.Source != source.GetComponent<HandZoneView>())
                throw new ArgumentException("The play route must start at this hand.", nameof(route));
            Unsubscribe(); hand = source; panel = view; playRoute = route;
            if (isActiveAndEnabled) Subscribe(); Refresh();
        }
        public void Play() { if (CanPlay) { hand.PlaySelected(Played, playRoute); Refresh(); } }
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
            panel.SetState(CanPlay, CanDiscard, Ready, hand != null && hand.SortOrder == CardZoneSortOrder.SuitFirst);
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
