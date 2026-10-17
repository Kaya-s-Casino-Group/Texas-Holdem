using System;
using UnityEngine;

namespace CardPresentation
{
    [Serializable]
    public struct CardHapticSettings
    {
        [InspectorName("启用设备震动")] public bool Enabled;
        [Range(.01f, .2f), InspectorName("手柄时长（秒）")] public float GamepadSeconds;
        [Range(0, 1), InspectorName("手柄强度")] public float GamepadStrength;
        public static CardHapticSettings Default => new CardHapticSettings {
            Enabled = true, GamepadSeconds = .045f, GamepadStrength = .25f };
        public void Validate()
        {
            if (!CardZoneRowLayout.Finite(GamepadSeconds) || GamepadSeconds < .01f || GamepadSeconds > .2f ||
                !CardZoneRowLayout.Finite(GamepadStrength) || GamepadStrength < 0 || GamepadStrength > 1)
                throw new ArgumentException("手柄震动时长须为 0.01–0.2 秒，强度须为 0–1，所有参数须有限。");
        }
    }
}
