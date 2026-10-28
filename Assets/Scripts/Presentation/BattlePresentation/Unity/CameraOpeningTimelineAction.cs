using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace BattlePresentation.Unity
{
    /// <summary>Moves from an authored opening shot to the rig's default shot, releasing a checkpoint near arrival.</summary>
    public sealed class CameraOpeningTimelineAction : PresentationActionBehaviour
    {
        [SerializeField] private CameraPresentation source;
        private CameraShot openingShot;
        private float duration = 1.8f;
        private Ease ease = Ease.OutCubic;
        private float dealStartProgress = .8f;
        private RunState current;
        private sealed class RunState
        {
            internal CameraPresentation Rig;
            internal PresentationScope Scope;
            internal ChannelLease Lease;
            internal PresentationOperation Operation;
            internal Tween Tween;
            internal Action CancelRoutine, Cancel;
        }
        public CameraPresentation Source => source;
        public CameraShot OpeningShot => openingShot;
        public float Duration => duration;
        public float DealStartProgress => dealStartProgress;
        public Ease Ease => ease;
        public void BindScene(CameraOpeningSettings settings)
        {
            ReleaseCurrent(); openingShot = settings.shot; duration = settings.duration;
            ease = settings.ease; dealStartProgress = settings.dealStartProgress;
        }
        public override ActionDescriptor Descriptor => new ActionDescriptor("camera.opening",
            new[] { "NearlyArrived", "Arrived" }, new[] { "camera.pose" });

        public void Configure(CameraPresentation rig, CameraShot start, float seconds = 1.8f, float readyProgress = .8f, Ease curve = Ease.OutCubic)
        { ReleaseCurrent(); source = rig; openingShot = start; duration = seconds; dealStartProgress = readyProgress; ease = curve; }

        public override IPresentationOperation Play(ActionContext context, IMarkerSink markers)
        {
            if (!isActiveAndEnabled) return PresentationOperation.Failed("ActionDisabled", name);
            if (current != null) return PresentationOperation.Failed("TargetBusy", name);
            if (source == null || !source.isActiveAndEnabled || openingShot == null)
                return PresentationOperation.Failed("MissingOpeningCamera", name);
            if (!source.ValidateConfiguration(out var error)) return PresentationOperation.Failed("InvalidCameraRig", error);
            if (!Finite(duration) || duration < 0 || !Finite(dealStartProgress) || dealStartProgress <= 0 || dealStartProgress > 1)
                return PresentationOperation.Failed("InvalidOpeningTiming", name);
            var run = new RunState { Rig = source, Scope = new PresentationScope(context.ActionInstanceId + "/opening-camera") };
            if (!source.TryAcquire(false, run.Scope, out run.Lease))
            { run.Scope.Dispose(); return PresentationOperation.Failed("TargetBusy", source.name); }
            run.Operation = new PresentationOperation(() => Release(run)); run.Cancel = run.Operation.Cancel;
            current = run; source.Track(run.Cancel); context.Scope.Own(run.Cancel);
            if (!context.Scope.IsActive) return run.Operation;
            try
            {
                var fromPosition = openingShot.transform.position; var fromRotation = openingShot.transform.rotation;
                float fromFov = openingShot.FieldOfView, fromSize = openingShot.OrthographicSize;
                var toPosition = source.DefaultShot.transform.position; var toRotation = source.DefaultShot.transform.rotation;
                float toFov = source.DefaultShot.FieldOfView, toSize = source.DefaultShot.OrthographicSize;
                Action<float> write = t => run.Lease.TryWrite(() => run.Rig.SetPose(Vector3.LerpUnclamped(fromPosition, toPosition, t),
                    Quaternion.SlerpUnclamped(fromRotation, toRotation, t), Mathf.LerpUnclamped(fromFov, toFov, t),
                    Mathf.LerpUnclamped(fromSize, toSize, t)));
                write(0);
                if (duration > 0)
                {
                    float progress = 0;
                    run.Tween = DOTween.To(() => progress, value => { progress = value; write(value); }, 1, duration)
                        .SetEase(ease).SetUpdate(UpdateType.Manual).SetAutoKill(false).SetRecyclable(false);
                    run.Tween.ForceInit();
                }
                var cancel = StartRoutine(Advance(run, markers, write, duration, dealStartProgress));
                if (run.Operation.Status == PresentationStatus.Pending) run.CancelRoutine = cancel; else cancel();
            }
            catch (Exception exception) { Fail(run, exception); }
            return run.Operation;
        }

        private IEnumerator Advance(RunState run, IMarkerSink markers, Action<float> write, float seconds, float readyProgress)
        {
            float elapsed = 0; bool ready = false;
            while (run.Operation.Status == PresentationStatus.Pending)
            {
                if (run.Rig == null || !run.Rig.isActiveAndEnabled) { run.Operation.Cancel(); yield break; }
                Exception failure = null;
                try
                {
                    if (!run.Lease.IsCurrent || !run.Rig.HasOutputControl || !run.Rig.ValidateConfiguration(out _))
                        throw new InvalidOperationException("Opening camera rig or ownership was lost.");
                    float dt = DeltaTime;
                    if (!Finite(dt) || dt < 0) throw new InvalidOperationException("Invalid opening camera clock.");
                    dt = Mathf.Min(dt, seconds - elapsed); elapsed += dt;
                    if (seconds > 0)
                    {
                        if (run.Tween == null || !run.Tween.IsActive()) throw new InvalidOperationException("Opening camera tween was interrupted.");
                        // Advance only this tween. Never advance the global DOTween clock or the hand's tweens.
                        run.Tween.ManualUpdate(dt, dt);
                    }
                    if (elapsed >= seconds) write(1);
                    if (!ready && (seconds == 0 || elapsed / seconds >= readyProgress))
                    {
                        ready = true; markers.Emit("NearlyArrived");
                    }
                    if (run.Operation.Status != PresentationStatus.Pending) yield break;
                    if (elapsed >= seconds)
                    {
                        run.CancelRoutine = null;
                        Release(run);
                        markers.Emit("Arrived");
                        run.Operation.TryComplete();
                    }
                }
                catch (Exception exception) { failure = exception; }
                if (failure != null) { Fail(run, failure); yield break; }
                if (run.Operation.Status != PresentationStatus.Pending) yield break;
                yield return null;
            }
        }
        private void Release(RunState run)
        {
            var cancel = run.CancelRoutine; run.CancelRoutine = null; cancel?.Invoke();
            run.Tween?.Kill(false); run.Tween = null;
            if (run.Rig != null) run.Rig.Untrack(run.Cancel);
            run.Scope.Dispose();
            if (ReferenceEquals(current, run)) current = null;
        }
        private void Fail(RunState run, Exception exception)
        { Release(run); run.Operation.TryFail("OpeningCameraFailed", exception.Message, exception); }
        private void ReleaseCurrent() => current?.Operation.Cancel();
        public override void Synchronize(ActionSyncContext context) => ReleaseCurrent();
        private void OnDisable() => ReleaseCurrent();
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
