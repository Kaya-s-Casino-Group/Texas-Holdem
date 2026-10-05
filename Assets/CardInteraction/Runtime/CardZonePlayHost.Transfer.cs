using System;
using System.Linq;
using CardPresentation;
using UnityEngine;

namespace CardInteraction
{
    public sealed partial class CardZonePlayHost
    {
        public int PlaySelected(CardZonePlayHost destination, CardZoneTransferPresenter route, bool animate = true)
        {
            if (!IsRunning || destination == null || !destination.IsRunning)
                throw new InvalidOperationException("出牌前请启动两个牌区的运行会话。");
            return TransferSelected(destination, route, animate, false);
        }

        /// <summary>Authoring-only counterpart. Edits serialized startup card data without enabling mouse input.</summary>
        public int PlaySelectedPreview(CardZonePlayHost destination, CardZoneTransferPresenter route, bool animate = true)
        {
            if (Application.isPlaying || IsRunning || destination == null || destination.IsRunning)
                throw new InvalidOperationException("初始快照预览只适用于未运行的牌区。");
            return TransferSelected(destination, route, animate, true);
        }

        private int TransferSelected(CardZonePlayHost destination, CardZoneTransferPresenter route, bool animate, bool preview)
        {
            if (!HasSnapshot || !destination.HasSnapshot || route == null ||
                route.Source != GetComponent<CardZoneView>() || route.Destination != destination.GetComponent<CardZoneView>())
                throw new InvalidOperationException("出牌路径必须匹配拥有卡牌数据快照的手牌区和出牌区。");
            var fromCards = preview ? initialCards : liveCards;
            var toCards = preview ? destination.initialCards : destination.liveCards;
            var batch = new CardZonePlayBatch(Zone, fromCards, destination.Zone, toCards, PlayedZoneView.SlotCount);
            if (batch.Played.Count == 0) return 0;
            var archivedIds = DiscardZone.Cards.Concat(destination.DiscardZone.Cards).ToArray();
            if (fromCards.Concat(toCards).Any(card => archivedIds.Contains(card.Card.Id)))
                throw new InvalidOperationException("卡牌身份与弃牌区重复。");
            // Prevent transferring against an unrelated/stale visual snapshot; identities include hidden cards' ViewId.
            if (route.Source.Data == null || route.Destination.Data == null ||
                !route.Source.Data.Cards.Select(card => card.ViewId).SequenceEqual(fromCards.Select(card => card.Id)) ||
                !route.Destination.Data.Cards.Select(card => card.ViewId).SequenceEqual(toCards.Select(card => card.Id)))
                throw new InvalidOperationException("牌区视觉快照与卡牌列表不一致，请先刷新牌区。");
            var remaining = batch.Remaining.ToList(); var received = batch.Destination.ToList();
            var sourceData = new CardZoneViewData(batch.Source.Id, remaining.Select(card => card.Project()));
            var targetData = new CardZoneViewData(batch.Target.Id, received.Select(card => card.Project()));
            route.PresentTransfer(sourceData, targetData, batch.Played.Select(card => card.Id), animate, () => {
                if (preview) { initialCards = remaining; destination.initialCards = received; }
                else { liveCards = remaining; destination.liveCards = received; }
            });
            return batch.Played.Count;
        }
    }
}
