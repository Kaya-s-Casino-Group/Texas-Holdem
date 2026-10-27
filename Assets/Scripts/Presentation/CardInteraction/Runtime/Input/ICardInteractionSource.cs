using CardPresentation;
using UnityEngine;

namespace CardInteraction
{
    public interface ICardInteractionGate { bool CanInteract { get; } }
    public interface ICardReorderSource
    {
        bool BeginReorder(string viewId, Ray pointer);
        bool UpdateReorder(Ray pointer);
        void EndReorder(bool cancel);
    }
    /// <summary>
    /// The host owns interaction state on each local card, keyed by its stable view identity.
    /// Implementations update local feedback or candidate queries; pointer input never executes rule commands.
    /// </summary>
    public interface ICardInteractionSource
    {
        bool TryGetInteraction(string viewId, out CardInteractionState state);
        bool CanSelect(string viewId);
        void SetInteraction(string viewId, CardInteractionState state);
    }
}
