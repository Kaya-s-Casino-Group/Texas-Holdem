using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CardInteraction;
using CardPresentation.Editor;
using DG.Tweening;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    [TestFixture]
    public sealed class PlayedZoneViewTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene = EditorSceneManager.NewPreviewScene();
            public readonly PlayedZoneView Zone;
            public Fixture()
            {
                var root = new GameObject("Played Zone Test") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, Scene); Zone = root.AddComponent<PlayedZoneView>();
                Zone.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
            }
            public void Dispose() => EditorSceneManager.ClosePreviewScene(Scene);
        }
        private static CardZoneViewData Data(int count) => Data(Enumerable.Range(0, count)
            .Select(i => new CardVisualData("card-" + i, "hearts_q")).ToArray());
        private static CardZoneViewData Data(params CardVisualData[] cards) => new CardZoneViewData(new ZoneId("played/test"), cards);
        private static void Near(Vector3 a, Vector3 b) => Assert.Less(Vector3.Distance(a, b), .00001f);
        private static CardZoneCardState[] Cards(int count) => Enumerable.Range(0, count).Select(i =>
            new CardZoneCardState(new CardInstance(new CardInstanceId("card-" + i), StandardDeck.CardId(Suit.Hearts, Rank.Queen)), "hearts_q", true, Color.white, default(CardInteractionState))).ToArray();
        private static CardZone ZoneFor(CardZoneCardState[] cards) => new CardZone(new ZoneId("played/test"), CardZoneKinds.Played, null, cards.Select(c => c.Card.Id));

        [Test] public void FiveSlotsRemainFixedForZeroThroughFiveCardsAndReuseAfterReorder()
        {
            using (var f = new Fixture())
            {
                var slots = f.Zone.GetSlotPoses(); Assert.AreEqual(5, slots.Count); Assert.AreEqual(0, f.Zone.Views.Count);
                for (int count = 0; count <= 5; count++)
                {
                    f.Zone.Present(Data(count)); Assert.AreEqual(count, f.Zone.Views.Count);
                    Assert.AreEqual(count, f.Zone.transform.childCount);
                    for (int i = 0; i < count; i++)
                    {
                        Near(slots[i].Position, f.Zone.Views[i].transform.localPosition);
                        Assert.AreEqual(Quaternion.identity, f.Zone.Views[i].transform.localRotation);
                        Assert.AreEqual(0, f.Zone.Views[i].transform.localPosition.z);
                        Assert.AreEqual(slots[0].Position.y, f.Zone.Views[i].transform.localPosition.y);
                    }
                    CollectionAssert.AreEqual(slots.Select(p => p.Position), f.Zone.GetSlotPoses().Select(p => p.Position));
                }
                var original = f.Zone.Views.ToDictionary(v => v.ViewId);
                f.Zone.Present(Data(f.Zone.Data.Cards.Reverse().ToArray()));
                for (int i = 0; i < 5; i++) { Assert.AreSame(original[f.Zone.Views[i].ViewId], f.Zone.Views[i]); Near(slots[i].Position, f.Zone.Views[i].transform.localPosition); }
                f.Zone.Present(Data(0)); Assert.AreEqual(0, f.Zone.Views.Count); Assert.AreEqual(5, f.Zone.GetSlotPoses().Count);
            }
        }

        [Test] public void BaseSettersCannotEnableArcOrSwayAndCardAttributesRemainVisible()
        {
            using (var f = new Fixture())
            {
                CardZoneView common = f.Zone;
                var layout = CardZoneLayoutSettings.Default; layout.ArcHeight = .1f;
                var motion = CardZoneMotionSettings.Default; motion.Enabled = true; motion.MaxSwayAngle = 10;
                common.SetLayout(layout, motion); common.SetMotionSettings(motion);
                Assert.AreEqual(0, common.Layout.ArcHeight); Assert.IsFalse(common.MotionSettings.Enabled);
                var shown = new CardVisualData("visible", "hearts_q", effects: CardEffects.Enhanced | CardEffects.Laminated,
                    isDisabled: true, interaction: new CardInteractionState(true, true));
                var hidden = new CardVisualData("hidden"); common.Present(Data(shown, hidden));
                var view = common.Views[0]; var data = common.Data; var pose = view.DisplayPose;
                common.AdvanceMotion(3); common.RefreshLayout(); common.Present(data);
                Assert.AreSame(data, common.Data); Assert.AreSame(view, common.Views[0]);
                Assert.IsTrue(view.Surface.SealEnabled); Assert.IsTrue(view.Surface.FoilEnabled); Assert.IsTrue(view.Surface.RedCrossEnabled);
                Assert.IsFalse(common.Views[1].Data.FaceUp); Assert.IsTrue(view.Data.Interaction.IsSelected);
                Assert.AreEqual(Vector3.zero, view.InteractionOffset); Assert.AreEqual(Quaternion.identity, view.SwayRoot.localRotation);
                Assert.IsFalse(DOTween.IsTweening(view.SwayRoot)); Assert.IsFalse(common.IsTransitioning);
                Near(pose.Position, view.DisplayPose.Position);
                var serialized = new SerializedObject(f.Zone);
                serialized.FindProperty("layout.ArcHeight").floatValue = .08f;
                serialized.FindProperty("motionSettings.Enabled").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                common.Present(data); common.AdvanceMotion(1);
                Assert.AreEqual(0, common.Layout.ArcHeight); Assert.IsFalse(common.MotionSettings.Enabled);
                Assert.AreEqual(Quaternion.identity, view.SwayRoot.localRotation);
            }
        }

        [Test] public void OverCapacityAndInsufficientWidthRejectBeforeChangingDataOrViews()
        {
            using (var f = new Fixture())
            {
                var data = Data(5); f.Zone.Present(data); var views = f.Zone.Views.ToArray(); var layout = f.Zone.Layout;
                CardZoneView common = f.Zone;
                Assert.Throws<ArgumentOutOfRangeException>(() => common.Present(Data(6)));
                Assert.AreSame(data, common.Data); CollectionAssert.AreEqual(views, common.Views);
                var narrow = layout; narrow.Size.x = .1f;
                Assert.Throws<ArgumentException>(() => common.SetLayout(narrow));
                Assert.AreEqual(layout.Size, common.Layout.Size); CollectionAssert.AreEqual(views, common.Views);
                var tight = layout; tight.Size.x = .34f; common.SetLayout(tight);
                float cardWidth = views[0].Surface.GetComponent<MeshFilter>().sharedMesh.bounds.size.x;
                for (int i = 1; i < common.Views.Count; i++)
                    Assert.GreaterOrEqual(common.Views[i].transform.localPosition.x - common.Views[i - 1].transform.localPosition.x, cardWidth - .00001f);
            }
        }

        [Test] public void HostCapacityFailureKeepsStartupSnapshotAndLiveCardsWithoutBindingMouse()
        {
            using (var f = new Fixture())
            {
                var host = f.Zone.gameObject.AddComponent<CardZonePlayHost>(); var five = Cards(5); var six = Cards(6);
                host.Capture(ZoneFor(five), five); host.BeginSession(); var data = f.Zone.Data;
                Assert.AreEqual(CardZoneKinds.Played, host.Zone.Kind); Assert.IsNull(host.MouseInput);
                Assert.Throws<ArgumentOutOfRangeException>(() => host.ReplaceCards(six));
                Assert.Throws<ArgumentOutOfRangeException>(() => host.Capture(ZoneFor(six), six));
                Assert.IsTrue(host.IsRunning); Assert.AreEqual(5, host.Cards.Count); Assert.AreEqual(5, host.InitialCards.Count);
                Assert.AreSame(data, f.Zone.Data);
                // Legacy host fields no longer own startup cards or reset the running match.
                var serialized = new SerializedObject(host); serialized.FindProperty("initialCards").arraySize = 6;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                host.BeginSession();
                Assert.IsTrue(host.IsRunning); Assert.AreEqual(5, host.Cards.Count);
                Assert.AreEqual(5, host.Match.GetZone(host.Zone.Id).Cards.Count);
            }
        }

        [Test] public void ValidatorUsesPlayedCapacityAndKeepsExistingHandContextSeparate()
        {
            using (var f = new Fixture())
            {
                var handRoot = new GameObject("Separate Hand"); SceneManager.MoveGameObjectToScene(handRoot, f.Scene);
                var hand = handRoot.AddComponent<HandZoneView>(); hand.Configure(f.Zone.CardPrefab, CardZoneLayoutSettings.Default);
                hand.Present(Data(8), false); var original = hand.Data;
                var window = ScriptableObject.CreateInstance<CardZoneValidationWindow>();
                try
                {
                    window.SelectTarget(f.Zone);
                    var type = typeof(CardZoneValidationWindow); const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    Assert.AreEqual(5, type.GetProperty("CurrentCapacity", flags).GetValue(window));
                    type.GetMethod("Apply", flags).Invoke(window, new object[] { false });
                    Assert.That(window.CurrentCards, Is.Empty);
                    Assert.That(f.Zone.GetComponent<CardZonePlayHost>(), Is.Null);
                    Assert.AreSame(f.Zone, CardZoneValidationWindow.ResolveTarget(f.Zone.gameObject));
                    Assert.AreSame(original, hand.Data); Assert.AreEqual(8, hand.Views.Count);
                    Assert.IsFalse(f.Zone.MotionSettings.Enabled); Assert.AreEqual(0, f.Zone.Layout.ArcHeight);
                }
                finally { window.SelectTarget(null); Object.DestroyImmediate(window); }
            }
        }

        [Test] public void ExportedPlayedPrefabPreservesCapacityStaticLayoutAndFiveEmptySlots()
        {
            using (var f = new Fixture())
            {
                string folder = "Assets/CardPresentation/Tests/PlayedExport_" + Guid.NewGuid().ToString("N");
                AssetDatabase.CreateFolder("Assets/CardPresentation/Tests", Path.GetFileName(folder));
                try
                {
                    f.Zone.Present(Data(5));
                    var prefab = CardZoneViewPrefabExporter.Export(f.Zone, folder + "/Played.prefab");
                    var view = prefab.GetComponent<PlayedZoneView>(); Assert.NotNull(view); Assert.AreEqual(5, view.CardCapacity);
                    Assert.AreEqual(0, view.Layout.ArcHeight); Assert.IsFalse(view.MotionSettings.Enabled); Assert.AreEqual(0, prefab.transform.childCount);
                    var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, f.Scene);
                    view = root.GetComponent<PlayedZoneView>(); Assert.IsNull(view.Data); Assert.AreEqual(5, view.GetSlotPoses().Count);
                    view.Present(Data(5)); view.AdvanceMotion(3);
                    Assert.IsFalse(view.IsTransitioning); Assert.AreEqual(5, view.Views.Count);
                    foreach (var card in view.Views) Assert.AreEqual(Quaternion.identity, card.SwayRoot.localRotation);
                    Assert.Throws<ArgumentOutOfRangeException>(() => view.Present(Data(6)));
                }
                finally { AssetDatabase.DeleteAsset(folder); }
            }
        }
    }
}
