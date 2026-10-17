using System;
using UnityEngine;

namespace CardPresentation
{
    public enum CardZoneAlignment { Left, Center, Right }

    [Serializable]
    public struct CardZoneLayoutSettings
    {
        public Vector2 Size;
        [Tooltip("按区域可用高度等比缩放卡牌；宽度过小时进一步缩小。出牌区始终预留五个槽位。关闭时使用 Prefab 原始大小。")]
        public bool FitCardsToZone;
        public Vector2 Padding;
        [Min(0)] public float PreferredGap;
        [Min(0)] public float OverlapLift;
        public float ArcHeight;
        public CardZoneAlignment Alignment;

        public static CardZoneLayoutSettings Default => new CardZoneLayoutSettings {
            Size = new Vector2(0.5f, 0.13f), Padding = new Vector2(0.005f, 0.005f),
            PreferredGap = 0.01f, OverlapLift = 0.0005f, ArcHeight = 0.008f, Alignment = CardZoneAlignment.Center };
    }

    public readonly struct CardZoneCardPose
    {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public CardZoneCardPose(Vector3 position, Quaternion rotation) { Position = position; Rotation = rotation; }
    }

    /// <summary>Centered XZ rectangle. Curved cards face +Y with their base +Z pointing toward one circle center.</summary>
    public static class CardZoneRowLayout
    {
        public static Vector3[] Calculate(CardZoneLayoutSettings settings, Vector3 cardSize, int count)
        {
            var poses = CalculatePoses(settings, cardSize, count);
            var positions = new Vector3[poses.Length];
            for (int i = 0; i < poses.Length; i++) positions[i] = poses[i].Position;
            return positions;
        }

        public static CardZoneCardPose[] CalculatePoses(CardZoneLayoutSettings settings, Vector3 cardSize, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (!Positive(settings.Size.x) || !Positive(settings.Size.y) || !Nonnegative(settings.Padding.x) ||
                !Nonnegative(settings.Padding.y) || !Nonnegative(settings.PreferredGap) || !Nonnegative(settings.OverlapLift) || !Finite(settings.ArcHeight) ||
                !Enum.IsDefined(typeof(CardZoneAlignment), settings.Alignment)) throw new ArgumentException("Invalid zone layout settings.");
            if (!Positive(cardSize.x) || !Positive(cardSize.z) || !Nonnegative(cardSize.y)) throw new ArgumentException("Invalid card dimensions.");
            float width = settings.Size.x - 2 * settings.Padding.x;
            float height = settings.Size.y - 2 * settings.Padding.y;
            if (width < cardSize.x || height < cardSize.z) throw new ArgumentException("The padded zone must fit at least one full card.");
            var result = new CardZoneCardPose[count];
            if (count == 0) return result;
            double step = count == 1 ? 0 : Math.Min((double)cardSize.x + settings.PreferredGap, (width - cardSize.x) / (count - 1.0));
            double halfSpan = step * (count - 1) / 2;
            double angle = halfSpan > 0 ? 2 * Math.Atan(Math.Min(Math.Abs((double)settings.ArcHeight), halfSpan) / halfSpan) : 0;
            // Reserve the entire rotated footprint, including the sway envelope supplied by the caller.
            // Reduce the angle until the circle AND all card corners fit; never squash the cards.
            double low = 0, high = angle;
            for (int iteration = 0; iteration < 40; iteration++)
            {
                double candidate = (low + high) / 2;
                double candidateSpan = FittedHalfSpan(candidate, halfSpan, width, cardSize);
                double nearestAngle = count % 2 == 0 && count > 1 ? candidate / (count - 1) : 0;
                double depth = RotatedExtent(cardSize.z, cardSize.x, candidate);
                if (candidate > 0) depth += candidateSpan / Math.Sin(candidate) * CosDifference(nearestAngle, candidate);
                if (candidateSpan > 0 && depth <= height) low = candidate; else high = candidate;
            }
            angle = low;
            bool curved = count > 1 && angle > 0.0000001;
            double span = curved ? FittedHalfSpan(angle, halfSpan, width, cardSize) : halfSpan;
            double footprintWidth = curved ? RotatedExtent(cardSize.x, cardSize.z, angle) : cardSize.x;
            double occupied = span * 2 + footprintWidth;
            double centerX = settings.Alignment == CardZoneAlignment.Left ? -(width - occupied) / 2 :
                settings.Alignment == CardZoneAlignment.Right ? (width - occupied) / 2 : 0;
            float lift = count > 1 && (curved || step < cardSize.x) ? Mathf.Max(settings.OverlapLift, cardSize.y + 0.00001f) : 0;
            double radius = curved ? span / Math.Sin(angle) : 0;
            double nearest = count % 2 == 0 && count > 1 ? angle / (count - 1) : 0;
            double depthSpan = radius * CosDifference(nearest, angle);
            int sign = Math.Sign(settings.ArcHeight);
            for (int i = 0; i < count; i++)
            {
                double phi = count > 1 ? angle * (2.0 * i / (count - 1) - 1) : 0;
                double x = curved ? radius * Math.Sin(phi) : -span + step * i;
                double z = curved ? sign * (radius * CosDifference(Math.Abs(phi), angle) - depthSpan / 2) : 0;
                var position = new Vector3((float)(centerX + x), cardSize.y / 2 + i * lift, (float)z);
                if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z)) throw new ArgumentException("Layout exceeds finite coordinates.");
                var rotation = curved ? Quaternion.LookRotation(new Vector3((float)-Math.Sin(phi), 0, (float)(-sign * Math.Cos(phi))), Vector3.up) : Quaternion.identity;
                result[i] = new CardZoneCardPose(position, rotation);
            }
            return result;
        }

        // Maximum full X or Z projection over all yaw angles from zero to halfAngle.
        private static double RotatedExtent(double primary, double secondary, double halfAngle)
        {
            double angle = Math.Min(halfAngle, Math.Atan2(secondary, primary));
            return primary * Math.Cos(angle) + secondary * Math.Sin(angle);
        }

        private static double FittedHalfSpan(double angle, double preferred, double width, Vector3 size) =>
            Math.Max(0, Math.Min(preferred, (width - RotatedExtent(size.x, size.z, angle)) / 2));

        // cos(a) - cos(b), without catastrophic cancellation for very shallow arcs.
        private static double CosDifference(double a, double b) => 2 * Math.Sin((b + a) / 2) * Math.Sin((b - a) / 2);

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Positive(float value) => Finite(value) && value > 0;
        private static bool Nonnegative(float value) => Finite(value) && value >= 0;
    }
}
