using CardPresentation;

namespace CardInteraction
{
    /// <summary>
    /// The host owns interaction state on each local card, keyed by its stable view identity.
    /// Implementations write card data only; republish the zone after the controller's StateChanged event.
    /// </summary>
    public interface ICardInteractionSource
    {
        bool TryGetInteraction(string viewId, out CardInteractionState state);
        bool CanSelect(string viewId);
        void SetInteraction(string viewId, CardInteractionState state);
    }
}
