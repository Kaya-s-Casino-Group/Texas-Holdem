using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CardInteraction;
using PokerEngine;

namespace Poker.SingleBattle
{
    /// <summary>Player-only input/energy access. Submits requests to the battle owner, never resolves damage itself.</summary>
    public sealed class PlayerBattleActor : BattleActor, ICardMatchCommands, ICardScoreSource
    {
        public BattlePlayerRules Rules => IsBound ? Battle.PresentedState.Duel.Player(ParticipantId).Rules : null;
        protected override bool OwnsParticipant(CardDuelState state, ParticipantId id) => Battle.OwnsPlayer(this, id);
        public PlayerTurnState Turn => IsBound ? Battle.PresentedState.Duel.Player(ParticipantId).Energy : null;
        public bool CanInteract => isActiveAndEnabled && IsBound && IsAlive && Battle.Player == this && Battle.CanReceivePlayerInput;
        public bool CanEndTurn => CanInteract;
        private bool CanSubmit(CardSelectionRequest request, bool discard)
        {
            if (!CanInteract || request == null) return false;
            var state = Battle.AuthorityState;
            if (request.MatchId != state.Table.MatchId ||
                request.ControllerId != ParticipantId || request.ZoneId != state.Zone(CardZoneKinds.Hand, ParticipantId).Id) return false;
            return discard ? Battle.Playback.CanDiscard(ParticipantId, request.Cards) : Battle.Playback.CanPlay(ParticipantId, request.Cards);
        }
        public bool TryGetCardBasePoints(CardInstanceId card, out long points)
        {
            points = 0;
            if (!IsBound || !Battle.PresentedState.Table.Zones.Any(zone => zone.ControllerId == ParticipantId && zone.Cards.Contains(card))) return false;
            var face = Battle.Engine.Face(Battle.PresentedState.Table.GetCard(card));
            points = Battle.Engine.Scoring.CardPoints.GetBasePoints(new CardFace(face.Rank, face.Suit));
            return true;
        }
        public bool CanPlay(CardSelectionRequest request) => CanSubmit(request, false);
        public bool CanDiscard(CardSelectionRequest request) => CanSubmit(request, true);
        public bool EndTurn(bool animate = true) => CanEndTurn && Battle.ExecutePlayerEndTurn(this, animate);
        public int Play(CardSelectionRequest request, bool animate) =>
            CanPlay(request) ? Battle.ExecutePlayerPlay(this, request, animate) : 0;
        public int Discard(CardSelectionRequest request, bool animate) => CanDiscard(request) ? Battle.ExecutePlayerDiscard(this, request, animate) : 0;
    }
}
