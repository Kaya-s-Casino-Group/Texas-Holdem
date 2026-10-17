using System;
using System.Collections.Generic;
using PokerEngine;

namespace Poker.SingleBattle
{
    /// <summary>Builds an undealt rules session from authored rules and explicit participant setups.</summary>
    public static class BattleSessionFactory
    {
        public static string CreateBattleId(string prefix = "battle") => prefix + "/" + Guid.NewGuid().ToString("N");

        /// <summary>Captures the rule profiles once. The caller chooses when to submit Start and how to present it.</summary>
        public static BattleSession Create(BattleRulesProfile rules, IEnumerable<BattlePlayerSetup> players,
            IEnumerable<BattleEnemySetup> enemies, string battleId = null, RandomState random = null)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            var engine = new CardDuelEngine(rules.BuildRules(), scoring: rules.BuildScoring());
            var initial = engine.Create(battleId ?? CreateBattleId(), random ?? HmacDeckShuffler.CreateRandomState(),
                players, enemies);
            return new BattleSession(engine, initial);
        }
    }
}
