using NUnit.Framework;
using UIFoundation;
using UnityEngine;

namespace CardPresentation.Tests
{
    public sealed class DesktopDisplaySettingsTests
    {
        private bool hadPreference;
        private int previous;

        [SetUp]
        public void RememberPreference()
        {
            hadPreference = PlayerPrefs.HasKey(PokerDisplaySettings.PreferenceKey);
            previous = PlayerPrefs.GetInt(PokerDisplaySettings.PreferenceKey);
            PlayerPrefs.DeleteKey(PokerDisplaySettings.PreferenceKey);
        }

        [TearDown]
        public void RestorePreference()
        {
            if (hadPreference) PlayerPrefs.SetInt(PokerDisplaySettings.PreferenceKey, previous);
            else PlayerPrefs.DeleteKey(PokerDisplaySettings.PreferenceKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void FreshOrInvalidPreferenceFallsBackToUltrawide()
        {
            Assert.AreEqual(new Vector2Int(2340, 1080), PokerDisplaySettings.GetResolution(PokerDisplaySettings.SelectedPreset));
            PlayerPrefs.SetInt(PokerDisplaySettings.PreferenceKey, int.MaxValue);
            Assert.AreEqual(new Vector2Int(2340, 1080), PokerDisplaySettings.GetResolution(PokerDisplaySettings.SelectedPreset));
        }

        [Test]
        public void ApplyingResolutionPersistsSelectionAndRejectsInvalidChanges()
        {
            PokerDisplaySettings.SelectPreset(1);
            Assert.AreEqual(1, PlayerPrefs.GetInt(PokerDisplaySettings.PreferenceKey));
            Assert.AreEqual(new Vector2Int(1920, 1080), PokerDisplaySettings.GetResolution(PokerDisplaySettings.SelectedPreset));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => PokerDisplaySettings.SelectPreset(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => PokerDisplaySettings.SelectPreset(PokerDisplaySettings.PresetCount));
            Assert.AreEqual(1, PokerDisplaySettings.SelectedPreset);
        }
    }
}
