using System;
using System.Collections;
using UnityEngine;

namespace BattlePresentation.Unity
{
    public enum CameraActionKind { Shot, Focus, Return, Shake }

    /// <summary>Finite camera cue. Shot/focus emits Arrived, holds, then returns; shake emits Settled.</summary>
    public sealed class CameraTimelineAction : PresentationActionBehaviour
    {
        [SerializeField] private CameraPresentation source;
        [SerializeField] private string cueKey;
        public string CueKey => cueKey;
        public void SetSceneCueKey(string key) => cueKey = key;
        private CameraActionKind kind;
        private CameraShot shot;
        private Transform focusTarget;
        private Vector3 focusOffset;
        private float duration = .25f;
        private float holdDuration = .2f;
        private float returnDuration = .25f;
        private Vector3 shakePosition = new Vector3(.025f, .015f, 0);
        private Vector3 shakeEuler = new Vector3(0, 0, .5f);
        private float shakeFrequency = 18;
        private RunState current;
        private sealed class RunState
        {
            internal CameraPresentation Rig;
            internal PresentationScope Scope;
            internal ChannelLease Lease;
            internal PresentationOperation Operation;
            internal Action CancelRoutine, Cancel;
            internal bool Shake;
        }
        public CameraPresentation Source => source;
        public CameraActionKind Kind => kind;
        public void BindScene(CameraCueSettings settings)
        {
            ReleaseCurrent();
            kind = settings.kind; shot = settings.shot; focusTarget = settings.focusTarget; focusOffset = settings.focusOffset;
            duration = settings.duration; holdDuration = settings.holdDuration; returnDuration = settings.returnDuration;
            shakePosition = settings.shakePosition; shakeEuler = settings.shakeEuler; shakeFrequency = settings.shakeFrequency;
        }
        public override ActionDescriptor Descriptor => new ActionDescriptor("camera." + kind.ToString().ToLowerInvariant(),
            kind == CameraActionKind.Shake ? new[] { "Settled" } : new[] { "Arrived", "Settled" },
            new[] { kind == CameraActionKind.Shake ? "camera.shake" : "camera.pose" });

        public void Configure(CameraPresentation rig, CameraActionKind actionKind, CameraShot destination = null,
            Transform focus = null, float seconds = .25f, float hold = .2f, float returnSeconds = .25f)
        {
            ReleaseCurrent(); source = rig; kind = actionKind; shot = destination; focusTarget = focus;
            duration = seconds; holdDuration = hold; returnDuration = returnSeconds;
        }

        public override IPresentationOperation Play(ActionContext context, IMarkerSink markers)
        {
            if (!isActiveAndEnabled) return PresentationOperation.Failed("ActionDisabled", name);
            if (current != null) return PresentationOperation.Failed("TargetBusy", name);
            if (source == null || !source.isActiveAndEnabled) return PresentationOperation.Failed("MissingCamera", name);
            if (!source.ValidateConfiguration(out var error)) return PresentationOperation.Failed("InvalidCameraRig", error);
            if (!Finite(duration) || duration < 0 || !Finite(holdDuration) || holdDuration < 0 ||
                !Finite(returnDuration) || returnDuration < 0 || !Finite(shakeFrequency) || shakeFrequency <= 0 ||
                !Finite(focusOffset) || !Finite(shakePosition) || !Finite(shakeEuler) || !Enum.IsDefined(typeof(CameraActionKind), kind))
                return PresentationOperation.Failed("InvalidCameraAction", name);
            var targetShot = kind == CameraActionKind.Return ? source.DefaultShot : shot;
            if (kind == CameraActionKind.Shot && targetShot == null)
                return PresentationOperation.Failed("MissingCameraShot", name);
            var focus = focusTarget != null ? focusTarget : ResolveTransform(context.Target);
            if (focus == null && source.SceneConfiguration != null) focus = source.SceneConfiguration.DefaultFocus;
            if (kind == CameraActionKind.Focus && focus == null)
                return PresentationOperation.Failed("MissingFocusTarget", name);
            if (kind == CameraActionKind.Focus && (focus.position + focusOffset - source.BasePosition).sqrMagnitude < .000001f)
                return PresentationOperation.Failed("InvalidFocusTarget", "Focus target coincides with the camera.");

            var run = new RunState { Rig = source, Shake = kind == CameraActionKind.Shake,
                Scope = new PresentationScope(context.ActionInstanceId + "/camera") };
            if (!source.TryAcquire(run.Shake, run.Scope, out run.Lease))
            { run.Scope.Dispose(); return PresentationOperation.Failed("TargetBusy", source.name); }
            run.Operation = new PresentationOperation(() => Release(run));
            run.Cancel = run.Operation.Cancel;
            current = run; source.Track(run.Cancel);
            context.Scope.Own(run.Cancel);
            if (!context.Scope.IsActive) return run.Operation;
            // Sample the requested shot/focus once; this is a cue, not a continuous target-follow controller.
            Vector3 position = targetShot != null ? targetShot.transform.position : source.BasePosition;
            Quaternion rotation = targetShot != null ? targetShot.transform.rotation : source.BaseRotation;
            if (kind == CameraActionKind.Focus)
            {
                position = source.BasePosition;
                rotation = Quaternion.LookRotation(focus.position + focusOffset - position, Vector3.up);
            }
            float fov = targetShot != null && kind != CameraActionKind.Focus ? targetShot.FieldOfView : source.BaseFieldOfView;
            float size = targetShot != null && kind != CameraActionKind.Focus ? targetShot.OrthographicSize : source.BaseOrthographicSize;
            try
            {
                var cancel = StartRoutine(Guard(run, Execute(run, markers, position, rotation, fov, size)));
                if (run.Operation.Status == PresentationStatus.Pending) run.CancelRoutine = cancel; else cancel();
            }
            catch (Exception exception) { Fail(run, exception); }
            return run.Operation;
        }

