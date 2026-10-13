using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    [CreateAssetMenu(menuName = "玩法/战局/公共规则", fileName = "BattleRulesProfile")]
    public sealed class BattleRulesProfile : ScriptableObject
    {
        [SerializeField, InspectorName("规则版本")] private string version = "team-battle.demo.1";
        [SerializeField, Range(1, 1000), InspectorName("回合上限")] private int roundLimit = 8;
        [SerializeField, Min(1), InspectorName("每点伤害所需分数")] private int pointsPerDamage = 5;
        public BattleRules BuildRules() => new BattleRules(version, new ScoreDamagePolicy(pointsPerDamage), roundLimit);
    }
}
