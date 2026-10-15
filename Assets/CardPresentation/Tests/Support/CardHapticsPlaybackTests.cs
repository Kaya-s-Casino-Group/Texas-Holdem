#if UNITY_EDITOR
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    // Run in PlayMode: this verifies the same gate as actual AudioSource.PlayOneShot calls.
    public class CardHapticsPlaybackTests
    {
        private GameObject root;
        private HandZoneView hand;
        private AudioClip clip;
        private int requests;
        private static CardVisualData Card(string id, bool selected = false) =>
            new CardVisualData(id, "hearts_q", interaction: new CardInteractionState(isSelected: selected));
        private static CardZoneViewData Data(params CardVisualData[] cards) => new CardZoneViewData(new ZoneId("haptics"), cards);
        [SetUp] public void Setup()
        {
            if (!Application.isPlaying) Assert.Ignore("Requires PlayMode to verify actual audio playback.");
            root = new GameObject("Haptic Playback Test");
            hand = root.AddComponent<HandZoneView>();
            hand.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
            var motion = hand.MotionSettings; motion.Enabled = false; hand.SetMotionSettings(motion);
            // Zero controller intensity prevents physical rumble during automated tests; logical requests are still observed.
            var settings = hand.HapticSettings; settings.GamepadStrength = 0; hand.SetHapticSettings(settings);
            clip = AudioClip.Create("Haptic Cue Test", 256, 1, 48000, false);
            hand.SetDrawSound(clip); hand.SetDiscardSound(clip); hand.SetSelectionSound(clip);
            hand.SetDeselectionSound(clip); hand.SetSelectionDeniedSound(clip); hand.SetReflowSound(clip);
            requests = 0; hand.HapticRequested += () => requests++;
        }
        [TearDown] public void Teardown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (clip != null) Object.DestroyImmediate(clip);
            // Persist diagnostics across the PlayMode domain reload that disconnects editor tool clients.
            System.IO.Directory.CreateDirectory("Library/CardHapticsTests");
            var context = TestContext.CurrentContext;
            System.IO.File.WriteAllText("Library/CardHapticsTests/" + context.Test.Name + ".txt",
                System.DateTime.UtcNow.ToString("O") + "\n" + context.Result.Outcome + "\n" + context.Result.Message);
        }
        [Test] public void EveryCardAudioOperationRequestsHapticsOncePerSound()
        {
            hand.Present(Data(Card("a"), Card("b")), false); Assert.AreEqual(0, requests);
            hand.Present(Data(Card("a", true), Card("b"))); Assert.AreEqual(1, requests);
            hand.Present(Data(Card("a", true), Card("b"))); Assert.AreEqual(1, requests);
            hand.Present(Data(Card("a"), Card("b"))); Assert.AreEqual(2, requests);
            hand.PlaySelectionDenied("a"); Assert.AreEqual(3, requests);
            hand.Present(Data(Card("b"), Card("a"))); Assert.AreEqual(4, requests);
            hand.AdvanceMotion(1); Assert.AreEqual(4, requests);
            // Discard and survivor reflow are two distinct audio calls, so they request two pulses.
            hand.Present(Data(Card("a")), new[] { "b" }); Assert.AreEqual(6, requests);
            hand.AdvanceMotion(2);
            hand.Present(Data(), false);
            hand.Present(Data(Card("c"))); Assert.AreEqual(6, requests);
            hand.AdvanceMotion(2); Assert.AreEqual(7, requests);
            Assert.IsNotEmpty(root.GetComponentsInChildren<AudioSource>());
        }
        [Test] public void SilentDisabledAndEmptyAudioNeverRequestsHaptics()
        {
            hand.Present(Data(Card("a")), false);
            hand.SetSelectionSound(clip, 0);
            hand.Present(Data(Card("a", true))); Assert.AreEqual(0, requests);
            hand.SetDeselectionSound(null);
            hand.Present(Data(Card("a"))); Assert.AreEqual(0, requests);
            hand.SetSelectionSound(clip, 1, false);
            hand.Present(Data(Card("a", true))); Assert.AreEqual(0, requests);
            hand.SetDeselectionSound(clip);
            hand.Present(Data(Card("a"))); Assert.AreEqual(1, requests);
            var settings = hand.HapticSettings; settings.Enabled = false; hand.SetHapticSettings(settings);
            hand.SetSelectionSound(clip);
            hand.Present(Data(Card("a", true))); Assert.AreEqual(1, requests);
            Assert.IsNotEmpty(root.GetComponentsInChildren<AudioSource>(), "Disabling haptics must not disable audio.");
        }
    }
}
#endif
