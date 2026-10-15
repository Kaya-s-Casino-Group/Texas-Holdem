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
        [Min(0.01f), InspectorName("理牌 / 让位时间（秒）")]
        [Tooltip("点数/花色理牌及卡牌增减后的让位总时长，包含惯性过冲和回稳；不改变补牌飞入和回稳时间。")]
        public float ReflowSeconds;
        [Range(0, 3), InspectorName("理牌 / 让位惯性强度")]
        [Tooltip("OutBack 过冲强度；0 使用原来的 OutCubic 减速。强度按移动距离生效，随牌区尺寸等比变化。")]
        public float ReflowOvershoot;
        [Min(0)] public float FlightLift;
        [Min(0)] public float OvershootDistance;
        [Range(0, 15)] public float LandingYaw;

        public static CardZoneTransitionSettings Default => new CardZoneTransitionSettings {
            Enabled = true, FlightSeconds = 0.45f, DealInterval = 0.12f, SettleSeconds = 0.65f,
            ReflowSeconds = 0.25f, ReflowOvershoot = 1.25f, FlightLift = 0.025f, OvershootDistance = 0.006f, LandingYaw = 3 };

        public void Validate()
        {
            if (!Positive(FlightSeconds) || !Positive(SettleSeconds) || !Positive(ReflowSeconds) ||
                !Nonnegative(DealInterval) || !Nonnegative(FlightLift) || !Nonnegative(OvershootDistance) ||
                !Nonnegative(LandingYaw) || LandingYaw > 15 || !Nonnegative(ReflowOvershoot) || ReflowOvershoot > 3 ||
                !CardZoneRowLayout.Finite(FlightSeconds + SettleSeconds + ReflowSeconds))
                throw new ArgumentException("飞入/落稳/让位时间须至少 0.01 秒，间隔/抬高/惯性距离须非负，惯性摆角须为 0–15 度，理牌惯性强度须为 0–3，所有参数须有限。");
        }
        private static bool Positive(float value) => CardZoneRowLayout.Finite(value) && value >= 0.01f;
        private static bool Nonnegative(float value) => CardZoneRowLayout.Finite(value) && value >= 0;
    }
}
