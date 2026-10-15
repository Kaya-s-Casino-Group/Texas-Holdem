using System;
using CardPresentation.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    public class CardHapticsTests
    {
        [Test] public void EverySoundStartsAPulseAndLatestDeadlineStopsItExactlyOnce()
        {
            var pulse = new CardHapticPulse(); var owner = new object();
            int starts = 0, stops = 0;
            pulse.Start(owner, 10, .05f, () => starts++, () => stops++);
            pulse.Tick(10.02); Assert.AreEqual(0, stops);
            pulse.Start(owner, 10.02, .05f, () => starts++, () => stops++);
            Assert.AreEqual(2, starts); Assert.AreEqual(1, stops);
            pulse.Tick(10.06); Assert.AreEqual(1, stops);
            pulse.Tick(10.08); Assert.AreEqual(2, stops);
            pulse.Tick(20); pulse.Stop(); Assert.AreEqual(2, stops);
        }

        [Test] public void SameFrameSoundsAreNotMergedAndStopOwnerDoesNotStopAnotherZone()
        {
            var pulse = new CardHapticPulse(); var first = new object(); var second = new object();
            int starts = 0, stops = 0;
            pulse.Start(first, 0, .05f, () => starts++, () => stops++);
            pulse.Start(second, 0, .05f, () => starts++, () => stops++);
            Assert.AreEqual(2, starts); Assert.AreEqual(1, stops);
            pulse.StopOwner(first); Assert.AreEqual(1, stops);
            pulse.StopOwner(second); Assert.AreEqual(2, stops);
            pulse.StopOwner(second); Assert.AreEqual(2, stops);
        }

        [Test] public void DeviceFailureStillStopsPulseAndAllowsTheNextSound()
        {
            var pulse = new CardHapticPulse(); int stops = 0, starts = 0;
            Assert.Throws<InvalidOperationException>(() => pulse.Start(this, 0, .05f,
                () => throw new InvalidOperationException(), () => stops++));
            Assert.AreEqual(1, stops);
            pulse.Start(this, 0, .05f, () => starts++, () => stops++);
            pulse.Stop(); Assert.AreEqual(1, starts); Assert.AreEqual(2, stops);
        }

        public class RecordingGamepad : Gamepad
        {
            public float Low, High;
            public int Pulses;
            public override void SetMotorSpeeds(float lowFrequency, float highFrequency)
            { Low = lowFrequency; High = highFrequency; if (lowFrequency > 0 || highFrequency > 0) Pulses++; }
        }

        [Test] public void InputSystemGamepadReceivesPulseAndStopsOnFocusPauseDisableAndDeadline()
        {
            var gamepad = InputSystem.AddDevice<RecordingGamepad>(); gamepad.MakeCurrent();
            var root = new GameObject("Haptics Driver Test");
            try
            {
                var driver = root.AddComponent<CardHapticsDriver>(); var hand = root.AddComponent<HandZoneView>();
                var play = typeof(CardHapticsDriver).GetMethod("Play", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Action start = () => play.Invoke(driver, new object[] { hand, CardHapticSettings.Default });
                Action<string, object[]> lifecycle = (method, args) => typeof(CardHapticsDriver)
                    .GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(driver, args);
                start(); Assert.AreEqual(.25f, gamepad.Low); Assert.AreEqual(.25f, gamepad.High);
                lifecycle("OnApplicationFocus", new object[] { false }); Assert.AreEqual(0, gamepad.Low);
                lifecycle("OnApplicationFocus", new object[] { true });
                start(); lifecycle("OnApplicationPause", new object[] { true }); Assert.AreEqual(0, gamepad.Low);
                lifecycle("OnApplicationPause", new object[] { false });
                start(); lifecycle("OnDisable", null); Assert.AreEqual(0, gamepad.Low);
                start();
                var pulse = (CardHapticPulse)typeof(CardHapticsDriver).GetField("pulse", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(driver);
                pulse.Tick(Time.realtimeSinceStartupAsDouble + 1); Assert.AreEqual(0, gamepad.Low);
                Assert.AreEqual(4, gamepad.Pulses);
            }
            finally { Object.DestroyImmediate(root); InputSystem.RemoveDevice(gamepad); }
        }

        [Test] public void HapticSettingsRoundTripThroughWindowAndRejectInvalidInput()
        {
            var root = new GameObject("Haptic Settings Test"); CardZoneValidationWindow window = null;
            try
            {
                var hand = root.AddComponent<HandZoneView>();
                hand.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                Assert.IsTrue(hand.HapticSettings.Enabled); Assert.AreEqual(.045f, hand.HapticSettings.GamepadSeconds);
                var tuning = hand.HapticSettings; tuning.GamepadSeconds = .07f; tuning.GamepadStrength = .4f;
                hand.SetHapticSettings(tuning);
                window = ScriptableObject.CreateInstance<CardZoneValidationWindow>(); window.SelectTarget(hand);
                var serialized = new SerializedObject(window); var property = serialized.FindProperty("hapticSettings");
                Assert.AreEqual(.07f, property.FindPropertyRelative("GamepadSeconds").floatValue);
                property.FindPropertyRelative("Enabled").boolValue = false;
                property.FindPropertyRelative("GamepadStrength").floatValue = .6f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                typeof(CardZoneValidationWindow).GetMethod("Apply", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(window, new object[] { false });
                Assert.IsFalse(hand.HapticSettings.Enabled); Assert.AreEqual(.6f, hand.HapticSettings.GamepadStrength);
                var json = JsonUtility.ToJson(hand); hand.SetHapticSettings(CardHapticSettings.Default);
                JsonUtility.FromJsonOverwrite(json, hand);
                Assert.IsFalse(hand.HapticSettings.Enabled); Assert.AreEqual(.07f, hand.HapticSettings.GamepadSeconds);
                foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, -.1f, 1.1f })
                {
                    tuning.GamepadStrength = invalid;
                    Assert.Throws<ArgumentException>(() => hand.SetHapticSettings(tuning));
                }
                tuning.GamepadStrength = .3f;
                foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, 0, .3f })
                {
                    tuning.GamepadSeconds = invalid;
                    Assert.Throws<ArgumentException>(() => hand.SetHapticSettings(tuning));
                }
                Assert.IsFalse(hand.HapticSettings.Enabled); Assert.AreEqual(.07f, hand.HapticSettings.GamepadSeconds);
            }
            finally { if (window != null) Object.DestroyImmediate(window); Object.DestroyImmediate(root); }
        }
    }
}
