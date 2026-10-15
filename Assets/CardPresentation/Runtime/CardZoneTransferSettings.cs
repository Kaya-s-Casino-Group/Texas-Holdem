using System;
using UnityEngine;

namespace CardPresentation
{
    [Serializable]
    public struct CardZoneTransferSettings
    {
        public bool Enabled;
        public float FlightSeconds;
        public float Interval;
        [Tooltip("最后一张交叉落位后，整批开始整理前的停顿。")]
        public float LandingPause;
        [Tooltip("整理回真实列表顺序的时长。0 使用默认 0.3 秒，兼容旧场景。")]
        public float ReorderSeconds;
        [Tooltip("默认曲线中点沿目标牌区 +Y 抬高的距离；目标启用自动尺寸时随卡牌和牌区缩放，否则保持世界距离。指定控制点后忽略。")]
        public float ArcHeight;
        public static CardZoneTransferSettings Default => new CardZoneTransferSettings {
            Enabled = true, FlightSeconds = .4f, Interval = .06f, ArcHeight = .02f,
            LandingPause = .08f, ReorderSeconds = .3f
        };
        public float ResolvedReorderSeconds => ReorderSeconds == 0 ? .3f : ReorderSeconds;
        public void Validate()
        {
            if (!CardZoneRowLayout.Finite(FlightSeconds) || FlightSeconds <= 0 ||
                !CardZoneRowLayout.Finite(Interval) || Interval < 0 ||
                !CardZoneRowLayout.Finite(LandingPause) || LandingPause < 0 ||
                !CardZoneRowLayout.Finite(ReorderSeconds) || ReorderSeconds < 0 ||
                !CardZoneRowLayout.Finite(ArcHeight) || ArcHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(CardZoneTransferSettings), "出牌时间必须大于零，间隔和弧高必须为有限非负数。");
        }
    }
}
