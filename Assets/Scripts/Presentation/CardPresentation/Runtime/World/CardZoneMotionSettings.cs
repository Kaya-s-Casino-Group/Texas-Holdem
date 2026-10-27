using System;
using UnityEngine;

namespace CardPresentation
{
    /// <summary>Cosmetic motion, independent of card rules and the match RNG.</summary>
    [Serializable]
    public struct CardZoneMotionSettings : IEquatable<CardZoneMotionSettings>
    {
        public bool Enabled;
        [Range(0, 10)] public float MaxSwayAngle;
        public Vector2 PeriodSeconds;
        public int Seed;

        public static CardZoneMotionSettings Default => new CardZoneMotionSettings {
            Enabled = true, MaxSwayAngle = 1.5f, PeriodSeconds = new Vector2(3.5f, 6.5f), Seed = 0 };

        public void Validate()
        {
            if (!CardZoneRowLayout.Finite(MaxSwayAngle) || MaxSwayAngle < 0 || MaxSwayAngle > 10 ||
                !CardZoneRowLayout.Finite(PeriodSeconds.x) || !CardZoneRowLayout.Finite(PeriodSeconds.y) ||
                PeriodSeconds.x < 0.1f || PeriodSeconds.y < PeriodSeconds.x)
                throw new ArgumentException("摆动角度须为 0–10 度，周期须为至少 0.1 秒的有限递增区间。");
        }

        internal Vector3 LayoutSize(Vector3 size)
        {
            Validate();
            if (!Enabled) return size;
            // Conservative XZ footprint for any yaw in [-MaxSwayAngle, MaxSwayAngle].
            float sin = Mathf.Sin(MaxSwayAngle * Mathf.Deg2Rad);
            return new Vector3(size.x + size.z * sin, size.y, size.z + size.x * sin);
        }

        internal void GetProfile(string viewId, out float amplitude, out float period, out float phase)
        {
            amplitude = MaxSwayAngle * Mathf.Lerp(0.65f, 1, Unit(viewId, 1));
            period = Mathf.Lerp(PeriodSeconds.x, PeriodSeconds.y, Unit(viewId, 2));
            phase = Unit(viewId, 3);
        }

        private float Unit(string id, uint salt)
        {
            unchecked
            {
                uint hash = 2166136261u ^ (uint)Seed ^ (salt * 2654435761u);
                foreach (char c in id) hash = (hash ^ c) * 16777619u;
                hash ^= hash >> 16; hash *= 2246822519u; hash ^= hash >> 13;
                return (hash & 0x00ffffffu) / 16777216f;
            }
        }

        public bool Equals(CardZoneMotionSettings other) => Enabled == other.Enabled && MaxSwayAngle == other.MaxSwayAngle &&
            PeriodSeconds.Equals(other.PeriodSeconds) && Seed == other.Seed;
        public override bool Equals(object obj) => obj is CardZoneMotionSettings other && Equals(other);
        public override int GetHashCode() => Enabled.GetHashCode() ^ MaxSwayAngle.GetHashCode() ^ PeriodSeconds.GetHashCode() ^ Seed;
    }
}
