namespace CardPresentation
{
    /// <summary>Local to one client's view context. Selection is the pending submission, never engine state.</summary>
    public readonly struct CardInteractionState
    {
        public bool IsHovered { get; }
        public bool IsSelected { get; }
        public bool IsRaised => IsHovered || IsSelected;

        public CardInteractionState(bool isHovered = false, bool isSelected = false)
        { IsHovered = isHovered; IsSelected = isSelected; }

        public CardInteractionState WithHovered(bool hovered) => new CardInteractionState(hovered, IsSelected);
        public CardInteractionState WithSelected(bool selected) => new CardInteractionState(IsHovered, selected);
    }
}
