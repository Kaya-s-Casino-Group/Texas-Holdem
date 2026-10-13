using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using PokerEngine;

namespace Poker.SingleBattle
{
    /// <summary>Derived intent, not another stored combat or damage state.</summary>
    public sealed class EnemyBattleIntent
    {
        public ParticipantId Source { get; }
        public IReadOnlyList<ParticipantId> Targets { get; }
        public ParticipantId Target => Targets[0];
        public long Damage { get; }
        public long ExpectedRevision { get; }
        internal EnemyBattleIntent(ParticipantId source, IReadOnlyList<ParticipantId> targets, long damage, long revision)
        { Source = source; Targets = targets; Damage = damage; ExpectedRevision = revision; }
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
        public EnemyBattleIntent ReadIntent() => IsBound && IsAlive && !Battle.State.Duel.IsFinished
            ? new EnemyBattleIntent(ParticipantId, Battle.Engine.Duel.Targets(Battle.State.Duel, ParticipantId), Battle.State.Duel.Enemies.Single(x => x.Id == ParticipantId).Damage, Battle.State.Revision) : null;
        internal CardDuelTransition ExecuteTurn()
        {
            var intent = ReadIntent();
            return CanAct && intent != null ? Battle.ExecuteEnemyTurn(this, intent.ExpectedRevision) : null;
        }
    }
}
