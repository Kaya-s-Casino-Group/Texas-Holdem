using System;
using System.Collections.Generic;

namespace BattlePresentation
{
    public enum PresentationStatus { Pending, Completed, Cancelled, Failed }
    public enum PlaybackMode { Animated, Instant }
    public sealed class PresentationError
    {
        public string Code { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public PresentationError(string code, string message, Exception exception = null)
        { Code = code; Message = message; Exception = exception; }
        public override string ToString() => Code + ": " + Message;
    }

    public interface IPresentationOperation
    {
        PresentationStatus Status { get; }
        PresentationError Error { get; }
        event Action<PresentationStatus> Finished;
        void Cancel();
    }

    /// <summary>The producer owns this source; consumers receive only IPresentationOperation.</summary>
    public sealed class PresentationOperation : IPresentationOperation
    {
        private readonly Action onCancel;
        public PresentationStatus Status { get; private set; } = PresentationStatus.Pending;
        public PresentationError Error { get; private set; }
        public event Action<PresentationStatus> Finished;
        public PresentationOperation(Action onCancel = null) { this.onCancel = onCancel; }
        public bool TryComplete() => Finish(PresentationStatus.Completed, null);
        public bool TryFail(string code, string message, Exception exception = null) => Finish(PresentationStatus.Failed, new PresentationError(code, message, exception));
        public bool TryFail(PresentationError error) => Finish(PresentationStatus.Failed, error);
        public void Cancel()
        {
            if (Status != PresentationStatus.Pending) return;
            Status = PresentationStatus.Cancelled;
            try { onCancel?.Invoke(); }
            finally { Finished?.Invoke(Status); }
        }
        private bool Finish(PresentationStatus status, PresentationError error)
        { if (Status != PresentationStatus.Pending) return false; Status = status; Error = error; Finished?.Invoke(status); return true; }
        public static IPresentationOperation Completed() { var source = new PresentationOperation(); source.TryComplete(); return source; }
        public static IPresentationOperation Failed(string code, string message) { var source = new PresentationOperation(); source.TryFail(code, message); return source; }
    }

    public interface IMarkerSink { bool Emit(string marker); }
    public interface ITargetResolver { object Resolve(string key, ActionContext context); }
    public sealed class DictionaryTargetResolver : ITargetResolver
    {
        private readonly Dictionary<string, object> bindings = new Dictionary<string, object>();
        public void Bind(string key, object target) { if (target == null) bindings.Remove(key); else bindings[key] = target; }
        public object Resolve(string key, ActionContext context) => key != null && bindings.TryGetValue(key, out var value) ? value : null;
    }

    public sealed class ActionDescriptor
    {
        public string Key { get; }
        public IReadOnlyList<string> Markers { get; }
        public IReadOnlyList<string> Channels { get; }
        public ActionDescriptor(string key, IEnumerable<string> markers = null, IEnumerable<string> channels = null)
        { Key = key; Markers = Frozen.List(markers); Channels = Frozen.List(channels); }
    }

    public sealed class ActionContext
    {
        public string BatchId { get; }
        public long PlaybackEpoch { get; }
        public string ActionInstanceId { get; }
        public BattleDisplayChange Data { get; }
        public BattleDisplaySnapshot Before { get; }
        public BattleDisplaySnapshot After => Data.After;
        public ITargetResolver Targets { get; }
        public PresentationScope Scope { get; }
        public PresentationChannels Channels { get; }
        public string TargetKey { get; }
        public object Target => Targets?.Resolve(TargetKey, this);
        public ActionContext(string batchId, long playbackEpoch, string actionInstanceId, BattleDisplayChange data,
            BattleDisplaySnapshot before, ITargetResolver targets, PresentationScope scope, string targetKey = null,
            PresentationChannels channels = null)
        { BatchId = batchId; PlaybackEpoch = playbackEpoch; ActionInstanceId = actionInstanceId; Data = data;
          Before = before; Targets = targets; Scope = scope; TargetKey = targetKey; Channels = channels; }
    }

    public sealed class ActionSyncContext
    {
        public BattleDisplaySnapshot Snapshot { get; }
        public ITargetResolver Targets { get; }
        public long PlaybackEpoch { get; }
        public ActionSyncContext(BattleDisplaySnapshot snapshot, ITargetResolver targets, long playbackEpoch)
        { Snapshot = snapshot; Targets = targets; PlaybackEpoch = playbackEpoch; }
    }

    public interface IPresentationAction
    {
        ActionDescriptor Descriptor { get; }
        IPresentationOperation Play(ActionContext context, IMarkerSink markers);
        void Synchronize(ActionSyncContext context);
    }

    public sealed class DelegatePresentationAction : IPresentationAction
    {
        private readonly Func<ActionContext, IMarkerSink, IPresentationOperation> play;
        private readonly Action<ActionSyncContext> synchronize;
        public ActionDescriptor Descriptor { get; }
        public DelegatePresentationAction(ActionDescriptor descriptor, Func<ActionContext, IMarkerSink, IPresentationOperation> play,
            Action<ActionSyncContext> synchronize = null)
        { Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor)); this.play = play ?? throw new ArgumentNullException(nameof(play)); this.synchronize = synchronize; }
        public IPresentationOperation Play(ActionContext context, IMarkerSink markers) => play(context, markers);
        public void Synchronize(ActionSyncContext context) => synchronize?.Invoke(context);
    }

    public enum DependencyKind { Started, Marker, Completed }
    public sealed class ActionDependency
    {
        public string ActionId { get; }
        public DependencyKind Kind { get; }
        public string MarkerKey { get; }
        private ActionDependency(string actionId, DependencyKind kind, string marker) { ActionId = actionId; Kind = kind; MarkerKey = marker; }
        public static ActionDependency Started(string id) => new ActionDependency(id, DependencyKind.Started, null);
        public static ActionDependency Marker(string id, string key) => new ActionDependency(id, DependencyKind.Marker, key);
        public static ActionDependency Completed(string id) => new ActionDependency(id, DependencyKind.Completed, null);
    }

    public sealed class ActionNode
    {
        public string Id { get; }
        public IPresentationAction Action { get; }
        public IReadOnlyList<ActionDependency> Dependencies { get; }
        public bool Blocking { get; }
        public string TargetKey { get; }
        public ActionNode(string id, IPresentationAction action, IEnumerable<ActionDependency> dependencies = null,
            bool blocking = true, string targetKey = null)
        { Id = id; Action = action ?? throw new ArgumentNullException(nameof(action)); Dependencies = Frozen.List(dependencies); Blocking = blocking; TargetKey = targetKey; }
    }

    public sealed class ActionPlan
    {
        public static readonly ActionPlan Empty = new ActionPlan(null);
        public IReadOnlyList<ActionNode> Nodes { get; }
        /// <summary>Null waits for every blocking action. Empty applies the fact immediately.</summary>
        public IReadOnlyList<ActionDependency> Checkpoint { get; }
        public ActionPlan(IEnumerable<ActionNode> nodes, IEnumerable<ActionDependency> checkpoint = null)
        { Nodes = Frozen.List(nodes); Checkpoint = checkpoint == null ? null : Frozen.List(checkpoint); }
    }

    public interface IBattlePresentationFrontend
    {
        bool IsPresenting { get; }
        void Initialize(BattleDisplaySnapshot snapshot);
        IPresentationOperation Present(PresentationBatch batch, PlaybackMode mode);
        void Synchronize(BattleDisplaySnapshot snapshot);
    }
}
