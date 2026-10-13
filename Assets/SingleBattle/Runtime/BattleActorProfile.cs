using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Shared authoring data for vitals. Current HP/block live only in CombatantState.</summary>
    public abstract class BattleActorProfile : ScriptableObject
    {
        [SerializeField, InspectorName("单位定义 ID")] private string unitId = "unit";
        [SerializeField, Min(1), InspectorName("最大生命")] private int maxHealth = 40;
        [SerializeField, Min(0), InspectorName("初始格挡")] private int initialBlock;
        public CombatantDefinition BuildCombatant() => new CombatantDefinition(unitId, maxHealth, initialBlock);
    }
}
