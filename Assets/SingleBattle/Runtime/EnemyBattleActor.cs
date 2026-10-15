using System;
using UnityEngine;
using PokerEngine;

namespace Poker.SingleBattle
{
    /// <summary>Derived intent, not another stored combat or damage state.</summary>
    public sealed class EnemyBattleIntent
    {
        public EnemyActionPreview Action { get; }
        public ParticipantId Source => Action.Source;
        // CardDuel revision belongs to commands; Action.ExpectedRevision belongs to the rule-layer forecast.
        public long ExpectedRevision { get; }
        internal EnemyBattleIntent(EnemyActionPreview action, long revision)
        { Action = action; ExpectedRevision = revision; }
    }
    /// <summary>Enemy-only intent/turn policy. The current policy chooses one fixed attack; the owner validates and resolves it.</summary>
    public sealed class EnemyBattleActor : BattleActor
    {
        [SerializeField, InspectorName("怪物配置")] private EnemyBattleProfile profile;
        public EnemyBattleProfile Profile => profile;
        public void ConfigureProfile(EnemyBattleProfile configuration) => profile = configuration != null ? configuration : throw new ArgumentNullException(nameof(configuration));
        public BattleEnemySetup CreateSetup(ParticipantId id) => profile != null ? profile.BuildSetup(id) : throw new InvalidOperationException("Bind an enemy profile first.");
        protected override bool OwnsParticipant(CardDuelState state, ParticipantId id) => Battle.OwnsEnemy(this, id);
        public bool CanAct => isActiveAndEnabled && IsBound && IsAlive && Battle.isActiveAndEnabled &&
            Battle.State.Duel.Phase == ScoredDuelPhase.AwaitingEnemyAction && Battle.State.Duel.NextEnemy == ParticipantId;
        public EnemyBattleIntent ReadIntent()
        {
            if (!IsBound || !IsAlive || Battle.State.Duel.IsFinished) return null;
            var state = Battle.State;
            var preview = Battle.Engine.Duel.PreviewEnemyAction(state.Duel, ParticipantId);
            return preview == null ? null : new EnemyBattleIntent(preview, state.Revision);
        }
        internal CardDuelTransition ExecuteTurn()
        {
            return CanAct ? Battle.ExecuteEnemyTurn(this, Battle.State.Revision) : null;
        }
    }
}
