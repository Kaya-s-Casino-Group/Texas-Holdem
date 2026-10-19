using PokerEngine;
namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleSceneAdapter
    {
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
