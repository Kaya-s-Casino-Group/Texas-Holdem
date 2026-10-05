using DG.Tweening;
using UnityEngine;

namespace CardPresentation
{
    public sealed partial class CardInstanceView
    {
        public Transform TransferRoot { get; private set; }
        public bool IsTransferring => transferTween != null && transferTween.IsActive();
        private Tween transferTween;
        private readonly object transferTweenId = new object();
        private WorldPose transferStart;
        private Vector3 transferControl, transferUp;
        private bool hasTransferControl;
        private float transferClock, transferHeight, transferFlightSeconds, transferReorderAt, transferReorderSeconds, transferReorderLift;
        private Transform transferLandingAnchor;

        internal struct WorldPose
        {
            public Vector3 Position, Scale;
            public Quaternion Rotation;
        }
        internal WorldPose CaptureWorldPose() => new WorldPose {
            Position = SwayRoot.position, Rotation = SwayRoot.rotation, Scale = SwayRoot.lossyScale
        };

        internal void BeginTransfer(WorldPose start, CardZoneTransferSettings settings, float delay,
            Vector3 up, Vector3? control, Transform landingAnchor = null, float waitForBatch = 0, float reorderLift = 0)
        {
            CompleteTransfer();
            transferStart = start; transferClock = 0; transferHeight = settings.ArcHeight;
            transferUp = up; hasTransferControl = control.HasValue; transferControl = control.GetValueOrDefault();
            transferLandingAnchor = landingAnchor;
            transferFlightSeconds = settings.FlightSeconds;
            bool rearrange = landingAnchor != null;
            transferReorderAt = settings.FlightSeconds + (rearrange ? waitForBatch + settings.LandingPause : 0);
            transferReorderSeconds = rearrange ? settings.ResolvedReorderSeconds : 0;
            transferReorderLift = reorderLift;
            float duration = transferReorderAt + transferReorderSeconds;
            // A single local clock covers flight, the batch arrival barrier, and reordering.
            // Large frames can cross all phases without callbacks starting a second tween a frame late.
            transferTween = DOTween.To(() => transferClock, value => {
                transferClock = value; RefreshTransferPose();
            }, duration, duration).SetDelay(delay).SetEase(Ease.Linear)
                .SetUpdate(UpdateType.Manual).SetAutoKill(true).SetRecyclable(false)
                .SetTarget(TransferRoot).SetId(transferTweenId)
                .OnComplete(() => { transferTween = null; transferLandingAnchor = null; ResetTransferPose(); });
            transferTween.ForceInit(); RefreshTransferPose();
        }

        private void AdvanceTransfer(float deltaTime)
        {
            if (!IsTransferring) return;
            transferTween.ManualUpdate(deltaTime, deltaTime);
            // Also hold the source world pose during stagger delays and track a moved destination.
            RefreshTransferPose();
        }

        internal void RefreshTransferPose()
        {
            if (!IsTransferring) return;
            ResetTransferPose();
            var end = CaptureWorldPose();
            // Landing anchors are the other cards' logical layout roots, never their animated children.
            var landing = transferLandingAnchor != null ? transferLandingAnchor.position : end.Position;
            var control = hasTransferControl ? transferControl :
                (transferStart.Position + landing) * .5f + transferUp * (2 * transferHeight);
            float t = EaseTransfer(Mathf.Clamp01(transferClock / transferFlightSeconds));
            var position = (1 - t) * (1 - t) * transferStart.Position + 2 * (1 - t) * t * control + t * t * landing;
            if (transferReorderSeconds > 0 && transferClock >= transferReorderAt)
            {
                float progress = EaseTransfer(Mathf.Clamp01((transferClock - transferReorderAt) / transferReorderSeconds));
                // Small, distinct lift per card keeps cards from occupying the same plane when swapping places.
                position = Vector3.Lerp(landing, end.Position, progress) + transferUp * (4 * progress * (1 - progress) * transferReorderLift);
            }
            var rotation = Quaternion.Slerp(transferStart.Rotation, end.Rotation, t);
            var scale = Vector3.Lerp(transferStart.Scale, end.Scale, t);
            TransferRoot.localScale = new Vector3(scale.x / end.Scale.x, scale.y / end.Scale.y, scale.z / end.Scale.z);
            TransferRoot.rotation = rotation * Quaternion.Inverse(end.Rotation) * TransferRoot.rotation;
            TransferRoot.position += position - SwayRoot.position;
        }
        private static float EaseTransfer(float value) => .5f - .5f * Mathf.Cos(Mathf.PI * value);

        private void ResetTransferPose()
        {
            if (TransferRoot == null) return;
#if UNITY_EDITOR
            // Undo can disable sibling components while their shared GameObject is being restored.
            // Kill the tween now, but wait until Unity allows transform writes again.
            if (UnityEditor.Undo.isProcessing)
            {
                UnityEditor.EditorApplication.delayCall -= ResetTransferAfterUndo;
                UnityEditor.EditorApplication.delayCall += ResetTransferAfterUndo;
                return;
            }
#endif
            TransferRoot.localPosition = Vector3.zero; TransferRoot.localRotation = Quaternion.identity;
            TransferRoot.localScale = Vector3.one;
        }
#if UNITY_EDITOR
        private void ResetTransferAfterUndo() { if (this != null && !IsTransferring) ResetTransferPose(); }
#endif
        internal void CompleteTransfer()
        {
            if (transferTween != null) DOTween.Kill(transferTweenId);
            transferTween = null; transferLandingAnchor = null; ResetTransferPose();
        }
    }
}
