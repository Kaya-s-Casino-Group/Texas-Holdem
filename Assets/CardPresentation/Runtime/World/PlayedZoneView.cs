using System;
using System.Collections.Generic;
using PokerEngine;
using UnityEngine;

namespace CardPresentation
{
    /// <summary>Up to five played cards in fixed straight slots. Empty slots are positions, not fabricated cards.</summary>
    [AddComponentMenu("Cards/Played Zone View")]
    public class PlayedZoneView : CardZoneView
    {
        public const int SlotCount = 5;
        public override string DefaultZoneKind => CardZoneKinds.Played;
        public override int? CardCapacity => SlotCount;
        public override bool SupportsArc => false;
        public override bool SupportsSway => false;

        /// <summary>All five local poses, including unoccupied slots, in the same order used by Present.</summary>
        public IReadOnlyList<CardZoneCardPose> GetSlotPoses()
        {
            var bounds = CardInstanceView.ValidatePrefab(CardPrefab, out _);
            return Array.AsReadOnly(CalculateSlots(Layout, bounds.size * CalculateCardScale(Layout, MotionSettings, bounds.size)));
        }

        protected override CardZoneCardPose[] CalculateCardPoses(CardZoneLayoutSettings settings, Vector3 cardSize, int count)
        {
            ValidateCardCount(count);
            var slots = CalculateSlots(settings, cardSize);
            var result = new CardZoneCardPose[count];
            Array.Copy(slots, result, count);
            return result;
        }

        private static CardZoneCardPose[] CalculateSlots(CardZoneLayoutSettings settings, Vector3 cardSize)
        {
            settings.ArcHeight = 0;
            // The shared row layout validates dimensions and may compress gaps. A played row never overlaps cards.
            var slots = CardZoneRowLayout.CalculatePoses(settings, cardSize, SlotCount);
            if ((double)settings.Size.x - 2 * settings.Padding.x + 0.0000001 < (double)cardSize.x * SlotCount)
                throw new ArgumentException("出牌区可用宽度必须容纳五张完整卡牌；请增大宽度或减少留白。");
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new CardZoneCardPose(new Vector3(slots[i].Position.x, cardSize.y / 2, 0), Quaternion.identity);
            return slots;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (CardPrefab == null) return;
            Bounds bounds; CardZoneCardPose[] slots;
            try
            {
                bounds = CardInstanceView.ValidatePrefab(CardPrefab, out _);
                bounds.size *= CalculateCardScale(Layout, MotionSettings, bounds.size);
                slots = CalculateSlots(Layout, bounds.size);
            }
            catch (ArgumentException) { return; }
            var matrix = Gizmos.matrix; var color = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(.45f, .75f, 1, .8f);
            foreach (var slot in slots) Gizmos.DrawWireCube(slot.Position, bounds.size);
            Gizmos.matrix = matrix; Gizmos.color = color;
        }
    }
}
