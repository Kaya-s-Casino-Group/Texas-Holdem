using System.Linq;
using PokerEngine;
using TMPro;
using UIFoundation;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Read-only selected-hand projection. Numeric labels receive only the resulting numbers.</summary>
    public sealed class HandCandidatePresenter : MonoBehaviour
    {
        [SerializeField] private SingleBattleHost host;
        [SerializeField] private TMP_Text handName, details;
        [SerializeField] private RollingNumberText chips, multiplier;
        private CardDuelState cachedState;
        private ParticipantId cachedPlayer;
        private CardInstanceId[] cachedSelection;
        public CardDuelTransition Candidate { get; private set; }
        public RollingNumberText Chips => chips;
        public RollingNumberText Multiplier => multiplier;
        public string HandText => handName != null ? handName.text : "";

        public void Configure(SingleBattleHost owner, TMP_Text name, TMP_Text info, RollingNumberText points, RollingNumberText mult)
        {
            if (owner == null || name == null || info == null || points == null || mult == null)
                throw new System.ArgumentException("Bind the battle, labels and numeric views.");
            host = owner; handName = name; details = info; chips = points; multiplier = mult;
            cachedState = null; cachedSelection = null; Refresh();
        }
        public void Refresh()
        {
            if (host == null || handName == null || details == null || chips == null || multiplier == null) return;
            if (host.State == null || host.Player == null || !host.Player.IsBound || !host.Actions.Hand.IsRunning)
            { ShowEmpty(0); cachedState = null; return; }
            var selected = host.Actions.Hand.Cards.Where(card => card.Interaction.IsSelected)
                .Select(card => card.Card.Id).OrderBy(id => id.Value, System.StringComparer.Ordinal).ToArray();
            var player = host.Player.ParticipantId;
            if (ReferenceEquals(cachedState, host.State) && cachedPlayer == player && cachedSelection != null && cachedSelection.SequenceEqual(selected)) return;
            cachedState = host.State; cachedPlayer = player; cachedSelection = selected;
            if (selected.Length == 0 || host.State.Duel.IsFinished) { ShowEmpty(selected.Length); return; }
            Candidate = host.Engine.PreviewHand(host.State, player, selected);
            if (!Candidate.Accepted)
            {
                bool overflow = Candidate.Error == CardDuelError.Overflow;
                ShowEmpty(selected.Length);
                handName.text = overflow ? "数值超限" : "无法判定";
                return;
            }
            handName.text = SingleBattleHud.HandName(Candidate.Pattern.Category);
            int scored = Candidate.Pattern.ScoringCards.Count(id => CardEffectRules.CanScore(host.State.Table.GetCard(id).Effects));
            details.text = "选中 " + selected.Length + " · 计分 " + scored;
            chips.SetTarget(Candidate.Score.Points); multiplier.SetTarget(Candidate.Score.Multiplier);
        }
        private void ShowEmpty(int count)
        {
            Candidate = null; handName.text = "未选牌"; details.text = "选中 " + count + " · 计分 0";
            chips.SetTarget(0); multiplier.SetTarget(0);
        }
        private void OnEnable() { cachedState = null; cachedSelection = null; Refresh(); }
        private void LateUpdate() => Refresh();
    }
}
