using System;
using UnityEngine;

namespace CardPresentation
{
    [Serializable]
    public struct CardZoneDiscardSettings
    {
        [Min(0.01f)] public float FlightSeconds;
        [Min(0)] public float Interval;
        [Min(0)] public float FlightLift;

        public static CardZoneDiscardSettings Default => new CardZoneDiscardSettings {
            FlightSeconds = 0.45f, Interval = 0.08f, FlightLift = 0.025f };

        public void Validate()
        {
            if (!CardZoneRowLayout.Finite(FlightSeconds) || FlightSeconds < 0.01f ||
                !CardZoneRowLayout.Finite(Interval) || Interval < 0 ||
                !CardZoneRowLayout.Finite(FlightLift) || FlightLift < 0)
                throw new ArgumentException("弃牌飞行时间须至少 0.01 秒，间隔/抬高须非负，所有参数须有限。");
        }
    }
}
