using System;
using System.Linq;
using NUnit.Framework;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using Poker.BattleHosting;
using PokerEngine;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Poker.BattlePresentation.Tests
{
    public sealed class BattleSceneLayoutTests
    {
        private GameObject root;
        private BattleSceneLayout layout;
        [SetUp] public void SetUp()
        {
            root = new GameObject("Layout test"); layout = root.AddComponent<BattleSceneLayout>();
            BattleLayoutSlot[] Slots(string prefix, int count) => Enumerable.Range(1, count).Select(i => {
                var go = new GameObject(prefix + i); go.transform.SetParent(root.transform); go.transform.position = new Vector3(i, 2, 3);
                return new BattleLayoutSlot(prefix + i, go.transform); }).ToArray();
            layout.Configure(Slots("P", 2), Slots("E", 5));
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);
        private static UnitDisplayData Unit(string id, string team = "enemy", long health = 10) => new UnitDisplayData(id, id, team, health, 10, 0, 0, 0);
        private static BattleDisplaySnapshot State(params UnitDisplayData[] units) => new BattleDisplaySnapshot("battle", "p", 0, "player", 0, null, units, null);
        private BattleLayoutBindings Resolve(BattleDisplaySnapshot state, BattleLayoutBindings old = null, params BattleUnitLayoutOverride[] mappings) =>
            BattleLayoutBindings.Resolve(layout, state, mappings, old);
        [Test] public void PositionsAreCopiedInWorldSpaceAndLayoutNeverParentsActors()
        {
            root.transform.position += Vector3.right * 7;
            var state = Resolve(State(Unit("p", "player"), Unit("enemy")));
            Assert.AreEqual(layout.Enemies[0].anchor.position, state.Units["enemy"].RootPosition);
            var oldPose = state.Units["enemy"].RootPosition;
            layout.Enemies[0].anchor.position += Vector3.up;
            Assert.AreEqual(oldPose, state.Units["enemy"].RootPosition);
            Assert.AreEqual(7, root.transform.childCount);
        }
        [Test] public void MissingOrDuplicateAnchorsAreRejected()
        {
            layout.Enemies[1].anchor = layout.Enemies[0].anchor;
            Assert.Throws<InvalidOperationException>(() => layout.Validate());
            layout.Enemies[1].anchor = null;
            Assert.Throws<InvalidOperationException>(() => layout.Validate());
        }
        [Test] public void DuplicateSlotIdsAndWrongCapacityAreRejected()
        {
            layout.Enemies[0].id = "P1";
            Assert.Throws<InvalidOperationException>(() => layout.Validate());
            layout.Configure(layout.Players.Take(1).ToArray(), layout.Enemies.ToArray());
            Assert.Throws<InvalidOperationException>(() => layout.Validate());
        }
        [TestCase("player", 3)] [TestCase("enemy", 6)]
        public void IndependentCapacityCannotSilentlyHideAUnit(string team, int count) => Assert.Throws<InvalidOperationException>(() =>
            Resolve(State(Enumerable.Range(0, count).Select(i => Unit("u" + i, team)).ToArray())));
        [Test] public void FivePartsShareOneBossSlotButKeepDistinctIdentities()
        {
            var parts = Enumerable.Range(1, 5).Select(i => Unit("hand-" + i));
            var mappings = parts.Select(u => new BattleUnitLayoutOverride { unitId = u.Id, ownerUnitId = "boss", partKey = u.Id }).ToArray();
            var bindings = Resolve(State(new[] { Unit("boss") }.Concat(parts).ToArray()), null, mappings);
            Assert.AreEqual(6, bindings.Units.Count);
            Assert.IsTrue(bindings.Units.Values.All(p => p.SlotId == "E1"));
            Assert.IsTrue(bindings.Units["hand-1"].IsPart);
        }
        [Test] public void PartsCannotReferenceMissingOwnersOrFormCycles()
        {
            Assert.Throws<InvalidOperationException>(() => Resolve(State(Unit("a")), null,
                new BattleUnitLayoutOverride { unitId = "a", ownerUnitId = "missing", partKey = "head" }));
            Assert.Throws<InvalidOperationException>(() => Resolve(State(Unit("a"), Unit("b")), null,
                new BattleUnitLayoutOverride { unitId = "a", ownerUnitId = "b", partKey = "head" },
                new BattleUnitLayoutOverride { unitId = "b", ownerUnitId = "a", partKey = "head" }));
        }
        [Test] public void ExplicitSlotIsReservedBeforeAutomaticAllocation()
        {
            var bindings = Resolve(State(Unit("auto"), Unit("fixed")), null,
                new BattleUnitLayoutOverride { unitId = "fixed", slotId = "E1" });
            Assert.AreEqual("E2", bindings.Units["auto"].SlotId);
            Assert.AreEqual("E1", bindings.Units["fixed"].SlotId);
            Assert.Throws<InvalidOperationException>(() => Resolve(State(Unit("enemy")), null,
                new BattleUnitLayoutOverride { unitId = "enemy", slotId = "P1" }));
        }
        [Test] public void DeathDoesNotShiftSurvivorsAndSummonCanReuseVacancy()
        {
            var first = Resolve(State(Unit("a"), Unit("b")));
            var next = Resolve(State(Unit("b"), Unit("a", health: 0), Unit("summon")), first);
            Assert.AreEqual("E2", next.Units["b"].SlotId);
            Assert.AreEqual("E1", next.Units["summon"].SlotId);
            Assert.IsFalse(next.Units["a"].OccupiesSlot);
            Assert.AreEqual(first.Units["a"].RootPosition, next.Units["a"].RootPosition);
            var revived = Resolve(State(Unit("a"), Unit("b"), Unit("summon")), next);
            Assert.AreEqual("E1", revived.Units["summon"].SlotId);
            Assert.AreEqual("E3", revived.Units["a"].SlotId);
        }
        [Test] public void LivingPartKeepsItsDeadOwnersSlotOccupied()
        {
            var bindings = Resolve(State(Unit("boss", health: 0), Unit("hand"), Unit("other")), null,
                new BattleUnitLayoutOverride { unitId = "hand", ownerUnitId = "boss", partKey = "hand" });
            Assert.AreEqual("E1", bindings.Units["boss"].SlotId);
            Assert.AreEqual("E2", bindings.Units["other"].SlotId);
        }
        [Test] public void NewExplicitUnitCannotDisplaceExistingLivingOccupant()
        {
            var initial = Resolve(State(Unit("a")));
            Assert.Throws<InvalidOperationException>(() => Resolve(State(Unit("a"), Unit("b")), initial,
                new BattleUnitLayoutOverride { unitId = "b", slotId = "E1" }));
            Assert.AreEqual("E1", initial.Units["a"].SlotId);
        }
        [Test] public void AnotherBattleDoesNotInheritOldSlotAssignments()
        {
            var initial = Resolve(State(Unit("a"), Unit("b")));
            var next = new BattleDisplaySnapshot("new-battle", "p", 0, "player", 0, null, new[] { Unit("b") }, null);
            Assert.AreEqual("E1", Resolve(next, initial).Units["b"].SlotId);
        }
        [Test] public void OverflowPreflightDoesNotPublishOrPartiallyReplaceBindings()
        {
            var go = new GameObject("Director"); go.transform.SetParent(root.transform);
            var director = go.AddComponent<ScenePresentationDirector>();
            using (var frontend = new BattleLayoutFrontend(director, layout, null))
            {
                var initial = State(Unit("a")); frontend.Initialize(initial);
                var before = frontend.Bindings;
                var after = State(Enumerable.Range(0, 6).Select(i => Unit("u" + i)).ToArray());
                Assert.Throws<InvalidOperationException>(() => frontend.Present(new PresentationBatch("batch", initial,
                    new[] { new BattleDisplayChange(0, initial.Cursor, after, "summon") }, after), PlaybackMode.Animated));
                Assert.AreSame(before, frontend.Bindings);
                Assert.AreEqual(1, director.Display.Current.Units.Count);
            }
        }
        private sealed class Hold : IPresentationAction
        {
            public ActionDescriptor Descriptor => new ActionDescriptor("hold", new[] { "Impact" });
            public PresentationOperation Operation;
            public IPresentationOperation Play(ActionContext context, IMarkerSink markers) => Operation = new PresentationOperation();
            public void Synchronize(ActionSyncContext context) { Operation?.Cancel(); }
        }
        [Test] public void LayoutCanBeDestroyedBeforeDirectorCleanupWithoutLeavingStaleBindings()
        {
            var go = new GameObject("Director"); go.transform.SetParent(root.transform);
            var director = go.AddComponent<ScenePresentationDirector>();
            using (var frontend = new BattleLayoutFrontend(director, layout, null))
            {
                var initial = State(Unit("a")); frontend.Initialize(initial);
                Object.DestroyImmediate(layout);
                Assert.DoesNotThrow(() => frontend.Synchronize(initial));
                Assert.IsNull(frontend.Bindings);
                Assert.Throws<ArgumentNullException>(() => frontend.Initialize(initial));
            }
        }
        [Test] public void FutureUnitsAreNotPublishedBeforeCheckpointAndSyncRebuildsBindings()
        {
            var go = new GameObject("Director"); go.transform.SetParent(root.transform);
            var director = go.AddComponent<ScenePresentationDirector>(); var hold = new Hold();
            director.Configure(_ => new ActionPlan(new[] { new ActionNode("wait", hold) }));
            using (var frontend = new BattleLayoutFrontend(director, layout, null))
            {
                var initial = State(Unit("a")); var after = State(Unit("a"), Unit("b"));
                frontend.Initialize(initial);
                var batch = new PresentationBatch("batch", initial,
                    new[] { new BattleDisplayChange(0, initial.Cursor, after, "summon") }, after);
                frontend.Present(batch, PlaybackMode.Animated);
                Assert.IsFalse(frontend.Bindings.Units.ContainsKey("b"));
                hold.Operation.TryComplete(); director.Tick();
                Assert.AreEqual("E2", frontend.Bindings.Units["b"].SlotId);
                frontend.Synchronize(initial);
                Assert.IsFalse(frontend.Bindings.Units.ContainsKey("b"));
                frontend.Initialize(State(Unit("b")));
                Assert.AreEqual("E1", frontend.Bindings.Units["b"].SlotId);
            }
        }
        [Test] public void BridgeReadsSceneLayoutOnAttachAndOnRestartedInitialization()
        {
            var go = new GameObject("Battle"); go.transform.SetParent(root.transform);
            var director = go.AddComponent<ScenePresentationDirector>(); var host = go.AddComponent<BattleSceneHost>();
            var adapter = go.AddComponent<BattleTimelineSceneAdapter>(); adapter.Configure(director); adapter.ConfigureLayout(layout); adapter.BindHost(host);
            var engine = new CardDuelEngine(new BattleRules("layout-test", 5));
            var session = new BattleSession(engine, engine.Create("battle", HmacDeckShuffler.FromSeed(new byte[32]),
                new[] { new BattlePlayerSetup(new ParticipantId("player"), new CombatantDefinition("hero", 100),
                    deck: StandardDeck.Cards().Select((c, i) => new DeckEntry("c" + i, c.Id))) },
                new[] { new BattleEnemySetup(new ParticipantId("enemy"), new CombatantDefinition("enemy", 100), 1) }));
            adapter.ValidateSession(session);
            // The host must install session/engine before presentation initializes.
            host.Configure(go.AddComponent<LayoutTestSource>(), adapter, false);
            host.AttachSession(session);
            Assert.AreEqual("P1", adapter.LayoutBindings.Units["player"].SlotId);
            Assert.AreEqual("E1", adapter.LayoutBindings.Units["enemy"].SlotId);
            layout.Enemies[0].anchor.position += Vector3.right * 4;
            host.SynchronizeBattle();
            Assert.AreEqual(layout.Enemies[0].anchor.position, adapter.LayoutBindings.Units["enemy"].RootPosition);
            host.AttachSession(session);
            Assert.AreEqual(2, adapter.LayoutBindings.Units.Count);
        }
    }
    public sealed class LayoutTestSource : MonoBehaviour, IBattleSessionSource
    { public BattleSession CreateSession() => throw new NotSupportedException(); }
}
