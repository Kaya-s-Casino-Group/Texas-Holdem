using UnityEngine;

namespace CardPresentation
{
    public sealed partial class CardInstanceView
    {
        // A visual contribution, separate from layout and selection. Its owner can never restore old layout data.
        private object selectionActionOwner;
        private Vector3 selectionActionOffset;
        public Vector3 SelectionActionOffset => selectionActionOffset * PresentationScale;
        internal Vector3 ClaimSelectionAction(object owner)
        { selectionActionOwner = owner; return selectionActionOffset; }
        internal bool SetSelectionActionOffset(object owner, Vector3 value)
        {
            if (!ReferenceEquals(owner, selectionActionOwner)) return false;
            selectionActionOffset = value; RefreshInteractionPose(); return true;
        }
        internal void ClearSelectionActionOffset(object owner)
        {
            if (!ReferenceEquals(owner, selectionActionOwner)) return;
            selectionActionOwner = null; selectionActionOffset = Vector3.zero; RefreshInteractionPose();
        }
        private void ConsumeSelectionActionOffset()
        {
            // Discard flight starts at the actual displayed location, with no discontinuity or duplicated offset.
            interactionOffset += selectionActionOffset;
            selectionActionOwner = null; selectionActionOffset = Vector3.zero;
        }
    }
}
