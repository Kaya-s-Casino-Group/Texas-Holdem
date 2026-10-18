using System;
using UnityEngine;

namespace BattlePresentation.Unity
{
    /// <summary>
    /// Replaceable feedback on a dedicated neutral child transform. The base layout belongs on its parent.
    /// Release approaches zero/identity/one on this contribution layer, never a captured layout snapshot.
    /// </summary>
    public sealed class CandidateTransformFeedback : MonoBehaviour, IInteractionFeedback
    {
        [Tooltip("A dedicated child layer with neutral local position, rotation and scale; never the layout root.")]
        [SerializeField] private Transform offsetLayer;
        [SerializeField] private Vector3 positionOffset;
        [SerializeField] private Vector3 eulerOffset;
        [SerializeField] private Vector3 scaleMultiplier = Vector3.one;
        [SerializeField, Min(0)] private float smoothSeconds = .12f;
        private PresentationScope inputScope, contributionScope;
        private ChannelLease lease;
        private bool returning;
        private Vector3 targetPosition, targetScale = Vector3.one;
        private Quaternion targetRotation = Quaternion.identity;
        internal bool CanTransfer => returning || inputScope == null || !inputScope.IsActive;
        internal void ReleaseTransferred() => FinishRelease();
        public void Configure(Transform layer, Vector3 position, Vector3 euler, Vector3 scale, float seconds = .12f)
        { FinishRelease(); offsetLayer = layer; positionOffset = position; eulerOffset = euler; scaleMultiplier = scale; smoothSeconds = seconds; }

        public void Reconcile(CandidatePresentationModel next, PresentationScope scope)
        {
            if (!isActiveAndEnabled || !scope.IsActive) return;
            if (offsetLayer == null) throw new InvalidOperationException(name + " requires a dedicated offset layer.");
            if (float.IsNaN(smoothSeconds) || float.IsInfinity(smoothSeconds) || smoothSeconds < 0)
                throw new InvalidOperationException("Feedback smoothing must be finite and nonnegative.");
            if (lease == null || !lease.IsCurrent)
            {
                contributionScope = new PresentationScope(scope.Id + "/transform-feedback/" + GetInstanceID());
                if (!TransformPresentationChannels.TryAcquireFeedback(offsetLayer, this, contributionScope, out lease, ResetLayer))
                { contributionScope.Dispose(); contributionScope = null; throw new InvalidOperationException(offsetLayer.name + " already has a transform action or feedback owner."); }
            }
            if (!ReferenceEquals(inputScope, scope))
            {
                inputScope = scope;
                scope.Own(() => BeginRelease(scope, ReleaseMode.Replaced));
            }
            returning = false;
            targetPosition = positionOffset; targetRotation = Quaternion.Euler(eulerOffset); targetScale = scaleMultiplier;
            if (smoothSeconds == 0) ApplyTargets();
        }

        public void Release(PresentationScope scope, ReleaseMode mode) => BeginRelease(scope, mode);
        private void BeginRelease(PresentationScope scope, ReleaseMode mode)
        {
            if (!ReferenceEquals(inputScope, scope)) return;
            returning = true; targetPosition = Vector3.zero; targetRotation = Quaternion.identity; targetScale = Vector3.one;
            if (mode == ReleaseMode.Committed || mode == ReleaseMode.Synchronized || !isActiveAndEnabled || smoothSeconds == 0)
                FinishRelease();
        }
        private void Update() => AdvanceFeedback(Time.unscaledDeltaTime);
        public void AdvanceFeedback(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (lease == null || !lease.IsCurrent || offsetLayer == null) return;
            float factor = smoothSeconds <= 0 ? 1 : 1 - Mathf.Exp(-deltaTime * 6 / smoothSeconds);
            lease.TryWrite(() => {
                offsetLayer.localPosition = Vector3.LerpUnclamped(offsetLayer.localPosition, targetPosition, factor);
                offsetLayer.localRotation = Quaternion.SlerpUnclamped(offsetLayer.localRotation, targetRotation, factor);
                offsetLayer.localScale = Vector3.LerpUnclamped(offsetLayer.localScale, targetScale, factor);
            });
            if (returning && (offsetLayer.localPosition - targetPosition).sqrMagnitude < .00000001f &&
                Quaternion.Angle(offsetLayer.localRotation, targetRotation) < .01f &&
                (offsetLayer.localScale - targetScale).sqrMagnitude < .00000001f) FinishRelease();
        }
        private void ApplyTargets() => lease?.TryWrite(() => {
            if (offsetLayer != null) { offsetLayer.localPosition = targetPosition; offsetLayer.localRotation = targetRotation; offsetLayer.localScale = targetScale; }
        });
        private void FinishRelease()
        {
            TransformPresentationChannels.ReleaseFeedback(offsetLayer, this);
            contributionScope?.Dispose(); contributionScope = null; lease = null; inputScope = null; returning = false;
        }
        private void ResetLayer()
        { if (offsetLayer != null) { offsetLayer.localPosition = Vector3.zero; offsetLayer.localRotation = Quaternion.identity; offsetLayer.localScale = Vector3.one; } }
        private void OnDisable() => FinishRelease();
    }
}
