using System;
using UnityEngine;

namespace CardPresentation
{
    public partial class HandZoneView
    {
        [Header("设备震动反馈")]
        [SerializeField] private CardHapticSettings hapticSettings = CardHapticSettings.Default;
        public CardHapticSettings HapticSettings => hapticSettings;
        /// <summary>One request after each actual PlayOneShot call. Empty or muted audio never requests haptics.</summary>
        public event Action HapticRequested;
        public void SetHapticSettings(CardHapticSettings settings)
        {
            settings.Validate(); hapticSettings = settings;
            if (!settings.Enabled || settings.GamepadStrength == 0) CardHapticsDriver.StopFor(this);
        }
        private void RequestHapticFeedback()
        {
            if (!isActiveAndEnabled || !hapticSettings.Enabled) return;
            CardHapticsDriver.Request(this, hapticSettings);
            HapticRequested?.Invoke();
        }
    }
}
