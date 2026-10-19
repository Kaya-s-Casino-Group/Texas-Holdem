#if UNITY_EDITOR
using System;
using System.Linq;
using BattlePresentation;
using CardPresentation.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    public sealed class CardZoneEditorPreviewTests
    {
        private Scene scene;
        private GameObject root;
        private WorldCardZonePresentation world;
        private CanvasCardZonePresentation canvas;
        private static CardZoneDisplayData Zone(params string[] ids) => new CardZoneDisplayData("preview-hand", "p", "hand",
            ids.Select(id => new CardDisplayData(id, id, "Q", "hearts_q")));
        [SetUp] public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene(); root = new GameObject("Preview tests"); SceneManager.MoveGameObjectToScene(root, scene);
            var hand = root.AddComponent<HandZoneView>();
            hand.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
            world = root.AddComponent<WorldCardZonePresentation>();
            var ui = new GameObject("Canvas provider", typeof(RectTransform)); ui.transform.SetParent(root.transform);
            canvas = ui.AddComponent<CanvasCardZonePresentation>();
            canvas.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardPresentation/Prefabs/CardUIItem.prefab").GetComponentInChildren<CardUIGraphic>(true), new Vector2(128, 192));
        }
        [TearDown] public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);
        private MonoBehaviour Provider(bool ui) => ui ? (MonoBehaviour)canvas : world;
        [TestCase(false)] [TestCase(true)] public void ManualClockPausesStepsAndRestoresOriginalDisplay(bool ui)
        {
            var provider = Provider(ui); var view = (ICardZonePresentation)provider; view.Synchronize(Zone("original"));
            using (var preview = new CardZonePreviewSession(provider))
            {
                Assert.Throws<InvalidOperationException>(() => new CardZonePreviewSession(provider));
                preview.Load(Zone("a", "b"));
                var op = preview.Play(CardZoneActions.Prepare, Zone("a", "b"), new[] { "a" });
                Assert.AreEqual(PresentationStatus.Pending, op.Status);
                preview.Paused = true; preview.Advance(2);
                Assert.AreEqual(PresentationStatus.Pending, op.Status); Assert.AreEqual(0, preview.Time);
                for (int i = 0; i < 60; i++) preview.Advance(1f / 60, true);
                Assert.AreEqual(PresentationStatus.Completed, op.Status, op.Error?.ToString());
                Assert.AreEqual(1, preview.Events.Count(entry => entry.Contains("信号 Prepared")));
            }
            CollectionAssert.AreEqual(new[] { "original" }, view.Current.Cards.Select(card => card.ViewId));
        }
        [TestCase(false)] [TestCase(true)] public void ClosingWindowCancelsPendingActionAndClearsTemporaryCards(bool ui)
        {
            var provider = Provider(ui); var view = (ICardZonePresentation)provider;
            var window = ScriptableObject.CreateInstance<CardZoneValidationWindow>();
            IPresentationOperation operation;
            try
            {
                window.SelectPresentation(provider); window.BeginPreview(); window.Preview.Load(Zone("a"));
                operation = window.Preview.Play(CardZoneActions.Prepare, Zone("a"), new[] { "a" });
                Assert.AreEqual(PresentationStatus.Pending, operation.Status);
            }
            finally { Object.DestroyImmediate(window); }
            Assert.AreEqual(PresentationStatus.Cancelled, operation.Status);
            Assert.IsNull(view.Current);
            if (ui) Assert.AreEqual(0, canvas.transform.childCount); else Assert.IsEmpty(root.GetComponent<HandZoneView>().Views);
        }
        [Test] public void SelectionResolvesCanvasAndSwitchingTargetReleasesOldPreview()
        {
            var child = new GameObject("Selection child"); child.transform.SetParent(canvas.transform);
            Assert.AreSame(canvas, CardZoneValidationWindow.ResolvePresentation(child));
            var window = ScriptableObject.CreateInstance<CardZoneValidationWindow>();
            try
            {
                window.SelectPresentation(canvas); window.BeginPreview(); window.Preview.Load(Zone("a"));
                window.SelectPresentation(world);
                Assert.IsNull(window.Preview); Assert.IsNull(canvas.Current);
                Assert.AreSame(world, window.Presentation);
            }
            finally { Object.DestroyImmediate(window); }
        }
        [TestCase(false)] [TestCase(true)] public void PreviewClockDrawsThenReleasesCardsAndRecordsRealMarkers(bool ui)
        {
            using (var preview = new CardZonePreviewSession(Provider(ui)))
            {
                preview.Load(Zone()); var draw = preview.Play(CardZoneActions.Draw, Zone("a", "b"), new[] { "a", "b" });
                for (int i = 0; i < 240; i++) preview.Advance(1f / 60);
                Assert.AreEqual(PresentationStatus.Completed, draw.Status, draw.Error?.ToString());
                Assert.IsTrue(preview.Events.Any(entry => entry.Contains("信号 AllArrived")));
                var play = preview.Play(CardZoneActions.Play, Zone("b"), new[] { "a" });
                for (int i = 0; i < 240; i++) preview.Advance(1f / 60);
                Assert.AreEqual(PresentationStatus.Completed, play.Status, play.Error?.ToString());
                Assert.IsTrue(preview.Events.Any(entry => entry.Contains("信号 Released")));
                Assert.AreEqual("b", preview.Current.Cards.Single().ViewId);
            }
        }
        [Test] public void WorldPreviewRestoresNativeArtworkAndDoesNotSeedTheMatch()
        {
            var hand = root.GetComponent<HandZoneView>();
            var data = new CardZoneViewData(new PokerEngine.ZoneId("original"), new[] {
                new CardVisualData("original", "hearts_q", tint: Color.cyan) });
            hand.Present(data, false);
            var match = root.AddComponent<CardInteraction.CardMatchHost>(); match.ConfigureRuntimeManaged();
            var initial = PokerEngine.CardTableSnapshotCodec.Serialize(match.InitialState);
            using (var preview = new CardZonePreviewSession(world)) preview.Load(Zone("temporary"));
            Assert.AreSame(data, hand.Data); Assert.AreEqual(Color.cyan, hand.Data.Cards.Single().Tint);
            Assert.AreEqual(initial, PokerEngine.CardTableSnapshotCodec.Serialize(match.InitialState)); Assert.IsFalse(match.IsRunning);
        }
        [TestCase(false)] [TestCase(true)] public void ConfiguredActionNeedsNoManualDataAndRestoresAfterRandomHand(bool ui)
        {
            var provider = Provider(ui); var window = ScriptableObject.CreateInstance<CardZoneValidationWindow>();
            var randomState = UnityEngine.Random.state;
            try
            {
                window.SelectPresentation(provider); window.PlayConfiguredAction(CardZoneActions.Prepare);
                Assert.AreEqual(8, window.Preview.Current.Cards.Count);
                Assert.AreEqual(8, window.Preview.Current.Cards.Select(card => card.ArtKey).Distinct().Count());
                for (int i = 0; i < 120; i++) window.Preview.Advance(1f / 60);
                Assert.AreEqual(PresentationStatus.Completed, window.Preview.Operation.Status, window.Preview.Operation.Error?.ToString());
                window.PlayConfiguredAction(CardZoneActions.Discard);
                for (int i = 0; i < 240; i++) window.Preview.Advance(1f / 60);
                Assert.AreEqual(PresentationStatus.Completed, window.Preview.Operation.Status, window.Preview.Operation.Error?.ToString());
                Assert.AreEqual(6, window.Preview.Current.Cards.Count);
                window.LoadRandomHand(); Assert.AreEqual(8, window.Preview.Current.Cards.Count);
                Assert.AreEqual(randomState, UnityEngine.Random.state, "Preview randomization must not consume the game's Unity random stream.");
                window.StopPreview(); Assert.IsNull(((ICardZonePresentation)provider).Current);
            }
            finally { Object.DestroyImmediate(window); }
        }
        [Test] public void SaveBoundaryStopsPreviewBeforeSceneSerialization()
        {
            var window = ScriptableObject.CreateInstance<CardZoneValidationWindow>();
            try
            {
                window.SelectPresentation(canvas); window.BeginPreview(); window.Preview.Load(Zone("temp"));
                typeof(CardZoneValidationWindow).GetMethod("OnSceneSaving", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(window, new object[] { scene, "unused" });
                Assert.IsNull(window.Preview); Assert.IsNull(canvas.Current); Assert.AreEqual(0, canvas.transform.childCount);
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
#endif
