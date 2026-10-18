using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace BattlePresentation.Tests
{
    public sealed class PresentationCoreTests
    {
        private static BattleDisplaySnapshot State(long health = 100, DisplayCursor cursor = null, string observer = "p") =>
            new BattleDisplaySnapshot("battle", observer, 1, "player", 1, null,
                new[] { new UnitDisplayData("e", "Enemy", "enemy", health, 100, 0, 0, 0) }, null, cursor: cursor);

        private static PresentationBatch Batch(params long[] health)
        {
            var changes = new List<BattleDisplayChange>();
            var previous = State();
            var cursor = DisplayCursor.Empty;
            for (var index = 0; index < health.Length; ++index)
            {
                var after = State(health[index]);
                changes.Add(new BattleDisplayChange(index, cursor, after, index == 0 ? "ActionStarted" : "EffectSettled", before: previous));
                cursor = new DisplayCursor("batch", index);
                previous = after;
            }
            return new PresentationBatch("batch", State(), changes, previous);
        }

        private sealed class ManualAction : IPresentationAction
        {
            public ActionDescriptor Descriptor { get; } = new ActionDescriptor("manual", new[] { "Impact", "Ready" });
            public PresentationOperation Operation;
            public IMarkerSink Markers;
            public int Plays, Syncs;
            public Action<ActionContext, IMarkerSink> OnPlay;
            public IPresentationOperation Play(ActionContext context, IMarkerSink markers)
            {
                ++Plays; Markers = markers; Operation = new PresentationOperation();
                OnPlay?.Invoke(context, markers); return Operation;
            }
            public void Synchronize(ActionSyncContext context) { ++Syncs; }
        }

        [Test]
        public void CheckpointAdvancesFactsWhileAttackTailStillBlocksBatch()
        {
            var attack = new ManualAction();
            var hit = new ManualAction();
            var store = new DisplayStore();
            var director = new PresentationDirector(store, null, context => context.Data.Sequence == 0
                ? new ActionPlan(new[] { new ActionNode("attack", attack) }, new[] { ActionDependency.Marker("attack", "Impact") })
                : new ActionPlan(new[] { new ActionNode("hit", hit) }));
            director.Initialize(State());
            var batch = director.Present(Batch(100, 60), PlaybackMode.Animated);
            Assert.That(hit.Plays, Is.Zero);
            attack.Markers.Emit("Impact"); director.Tick();
            Assert.That(hit.Plays, Is.EqualTo(1), "The next fact must start before attack recovery finishes.");
            Assert.That(store.Current.Unit("e").Health, Is.EqualTo(100));
            hit.Operation.TryComplete(); director.Tick();
            Assert.That(store.Current.Unit("e").Health, Is.EqualTo(60));
            Assert.That(batch.Status, Is.EqualTo(PresentationStatus.Pending));
            attack.Operation.TryComplete(); director.Tick();
            Assert.That(batch.Status, Is.EqualTo(PresentationStatus.Completed));
        }

        [Test]
        public void SynchronousMarkerIsLatchedBeforePlayReturnsWithoutRecursiveSuccessor()
        {
            var inside = false;
            var first = new ManualAction();
            first.OnPlay = (_, marker) => { inside = true; marker.Emit("Ready"); first.Operation.TryComplete(); inside = false; };
            var next = new ManualAction();
            next.OnPlay = (_, __) => { Assert.That(inside, Is.False); next.Operation.TryComplete(); };
            var director = new PresentationDirector(new DisplayStore(), null, _ => new ActionPlan(new[]
            {
                new ActionNode("first", first),
                new ActionNode("next", next, new[] { ActionDependency.Marker("first", "Ready") })
            }));
            director.Initialize(State());
            Assert.That(director.Present(Batch(80), PlaybackMode.Animated).Status, Is.EqualTo(PresentationStatus.Completed));
            Assert.That(next.Plays, Is.EqualTo(1));
        }

        [Test]
        public void MultiDependencyWaitsForAllAndDuplicatesDoNotReplay()
        {
            var first = new ManualAction(); var second = new ManualAction(); var next = new ManualAction();
            var director = new PresentationDirector(new DisplayStore(), null, _ => new ActionPlan(new[]
            {
                new ActionNode("a", first), new ActionNode("b", second),
                new ActionNode("c", next, new[] { ActionDependency.Marker("a", "Ready"), ActionDependency.Completed("b") })
            }));
            director.Initialize(State()); director.Present(Batch(80), PlaybackMode.Animated);
            first.Markers.Emit("Ready"); first.Markers.Emit("Ready"); director.Tick();
            Assert.That(next.Plays, Is.Zero);
            second.Operation.TryComplete(); director.Tick();
            Assert.That(next.Plays, Is.EqualTo(1));
            first.Markers.Emit("Ready"); director.Tick();
            Assert.That(next.Plays, Is.EqualTo(1));
        }

        [Test]
        public void NonblockingTailDependenciesContinueAfterBatchGateOpens()
        {
            var first = new ManualAction(); var next = new ManualAction();
            var director = new PresentationDirector(new DisplayStore(), null, _ => new ActionPlan(new[]
            {
                new ActionNode("a", first, blocking: false),
                new ActionNode("b", next, new[] { ActionDependency.Completed("a") }, blocking: false)
            }));
            director.Initialize(State());
            var operation = director.Present(Batch(80), PlaybackMode.Animated);
            Assert.That(operation.Status, Is.EqualTo(PresentationStatus.Completed));
            Assert.That(next.Plays, Is.Zero);
            first.Operation.TryComplete(); director.Tick();
            Assert.That(next.Plays, Is.EqualTo(1));
            Assert.That(director.IsPresenting, Is.False);
            director.Synchronize(State(80));
            Assert.That(next.Operation.Status, Is.EqualTo(PresentationStatus.Cancelled));
        }

        [Test]
        public void MissingRequiredMarkerFailsAndRecoversCommittedSnapshot()
        {
            var action = new ManualAction();
            var store = new DisplayStore();
            var director = new PresentationDirector(store, null, _ => new ActionPlan(new[] { new ActionNode("a", action) },
                new[] { ActionDependency.Marker("a", "Impact") }));
            director.Initialize(State()); var operation = director.Present(Batch(50), PlaybackMode.Animated);
            action.Operation.TryComplete(); director.Tick();
            Assert.That(operation.Status, Is.EqualTo(PresentationStatus.Failed));
            Assert.That(operation.Error.Code, Is.EqualTo("MissingMarker"));
            Assert.That(store.Current.Unit("e").Health, Is.EqualTo(50));
            Assert.That(action.Syncs, Is.EqualTo(1));
        }

        [Test]
        public void ThrowAfterSynchronousMarkerDoesNotStartSuccessor()
        {
            var first = new ManualAction { OnPlay = (_, sink) => { sink.Emit("Impact"); throw new InvalidOperationException("boom"); } };
            var next = new ManualAction();
            var director = new PresentationDirector(new DisplayStore(), null, _ => new ActionPlan(new[]
            { new ActionNode("a", first), new ActionNode("b", next, new[] { ActionDependency.Marker("a", "Impact") }) }));
            director.Initialize(State());
            Assert.That(director.Present(Batch(70), PlaybackMode.Animated).Status, Is.EqualTo(PresentationStatus.Failed));
            Assert.That(next.Plays, Is.Zero);
        }

        [Test]
        public void CancellationInvalidatesMarkersAndCannotBecomeSuccess()
        {
            var first = new ManualAction(); var next = new ManualAction();
            var store = new DisplayStore();
            var director = new PresentationDirector(store, null, _ => new ActionPlan(new[]
            { new ActionNode("a", first), new ActionNode("b", next, new[] { ActionDependency.Marker("a", "Impact") }) }));
            director.Initialize(State()); var operation = director.Present(Batch(40), PlaybackMode.Animated);
            operation.Cancel();
            Assert.That(first.Markers.Emit("Impact"), Is.False);
            Assert.That(first.Operation.TryComplete(), Is.False);
            director.Tick();
            Assert.That(operation.Status, Is.EqualTo(PresentationStatus.Cancelled));
            Assert.That(next.Plays, Is.Zero);
            Assert.That(store.Current.Unit("e").Health, Is.EqualTo(40));
        }

        [Test]
        public void LateFailureCancelsAlreadyStartedSuccessorsAndRecoversWholeBatch()
        {
            var first = new ManualAction(); var next = new ManualAction();
            var store = new DisplayStore();
            var director = new PresentationDirector(store, null, context => context.Data.Sequence == 0
                ? new ActionPlan(new[] { new ActionNode("a", first) }, new[] { ActionDependency.Marker("a", "Impact") })
                : new ActionPlan(new[] { new ActionNode("b", next) }));
            director.Initialize(State()); var operation = director.Present(Batch(90, 40), PlaybackMode.Animated);
            first.Markers.Emit("Impact"); director.Tick(); Assert.That(next.Plays, Is.EqualTo(1));
            first.Operation.TryFail("AnimatorDisabled", "animation stopped"); director.Tick();
            Assert.That(operation.Status, Is.EqualTo(PresentationStatus.Failed));
            Assert.That(next.Operation.Status, Is.EqualTo(PresentationStatus.Cancelled));
            Assert.That(store.Current.Unit("e").Health, Is.EqualTo(40));
        }

        [Test]
        public void RecoveryFailureKeepsGateClosedUntilExplicitSynchronizationSucceeds()
        {
            var failSync = true;
            var action = new DelegatePresentationAction(new ActionDescriptor("failure"),
                (_, __) => PresentationOperation.Failed("failure", "animation failed"),
                _ => { if (failSync) throw new InvalidOperationException("view could not synchronize"); });
            var director = new PresentationDirector(new DisplayStore(), null, _ => new ActionPlan(new[] { new ActionNode("a", action) }));
            director.Initialize(State()); var operation = director.Present(Batch(50), PlaybackMode.Animated);
            Assert.That(operation.Status, Is.EqualTo(PresentationStatus.Failed));
            Assert.That(director.IsRecoveryBlocked, Is.True); Assert.That(director.IsPresenting, Is.True);
            director.Tick();
            failSync = false; director.Synchronize(State(50));
            Assert.That(director.IsRecoveryBlocked, Is.False); Assert.That(director.IsPresenting, Is.False);
        }

        [Test]
        public void CompletedScopesDoNotRetainHistoricalActionAdaptersAcrossCommands()
        {
            var first = new ManualAction(); first.OnPlay = (_, __) => first.Operation.TryComplete();
            var second = new ManualAction(); second.OnPlay = (_, __) => second.Operation.TryComplete();
            var chosen = first;
            var store = new DisplayStore();
            var director = new PresentationDirector(store, null, _ => new ActionPlan(new[] { new ActionNode("action", chosen) }));
            director.Initialize(State()); director.Present(Batch(90), PlaybackMode.Animated);
            chosen = second;
            var initial = store.Current;
            var change = new BattleDisplayChange(0, initial.Cursor, State(80), "effect");
            director.Present(new PresentationBatch("next", initial, new[] { change }, State(80)), PlaybackMode.Animated);
            Assert.That(first.Syncs, Is.EqualTo(1));
            director.Synchronize(State(80));
            Assert.That(first.Syncs, Is.EqualTo(1), "Historical adapters must no longer be retained or synchronized.");
            Assert.That(second.Syncs, Is.EqualTo(1));
        }

        [Test]
        public void DependencyCyclesFailBeforeAnyPlay()
        {
            var first = new ManualAction(); var next = new ManualAction();
            var director = new PresentationDirector(new DisplayStore(), null, _ => new ActionPlan(new[]
            { new ActionNode("a", first, new[] { ActionDependency.Started("b") }), new ActionNode("b", next, new[] { ActionDependency.Started("a") }) }));
            director.Initialize(State());
            Assert.That(director.Present(Batch(70), PlaybackMode.Animated).Status, Is.EqualTo(PresentationStatus.Failed));
            Assert.That(first.Plays + next.Plays, Is.Zero);
        }

        [Test]
        public void InstantPublishesOnceWithoutBuildingOrPlayingAnimatedPlan()
        {
            var planCalls = 0; var publications = 0; var store = new DisplayStore();
            var director = new PresentationDirector(store, null, _ => { ++planCalls; return ActionPlan.Empty; });
            director.Initialize(State()); store.Changed += _ => ++publications;
            Assert.That(director.Present(Batch(90, 80, 70), PlaybackMode.Instant).Status, Is.EqualTo(PresentationStatus.Completed));
            Assert.That(planCalls, Is.Zero); Assert.That(publications, Is.EqualTo(1));
            Assert.That(store.Current.Unit("e").Health, Is.EqualTo(70));
            Assert.That(store.Current.Cursor, Is.EqualTo(new DisplayCursor("batch", 2)));
        }

        [Test]
        public void InvalidDraftDoesNotPublishPartialDataAndCursorIsNotDisplayVersion()
        {
            var store = new DisplayStore(); store.Initialize(State());
            var batch = Batch(90, 80); var version = store.Current.DisplayVersion;
            var invalid = new BattleDisplayChange(1, DisplayCursor.Empty, State(1), "bad");
            Assert.Throws<InvalidOperationException>(() => store.Apply(batch.Id, new[] { batch.Changes[0], invalid }));
            Assert.That(store.Current.Unit("e").Health, Is.EqualTo(100));
            Assert.That(store.Current.DisplayVersion, Is.EqualTo(version));
            store.Apply(batch.Id, batch.Changes);
            Assert.That(store.Current.DisplayVersion, Is.EqualTo(version + 1));
            Assert.Throws<InvalidOperationException>(() => store.Apply(batch.Id, batch.Changes[0]));
        }

        private sealed class Feedback : IInteractionFeedback
        {
            public readonly List<PresentationScope> Seen = new List<PresentationScope>();
            public int Releases;
            public void Reconcile(CandidatePresentationModel next, PresentationScope scope) { Seen.Add(scope); }
            public void Release(PresentationScope scope, ReleaseMode mode) { Assert.That(scope.IsActive, Is.False); ++Releases; }
        }

        private static CandidatePresentationModel Candidate(long version, string pattern, string[] cards = null) =>
            new CandidatePresentationModel("battle", "p", 1, version, CandidateState.Valid, cards ?? new[] { "1" },
                pattern, pattern, 20, 1.5m, 30, true, true);

        [Test]
        public void CandidateUpdatesReuseScopeButLatestReplacementReleasesOldScope()
        {
            var feedback = new Feedback(); var interaction = new InteractionSession(feedback);
            interaction.Reconcile(Candidate(1, "pair"));
            interaction.Reconcile(Candidate(2, "pair", new[] { "2" }));
            Assert.That(feedback.Seen[0], Is.SameAs(feedback.Seen[1]));
            interaction.Reconcile(Candidate(3, "three"));
            Assert.That(feedback.Seen[0].IsActive, Is.False);
            Assert.That(feedback.Releases, Is.EqualTo(1));
            Assert.That(interaction.Reconcile(Candidate(2, "pair")), Is.False);
            interaction.Clear();
            Assert.That(interaction.Current.State, Is.EqualTo(CandidateState.Empty));
            Assert.That(interaction.Current.SelectedIds, Is.Empty);
        }

        [Test]
        public void AsynchronousPreviewMustMatchBattleRevisionObserverSelectionAndLatestRequest()
        {
            var interaction = new InteractionSession();
            var first = interaction.BeginRequest("battle", "p", 1, new[] { "1" });
            var second = interaction.BeginRequest("battle", "p", 1, new[] { "2" });
            Assert.That(interaction.Apply(Candidate(first, "pair")), Is.False);
            Assert.That(interaction.Apply(Candidate(second, "three", new[] { "2" })), Is.True);
            var committed = interaction.BeginCommit();
            Assert.That(committed.SelectedIds, Is.EqualTo(new[] { "2" }));
            Assert.That(interaction.Current.State, Is.EqualTo(CandidateState.Committing));
            Assert.That(interaction.Apply(Candidate(second, "three", new[] { "2" })), Is.False);
        }

        [Test]
        public void OldLeaseCannotRestoreOverNewOwnerAndReleaseUsesLatestBase()
        {
            var channels = new PresentationChannels();
            var firstScope = new PresentationScope("first"); var nextScope = new PresentationScope("next");
            var position = 20; var currentLayout = 10;
            var old = channels.Acquire("card/position", firstScope, () => position = currentLayout);
            var next = channels.Acquire("card/position", nextScope, () => position = currentLayout);
            next.TryWrite(() => position = 30);
            firstScope.Dispose();
            Assert.That(position, Is.EqualTo(30)); Assert.That(old.TryWrite(() => position = -1), Is.False);
            currentLayout = 42; nextScope.Dispose();
            Assert.That(position, Is.EqualTo(42));
        }
    }
}
