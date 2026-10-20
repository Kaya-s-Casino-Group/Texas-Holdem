using PokerEngine;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleSceneAdapter
    {
        // Item usability is independent of card energy costs. The authority enforces the player action window.
        private bool CanReceiveItemInput => isActiveAndEnabled && SceneHost.isActiveAndEnabled &&
            State != null && ReferenceEquals(State, AuthorityState) && !IsPresenting &&
            playerActor != null && match != null && match.IsRunning && ReferenceEquals(match.Commands, playerActor) &&
            actions != null && actions.Hand != null && actions.Hand.IsRunning && Playback != null;

        public bool CanUseItem(string instanceId) =>
            CanReceiveItemInput && Playback.CanUseItem(playerActor.ParticipantId, instanceId);

        public BattleSessionResult PreviewItem(string instanceId, string targetInstanceId = null) =>
            CanReceiveItemInput ? Playback.PreviewItem(playerActor.ParticipantId, instanceId, targetInstanceId) : null;

        public BattleSessionResult UseItem(string instanceId, string targetInstanceId = null, bool animate = true)
        {
            if (!CanReceiveItemInput) return null;
            Interaction.BeginCommit();
            var result = Playback.UseItem(AuthorityState.Revision, playerActor.ParticipantId, instanceId, targetInstanceId, animate);
            if (!result.Accepted) { candidateKey = null; RefreshCandidate(); Publish(); }
            return result;
        }
    }
}
