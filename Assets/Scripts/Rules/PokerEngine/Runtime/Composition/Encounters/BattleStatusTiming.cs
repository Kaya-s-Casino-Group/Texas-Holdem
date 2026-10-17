using System;
using System.Globalization;

namespace PokerEngine
{
    /// <summary>Binds mode-neutral lifetime declarations to this mode's stable player/enemy phase boundaries.</summary>
    public static class BattleStatusTiming
    {
        public const string PlayerPhase = "player";
        public const string EnemyPhase = "enemy";

        public static string RoundScope(TeamBattleState state, int roundsAhead = 0)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (roundsAhead < 0) throw new ArgumentOutOfRangeException(nameof(roundsAhead));
            return state.Combat.Id + "/round/" + checked(state.CompletedRounds + 1 + roundsAhead).ToString("D10", CultureInfo.InvariantCulture);
        }

        public static StatusDuration Bind(TeamBattleState state, StatusLifetimeSpec lifetime)
        {
            if (state == null || lifetime == null) throw new ArgumentNullException();
            switch (lifetime.Kind)
            {
                case StatusLifetimeKind.UntilRemoved: return StatusDuration.UntilRemoved();
                case StatusLifetimeKind.TriggerCount: return StatusDuration.ForTriggers(lifetime.Count.Value);
                case StatusLifetimeKind.ThroughCurrentRound: return StatusDuration.UntilRoundEnd(RoundScope(state));
                case StatusLifetimeKind.ThroughNextPhase:
                    if (lifetime.PhaseKey != PlayerPhase && lifetime.PhaseKey != EnemyPhase)
                        throw new ArgumentException("This battle supports player and enemy phase lifetimes only.");
                    // The upcoming complete phase: player -> enemy is this round; all other combinations start next round.
                    int ahead = state.Phase == ScoredDuelPhase.AwaitingPlayerHand && lifetime.PhaseKey == EnemyPhase ? 0 : 1;
                    return StatusDuration.UntilPhaseEnd(RoundScope(state, ahead), lifetime.PhaseKey);
                default: throw new ArgumentException("Unknown status lifetime.");
            }
        }
    }
}
