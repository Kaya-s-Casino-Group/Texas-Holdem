using System;
using System.Collections.Generic;
using System.Linq;
using BattlePresentation;
using BattlePresentation.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardPresentation.Tests
{
    public sealed class CardZoneCustomTestAction : MonoBehaviour, ICardZonePresentationAction
    {
        public ActionDescriptor Descriptor => new ActionDescriptor("cards.custom", new[] { "CustomPoint" });
        public IMarkerSink Sink;
        public bool Released;
        public IPresentationOperation Play(CardZonePresentationRequest request, IMarkerSink markers)
        { Sink = markers; request.Scope.Own(() => Released = true); return new PresentationOperation(); }
    }
    public sealed class CardZoneCapabilityTests
    {
        private sealed class Markers : IMarkerSink
        { public readonly List<string> Values = new List<string>(); public bool Emit(string value) { Values.Add(value); return true; } }
        private Scene scene;
        private GameObject root;
        private WorldCardZonePresentation world;
        private CanvasCardZonePresentation canvas;
        private HandZoneView hand;
        private static CardDisplayData Card(string id, string art = "hearts_q") => new CardDisplayData("view-" + id, id, "Q", art);
        private static CardZoneDisplayData Zone(params CardDisplayData[] cards) => new CardZoneDisplayData("hand", "player", "hand", cards);
        [SetUp] public void Setup()
        {
            scene = EditorSceneManager.NewPreviewScene(); root = new GameObject("Card capabilities"); SceneManager.MoveGameObjectToScene(root, scene);
            var go = new GameObject("World"); go.transform.SetParent(root.transform);
            hand = go.AddComponent<HandZoneView>(); hand.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
            var motion = hand.MotionSettings; motion.Enabled = false; hand.SetMotionSettings(motion);
            world = go.AddComponent<WorldCardZonePresentation>();
            var ui = new GameObject("Canvas cards", typeof(RectTransform)); ui.transform.SetParent(root.transform);
            canvas = ui.AddComponent<CanvasCardZonePresentation>();
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardPresentation/Prefabs/CardUIItem.prefab");
            Assert.NotNull(template);
            canvas.Configure(template.GetComponentInChildren<CardUIGraphic>(true), new Vector2(128, 192));
        }
        [TearDown] public void Teardown() => EditorSceneManager.ClosePreviewScene(scene);
        private ICardZonePresentation View(bool isCanvas) => isCanvas ? (ICardZonePresentation)canvas : world;
        private void Advance(bool isCanvas, float seconds = 2) { if (isCanvas) canvas.AdvanceMotion(seconds); else { hand.AdvanceMotion(seconds); world.Tick(); } }
        [TestCase(false)] [TestCase(true)]
        public void DrawPrepareCancelAndReleaseShareOneProtocol(bool isCanvas)
        {
            var view = View(isCanvas); var empty = Zone(); var dealt = Zone(Card("a"), Card("b", "spades_a"));
            view.Synchronize(empty);
            using (var scope = new PresentationScope("draw"))
            {
                var signals = new Markers();
                var op = view.Play(new CardZonePresentationRequest(CardZoneActions.Draw, "draw", scope, empty, dealt, new[] { "a", "b" }), signals);
                Advance(isCanvas); Assert.AreEqual(PresentationStatus.Completed, op.Status, op.Error?.ToString());
                CollectionAssert.AreEqual(new[] { "AllArrived" }, signals.Values);
                CollectionAssert.AreEqual(new[] { "view-a", "view-b" }, view.Current.Cards.Select(x => x.ViewId));
            }
            var preparing = new PresentationScope("prepare"); var prepareSignals = new Markers();
            var preparingOp = view.Play(new CardZonePresentationRequest(CardZoneActions.Prepare, "prepare", preparing, dealt, dealt, new[] { "a" }), prepareSignals);
            Advance(isCanvas, isCanvas ? .18f : .09f);
            CollectionAssert.AreEqual(new[] { "Prepared" }, prepareSignals.Values);
            preparing.Dispose(); Advance(isCanvas);
            Assert.AreEqual(PresentationStatus.Cancelled, preparingOp.Status);
            CollectionAssert.AreEqual(new[] { "Prepared" }, prepareSignals.Values);
            Assert.AreEqual(2, view.Current.Cards.Count);
            if (!isCanvas) Assert.AreEqual(Vector3.zero, hand.Views[0].SelectionActionOffset);
            using (var scope = new PresentationScope("play"))
            {
                var signals = new Markers(); var after = Zone(Card("b", "spades_a"));
                var op = view.Play(new CardZonePresentationRequest(CardZoneActions.Play, "play", scope, dealt, after, new[] { "a" }), signals);
                CollectionAssert.AreEqual(new[] { "Released" }, signals.Values);
                Advance(isCanvas); Assert.AreEqual(PresentationStatus.Completed, op.Status, op.Error?.ToString());
                CollectionAssert.AreEqual(new[] { "Released", "AllArrived" }, signals.Values);
                Assert.AreEqual("view-b", view.Current.Cards.Single().ViewId);
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void CustomScopeRejectsLateSignalsAndSynchronizeRestoresLatest(bool isCanvas)
        {
            var provider = isCanvas ? (CardZonePresentationBehaviour)canvas : world;
            var custom = provider.gameObject.AddComponent<CardZoneCustomTestAction>(); provider.ConfigureCustomActions(custom);
            var before = Zone(Card("a")); provider.Synchronize(before);
            using (var scope = new PresentationScope("custom"))
            {
                var signals = new Markers(); var op = provider.Play(new CardZonePresentationRequest("cards.custom", "custom", scope, before, before), signals);
                Assert.IsTrue(custom.Sink.Emit("CustomPoint")); Assert.IsFalse(custom.Sink.Emit("CustomPoint"));
                provider.Synchronize(Zone(Card("b", "spades_a")));
                Assert.IsTrue(custom.Released); Assert.AreEqual(PresentationStatus.Cancelled, op.Status);
                Assert.IsFalse(custom.Sink.Emit("CustomPoint")); Assert.AreEqual("view-b", provider.Current.Cards.Single().ViewId);
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void SameTimelineComponentResolvesBothFrontends(bool isCanvas)
        {
            var view = View(isCanvas); var zone = Zone(Card("a")); view.Synchronize(zone);
            var component = isCanvas ? (MonoBehaviour)canvas : world;
            var action = root.AddComponent<CardZoneTimelineAction>(); action.Configure(component, CardZoneActions.Prepare);
            var snapshot = new BattleDisplaySnapshot("battle", "player", 1, "player", 1, null, null, new[] { zone });
            using (var scope = new PresentationScope("timeline"))
            {
                var change = new BattleDisplayChange(0, DisplayCursor.Empty, snapshot, "ActionStarted", cardIds: new[] { "a" });
                var signals = new Markers();
                var op = action.Play(new ActionContext("batch", 1, "prepare", change, snapshot, null, scope), signals);
                Advance(isCanvas); Assert.AreEqual(PresentationStatus.Completed, op.Status, op.Error?.ToString());
                CollectionAssert.AreEqual(new[] { "Prepared" }, signals.Values);
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void DirectCancelCleansCustomContributionAndRejectsLaterMarkers(bool isCanvas)
        {
            var provider = isCanvas ? (CardZonePresentationBehaviour)canvas : world;
            var custom = provider.gameObject.AddComponent<CardZoneCustomTestAction>(); provider.ConfigureCustomActions(custom);
            var zone = Zone(Card("a")); provider.Synchronize(zone);
            using (var scope = new PresentationScope("custom-cancel"))
            {
                var signals = new Markers();
                var op = provider.Play(new CardZonePresentationRequest("cards.custom", "custom", scope, zone, zone), signals);
                op.Cancel();
                Assert.IsTrue(custom.Released); Assert.IsTrue(scope.IsActive, "Cancellation must not dispose unrelated actions in the parent scope.");
                Assert.IsFalse(custom.Sink.Emit("CustomPoint")); Assert.IsEmpty(signals.Values);
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void InstantArrangeAndDiscardDoNotRequireAnUpdateLoop(bool isCanvas)
        {
            var view = View(isCanvas); var before = Zone(Card("a"), Card("b", "spades_a")); view.Synchronize(before);
            using (var scope = new PresentationScope("arrange"))
            {
                var after = Zone(Card("b", "spades_a"), Card("a")); var signals = new Markers();
                var op = view.Play(new CardZonePresentationRequest(CardZoneActions.Arrange, "arrange", scope, before, after, mode: PlaybackMode.Instant), signals);
                Assert.AreEqual(PresentationStatus.Completed, op.Status, op.Error?.ToString());
                CollectionAssert.AreEqual(new[] { "view-b", "view-a" }, view.Current.Cards.Select(x => x.ViewId));
                CollectionAssert.AreEqual(new[] { "Settled" }, signals.Values);
                before = after;
            }
            using (var scope = new PresentationScope("discard"))
            {
                var signals = new Markers();
                var op = view.Play(new CardZonePresentationRequest(CardZoneActions.Discard, "discard", scope, before, Zone(Card("a")), new[] { "b" }, PlaybackMode.Instant), signals);
                Assert.AreEqual(PresentationStatus.Completed, op.Status, op.Error?.ToString());
                CollectionAssert.AreEqual(new[] { "Released", "AllArrived" }, signals.Values);
                Assert.AreEqual("view-a", view.Current.Cards.Single().ViewId);
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void CompletedOldScopeCannotResetNewPreparation(bool isCanvas)
        {
            var view = View(isCanvas); var zone = Zone(Card("a")); view.Synchronize(zone);
            var oldScope = new PresentationScope("old"); var nextScope = new PresentationScope("new");
            try
            {
                var first = view.Play(new CardZonePresentationRequest(CardZoneActions.Prepare, "old", oldScope, zone, zone, new[] { "a" }), new Markers());
                Advance(isCanvas); Assert.AreEqual(PresentationStatus.Completed, first.Status);
                var second = view.Play(new CardZonePresentationRequest(CardZoneActions.Prepare, "new", nextScope, zone, zone, new[] { "a" }), new Markers());
                oldScope.Dispose(); Advance(isCanvas);
                Assert.AreEqual(PresentationStatus.Completed, second.Status, second.Error?.ToString());
            }
            finally { oldScope.Dispose(); nextScope.Dispose(); }
        }
        [TestCase(false)] [TestCase(true)]
        public void SelectionCanSwitchBeforeItsPreviousAnimationFinishes(bool isCanvas)
        {
            var view = View(isCanvas); var original = Zone(Card("a")); view.Synchronize(original);
            var selected = Zone(new CardDisplayData("view-a", "a", "Q", "hearts_q",
                properties: new Dictionary<string, string> { ["selected"] = "true" }));
            var cleared = Zone(new CardDisplayData("view-a", "a", "Q", "hearts_q",
                properties: new Dictionary<string, string> { ["selected"] = "false" }));
            using (var firstScope = new PresentationScope("select"))
            using (var nextScope = new PresentationScope("clear"))
            {
                var first = view.Play(new CardZonePresentationRequest(CardZoneActions.Selection, "select", firstScope, original, selected), new Markers());
                Advance(isCanvas, .02f);
                var next = view.Play(new CardZonePresentationRequest(CardZoneActions.Selection, "clear", nextScope, selected, cleared), new Markers());
                Assert.AreEqual(PresentationStatus.Cancelled, first.Status);
                firstScope.Dispose(); Advance(isCanvas);
                Assert.AreEqual(PresentationStatus.Completed, next.Status, next.Error?.ToString());
                Assert.AreEqual("false", view.Current.Cards.Single().Properties["selected"].ToLowerInvariant());
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void TimelineResolutionIgnoresDestroyedFrontend(bool isCanvas)
        {
            object provider = View(isCanvas);
            UnityEngine.Object.DestroyImmediate((MonoBehaviour)provider);
            Assert.IsNull(CardZoneTimelineAction.Resolve(provider));
        }
        [Test] public void ContractHasNoUnityOrRuleAssemblyDependency()
        {
            var names = typeof(ICardZonePresentation).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
            Assert.IsFalse(names.Any(x => x.StartsWith("UnityEngine") || x.StartsWith("PokerEngine") || x == "CardPresentation.Runtime"));
        }
    }
}
