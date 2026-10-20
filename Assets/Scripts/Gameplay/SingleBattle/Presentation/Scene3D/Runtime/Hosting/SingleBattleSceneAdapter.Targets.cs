using System;
using System.Collections.Generic;
using Poker.BattlePresentation;
using PokerEngine;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleSceneAdapter
    {
        private readonly BattleTargetSelection playTargets = new BattleTargetSelection();
        public IReadOnlyList<BattleTargetChoice> PlayTargetChoices => playTargets.Choices;
        public bool CanChoosePlayTarget => CanReceivePlayerInput;
        private string PlayTargetSelectionKey => playTargets.Key;

        private BattleTargetBindings ResolvePlayTargetBindings(IEnumerable<CardInstanceId> selected) =>
            playTargets.Reconcile(AuthorityState, Engine, playerActor.ParticipantId, selected);

        public bool SelectPlayTarget(string slot, string participantId)
        {
            if (!CanChoosePlayTarget) return false;
            ResolvePlayTargetBindings(actions.Hand.CaptureSelection().Cards);
            if (!playTargets.Select(slot, participantId)) return false;
            candidateKey = null; RefreshCandidate(); StateChanged?.Invoke(); actions.Refresh();
            return true;
        }

        internal bool CanPlayWithTargets(ParticipantId actor, IEnumerable<CardInstanceId> selected)
        {
            if (!Playback.CanPlay(actor, selected)) return false;
            var preview = PreviewPlayWithTargets(actor, selected);
            return preview != null && preview.Accepted;
        }

        private CardDuelTransition PreviewPlayWithTargets(ParticipantId actor, IEnumerable<CardInstanceId> selected)
        {
            var bindings = ResolvePlayTargetBindings(selected);
            return Playback.PreviewPlay(actor, selected, BattleInteractionQueries.LegacyTargets(AuthorityState, actor, bindings),
                BattleInteractionQueries.EffectBindings(AuthorityState, actor, bindings));
        }
    }
}
