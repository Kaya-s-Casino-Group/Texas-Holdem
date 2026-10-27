using CardPresentation;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.BattlePresentation
{
    /// <summary>Temporary view-only arrangement controls; no rule phase or command dependency.</summary>
    public sealed class BattleHandOrderPanel : MonoBehaviour
    {
        [SerializeField] private WorldCardZonePresentation hand;
        [SerializeField] private Button rank, suit;
        public void Configure(WorldCardZonePresentation source, Button rankButton, Button suitButton)
        {
            Unbind(); hand = source; rank = rankButton; suit = suitButton;
            if (isActiveAndEnabled) Bind();
        }
        public void SortByRank() { if (hand != null) hand.SortByRank(); }
        public void SortBySuit() { if (hand != null) hand.SortBySuit(); }
        private void Bind() { rank?.onClick.AddListener(SortByRank); suit?.onClick.AddListener(SortBySuit); }
        private void Unbind() { rank?.onClick.RemoveListener(SortByRank); suit?.onClick.RemoveListener(SortBySuit); }
        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();
        private void LateUpdate()
        {
            bool available = hand != null && hand.CanReorder && !hand.IsReordering;
            if (rank != null) rank.interactable = available;
            if (suit != null) suit.interactable = available;
        }
    }
}
