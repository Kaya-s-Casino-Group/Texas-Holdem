using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Shared player/enemy authoring for vitals and unit-owned damage rules. Current vitals live in CombatantState.</summary>
    public abstract class BattleActorProfile : ScriptableObject
    {
        [SerializeField, InspectorName("单位定义 ID")] private string unitId = "unit";
        [SerializeField, Min(1), InspectorName("最大生命")] private int maxHealth = 40;
        [SerializeField, Min(0), InspectorName("初始格挡")] private int initialBlock;
        [Header("角色伤害修正（开战时生成快照）")]
        [SerializeField, InspectorName("攻击修正配置（空为不修正）")] private AttackDamageModifierProfile attackModifier;
        [SerializeField, InspectorName("承伤修正配置（空为不修正）")] private IncomingDamageModifierProfile incomingModifier;
        public AttackDamageModifierProfile AttackModifier => attackModifier;
        public IncomingDamageModifierProfile IncomingModifier => incomingModifier;
        public void ConfigureDamageModifiers(AttackDamageModifierProfile attack, IncomingDamageModifierProfile incoming)
        { attackModifier = attack; incomingModifier = incoming; }
        public BattleActorDamageRules BuildDamageRules() => new BattleActorDamageRules(
            attackModifier != null ? attackModifier.BuildRule() ?? throw new System.InvalidOperationException("攻击修正配置返回了空规则。") : null,
            incomingModifier != null ? incomingModifier.BuildRule() ?? throw new System.InvalidOperationException("承伤修正配置返回了空规则。") : null);
        public CombatantDefinition BuildCombatant() => new CombatantDefinition(unitId, maxHealth, initialBlock);
    }
}
