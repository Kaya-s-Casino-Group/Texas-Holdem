using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    [CreateAssetMenu(menuName = "玩法/战局/怪物配置", fileName = "EnemyBattleProfile")]
    public sealed class EnemyBattleProfile : BattleActorProfile
    {
        [SerializeField, Min(0), InspectorName("基础攻击伤害")] private int attackDamage = 7;
        public BattleEnemySetup BuildSetup(ParticipantId id) => new BattleEnemySetup(id, BuildCombatant(), attackDamage, BuildDamageRules());
    }
}
