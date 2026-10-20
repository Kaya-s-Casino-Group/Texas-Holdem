using System;
using System.Linq;
using CardPresentation;
using PokerEngine;
namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleSceneAdapter
    {
        private int SelectionLimit()
        {
            var rules = playerActor.Rules;
            if (!rules.Flow.ManualDraw) return rules.MaxSelection;
            switch (State.FlowFor(playerActor.ParticipantId).Window)
            {
                case BattlePlayerWindow.Draw:
                case BattlePlayerWindow.ForcedDiscard:
                    return Math.Max(1, PlayerZone(CardZoneKinds.Hand).Cards.Count);
                case BattlePlayerWindow.Ending:
                    return rules.MaxDiscardCards;
                default:
                    return Math.Max(1, Engine.PlayLimit(State, playerActor.ParticipantId, false));
            }
        }

        private void RefreshSelectionLimit()
        {
            if (State == null || playerActor == null || actions?.Hand == null || !actions.Hand.IsRunning) return;
            var hand = actions.Hand;
            int limit = SelectionLimit();
            hand.GetComponent<HandZoneView>().SetMaxSelectedCards(limit);
            var selected = hand.Cards.Where(card => card.Interaction.IsSelected).ToArray();
            if (selected.Length <= limit) return;
            // A draw-stage discard selection cannot carry more cards into the action stage.
            foreach (var card in selected)
                hand.SetInteraction(card.Id, new CardInteractionState(isHovered: card.Interaction.IsHovered));
            hand.Publish();
        }

        public bool DrawCards(int count, bool animate = true) => SubmitFlow(CardDuelAction.Draw, count, false, animate);
        public bool SkipDraw(bool animate = true) => SubmitFlow(CardDuelAction.SkipDraw, 0, false, animate);
        public bool DiscardBeforeDraw(bool animate = true) => SubmitFlow(CardDuelAction.DiscardForDraw, 0, true, animate);
        public bool ConfirmOverflow(bool animate = true) => SubmitFlow(CardDuelAction.ConfirmOverflow, 0, true, animate);
        private bool SubmitFlow(CardDuelAction action, int count, bool useSelection, bool animate)
        {
            if (!CanReceivePlayerInput) return false;
            var cards = useSelection ? actions.Hand.CaptureSelection().Cards : null;
            Interaction.BeginCommit();
            var result = Playback.Submit(Player.ParticipantId, action, cards, animate: animate, drawCount: count);
            if (!result.Accepted) { candidateKey = null; RefreshCandidate(); }
            return result.Accepted;
        }
    }
}
