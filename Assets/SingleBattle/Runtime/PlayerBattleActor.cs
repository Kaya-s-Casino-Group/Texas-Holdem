using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CardInteraction;
using CardPresentation;
using PokerEngine;

namespace Poker.SingleBattle
{
    /// <summary>Player-only input/energy access. Submits requests to the battle owner, never resolves damage itself.</summary>
    public sealed class PlayerBattleActor : BattleActor, ICardMatchCommands, ICardScoreSource
    {
        [SerializeField, InspectorName("玩家配置")] private PlayerBattleProfile profile;
        public PlayerBattleProfile Profile => profile;
        public void ConfigureProfile(PlayerBattleProfile configuration) => profile = configuration != null ? configuration : throw new ArgumentNullException(nameof(configuration));
        public BattlePlayerSetup CreateSetup(ParticipantId id, IEnumerable<DeckEntry> deck = null, int? initialHealth = null) =>
            profile != null ? profile.BuildSetup(id, deck, initialHealth) : throw new InvalidOperationException("Bind a player profile first.");
        public BattlePlayerRules Rules => IsBound ? Battle.State.Duel.Player(ParticipantId).Rules : profile?.BuildRules();
        protected override bool OwnsParticipant(CardDuelState state, ParticipantId id) => Battle.OwnsPlayer(this, id);
        public PlayerTurnState Turn => IsBound ? Battle.State.Duel.Player(ParticipantId).Energy : null;
        public bool CanInteract => isActiveAndEnabled && IsBound && IsAlive && Battle.Player == this && Battle.CanReceivePlayerInput;
        public bool CanEndTurn => CanInteract;
        internal CardInstanceId[] SelectedCards() => Battle.Actions.Hand.Cards.Where(card => card.Interaction.IsSelected).Select(card => card.Card.Id).ToArray();
        private bool CanSubmit(CardZonePlayHost hand, bool discard)
        {
            if (!CanInteract || hand != Battle.Actions.Hand || discard && !Battle.State.HasDiscardBudgetFor(ParticipantId) ||
                Turn.Energy < (discard ? Rules.DiscardEnergyCost : Rules.PlayEnergyCost)) return false;
            var ids = SelectedCards();
            return ids.Length >= 1 && ids.Length <= (discard ? Rules.MaxDiscardCards : Rules.MaxPlayCards) && ids.All(id => Battle.State.Zone(CardZoneKinds.Hand, ParticipantId).Cards.Contains(id) &&
                (discard ? Battle.State.Table.GetCard(id).CanDiscard : Battle.State.Table.GetCard(id).CanPlay));
        }
        public bool TryGetCardBasePoints(CardInstanceId card, out long points)
        {
            points = 0;
            if (!IsBound || !Battle.State.Table.Zones.Any(zone => zone.ControllerId == ParticipantId && zone.Cards.Contains(card))) return false;
            var face = Battle.Engine.Face(Battle.State.Table.GetCard(card));
            points = Battle.Engine.Scoring.CardPoints.GetBasePoints(new CardFace(face.Rank, face.Suit));
            return true;
        }
        public bool CanPlay(CardZonePlayHost hand) => CanSubmit(hand, false);
        public bool CanDiscard(CardZonePlayHost hand) => CanSubmit(hand, true);
        public bool EndTurn(bool animate = true) => CanEndTurn && Battle.ExecutePlayerEndTurn(this, animate);
        public int Play(CardZonePlayHost hand, CardZonePlayHost destination, CardZoneTransferPresenter route, bool animate) =>
            CanPlay(hand) ? Battle.ExecutePlayerPlay(this, hand, destination, route, animate) : 0;
        public int Discard(CardZonePlayHost hand, bool animate) => CanDiscard(hand) ? Battle.ExecutePlayerDiscard(this, hand, animate) : 0;
    }
}
