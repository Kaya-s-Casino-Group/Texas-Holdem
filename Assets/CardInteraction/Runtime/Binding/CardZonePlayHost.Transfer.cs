using System;
using System.Linq;
using CardPresentation;

namespace CardInteraction
{
    public sealed partial class CardZonePlayHost
    {
        public int PlaySelected(bool animate = true)
        {
            if (!IsRunning || !isActiveAndEnabled || match.Commands == null)
                throw new InvalidOperationException("Start a hand binding with a gameplay command authority.");
            return match.Commands.Play(CaptureSelection(), animate);
        }

        public int PlaySelected(CardZonePlayHost destination, CardZoneTransferPresenter route, bool animate = true)
        {
            // External gameplay owns its route/action view. Explicit scene endpoints are for standalone tables.
            if (IsRunning && match.Commands != null) return PlaySelected(animate);
            if (!IsRunning || destination == null || !destination.IsRunning) throw new InvalidOperationException("Start both match zone bindings.");
            return TransferSelected(destination, route, animate, false);
        }
#if UNITY_EDITOR
        public int PlaySelectedPreview(CardZonePlayHost destination, CardZoneTransferPresenter route, bool animate = true)
        {
            if (UnityEngine.Application.isPlaying || IsRunning || destination == null || destination.IsRunning)
                throw new InvalidOperationException("Authoring preview requires stopped bindings.");
            return TransferSelected(destination, route, animate, true);
        }
#endif
        private int TransferSelected(CardZonePlayHost destination, CardZoneTransferPresenter route, bool animate, bool preview)
        {
            if (destination.Match != Match) throw new InvalidOperationException("Card transfers require one match owner.");
            if (!HasSnapshot || !destination.HasSnapshot || route == null || route.Source != GetComponent<CardZoneView>() ||
                route.Destination != destination.GetComponent<CardZoneView>()) throw new InvalidOperationException("The route must match both zone bindings.");
            var from = preview ? InitialCards : Cards; var to = preview ? destination.InitialCards : destination.Cards;
            if (route.Source.Data == null || route.Destination.Data == null ||
                !route.Source.Data.Cards.Select(card => card.ViewId).SequenceEqual(from.Select(card => card.Id)) ||
                !route.Destination.Data.Cards.Select(card => card.ViewId).SequenceEqual(to.Select(card => card.Id)))
                throw new InvalidOperationException("Refresh stale zone presentations before transfer.");
            var selected = from.Where(card => card.Interaction.IsSelected).Select(card => card.Card.Id);
            CardMatchUpdate update;
#if UNITY_EDITOR
            if (preview) update = match.PreparePreviewPlay(Zone.Id, destination.Zone.Id, selected, PlayedZoneView.SlotCount);
            else
#endif
                update = match.PreparePlay(Zone.Id, destination.Zone.Id, selected, PlayedZoneView.SlotCount);
            if (update.Change.Transferred.Count == 0) return 0;
            route.PresentTransfer(update.Project(Zone.Id), update.Project(destination.Zone.Id),
                update.Change.Transferred.Select(id => id.Value), animate, () => match.Commit(update));
            match.Notify(update, route.Source.Data, route.Destination.Data); return update.Change.Transferred.Count;
        }
    }
}
