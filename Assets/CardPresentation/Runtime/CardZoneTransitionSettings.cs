using System;
using UnityEngine;

namespace CardPresentation
{
    /// <summary>Presentation timing only; deck ownership and draw rules belong to the engine.</summary>
    [Serializable]
    public struct CardZoneTransitionSettings
    {
        public bool Enabled;
        [Min(0.01f)] public float FlightSeconds;
        [Min(0)] public float DealInterval;
        [Min(0.01f)] public float SettleSeconds;
        [Min(0.01f)] public float ReflowSeconds;
        [Min(0)] public float FlightLift;
        [Min(0)] public float OvershootDistance;
        [Range(0, 15)] public float LandingYaw;

        public static CardZoneTransitionSettings Default => new CardZoneTransitionSettings {
            Enabled = true, FlightSeconds = 0.45f, DealInterval = 0.12f, SettleSeconds = 0.65f,
            ReflowSeconds = 0.25f, FlightLift = 0.025f, OvershootDistance = 0.006f, LandingYaw = 3 };

        public void Validate()
        {
            if (!Positive(FlightSeconds) || !Positive(SettleSeconds) || !Positive(ReflowSeconds) ||
                !Nonnegative(DealInterval) || !Nonnegative(FlightLift) || !Nonnegative(OvershootDistance) ||
                !Nonnegative(LandingYaw) || LandingYaw > 15 ||
                !CardZoneRowLayout.Finite(FlightSeconds + SettleSeconds + ReflowSeconds))
                throw new ArgumentException("飞入/落稳/让位时间须至少 0.01 秒，间隔/抬高/惯性距离须非负，惯性摆角须为 0–15 度，所有参数须有限。");
        }
        private static bool Positive(float value) => CardZoneRowLayout.Finite(value) && value >= 0.01f;
        private static bool Nonnegative(float value) => CardZoneRowLayout.Finite(value) && value >= 0;
    }
}
