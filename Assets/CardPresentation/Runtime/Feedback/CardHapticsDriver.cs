using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CardPresentation
{
    [AddComponentMenu("")]
    public sealed class CardHapticsDriver : MonoBehaviour
    {
        private static CardHapticsDriver instance;
        private readonly CardHapticPulse pulse = new CardHapticPulse();
        private bool focused = true, paused;
        private bool warned;

        public static void Request(UnityEngine.Object owner, CardHapticSettings settings)
        {
            if (!Application.isPlaying || !Application.isFocused || !settings.Enabled) return;
            if (instance == null)
            {
                var root = new GameObject("Card Device Haptics") { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave };
                instance = root.AddComponent<CardHapticsDriver>();
                DontDestroyOnLoad(root);
            }
            if (!instance.focused || instance.paused) return;
            instance.Play(owner, settings);
        }
        public static void StopFor(UnityEngine.Object owner) { if (instance != null) instance.pulse.StopOwner(owner); }
        private void Play(UnityEngine.Object owner, CardHapticSettings settings)
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                var gamepad = Gamepad.current;
                if (gamepad != null && gamepad.added)
                {
                    if (settings.GamepadStrength <= 0) return;
                    pulse.Start(owner, Time.realtimeSinceStartupAsDouble, settings.GamepadSeconds,
                        () => gamepad.SetMotorSpeeds(settings.GamepadStrength, settings.GamepadStrength),
                        () => { if (gamepad.added) gamepad.SetMotorSpeeds(0, 0); });
                    return;
                }
#endif
                pulse.Start(owner, Time.realtimeSinceStartupAsDouble, .045f, CardMobileHaptics.Play, () => { });
            }
            catch (Exception error)
            {
                // Unsupported devices must never prevent a card action or audio cue from committing.
                if (!warned) { warned = true; Debug.LogWarning("卡牌设备震动不可用：" + error.Message, this); }
            }
        }
        private void Update() { pulse.Tick(Time.realtimeSinceStartupAsDouble); }
        private void OnApplicationFocus(bool value) { focused = value; if (!value) pulse.Stop(); }
        private void OnApplicationPause(bool value) { paused = value; if (value) pulse.Stop(); }
        private void OnApplicationQuit() { pulse.Stop(); }
        private void OnDisable() { pulse.Stop(); }
        private void OnDestroy() { pulse.Stop(); if (instance == this) instance = null; }
    }
}
