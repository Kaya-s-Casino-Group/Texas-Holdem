using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BattlePresentation.Unity
{
    [Serializable]
    public sealed class TransformTimelineStep
    {
        public Vector3 localOffset;
        public Vector3 localEuler;
        public Vector3 scaleMultiplier = Vector3.one;
        [Min(0)] public float duration = .2f;
        public string marker;
    }

    internal static class TransformPresentationChannels
    {
        internal static readonly PresentationChannels Pool = new PresentationChannels();
        private static readonly Dictionary<string, CandidateTransformFeedback> feedbackOwners = new Dictionary<string, CandidateTransformFeedback>();
        internal static string Key(Transform target) => "unity-transform/" + target.GetInstanceID();
        internal static bool TryAcquireFeedback(Transform target, CandidateTransformFeedback owner, PresentationScope scope,
            out ChannelLease lease, Action release)
        {
            var key = Key(target);
            if (Pool.IsOwned(key) && feedbackOwners.TryGetValue(key, out var previous) && previous != null && previous.CanTransfer)
            {
                lease = Pool.Acquire(key, scope, release);
                feedbackOwners[key] = owner;
                previous.ReleaseTransferred();
                return true;
            }
            if (!Pool.TryAcquire(key, scope, out lease, release)) return false;
            feedbackOwners[key] = owner; return true;
        }
        internal static void ReleaseFeedback(Transform target, CandidateTransformFeedback owner)
        {
            if (target == null) return;
            var key = Key(target);
            if (feedbackOwners.TryGetValue(key, out var current) && current == owner) feedbackOwners.Remove(key);
        }
    }

    /// <summary>Example arbitrary-object action with real segment markers and exclusive transform ownership.</summary>
    public sealed class TransformTimelineAction : PresentationActionBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private TransformTimelineStep[] steps = Array.Empty<TransformTimelineStep>();
        [Tooltip("When false, retain the final transient pose until this presentation scope is released.")]
        [SerializeField] private bool restoreOnComplete = true;
        private sealed class RunState
        {
            internal Transform Target;
            internal Vector3 Position, Scale;
            internal Quaternion Rotation;
            internal PresentationScope Scope;
            internal ChannelLease Lease;
            internal PresentationOperation Operation;
            internal Action CancelRoutine;
        }
        private RunState current;
        public void Configure(Transform value, TransformTimelineStep[] timeline, bool restore = true)
        { ReleaseCurrent(); target = value; steps = timeline ?? Array.Empty<TransformTimelineStep>(); restoreOnComplete = restore; }
        public override ActionDescriptor Descriptor => new ActionDescriptor(name,
            (steps ?? Array.Empty<TransformTimelineStep>()).Where(x => x != null).Select(x => x.marker).Where(x => !string.IsNullOrEmpty(x)), new[] { "transform" });

        public override IPresentationOperation Play(ActionContext context, IMarkerSink markers)
        {
            if (!isActiveAndEnabled) return PresentationOperation.Failed("ActionDisabled", name);
            if (current?.Operation.Status == PresentationStatus.Pending) return PresentationOperation.Failed("TargetBusy", name);
            ReleaseCurrent();
            var actual = target != null ? target : (context.Target as Transform) ?? (context.Target as GameObject)?.transform;
            if (actual == null) return PresentationOperation.Failed("MissingTarget", name);
            if (steps == null || steps.Any(step => step == null || !Finite(step.duration) || step.duration < 0 ||
                !Finite(step.localOffset) || !Finite(step.localEuler) || !Finite(step.scaleMultiplier)))
                return PresentationOperation.Failed("InvalidTransformTimeline", name);
            var run = new RunState { Target = actual, Position = actual.localPosition, Rotation = actual.localRotation,
                Scale = actual.localScale, Scope = new PresentationScope(context.ActionInstanceId + "/transform") };
            if (!TransformPresentationChannels.Pool.TryAcquire(TransformPresentationChannels.Key(actual), run.Scope, out var lease,
                () => Restore(run)))
            { run.Scope.Dispose(); return PresentationOperation.Failed("TargetBusy", actual.name + " already has a transform action or feedback owner."); }
            run.Lease = lease;
            run.Operation = new PresentationOperation(() => Release(run));
            current = run;
            context.Scope.Own(() => { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); });
            var cancelRoutine = StartRoutine(Run(run, markers));
            if (run.Operation.Status == PresentationStatus.Pending) run.CancelRoutine = cancelRoutine;
            else cancelRoutine();
            return run.Operation;
        }

        private IEnumerator Run(RunState run, IMarkerSink markers)
        {
            foreach (var step in steps)
            {
                if (run.Target == null || !run.Lease.IsCurrent) { Fail(run, "Transform target or ownership was lost."); yield break; }
                var position = run.Target.localPosition; var rotation = run.Target.localRotation; var scale = run.Target.localScale;
                float elapsed = 0;
                while (elapsed < step.duration && run.Operation.Status == PresentationStatus.Pending)
                {
                    if (run.Target == null || !run.Lease.IsCurrent) { Fail(run, "Transform target or ownership was lost."); yield break; }
                    elapsed += DeltaTime; float t = Mathf.Clamp01(elapsed / step.duration); t = t * t * (3 - 2 * t);
                    Write(run, Vector3.LerpUnclamped(position, run.Position + step.localOffset, t),
                        Quaternion.SlerpUnclamped(rotation, run.Rotation * Quaternion.Euler(step.localEuler), t),
                        Vector3.LerpUnclamped(scale, Vector3.Scale(run.Scale, step.scaleMultiplier), t));
                    yield return null;
                }
                if (run.Operation.Status != PresentationStatus.Pending) yield break;
                Write(run, run.Position + step.localOffset, run.Rotation * Quaternion.Euler(step.localEuler), Vector3.Scale(run.Scale, step.scaleMultiplier));
                Exception markerError = null;
                try { if (!string.IsNullOrEmpty(step.marker)) markers.Emit(step.marker); }
                catch (Exception error) { markerError = error; }
                if (markerError != null) { Fail(run, markerError.Message); yield break; }
            }
            run.CancelRoutine = null;
            if (restoreOnComplete) run.Scope.Dispose();
            run.Operation.TryComplete();
        }
        private static void Write(RunState run, Vector3 position, Quaternion rotation, Vector3 scale) => run.Lease.TryWrite(() =>
        { if (run.Target != null) { run.Target.localPosition = position; run.Target.localRotation = rotation; run.Target.localScale = scale; } });
        private static void Restore(RunState run)
        { if (run.Target != null) { run.Target.localPosition = run.Position; run.Target.localRotation = run.Rotation; run.Target.localScale = run.Scale; } }
        private void Fail(RunState run, string message) { Release(run); run.Operation.TryFail("TransformActionFailed", message); }
        private void Release(RunState run)
        {
            var cancelRoutine = run.CancelRoutine;
            run.CancelRoutine = null;
            cancelRoutine?.Invoke(); run.Scope.Dispose();
            if (ReferenceEquals(current, run)) current = null;
        }
        private void ReleaseCurrent()
        { var run = current; if (run == null) return; if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); }
        public override void Synchronize(ActionSyncContext context) => ReleaseCurrent();
        private void OnDisable() => ReleaseCurrent();
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
