using System;
using System.Linq;
using System.Reflection;
using CardInteraction;
using CardPresentation.Editor;
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
    public sealed class CardZoneInheritanceTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly Scene scene = EditorSceneManager.NewPreviewScene();
            public T Add<T>() where T : CardZoneView
            {
                var root = new GameObject(typeof(T).Name) { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, scene);
                var view = root.AddComponent<T>();
                view.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                return view;
            }
            public void Dispose() => EditorSceneManager.ClosePreviewScene(scene);
        }
        private static CardZoneViewData Data(params CardVisualData[] cards) => new CardZoneViewData(new ZoneId("test/zone"), cards);
        private static T Field<T>(CardZoneValidationWindow window, string name) =>
            (T)typeof(CardZoneValidationWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        private static void Apply(CardZoneValidationWindow window) =>
            typeof(CardZoneValidationWindow).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { false, null });
        private static CardZonePlayHost Capture(CardZoneView view, string id, string art, bool selected = false)
        {
            var model = new CardZonePreviewModel(); model.Add(art); model.Cards[0].IsSelected = selected;
            var host = view.gameObject.AddComponent<CardZonePlayHost>();
            host.Capture(model.BuildZone(new ZoneId(id), view.DefaultZoneKind, new ParticipantId("owner")),
                model.Cards.Select(card => new CardZoneCardState(card.ToCard(), card.ArtId, card.FaceUp, card.Tint, card.Interaction)));
            return host;
        }

        [Test] public void CommonZoneSyncsListAndAppearanceWithoutHandFlightsOrLift()
        {
            using (var f = new Fixture())
            {
                var zone = f.Add<CardZoneView>();
                zone.Present(Data(new CardVisualData("a", "hearts_q"), new CardVisualData("b")));
                var a = zone.Views[0]; var b = zone.Views[1];
                zone.Present(Data(new CardVisualData("b"), new CardVisualData("a", "spades_a", tint: Color.red,
                    interaction: new CardInteractionState(isSelected: true))));
                Assert.AreSame(a, zone.Views[1]); Assert.AreSame(b, zone.Views[0]);
                Assert.AreEqual("spades_a", a.Surface.CardId); Assert.AreEqual(Color.red, a.Surface.Tint);
                Assert.AreEqual(Vector3.zero, a.InteractionOffset); Assert.IsFalse(zone.IsTransitioning);
                Assert.IsTrue(a.Data.Interaction.IsSelected);
                Assert.IsNull(zone.GetComponent<CardZoneMouseInput>());
                var before = zone.Data;
                Assert.Throws<ArgumentException>(() => zone.Present(Data(new CardVisualData("bad", "no-such-art"))));
                Assert.AreSame(before, zone.Data); Assert.AreSame(a, zone.Views[1]);
                zone.Present(Data(new CardVisualData("a", "spades_a"))); Assert.IsTrue(b == null);
            }
        }

        [Test] public void BaseReferenceDispatchesHandEffectsAndCleanupToFurtherSubclass()
        {
            using (var f = new Fixture())
            {
                var hand = f.Add<SpecializedHandZoneViewForTests>(); CardZoneView common = hand;
                int deals = 0; hand.CardDealStarted += _ => deals++;
                var zone = new CardZone(new ZoneId("hand"), CardZoneKinds.Hand, null, new[] { new CardInstanceId("a") });
                common.Present(zone, id => new CardVisualData(id.Value, "spades_a"));
                Assert.IsTrue(common.IsTransitioning); Assert.AreEqual(1, deals);
                common.AdvanceMotion(3); Assert.IsFalse(common.IsTransitioning);
                hand.Present(new CardZoneViewData(zone.Id, new CardVisualData[0]), discardedViewIds: new[] { "a" });
                Assert.AreEqual(1, hand.DepartingViews.Count);
                var ghost = hand.DepartingViews[0]; common.Clear();
                Assert.IsTrue(ghost == null); Assert.AreEqual(0, hand.DepartingViews.Count); Assert.IsNull(common.Data);
            }
        }

        [Test] public void PlayHostOnlyAutomaticallyBindsHandMouseAndRejectsGenericDiscard()
        {
            using (var f = new Fixture())
            {
                var common = f.Add<CardZoneView>(); var genericHost = Capture(common, "common", "hearts_q", true);
                genericHost.BeginSession(); Assert.IsTrue(genericHost.IsRunning);
                Assert.IsNull(genericHost.MouseInput); Assert.IsNull(common.GetComponent<CardZoneMouseInput>());
                Assert.Throws<InvalidOperationException>(() => genericHost.DiscardSelected());
                Assert.AreEqual(1, genericHost.Cards.Count);
                var hand = f.Add<SpecializedHandZoneViewForTests>(); var host = Capture(hand, "hand", "spades_a", true);
                Assert.AreEqual(1, hand.GetComponents<CardZoneView>().Length);
                host.BeginSession(); Assert.NotNull(host.MouseInput); Assert.NotNull(host.MouseInput.Controller);
                Assert.AreEqual(1, host.DiscardSelected()); Assert.AreEqual(1, host.DiscardZone.Cards.Count);
                Assert.AreEqual(1, hand.GetComponents<CardZoneView>().Length); // No discard renderer is required.
                host.StopSession(); Assert.IsNull(host.MouseInput.Controller);
            }
        }

        [Test] public void EditorResolvesInactiveNestedSubclassesByTypeRatherThanObjectName()
        {
            using (var f = new Fixture())
            {
                var hand = f.Add<SpecializedHandZoneViewForTests>(); hand.name = "任意名称";
                var card = new GameObject("Card"); card.transform.SetParent(hand.transform, false); hand.gameObject.SetActive(false);
                Assert.AreSame(hand, CardZoneValidationWindow.ResolveTarget(card));
                Assert.AreSame(hand, CardZoneValidationWindow.ResolveTarget(hand.gameObject));
                Assert.IsNull(CardZoneValidationWindow.ResolveTarget(null));
                Assert.IsNull(CardZoneValidationWindow.ResolveTarget(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab")));
            }
        }

        [Test] public void EditorSwitchLoadsEachZonesCardsSettingsAndMetadataWithoutCrossContamination()
        {
            using (var f = new Fixture())
            {
                var common = f.Add<CardZoneView>(); var hand = f.Add<SpecializedHandZoneViewForTests>();
                var a = Capture(common, "left", "hearts_q"); var b = Capture(hand, "right", "spades_a");
                hand.SetInteractionOffsets(new Vector3(0, 0, .012f), new Vector3(0, 0, .027f));
                var window = ScriptableObject.CreateInstance<CardZoneValidationWindow>();
                try
                {
                    window.SelectTarget(common);
                    Assert.AreEqual(a.InitialCards[0].Id, Field<CardZonePreviewModel>(window, "model").Cards[0].Id);
                    window.SelectTarget(hand);
                    Assert.AreEqual(b.InitialCards[0].Id, Field<CardZonePreviewModel>(window, "model").Cards[0].Id);
                    Assert.AreEqual(hand.SelectedOffset, Field<Vector3>(window, "raisedOffset"));
                    Assert.AreEqual("hearts_q", a.InitialCards[0].ArtId);
                    Assert.AreEqual("spades_a", b.InitialCards[0].ArtId);
                    window.SelectTarget(common); Apply(window);
                    Assert.AreEqual("left", a.Zone.Id.Value); Assert.AreEqual("zone", a.Zone.Kind);
                    Assert.AreEqual("owner", a.Zone.ControllerId.Value.Value);
                    Assert.AreEqual("hand", b.Zone.Kind); Assert.AreEqual("right", b.Zone.Id.Value);
                    Assert.AreEqual(1, a.InitialCards.Count); Assert.AreEqual(1, b.InitialCards.Count);
                }
                finally { window.SelectTarget(null); Object.DestroyImmediate(window); }
            }
        }

        [Test] public void EditorDoesNotReconstructOrOverwriteAnExternalViewsHiddenCardData()
        {
            using (var f = new Fixture())
            {
                var zone = f.Add<CardZoneView>(); var data = Data(new CardVisualData("opaque-slot")); zone.Present(data);
                var window = ScriptableObject.CreateInstance<CardZoneValidationWindow>();
                try
                {
                    window.SelectTarget(zone); Assert.IsFalse(Field<bool>(window, "hasEditableSnapshot"));
                    Apply(window); Assert.AreSame(data, zone.Data);
                    Assert.IsNull(zone.GetComponent<CardZonePlayHost>());
                }
                finally { Object.DestroyImmediate(window); }
                Assert.AreSame(data, zone.Data); Assert.AreEqual(1, zone.Views.Count);
            }
        }

        [Test] public void OriginalMonoScriptGuidNowResolvesHandAndInheritedSerializedFieldsSurvive()
        {
            var oldScriptPath = AssetDatabase.GUIDToAssetPath("a21570f253844baebbfb3dbd4cda93d9");
            Assert.AreEqual("Assets/CardPresentation/Runtime/HandZoneView.cs", oldScriptPath);
            Assert.AreEqual(typeof(HandZoneView), AssetDatabase.LoadAssetAtPath<MonoScript>(oldScriptPath).GetClass());
            using (var f = new Fixture())
            {
                var source = f.Add<HandZoneView>(); var restored = f.Add<HandZoneView>();
                var layout = source.Layout; layout.ArcHeight = .006f; source.SetLayout(layout);
                source.SetDrawSound(null, .407f); source.SetDiscardDestination(new Vector3(-.5f, .1f, 0));
                source.SetInteractionOffsets(new Vector3(0, 0, .011f), new Vector3(0, 0, .023f));
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), restored);
                Assert.AreSame(source.CardPrefab, restored.CardPrefab); Assert.AreEqual(layout.ArcHeight, restored.Layout.ArcHeight);
                Assert.AreEqual(.407f, restored.DrawSoundVolume); Assert.AreEqual(source.DiscardLocalPosition, restored.DiscardLocalPosition);
                Assert.AreEqual(source.SelectedOffset, restored.SelectedOffset);
                CardZoneView common = restored; common.Present(Data(new CardVisualData("a", "spades_a")), false);
                Assert.AreEqual(1, common.Views.Count);
            }
        }
    }
}
