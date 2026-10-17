using CardInteraction;
using UnityEngine;

namespace CardPresentation.Tests
{
    public sealed class TestCardHoverSource : MonoBehaviour, ICardHoverSource
    {
        public bool HasTarget;
        public CardHoverTarget Target;
        public bool TryGetHoveredCard(out CardHoverTarget target) { target = Target; return HasTarget; }
    }
}
