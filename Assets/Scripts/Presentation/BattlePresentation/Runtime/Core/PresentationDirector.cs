using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlePresentation
{
    /// <summary>
    /// A single-threaded signal scheduler. Call Tick from the frontend's update loop.
    /// Checkpoints advance facts independently from live animation tails. No rule callbacks exist here.
    /// </summary>
    public sealed class PresentationDirector : IBattlePresentationFrontend
    {
        private readonly DisplayStore store;
        private readonly ITargetResolver targets;
        private readonly Func<ActionContext, ActionPlan> planFactory;
        private readonly List<Group> groups = new List<Group>();
        private readonly Queue<Action> events = new Queue<Action>();
        private readonly HashSet<IPresentationAction> synchronizedActions = new HashSet<IPresentationAction>();
        private PresentationOperation operation;
        private PresentationBatch batch;
        private int nextFact;
        private bool draining;
        private bool cancelling;
        private bool recoveryBlocked;
        public long PlaybackEpoch { get; private set; }
        private bool IsRunning => operation != null && operation.Status == PresentationStatus.Pending;
        private bool CanDrain => operation != null && (operation.Status == PresentationStatus.Pending || operation.Status == PresentationStatus.Completed);
        public bool IsPresenting => IsRunning || recoveryBlocked;
        public bool IsRecoveryBlocked => recoveryBlocked;
        public IDisplayReader Display => store;
        public PresentationChannels Channels { get; }
        public PresentationError LastError { get; private set; }

        public PresentationDirector(DisplayStore store, ITargetResolver targets, Func<ActionContext, ActionPlan> planFactory,
            PresentationChannels channels = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.targets = targets;
            this.planFactory = planFactory ?? (_ => ActionPlan.Empty);
            Channels = channels ?? new PresentationChannels();
        }

        public void Initialize(BattleDisplaySnapshot snapshot) => Synchronize(snapshot);

        public IPresentationOperation Present(PresentationBatch value, PlaybackMode mode)
        {
            if (IsPresenting) return PresentationOperation.Failed("Busy", "The previous presentation has not finished.");
            try { store.Validate(value); }
            catch (Exception error) { return PresentationOperation.Failed("InvalidBatch", error.Message); }

            // Completed nonblocking tails remain scoped until the next command or explicit synchronization.
            CancelWork();
            // End the preceding scope before creating new action instances. No historical adapter registry grows.
            try { SynchronizeActions(store.Current); }
            catch (Exception error)
            {
                recoveryBlocked = true;
                LastError = new PresentationError("SynchronizeFailed", error.Message, error);
                return PresentationOperation.Failed(LastError.Code, LastError.Message);
            }
            LastError = null;
            batch = value;
            nextFact = 0;
            operation = new PresentationOperation(CancelAndRecover);
            var result = operation;
            if (mode == PlaybackMode.Instant || value.Changes.Count == 0)
            {
                try
                {
                    // Instant batches have consumed every fact; later Unity ticks must not replay them.
                    nextFact = value.Changes.Count;
                    store.ApplyInstant(value);
                    SynchronizeActions(store.Current);
                    result.TryComplete();
                }
                catch (Exception error) { Fail("InstantFailed", error.Message, error); }
                return result;
            }
            Drain();
            return result;
        }

        public void Tick() => Drain();
        public void Drain()
        {
            if (draining || cancelling) return;
            draining = true;
            try
            {
                var progress = true;
                while (progress && CanDrain)
                {
                    progress = false;
                    while (events.Count > 0 && CanDrain) { events.Dequeue()(); progress = true; }
                    if (!CanDrain) break;
                    foreach (var group in groups.ToArray())
                    {
                        if (!group.Scope.IsActive) continue;
                        foreach (var node in group.Nodes)
                        {
                            if (!node.Started && group.Matches(node.Definition.Dependencies))
                            { Start(group, node); progress = true; if (!CanDrain) break; }
                        }
                        if (!CanDrain) break;
                    }
                    if (!CanDrain) break;
                    // Drain markers before completing a checkpoint; never recurse from inside Play.
                    if (events.Count > 0) { progress = true; continue; }
                    var last = groups.Count == 0 ? null : groups[groups.Count - 1];
                    if (last != null && !last.Applied && last.CheckpointReady)
                    {
                        store.Apply(batch.Id, last.Context.Data);
                        last.Applied = true;
                        progress = true;
                    }
                    if ((last == null || last.Applied) && nextFact < batch.Changes.Count)
                    {
                        CreateGroup(batch.Changes[nextFact++]);
                        progress = true;
                    }
                    if (IsRunning && nextFact == batch.Changes.Count && groups.All(x => x.Applied && x.BlockingComplete))
                    { operation.TryComplete(); break; }
                }
            }
            catch (Exception error) { Fail("DirectorFailed", error.Message, error); }
            finally { draining = false; }
        }

        public void Synchronize(BattleDisplaySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            CancelWork();
            batch = null;
            var old = operation;
            operation = null;
            old?.Cancel();
            try { store.Synchronize(snapshot); SynchronizeActions(store.Current); LastError = null; recoveryBlocked = false; }
            catch (Exception error) { recoveryBlocked = true; LastError = new PresentationError("SynchronizeFailed", error.Message, error); throw; }
        }

        private void CreateGroup(BattleDisplayChange change)
        {
            var scope = new PresentationScope(PlaybackEpoch + "/" + batch.Id + "/" + change.Sequence);
            var context = new ActionContext(batch.Id, PlaybackEpoch, scope.Id, change, change.Before ?? store.Current, targets, scope,
                channels: Channels);
            ActionPlan plan;
            try { plan = planFactory(context) ?? ActionPlan.Empty; }
            catch { scope.Dispose(); throw; }
            var group = new Group(context, scope, plan);
            try { group.Validate(); } catch { scope.Dispose(); throw; }
            groups.Add(group);
            foreach (var node in group.Nodes) synchronizedActions.Add(node.Definition.Action);
        }

        private void Start(Group group, Node node)
        {
            node.Started = true;
            var epoch = PlaybackEpoch;
            var sink = new Sink(this, group, node, epoch);
            node.Sink = sink;
            var context = new ActionContext(batch.Id, epoch, group.Context.ActionInstanceId + "/" + node.Definition.Id,
                group.Context.Data, group.Context.Before, targets, group.Scope, node.Definition.TargetKey, Channels);
            try
            {
                node.Operation = node.Definition.Action.Play(context, sink);
                if (node.Operation == null) throw new InvalidOperationException("Action " + node.Definition.Id + " returned no operation.");
                node.FinishedHandler = status => EnqueueTerminal(group, node, status, epoch);
                node.Operation.Finished += node.FinishedHandler;
                sink.Activate();
                if (node.Operation.Status != PresentationStatus.Pending)
                    EnqueueTerminal(group, node, node.Operation.Status, epoch);
            }
            catch (Exception error)
            {
                sink.Discard();
                Fail("ActionPlayFailed", node.Definition.Id + ": " + error.Message, error);
            }
        }

        private void EnqueueTerminal(Group group, Node node, PresentationStatus status, long epoch)
        {
            events.Enqueue(() =>
            {
                if (epoch != PlaybackEpoch || !group.Scope.IsActive || node.Terminal) return;
                node.Terminal = true;
                node.Status = status;
                node.Sink.Close();
                if (status == PresentationStatus.Failed) { Fail("ActionFailed", node.Operation.Error?.Message ?? node.Definition.Id); return; }
                if (status == PresentationStatus.Cancelled) { Fail("ActionCancelled", node.Definition.Id); return; }
                foreach (var required in group.RequiredMarkers[node.Definition.Id])
                    if (!node.Markers.Contains(required)) { Fail("MissingMarker", node.Definition.Id + " completed without " + required); return; }
            });
        }

        private void Fail(string code, string message, Exception exception = null)
        {
            if (cancelling) return;
            var currentOperation = operation;
            var final = batch?.FinalSnapshot;
            LastError = new PresentationError(code, message, exception);
            CancelWork();
            try { if (final != null) { store.Synchronize(final); SynchronizeActions(store.Current); } }
            catch (Exception recovery) { recoveryBlocked = true; LastError = new PresentationError("RecoveryFailed", message + "; recovery: " + recovery.Message, recovery); }
            currentOperation?.TryFail(LastError);
        }

        private void CancelAndRecover()
        {
            if (cancelling) return;
            var final = batch?.FinalSnapshot;
            CancelWork();
            try { if (final != null) { store.Synchronize(final); SynchronizeActions(store.Current); } }
            catch (Exception error) { recoveryBlocked = true; LastError = new PresentationError("RecoveryFailed", error.Message, error); }
        }

        private void CancelWork()
        {
            if (cancelling) return;
            cancelling = true;
            ++PlaybackEpoch;
            events.Clear();
            try
            {
                foreach (var group in groups)
                {
                    // Invalidate scope-gated work before Cancel can run arbitrary implementation callbacks.
                    try { group.Scope.Dispose(); }
                    catch (Exception error) { LastError = new PresentationError("CleanupFailed", error.Message, error); }
                    foreach (var node in group.Nodes)
                    {
                        node.Sink?.Discard();
                        if (node.Operation != null)
                        {
                            if (node.FinishedHandler != null) node.Operation.Finished -= node.FinishedHandler;
                            try { node.Operation.Cancel(); }
                            catch (Exception error) { LastError = new PresentationError("CancelFailed", error.Message, error); }
                        }
                    }
                }
                groups.Clear();
            }
            finally { cancelling = false; }
        }

        private void SynchronizeActions(BattleDisplaySnapshot snapshot)
        {
            var context = new ActionSyncContext(snapshot, targets, PlaybackEpoch);
            foreach (var action in synchronizedActions) action.Synchronize(context);
            synchronizedActions.Clear();
        }

        private sealed class Node
        {
            internal readonly ActionNode Definition;
            internal bool Started, Terminal;
            internal PresentationStatus Status;
            internal readonly HashSet<string> Markers = new HashSet<string>();
            internal IPresentationOperation Operation;
            internal Action<PresentationStatus> FinishedHandler;
            internal Sink Sink;
            internal Node(ActionNode definition) { Definition = definition; }
        }

        private sealed class Group
        {
            internal readonly ActionContext Context;
            internal readonly PresentationScope Scope;
            internal readonly ActionPlan Plan;
            internal readonly List<Node> Nodes;
            internal readonly Dictionary<string, Node> ById = new Dictionary<string, Node>();
            internal readonly Dictionary<string, HashSet<string>> RequiredMarkers = new Dictionary<string, HashSet<string>>();
            internal bool Applied;
            internal bool BlockingComplete => Nodes.All(x => !x.Definition.Blocking || x.Status == PresentationStatus.Completed);
            internal bool CheckpointReady => Plan.Checkpoint == null ? BlockingComplete : Matches(Plan.Checkpoint);
            internal Group(ActionContext context, PresentationScope scope, ActionPlan plan)
            { Context = context; Scope = scope; Plan = plan; Nodes = plan.Nodes.Select(x => new Node(x)).ToList(); }
            internal bool Matches(IEnumerable<ActionDependency> dependencies) => dependencies.All(x =>
            { var node = ById[x.ActionId]; return x.Kind == DependencyKind.Started ? node.Started :
                x.Kind == DependencyKind.Completed ? node.Status == PresentationStatus.Completed : node.Markers.Contains(x.MarkerKey); });
            internal void Validate()
            {
                foreach (var node in Nodes)
                {
                    if (string.IsNullOrEmpty(node.Definition.Id) || ById.ContainsKey(node.Definition.Id))
                        throw new InvalidOperationException("Action IDs must be unique and nonempty.");
                    ById.Add(node.Definition.Id, node);
                    RequiredMarkers.Add(node.Definition.Id, new HashSet<string>());
                }
                foreach (var node in Nodes) foreach (var dependency in node.Definition.Dependencies) ValidateDependency(dependency);
                if (Plan.Checkpoint != null) foreach (var dependency in Plan.Checkpoint) ValidateDependency(dependency);
                var visited = new HashSet<string>();
                var visiting = new HashSet<string>();
                foreach (var node in Nodes) Visit(node, visited, visiting);
            }
            private void ValidateDependency(ActionDependency dependency)
            {
                if (!ById.TryGetValue(dependency.ActionId, out var node)) throw new InvalidOperationException("Unknown action: " + dependency.ActionId);
                if (dependency.Kind != DependencyKind.Marker) return;
                if (!node.Definition.Action.Descriptor.Markers.Contains(dependency.MarkerKey))
                    throw new InvalidOperationException("Undeclared marker: " + dependency.ActionId + "." + dependency.MarkerKey);
                RequiredMarkers[dependency.ActionId].Add(dependency.MarkerKey);
            }
            private void Visit(Node node, HashSet<string> visited, HashSet<string> visiting)
            {
                if (visited.Contains(node.Definition.Id)) return;
                if (!visiting.Add(node.Definition.Id)) throw new InvalidOperationException("Cyclic action dependency: " + node.Definition.Id);
                foreach (var dependency in node.Definition.Dependencies) Visit(ById[dependency.ActionId], visited, visiting);
                visiting.Remove(node.Definition.Id); visited.Add(node.Definition.Id);
            }
        }

        private sealed class Sink : IMarkerSink
        {
            private readonly PresentationDirector director;
            private readonly Group group;
            private readonly Node node;
            private readonly long epoch;
            private readonly List<string> buffer = new List<string>();
            private bool activated, closed;
            internal Sink(PresentationDirector director, Group group, Node node, long epoch)
            { this.director = director; this.group = group; this.node = node; this.epoch = epoch; }
            public bool Emit(string marker)
            {
                if (closed || epoch != director.PlaybackEpoch || !group.Scope.IsActive) return false;
                if (!node.Definition.Action.Descriptor.Markers.Contains(marker))
                    throw new InvalidOperationException("Action emitted undeclared marker: " + marker);
                if (!activated) buffer.Add(marker); else Queue(marker);
                return true;
            }
            internal void Activate() { activated = true; foreach (var marker in buffer) Queue(marker); buffer.Clear(); }
            private void Queue(string marker) => director.events.Enqueue(() =>
            { if (epoch == director.PlaybackEpoch && group.Scope.IsActive && !node.Terminal) node.Markers.Add(marker); });
            internal void Close() { closed = true; }
            internal void Discard() { closed = true; buffer.Clear(); }
        }
    }
}
