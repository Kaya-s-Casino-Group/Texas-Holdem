using System.Linq;
using CardInteraction;
using CardPresentation;
using PokerEngine;

namespace Poker.SingleBattle
{
    /// <summary>Player-only input/energy access. Submits requests to the battle owner, never resolves damage itself.</summary>
    public sealed class PlayerBattleActor : BattleActor, ICardMatchCommands
    {
        protected override bool OwnsParticipant(CardDuelState state, ParticipantId id) => state.Duel.PlayerId == id;
        public PlayerTurnState Turn => IsBound ? Battle.State.Duel.PlayerTurn : null;
        public bool CanInteract => isActiveAndEnabled && IsBound && IsAlive && Battle.CanReceivePlayerInput;
        public bool CanEndTurn => CanInteract;
        internal CardInstanceId[] SelectedCards() => Battle.Actions.Hand.Cards.Where(card => card.Interaction.IsSelected).Select(card => card.Card.Id).ToArray();
        private bool CanSubmit(CardZonePlayHost hand, bool discard)
        {
            if (!CanInteract || hand != Battle.Actions.Hand || discard && Battle.State.DiscardsRemaining == 0 ||
                Turn.Energy < (discard ? Battle.Engine.Duel.Definition.DiscardEnergyCost : Battle.Engine.Duel.Definition.PlayEnergyCost)) return false;
            var ids = SelectedCards();
            return ids.Length >= 1 && ids.Length <= Battle.Engine.MaxSelection && ids.All(id => Battle.State.Zone(CardZoneKinds.Hand).Cards.Contains(id) &&
                (discard ? Battle.State.Table.GetCard(id).CanDiscard : Battle.State.Table.GetCard(id).CanPlay));
        }
        public bool CanPlay(CardZonePlayHost hand) => CanSubmit(hand, false);
        public bool CanDiscard(CardZonePlayHost hand) => CanSubmit(hand, true);
        public bool EndTurn(bool animate = true) => CanEndTurn && Battle.ExecutePlayerEndTurn(this, animate);
        public int Play(CardZonePlayHost hand, CardZonePlayHost destination, CardZoneTransferPresenter route, bool animate) =>
            CanPlay(hand) ? Battle.ExecutePlayerPlay(this, hand, destination, route, animate) : 0;
        public int Discard(CardZonePlayHost hand, bool animate) => CanDiscard(hand) ? Battle.ExecutePlayerDiscard(this, hand, animate) : 0;
    }
}
