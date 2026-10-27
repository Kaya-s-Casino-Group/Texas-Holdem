using UnityEngine;

namespace CardPresentation
{
    public sealed partial class CardInstanceView
    {
        public Transform DragRoot { get; private set; }
        private bool dragHeld;
        private Vector3 dragWorldCenter;
        internal void HoldAt(Vector3 worldCenter)
        {
            dragHeld = true; dragWorldCenter = worldCenter; PinDrag();
        }
        internal void ReleaseDrag(bool animate)
        {
            dragHeld = false;
            if (!animate && DragRoot != null) DragRoot.localPosition = Vector3.zero;
        }
        private void PinDrag()
        {
            if (DragRoot != null && surfaceRenderer != null)
                DragRoot.position += dragWorldCenter - surfaceRenderer.bounds.center;
        }
        private void LateUpdate()
        {
            if (DragRoot == null) return;
            if (dragHeld) PinDrag();
            else
            {
                DragRoot.localPosition = Vector3.Lerp(DragRoot.localPosition, Vector3.zero, 1 - Mathf.Exp(-25 * Time.unscaledDeltaTime));
                if (DragRoot.localPosition.sqrMagnitude < .00000001f) DragRoot.localPosition = Vector3.zero;
            }
        }
    }
}
