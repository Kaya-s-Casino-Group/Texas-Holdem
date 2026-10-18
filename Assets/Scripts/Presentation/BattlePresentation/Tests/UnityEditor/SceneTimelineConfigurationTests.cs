using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BattlePresentation.Unity.Tests
{
    public sealed class SceneTimelineConfigurationTests
    {
        private static UnitDisplayData Unit(string id, string team, long health = 100, long block = 0) =>
            new UnitDisplayData(id, id, team, health, 100, block, 3, 3);
        private static BattleDisplaySnapshot Snapshot(long enemyHealth = 100) => new BattleDisplaySnapshot(
            "battle", "player", 1, "player", 1, null,
            new[] { Unit("player", "players"), Unit("enemy", "enemies", enemyHealth) }, null);
        private static EffectDisplayData Effect(long lost = 10, long absorbed = 0, long beforeBlock = 0,
            long afterBlock = 0, bool death = false) => new EffectDisplayData("Damage", "player", "enemy", lost + absorbed,
                lost, absorbed, Unit("enemy", "enemies", 100, beforeBlock), Unit("enemy", "enemies", 100 - lost, afterBlock), death);
        private static ActionContext Context(EffectDisplayData effect = null, bool skipped = false, string[] targetIds = null)
        {
            var before = Snapshot();
            var change = new BattleDisplayChange(0, DisplayCursor.Empty, Snapshot(90), "EffectSettled", "player",
                targetIds ?? new[] { "enemy" }, values: new Dictionary<string, string> { ["action"] = "Play", ["skipped"] = skipped.ToString() },
                before: before, effect: effect);
            return new ActionContext("batch", 1, "action", change, before, null, new PresentationScope("test"));
        }
        private static PresentationBatch Batch(ActionContext context) =>
            new PresentationBatch("batch", context.Before, new[] { context.Data }, context.After);

        [Test]
        public void EffectKindTargetIdentityAndAmountPredicatesMustAllMatch()
        {
            var context = Context(Effect());
            var filter = new SceneTimelineFilter { effectKind = "Damage", targetTeam = "enemies", targetId = "enemy",
                targetRelation = SceneTargetRelation.OtherThanObserver, actorRelation = SceneTargetRelation.Observer,
                healthLost = SceneAmountMatch.Positive, blockAbsorbed = SceneAmountMatch.Zero, isDeath = SceneMatchBoolean.False,
                skipped = SceneMatchBoolean.False };
            Assert.That(filter.Matches(context), Is.True);
            filter.targetRelation = SceneTargetRelation.OtherThanActor;
            Assert.That(filter.Matches(context), Is.True);
            filter.targetRelation = SceneTargetRelation.Actor;
            Assert.That(filter.Matches(context), Is.False);
            filter.targetRelation = SceneTargetRelation.Any;
            filter.effectKind = "Heal";
            Assert.That(filter.Matches(context), Is.False);
            filter.effectKind = "Damage"; filter.blockAbsorbed = SceneAmountMatch.Positive;
            Assert.That(filter.Matches(context), Is.False);
            filter.blockAbsorbed = SceneAmountMatch.Any; filter.targetTeam = "players";
            Assert.That(filter.Matches(context), Is.False);
            context.Scope.Dispose();
        }

        [Test]
        public void ZeroAndFalseEffectPredicatesDoNotInventEffectsOnOrdinaryFacts()
        {
            var context = Context();
            Assert.That(new SceneTimelineFilter().Matches(context), Is.True);
            Assert.That(new SceneTimelineFilter { effectPresence = SceneMatchBoolean.False }.Matches(context), Is.True);
            Assert.That(new SceneTimelineFilter { effectPresence = SceneMatchBoolean.True }.Matches(context), Is.False);
            Assert.That(new SceneTimelineFilter { healthLost = SceneAmountMatch.Zero }.Matches(context), Is.False);
            Assert.That(new SceneTimelineFilter { blockAbsorbed = SceneAmountMatch.Zero }.Matches(context), Is.False);
            Assert.That(new SceneTimelineFilter { healthChange = SceneValueChange.Unchanged }.Matches(context), Is.False);
            Assert.That(new SceneTimelineFilter { isDeath = SceneMatchBoolean.False }.Matches(context), Is.False);
            context.Scope.Dispose();
        }

        [Test]
        public void TargetPredicatesUseOneSubjectAndPreferActualEffectTarget()
        {
            var multiple = Context(targetIds: new[] { "player", "enemy" });
            Assert.That(new SceneTimelineFilter { targetId = "player", targetTeam = "enemies" }.Matches(multiple), Is.False,
                "An id from one target and team from another cannot satisfy one target filter.");
            var actual = Context(Effect(), targetIds: new[] { "player" });
            Assert.That(new SceneTimelineFilter { targetId = "enemy", targetTeam = "enemies" }.Matches(actual), Is.True);
            Assert.That(new SceneTimelineFilter { targetId = "player" }.Matches(actual), Is.False);
            multiple.Scope.Dispose(); actual.Scope.Dispose();
        }

        [TestCase(5, 8, SceneValueChange.Increased)]
        [TestCase(8, 3, SceneValueChange.Decreased)]
        [TestCase(5, 5, SceneValueChange.Unchanged)]
        public void BlockChangeUsesDisplayedBeforeAndAfter(long before, long after, SceneValueChange expected)
        {
            var context = Context(Effect(beforeBlock: before, afterBlock: after));
            foreach (SceneValueChange change in Enum.GetValues(typeof(SceneValueChange)))
                Assert.That(new SceneTimelineFilter { blockChange = change }.Matches(context),
                    Is.EqualTo(change == SceneValueChange.Any || change == expected));
            context.Scope.Dispose();
        }

        [TestCase(50, 60, true)]
        [TestCase(100, 100, false)]
        [TestCase(60, 50, false)]
        public void HealFeedbackRequiresActualHealthIncrease(long before, long after, bool expected)
        {
            var effect = new EffectDisplayData("Heal", "player", "enemy", 10, 0, 0,
                Unit("enemy", "enemies", before), Unit("enemy", "enemies", after));
            var context = Context(effect);
            var filter = new SceneTimelineFilter { effectKind = "Heal", healthChange = SceneValueChange.Increased };
            Assert.That(filter.Matches(context), Is.EqualTo(expected));
            context.Scope.Dispose();
        }

        [Test]
        public void SkippedAndDeathFiltersDistinguishTrueFalseAndAny()
        {
            var context = Context(Effect(death: true), skipped: true);
            Assert.That(new SceneTimelineFilter { skipped = SceneMatchBoolean.True, isDeath = SceneMatchBoolean.True }.Matches(context), Is.True);
            Assert.That(new SceneTimelineFilter { skipped = SceneMatchBoolean.False }.Matches(context), Is.False);
            Assert.That(new SceneTimelineFilter { isDeath = SceneMatchBoolean.False }.Matches(context), Is.False);
            Assert.That(new SceneTimelineFilter().Matches(context), Is.True);
            context.Scope.Dispose();
        }

        [Test]
        public void UnmatchedGroupDoesNotContributeCheckpointAndFilteredRequiredNodeFailsBeforeAnyAction()
        {
            var root = new GameObject("Timeline configuration");
            var director = root.AddComponent<ScenePresentationDirector>();
            try
            {
                var context = Context();
                director.ConfigureTimeline(new[] { new SceneTimelineGroup { semanticKey = "EffectSettled",
                    filter = new SceneTimelineFilter { healthLost = SceneAmountMatch.Positive },
                    explicitCheckpoint = true,
                    checkpoint = new[] { new SceneActionDependency { actionId = "effect", kind = DependencyKind.Marker, marker = "Revealed" } }
                } }, null);
                director.Initialize(context.Before);
                Assert.That(director.Present(Batch(context), PlaybackMode.Animated).Status, Is.EqualTo(PresentationStatus.Completed));

                var effectContext = Context(Effect());
                var action = root.AddComponent<TransformTimelineAction>();
                action.Configure(root.transform, Array.Empty<TransformTimelineStep>());
                director.ConfigureTimeline(new[] { new SceneTimelineGroup { semanticKey = "EffectSettled",
                    filter = new SceneTimelineFilter { healthLost = SceneAmountMatch.Positive },
                    nodes = new[] { new SceneActionNode { id = "effect", action = action,
                        filter = new SceneTimelineFilter { targetRelation = SceneTargetRelation.Observer } } },
                    explicitCheckpoint = true,
                    checkpoint = new[] { new SceneActionDependency { actionId = "effect", kind = DependencyKind.Completed } }
                } }, null);
                director.Initialize(effectContext.Before);
                var failed = director.Present(Batch(effectContext), PlaybackMode.Animated);
                Assert.That(failed.Status, Is.EqualTo(PresentationStatus.Failed));
                Assert.That(failed.Error.Message, Does.Contain("Unknown action: effect"));
                Assert.That(director.Display.Current.Unit("enemy").Health, Is.EqualTo(90), "Invalid presentation recovers to its final snapshot.");
                Assert.That(root.transform.localPosition, Is.EqualTo(Vector3.zero));
                context.Scope.Dispose(); effectContext.Scope.Dispose();
            }
            finally { Object.DestroyImmediate(root); }
        }

        private sealed class ManualRunner
        {
            private readonly List<IEnumerator> routines = new List<IEnumerator>();
            public Action Start(IEnumerator routine) { routines.Add(routine); return () => routines.Remove(routine); }
            public void Tick()
            {
                foreach (var routine in routines.ToArray())
                    if (routines.Contains(routine) && !routine.MoveNext()) routines.Remove(routine);
            }
        }

        [Test]
        public void ReconfigurationCancelsCompletedBatchTailsBeforeReplacingTheirTargetBindings()
        {
            var root = new GameObject("Timeline reconfiguration");
            var source = new GameObject("source"); source.transform.SetParent(root.transform);
            var oldTarget = new GameObject("old binding"); oldTarget.transform.SetParent(root.transform);
            var newTarget = new GameObject("new binding"); newTarget.transform.SetParent(root.transform);
            try
            {
                var runner = new ManualRunner();
                var tail = root.AddComponent<TransformTimelineAction>();
                tail.Configure(source.transform, new[] { new TransformTimelineStep {
                    duration = 1, localOffset = Vector3.right, marker = "Late" } });
                var follower = root.AddComponent<TransformTimelineAction>();
                follower.Configure(null, new[] { new TransformTimelineStep { duration = 0, localOffset = Vector3.up } }, false);
                tail.StartPresentationRoutine = runner.Start; tail.DeltaTimeProvider = () => .1f;
                follower.StartPresentationRoutine = runner.Start;
                var director = root.AddComponent<ScenePresentationDirector>();
                director.ConfigureTimeline(new[] { new SceneTimelineGroup { semanticKey = "EffectSettled",
                    explicitCheckpoint = true, nodes = new[] {
                        new SceneActionNode { id = "tail", action = tail, blocking = false },
                        new SceneActionNode { id = "follow", action = follower, blocking = false, targetKey = "dynamic", after = new[] {
                            new SceneActionDependency { actionId = "tail", kind = DependencyKind.Marker, marker = "Late" } } }
                    }
                } }, new[] { new SceneTargetBinding { key = "dynamic", target = oldTarget.transform } });
                var context = Context(Effect());
                director.Initialize(context.Before);
                int finished = 0; director.PlaybackFinished += () => finished++;
                var operation = director.Present(Batch(context), PlaybackMode.Animated);
                Assert.That(operation.Status, Is.EqualTo(PresentationStatus.Completed));
                runner.Tick(); director.Tick();
                Assert.That(source.transform.localPosition.x, Is.GreaterThan(0));
                director.ConfigureTimeline(Array.Empty<SceneTimelineGroup>(), new[] {
                    new SceneTargetBinding { key = "dynamic", target = newTarget.transform } });
                director.Configure(null); // Resolve the new serialized bindings without starting another batch.
                Assert.That(source.transform.localPosition, Is.EqualTo(Vector3.zero));
                for (int tick = 0; tick < 30; tick++) { runner.Tick(); director.Tick(); }
                Assert.That(oldTarget.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(newTarget.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(finished, Is.EqualTo(1), "Reconfiguring a completed batch must not announce completion again.");
                Assert.That(director.LastError, Is.Null);
                context.Scope.Dispose();
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ConfiguredMarkerStartsFollowingActionBeforeTailAndCancellationRestoresBothTargets()
        {
            var root = new GameObject("Timeline signals");
            var source = new GameObject("source"); source.transform.SetParent(root.transform);
            var follower = new GameObject("follower"); follower.transform.SetParent(root.transform);
            try
            {
                var runner = new ManualRunner();
                var first = source.AddComponent<TransformTimelineAction>();
                first.Configure(source.transform, new[] {
                    new TransformTimelineStep { localOffset = Vector3.right, duration = 0, marker = "Ready" },
                    new TransformTimelineStep { localOffset = Vector3.right * 2, duration = 1 }
                });
                var second = follower.AddComponent<TransformTimelineAction>();
                second.Configure(follower.transform, new[] {
                    new TransformTimelineStep { localOffset = Vector3.up, duration = 0, marker = "Impact" },
                    new TransformTimelineStep { localOffset = Vector3.up * 2, duration = 1 }
                });
                first.StartPresentationRoutine = runner.Start; first.DeltaTimeProvider = () => .1f;
                second.StartPresentationRoutine = runner.Start; second.DeltaTimeProvider = () => .1f;
                var director = root.AddComponent<ScenePresentationDirector>();
                director.ConfigureTimeline(new[] { new SceneTimelineGroup { semanticKey = "EffectSettled",
                    nodes = new[] {
                        new SceneActionNode { id = "prepare", action = first },
                        new SceneActionNode { id = "attack", action = second, after = new[] {
                            new SceneActionDependency { actionId = "prepare", kind = DependencyKind.Marker, marker = "Ready" } } }
                    }, explicitCheckpoint = true, checkpoint = new[] {
                        new SceneActionDependency { actionId = "attack", kind = DependencyKind.Marker, marker = "Impact" } }
                } }, null);
                var context = Context(Effect());
                director.Initialize(context.Before);
                var operation = director.Present(Batch(context), PlaybackMode.Animated);
                Assert.That(director.HasConfiguredTimeline, Is.True);
                runner.Tick(); director.Tick();
                Assert.That(source.transform.localPosition.x, Is.GreaterThanOrEqualTo(1));
                Assert.That(follower.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(director.Display.Current.Cursor, Is.EqualTo(DisplayCursor.Empty));
                runner.Tick(); director.Tick();
                Assert.That(follower.transform.localPosition.y, Is.GreaterThanOrEqualTo(1));
                Assert.That(director.Display.Current.Cursor, Is.EqualTo(new DisplayCursor("batch", 0)));
                Assert.That(operation.Status, Is.EqualTo(PresentationStatus.Pending), "The checkpoint does not discard blocking tails.");
                Assert.That(director.TryGetChange(director.Display.Current.Cursor, out var change), Is.True);
                Assert.That(change, Is.SameAs(context.Data));
                Assert.That(director.TryGetChange(new DisplayCursor("other", 0), out _), Is.False);
                operation.Cancel();
                for (int tick = 0; tick < 20; tick++) { runner.Tick(); director.Tick(); }
                Assert.That(source.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(follower.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(director.Display.Current.Unit("enemy").Health, Is.EqualTo(90));
                director.Initialize(context.Before);
                Assert.That(director.LastBatch, Is.Null);
                context.Scope.Dispose();
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