        private IEnumerator Execute(RunState run, IMarkerSink markers, Vector3 position, Quaternion rotation, float fov, float size)
        {
            if (run.Shake)
            {
                float elapsed = 0;
                while (elapsed < duration)
                {
                    elapsed = Mathf.Min(duration, elapsed + StepTime());
                    float wave = elapsed * shakeFrequency * Mathf.PI * 2;
                    float fade = 1 - elapsed / duration;
                    var noise = new Vector3(Mathf.Sin(wave), Mathf.Sin(wave * 1.37f), Mathf.Sin(wave * 1.79f)) * fade;
                    run.Lease.TryWrite(() => run.Rig.SetShake(Vector3.Scale(shakePosition, noise),
                        Quaternion.Euler(Vector3.Scale(shakeEuler, noise))));
                    yield return null;
                }
            }
            else
            {
                var move = Move(run, position, rotation, fov, size, duration);
                while (move.MoveNext()) yield return move.Current;
                markers.Emit("Arrived");
                if (run.Operation.Status != PresentationStatus.Pending) yield break;
                if (kind != CameraActionKind.Return)
                {
                    float elapsed = 0;
                    while (elapsed < holdDuration) { elapsed += StepTime(); yield return null; }
                    var baseline = run.Rig.DefaultShot;
                    var back = Move(run, baseline.transform.position, baseline.transform.rotation,
                        baseline.FieldOfView, baseline.OrthographicSize, returnDuration);
                    while (back.MoveNext()) yield return back.Current;
                }
            }
            // Restore this channel before announcing Settled; the other channel can still be playing independently.
            run.CancelRoutine = null;
            Release(run);
            markers.Emit("Settled");
            run.Operation.TryComplete();
        }

        private IEnumerator Move(RunState run, Vector3 position, Quaternion rotation, float fov, float size, float seconds)
        {
            var rig = run.Rig;
            var fromPosition = rig.BasePosition; var fromRotation = rig.BaseRotation;
            float fromFov = rig.BaseFieldOfView, fromSize = rig.BaseOrthographicSize;
            float elapsed = 0;
            do
            {
                elapsed = seconds == 0 ? 0 : Mathf.Min(seconds, elapsed + StepTime());
                float t = seconds == 0 ? 1 : elapsed / seconds; t = t * t * (3 - 2 * t);
                run.Lease.TryWrite(() => rig.SetPose(Vector3.Lerp(fromPosition, position, t), Quaternion.Slerp(fromRotation, rotation, t),
                    Mathf.Lerp(fromFov, fov, t), Mathf.Lerp(fromSize, size, t)));
                if (seconds > 0) yield return null;
            } while (elapsed < seconds);
        }

        // Catch coroutine failures as operation failures; a Unity coroutine exception must not leave the batch pending.
        private IEnumerator Guard(RunState run, IEnumerator body)
        {
            while (run.Operation.Status == PresentationStatus.Pending)
            {
                if (run.Rig == null || !run.Rig.isActiveAndEnabled) { run.Operation.Cancel(); yield break; }
                bool more = false; Exception failure = null;
                try
                {
                    if (!run.Lease.IsCurrent || !run.Rig.HasOutputControl ||
                        !run.Rig.ValidateConfiguration(out _)) throw new InvalidOperationException("Camera rig or ownership was lost.");
                    more = body.MoveNext();
                }
                catch (Exception exception) { failure = exception; }
                if (failure != null) { Fail(run, failure); yield break; }
                if (!more) yield break;
                yield return body.Current;
            }
        }
        private float StepTime()
        {
            float value = DeltaTime;
            if (!Finite(value) || value < 0) throw new InvalidOperationException("Camera clock must return a finite, nonnegative delta.");
            return value;
        }
        private void Release(RunState run)
        {
            var cancelRoutine = run.CancelRoutine; run.CancelRoutine = null;
            cancelRoutine?.Invoke();
            if (run.Rig != null) run.Rig.Untrack(run.Cancel);
            run.Scope.Dispose();
            if (ReferenceEquals(current, run)) current = null;
        }
        private void Fail(RunState run, Exception exception)
        { Release(run); run.Operation.TryFail("CameraActionFailed", exception.Message, exception); }
        private void ReleaseCurrent() { current?.Operation.Cancel(); }
        public override void Synchronize(ActionSyncContext context) => ReleaseCurrent();
        private void OnDisable() => ReleaseCurrent();
        private static Transform ResolveTransform(object target) => target as Transform ??
            (target as GameObject)?.transform ?? (target as Component)?.transform;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
