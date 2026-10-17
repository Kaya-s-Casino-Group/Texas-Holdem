using DG.Tweening;
using UnityEngine;

namespace CardPresentation
{
    public sealed partial class CardInstanceView
    {
        public Transform HoverRoot { get; private set; }
        public float HoverScale => hoverScale;
        public bool IsHoverScaling => hoverTween != null && hoverTween.IsActive();
        private float hoverScale = 1, hoverTarget = 1;
        private Tween hoverTween;
        private readonly object hoverTweenId = new object();

        private void InitializeHoverRoot()
        {
            HoverRoot = new GameObject("Hover").transform;
            HoverRoot.SetParent(FaceRoot, false);
        }

        private void ApplyHoverScale(bool hovered, CardZoneLiftSettings settings, bool animate)
        {
            // Uniform linear scale: width * height grows by 20%, not by 44%.
            float next = hovered ? Mathf.Sqrt(HandZoneView.HoverAreaMultiplier) : 1;
            if (!animate || !settings.Enabled || !isActiveAndEnabled)
            { hoverTarget = next; CompleteHoverScale(); return; }
            if (next == hoverTarget) return;
            KillHoverScale(); hoverTarget = next;
            float duration = next < hoverScale ? settings.LowerSeconds : settings.RaiseSeconds;
            hoverTween = DOTween.To(() => hoverScale, SetHoverScale, next, duration)
                .SetEase(Ease.OutBack, settings.Overshoot).SetUpdate(UpdateType.Manual)
                .SetAutoKill(true).SetRecyclable(false).SetTarget(HoverRoot).SetId(hoverTweenId)
                .OnComplete(() => { hoverTween = null; SetHoverScale(hoverTarget); });
            hoverTween.ForceInit();
        }

        private void SetHoverScale(float value)
        {
            hoverScale = value;
            if (HoverRoot != null) HoverRoot.localScale = Vector3.one * value;
        }

        private void KillHoverScale()
        {
            if (hoverTween != null) DOTween.Kill(hoverTweenId);
            hoverTween = null;
        }

        private void CompleteHoverScale()
        {
            KillHoverScale(); SetHoverScale(hoverTarget);
        }
    }
}
