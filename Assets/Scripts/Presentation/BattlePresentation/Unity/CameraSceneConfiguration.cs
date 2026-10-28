using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

namespace BattlePresentation.Unity
{
    [Serializable]
    public sealed class CameraOpeningSettings
    {
        public CameraShot shot;
        [Min(0)] public float duration = 1.8f;
        public Ease ease = Ease.OutCubic;
        [Range(.01f, 1)] public float dealStartProgress = .8f;
    }
    [Serializable]
    public sealed class CameraCueSettings
    {
        public string key;
        public CameraActionKind kind;
        public CameraShot shot;
        [Tooltip("Optional; otherwise use the timeline target, then the scene's default focus.")]
        public Transform focusTarget;
        public Vector3 focusOffset;
        [Min(0)] public float duration = .25f;
        [Min(0)] public float holdDuration = .2f;
        [Min(0)] public float returnDuration = .25f;
        public Vector3 shakePosition = new Vector3(.025f, .015f, 0);
        public Vector3 shakeEuler = new Vector3(0, 0, .5f);
        [Min(.01f)] public float shakeFrequency = 18;
    }
    /// <summary>Scene-authored camera output, shot anchors and tuning. No playback or battle lifecycle.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraSceneConfiguration : MonoBehaviour
    {
        [SerializeField] private Camera outputCamera;
        [SerializeField] private CameraShot overview;
        [SerializeField] private Transform defaultFocus;
        [SerializeField] private CameraOpeningSettings opening = new CameraOpeningSettings();
        [SerializeField] private CameraCueSettings[] cues = Array.Empty<CameraCueSettings>();
        public Camera OutputCamera => outputCamera;
        public CameraShot Overview => overview;
        public Transform DefaultFocus => defaultFocus;
        public CameraOpeningSettings Opening => opening;
        public IReadOnlyList<CameraCueSettings> Cues => Array.AsReadOnly(cues ?? Array.Empty<CameraCueSettings>());
        public CameraCueSettings Cue(string key) => Cues.FirstOrDefault(c => c != null && c.key == key) ??
            throw new InvalidOperationException("Unknown scene camera cue: " + key);
        public void Configure(Camera camera, CameraShot baseline, Transform focus, CameraOpeningSettings start, CameraCueSettings[] actions)
        { outputCamera = camera; overview = baseline; defaultFocus = focus; opening = start; cues = actions?.ToArray() ?? Array.Empty<CameraCueSettings>(); }
        public void Validate()
        {
            if (outputCamera == null || outputCamera.gameObject.scene != gameObject.scene)
                throw new InvalidOperationException("Scene Camera requires an output camera in the same scene.");
            ValidateShot(overview);
            if (defaultFocus != null) ValidateAnchor(defaultFocus);
            if (opening != null && opening.shot != null)
            {
                ValidateShot(opening.shot);
                if (!Nonnegative(opening.duration) || !Finite(opening.dealStartProgress) || opening.dealStartProgress <= 0 ||
                    opening.dealStartProgress > 1 || !Enum.IsDefined(typeof(Ease), opening.ease) || opening.ease == Ease.INTERNAL_Custom || opening.ease == Ease.INTERNAL_Zero)
                    throw new InvalidOperationException("Invalid scene opening camera timing or ease.");
            }
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cue in Cues)
            {
                if (cue == null || string.IsNullOrWhiteSpace(cue.key) || !keys.Add(cue.key) || !Enum.IsDefined(typeof(CameraActionKind), cue.kind))
                    throw new InvalidOperationException("Scene camera cues require unique keys and valid action kinds.");
                if (!Nonnegative(cue.duration) || !Nonnegative(cue.holdDuration) || !Nonnegative(cue.returnDuration) ||
                    !Finite(cue.shakeFrequency) || cue.shakeFrequency <= 0 || !Finite(cue.focusOffset) || !Finite(cue.shakePosition) || !Finite(cue.shakeEuler))
                    throw new InvalidOperationException("Invalid scene camera cue parameters: " + cue.key);
                if (cue.kind == CameraActionKind.Shot || cue.shot != null) ValidateShot(cue.shot);
                if (cue.focusTarget != null) ValidateAnchor(cue.focusTarget);
            }
        }
        private void ValidateShot(CameraShot shot)
        {
            if (shot == null) throw new InvalidOperationException("Scene camera shot is missing.");
            ValidateAnchor(shot.transform);
            if (!Finite(shot.FieldOfView) || shot.FieldOfView <= 0 || shot.FieldOfView >= 180 ||
                !Finite(shot.OrthographicSize) || shot.OrthographicSize <= 0)
                throw new InvalidOperationException("Invalid lens on scene shot: " + shot.name);
        }
        private void ValidateAnchor(Transform anchor)
        {
            if (anchor.gameObject.scene != gameObject.scene || anchor == outputCamera.transform || anchor.IsChildOf(outputCamera.transform) ||
                !Finite(anchor.position)) throw new InvalidOperationException("Camera anchors must be finite, in the same scene and independent of the output camera.");
        }
        private static bool Nonnegative(float value) => Finite(value) && value >= 0;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
