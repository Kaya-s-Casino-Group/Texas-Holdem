using System.Collections.Generic;
using CardPresentation;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    [TestFixture]
    public sealed class BattleTimelineActionAdapterTests
    {
        private sealed class Markers : IMarkerSink
        {
            internal readonly List<string> Values = new List<string>();
            public bool Emit(string marker) { Values.Add(marker); return true; }
        }
        private static UnitDisplayData Unit(long health) => new UnitDisplayData("unit", "Unit", "player", health, 100, 0, 3, 3);
        private static BattleDisplaySnapshot Snapshot(long health = 100, CardZoneDisplayData zone = null) =>
            new BattleDisplaySnapshot("battle", "player", 1, "player", 1, null, new[] { Unit(health) },
                zone == null ? null : new[] { zone });
        private static ActionContext Context(BattleDisplaySnapshot snapshot, DictionaryTargetResolver targets,
            PresentationScope scope, string[] cards = null) =>
            new ActionContext("batch", 1, "action", new BattleDisplayChange(0, DisplayCursor.Empty, snapshot,
                "ActionStarted", "unit", cardIds: cards), snapshot, targets, scope);

        [Test] public void ActorAdapterResolvesVisualChildAndRecoversAfterScopeRelease()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Actor adapter"); SceneManager.MoveGameObjectToScene(root, scene);
                var actor = root.AddComponent<PlayerBattleActor>();
                var model = new GameObject("Visual").transform; model.SetParent(root.transform, false);
                var presenter = model.gameObject.AddComponent<BattleActorPresenter>(); presenter.Configure(model);
                actor.ConfigurePresentation(presenter); actor.SynchronizeDisplay(Unit(100), true);
                var action = root.AddComponent<BattleActorTimelineAction>(); action.Configure(null, BattlePresentationKind.Attack);
                var targets = new DictionaryTargetResolver(); targets.Bind("unit", model);
                var scope = new PresentationScope("attack"); var markers = new Markers();
                var operation = action.Play(Context(Snapshot(), targets, scope), markers);
                Assert.AreEqual(PresentationStatus.Completed, operation.Status);
                CollectionAssert.AreEqual(new[] { "Impact" }, markers.Values);
                Assert.IsNull(actor.Battle, "The action must operate on display data without a rule session.");
                scope.Dispose(); // Recovery must still find a dynamically resolved actor after its run is released.
                action.Synchronize(new ActionSyncContext(Snapshot(70), targets, 2));
                Assert.AreEqual(70, actor.DisplayUnit.Health);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test] public void HandAdapterMapsCardIdentityAndCancelsOnlyItsTemporaryContribution()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Hand adapter"); SceneManager.MoveGameObjectToScene(root, scene);
                var hand = root.AddComponent<HandZoneView>();
                var lift = CardZoneLiftSettings.Default; lift.Enabled = false; hand.SetLiftSettings(lift);
                hand.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                var motion = hand.MotionSettings; motion.Enabled = false; hand.SetMotionSettings(motion);
                var data = new CardZoneViewData(new PokerEngine.ZoneId("deal"), new[] {
                    new CardVisualData("visual-card", "hearts_q", interaction: new CardInteractionState(isSelected: true)) });
                hand.Present(data, false);
                var baseline = hand.Views[0].InteractionOffset;
                var snapshot = Snapshot(zone: new CardZoneDisplayData("deal", "unit", "hand", new[] {
                    new CardDisplayData("visual-card", "rule-card", "Q", "hearts_q") }));
                var targets = new DictionaryTargetResolver(); targets.Bind("cards", hand);
                var frontend = root.AddComponent<WorldCardZonePresentation>();
                var action = root.AddComponent<CardZoneTimelineAction>(); action.Configure(frontend, CardZoneActions.Prepare);
                var scope = new PresentationScope("cards"); var markers = new Markers();
                var operation = action.Play(Context(snapshot, targets, scope, new[] { "rule-card" }), markers);
                Assert.AreEqual(PresentationStatus.Pending, operation.Status);
                hand.AdvanceMotion(.09f);
                CollectionAssert.AreEqual(new[] { "Prepared" }, markers.Values);
                Assert.AreEqual(PresentationStatus.Pending, operation.Status, "The real midpoint starts dependencies before preparation finishes.");
                Assert.AreSame(data, hand.Data);
                scope.Dispose(); hand.AdvanceMotion(1);
                Assert.AreEqual(PresentationStatus.Cancelled, operation.Status);
                Assert.AreEqual(Vector3.zero, hand.Views[0].SelectionActionOffset);
                Assert.AreEqual(baseline, hand.Views[0].InteractionOffset);
                CollectionAssert.AreEqual(new[] { "Prepared" }, markers.Values);
                Assert.AreSame(data, hand.Data);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RevivingCompletesTheOldHitAndSkipsQueuedDeathEvenWhenItStartsLater(bool queueBeforeRevival)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Revived actor"); SceneManager.MoveGameObjectToScene(root, scene);
                var actor = root.AddComponent<PlayerBattleActor>();
                var model = new GameObject("Visual"); model.transform.SetParent(root.transform, false);
                var presenter = model.AddComponent<ManualBattlePresentation>();
                actor.ConfigurePresentation(presenter); actor.SynchronizeDisplay(Unit(100), true);
                var effect = new EffectDisplayData("Damage", "enemy", "unit", 100, 100, 0, Unit(100), Unit(0), true);
                var hit = actor.PresentDisplay(BattlePresentationKind.Hit, effect, null);
                var action = root.AddComponent<BattleActorTimelineAction>(); action.Configure(actor, BattlePresentationKind.Death);
                var scope = new PresentationScope("late-death"); var markers = new Markers();
                var context = new ActionContext("batch", 1, "death", new BattleDisplayChange(0, DisplayCursor.Empty,
                    Snapshot(0), "EffectSettled", "enemy", effect: effect), Snapshot(), new DictionaryTargetResolver(), scope);
                IPresentationOperation death = queueBeforeRevival ? action.Play(context, markers) : null;
                actor.SynchronizeDisplay(Unit(60));
                Assert.AreEqual(BattlePresentationStatus.Completed, hit.Status, "A superseded visual tail is not a failed battle.");
                if (!queueBeforeRevival) death = action.Play(context, markers);
                action.Tick();
                Assert.AreEqual(PresentationStatus.Completed, death.Status);
                Assert.AreEqual(1, presenter.Cues.Count, "The old death must never restart after the recovery.");
                Assert.AreEqual(60, actor.DisplayUnit.Health);
                CollectionAssert.AreEqual(new[] { "Revealed" }, markers.Values);
                scope.Dispose();
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
