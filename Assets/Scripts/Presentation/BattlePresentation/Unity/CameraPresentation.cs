using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattlePresentation.Unity
{
    /// <summary>Composes world-space framing and camera-local shake into one camera output. No effect transforms.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraPresentation : MonoBehaviour
    {
        private Camera outputCamera;
        private CameraShot defaultShot;
        private ScenePresentationDirector director;
        public CameraSceneConfiguration SceneConfiguration { get; private set; }
        private readonly HashSet<Action> cancellations = new HashSet<Action>();
        private ScenePresentationDirector subscribedDirector;
        private readonly PresentationChannels channels = new PresentationChannels();
        private PresentationScope outputScope;
        private ChannelLease outputLease;
        private int activeContributions;
        private Vector3 basePosition, shakeOffset;
        private Quaternion baseRotation = Quaternion.identity, shakeRotation = Quaternion.identity;
        private float baseFieldOfView, baseOrthographicSize;
        public Camera OutputCamera => outputCamera;
        public Vector3 BasePosition => outputScope != null ? basePosition : outputCamera != null ? outputCamera.transform.position : Vector3.zero;
        public Quaternion BaseRotation => outputScope != null ? baseRotation : outputCamera != null ? outputCamera.transform.rotation : Quaternion.identity;
        public float BaseFieldOfView => outputScope != null ? baseFieldOfView : outputCamera != null ? outputCamera.fieldOfView : 40;
        public float BaseOrthographicSize => outputScope != null ? baseOrthographicSize : outputCamera != null ? outputCamera.orthographicSize : 5;
        public Vector3 ShakeOffset => shakeOffset;
        public Quaternion ShakeRotation => shakeRotation;
        internal bool HasOutputControl => outputLease != null && outputLease.IsCurrent;
        public CameraShot DefaultShot => defaultShot;
        public ScenePresentationDirector Director => director;

        public void ValidateSceneBindings(CameraSceneConfiguration configuration)
        {
            if (configuration == null || configuration.gameObject.scene != gameObject.scene)
                throw new ArgumentException("Bind a camera configuration from the same scene.");
            configuration.Validate();
            foreach (var action in GetComponentsInChildren<CameraTimelineAction>(true))
            {
                if (action.Source != this) throw new InvalidOperationException("Camera cue belongs to another executor.");
                configuration.Cue(action.CueKey);
            }
            foreach (var action in GetComponentsInChildren<CameraOpeningTimelineAction>(true))
                if (action.Source != this || configuration.Opening?.shot == null)
                    throw new InvalidOperationException("Opening executor requires this rig and scene opening settings.");
        }

        /// <summary>Called by the battle bridge (or explicit editor preview). Binding alone never moves the camera.</summary>
        public void BindScene(CameraSceneConfiguration configuration, ScenePresentationDirector sceneDirector)
        {
            ValidateSceneBindings(configuration);
            if (sceneDirector == null || sceneDirector.gameObject.scene != gameObject.scene)
                throw new ArgumentException("Bind a director from the same scene.");
            if (activeContributions > 0) ResetPresentation();
            Unsubscribe(); SceneConfiguration = configuration;
            outputCamera = configuration.OutputCamera; defaultShot = configuration.Overview; director = sceneDirector;
            ReadCameraPose();
            foreach (var action in GetComponentsInChildren<CameraTimelineAction>(true)) action.BindScene(configuration.Cue(action.CueKey));
            foreach (var action in GetComponentsInChildren<CameraOpeningTimelineAction>(true)) action.BindScene(configuration.Opening);
            if (isActiveAndEnabled) Subscribe();
        }

        public void Configure(Camera camera, CameraShot baseline,
            ScenePresentationDirector sceneDirector = null)
        {
            ResetPresentation(); Unsubscribe();
            SceneConfiguration = null;
            outputCamera = camera; defaultShot = baseline; director = sceneDirector;
            if (!ValidateConfiguration(out var error)) throw new ArgumentException(error);
            ResetPresentation();
            if (isActiveAndEnabled) Subscribe();
        }

        public bool ValidateConfiguration(out string error)
        {
            error = null;
            if (outputCamera == null || defaultShot == null)
                error = "Camera Present requires an output camera and a default shot.";
            else if (defaultShot.transform == outputCamera.transform || defaultShot.transform.IsChildOf(outputCamera.transform))
                error = "Shot anchors must be independent of the animated camera.";
            else if (outputCamera.gameObject.scene != gameObject.scene || defaultShot.gameObject.scene != gameObject.scene ||
                director != null && director.gameObject.scene != gameObject.scene)
                error = "Camera Present bindings must belong to the same scene.";
            return error == null;
        }

        // Pose and shake are logical channels. Both share a single lease on the actual camera Transform,
        // so another rig or a generic Transform action cannot fight the compositor's output.
        internal bool TryAcquire(bool shake, PresentationScope scope, out ChannelLease lease)
        {
            lease = null;
            string channel = shake ? "shake" : "pose";
            if (outputCamera == null || channels.IsOwned(channel)) return false;
            if (outputScope == null)
            {
                var owner = new PresentationScope("camera-output/" + outputCamera.GetInstanceID());
                if (!TransformPresentationChannels.Pool.TryAcquire(TransformPresentationChannels.Key(outputCamera.transform), owner, out var acquired))
                { owner.Dispose(); return false; }
                outputScope = owner; outputLease = acquired;
                ReadCameraPose(); shakeOffset = Vector3.zero; shakeRotation = Quaternion.identity;
            }
            if (!HasOutputControl) return false;
            if (!channels.TryAcquire(channel, scope, out lease, () => ReleaseContribution(shake))) return false;
            activeContributions++;
            return true;
        }
        private void ReleaseContribution(bool shake)
        {
            if (shake) RestoreShake(); else RestorePose();
            activeContributions--;
            if (activeContributions != 0) return;
            outputScope?.Dispose(); outputScope = null; outputLease = null;
        }
        private void ReadCameraPose()
        {
            if (outputCamera == null) return;
            basePosition = outputCamera.transform.position; baseRotation = outputCamera.transform.rotation;
            baseFieldOfView = outputCamera.fieldOfView; baseOrthographicSize = outputCamera.orthographicSize;
        }
        internal void SetPose(Vector3 position, Quaternion rotation, float fieldOfView, float orthographicSize)
        {
            basePosition = position; baseRotation = rotation;
            baseFieldOfView = fieldOfView; baseOrthographicSize = orthographicSize;
            Compose();
        }
        internal void SetShake(Vector3 offset, Quaternion rotation)
        { shakeOffset = offset; shakeRotation = rotation; Compose(); }
        private void Compose()
        {
            if (outputCamera == null) return;
            if (outputScope != null ? !HasOutputControl : TransformPresentationChannels.Pool.IsOwned(TransformPresentationChannels.Key(outputCamera.transform))) return;
            outputCamera.transform.SetPositionAndRotation(basePosition + baseRotation * shakeOffset, baseRotation * shakeRotation);
            outputCamera.fieldOfView = baseFieldOfView;
            outputCamera.orthographicSize = baseOrthographicSize;
        }
        private void LateUpdate() { if (activeContributions > 0) Compose(); }

        internal void Track(Action cancel) => cancellations.Add(cancel);
        internal void Untrack(Action cancel) => cancellations.Remove(cancel);
        internal void RestorePose()
        {
            if (defaultShot == null || outputCamera == null) return;
            SetPose(defaultShot.transform.position, defaultShot.transform.rotation, defaultShot.FieldOfView, defaultShot.OrthographicSize);
        }
        internal void RestoreShake()
        {
            SetShake(Vector3.zero, Quaternion.identity);
        }
        [ContextMenu("Reset Camera Presentation")]
        public void ResetPresentation()
        {
            foreach (var cancel in new List<Action>(cancellations)) cancel();
            cancellations.Clear(); RestorePose(); RestoreShake();
        }
        private void OnDisplayChanged(DisplayUpdate update) { if (update.IsSynchronization) ResetPresentation(); }
        private void Subscribe()
        {
            Unsubscribe();
            subscribedDirector = director;
            if (subscribedDirector != null) subscribedDirector.Display.Changed += OnDisplayChanged;
        }
        private void Unsubscribe()
        {
            if (subscribedDirector != null) subscribedDirector.Display.Changed -= OnDisplayChanged;
            subscribedDirector = null;
        }
        private void OnEnable() { ReadCameraPose(); Subscribe(); }
        private void OnDisable()
        {
            Unsubscribe();
            // An idle component must not overwrite a camera the designer is editing.
            if (activeContributions > 0) ResetPresentation();
        }
    }
}
