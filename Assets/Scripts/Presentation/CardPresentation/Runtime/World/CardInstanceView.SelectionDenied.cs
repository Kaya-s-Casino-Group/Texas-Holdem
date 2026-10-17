using DG.Tweening;
using UnityEngine;

namespace CardPresentation
{
    public sealed partial class CardInstanceView
    {
        private Tween selectionDeniedTween;
        private readonly object selectionDeniedTweenId = new object();
        private Vector3 selectionDeniedOffset;
        public bool IsSelectionDeniedAnimating => selectionDeniedTween != null && selectionDeniedTween.IsActive();
        public Vector3 SelectionDeniedOffset => selectionDeniedOffset * PresentationScale;

        internal void BeginSelectionDenied(Vector3 offset, float riseSeconds, float returnSeconds)
        {
            // Repeated refused clicks retarget the current displacement; they never accumulate height.
            KillSelectionDenied();
            selectionDeniedTween = DOTween.Sequence()
                .Append(DOTween.To(() => selectionDeniedOffset, SetSelectionDeniedOffset, offset, riseSeconds).SetEase(Ease.OutQuad))
                .Append(DOTween.To(() => selectionDeniedOffset, SetSelectionDeniedOffset, Vector3.zero, returnSeconds).SetEase(Ease.OutBack, 1.2f))
                .SetUpdate(UpdateType.Manual).SetAutoKill(true).SetRecyclable(false)
                .SetId(selectionDeniedTweenId)
                .OnComplete(() => { selectionDeniedTween = null; SetSelectionDeniedOffset(Vector3.zero); });
            selectionDeniedTween.ForceInit();
        }
        private void SetSelectionDeniedOffset(Vector3 value)
        { selectionDeniedOffset = value; RefreshInteractionPose(); }
        private void KillSelectionDenied()
        {
            if (selectionDeniedTween != null) DOTween.Kill(selectionDeniedTweenId);
            selectionDeniedTween = null;
        }
        internal void CancelSelectionDenied()
        { KillSelectionDenied(); SetSelectionDeniedOffset(Vector3.zero); }
    }
}
