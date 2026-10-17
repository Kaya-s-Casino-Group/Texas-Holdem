using System;
using UnityEngine;

namespace CardPresentation
{
    [Serializable]
    public struct CardZoneLiftSettings
    {
        public bool Enabled;
        [Min(0.01f)] public float RaiseSeconds;
        [Min(0.01f)] public float LowerSeconds;
        [Range(0, 3)] public float Overshoot;
        // Durations include the small overshoot and return. Zero overshoot is the original OutCubic curve.
        public static CardZoneLiftSettings Default => new CardZoneLiftSettings {
            Enabled = true, RaiseSeconds = 0.12f, LowerSeconds = 0.14f, Overshoot = 2.2f };
        public void Validate()
        {
            if (!CardZoneRowLayout.Finite(RaiseSeconds) || !CardZoneRowLayout.Finite(LowerSeconds) || RaiseSeconds < 0.01f || LowerSeconds < 0.01f)
                throw new ArgumentException("上浮/回落时间须为至少 0.01 秒的有限值。");
            if (!CardZoneRowLayout.Finite(Overshoot) || Overshoot < 0 || Overshoot > 3)
                throw new ArgumentException("上浮/回落惯性强度须为 0–3 的有限值。");
        }
    }
}
