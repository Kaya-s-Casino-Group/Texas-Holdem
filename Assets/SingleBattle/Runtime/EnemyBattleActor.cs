using PokerEngine;

namespace Poker.SingleBattle
{
    /// <summary>Derived intent, not another stored combat or damage state.</summary>
    public sealed class EnemyBattleIntent
    {
        public ParticipantId Source { get; }
        public ParticipantId Target { get; }
        public long Damage { get; }
        public long ExpectedRevision { get; }
        internal EnemyBattleIntent(ParticipantId source, ParticipantId target, long damage, long revision)
        { Source = source; Target = target; Damage = damage; ExpectedRevision = revision; }
    }
    /// <summary>Enemy-only intent/turn policy. The current policy chooses one fixed attack; the owner validates and resolves it.</summary>
    public sealed class EnemyBattleActor : BattleActor
    {
        protected override bool OwnsParticipant(CardDuelState state, ParticipantId id) => state.Duel.EnemyId == id;
        public bool CanAct => isActiveAndEnabled && IsBound && IsAlive && Battle.isActiveAndEnabled &&
            Battle.State.Duel.Phase == ScoredDuelPhase.AwaitingEnemyAction;
        public EnemyBattleIntent ReadIntent() => IsBound && IsAlive && !Battle.State.Duel.IsFinished
            ? new EnemyBattleIntent(ParticipantId, Battle.State.Duel.PlayerId, Battle.Engine.Duel.Definition.EnemyDamage, Battle.State.Revision) : null;
        internal CardDuelTransition ExecuteTurn()
        {
            var intent = ReadIntent();
            return CanAct && intent != null ? Battle.ExecuteEnemyTurn(this, intent.ExpectedRevision) : null;
        }
    }
}
