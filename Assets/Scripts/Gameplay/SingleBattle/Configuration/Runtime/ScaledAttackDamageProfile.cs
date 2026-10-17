using System;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    [CreateAssetMenu(menuName = "玩法/角色/攻击修正（倍率与固定值）", fileName = "AttackDamageModifier")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Poker.SingleBattle", sourceAssembly: "Poker.SingleBattle.Runtime", sourceClassName: "ScaledAttackDamageProfile")]
    public sealed class ScaledAttackDamageProfile : AttackDamageModifierProfile
    {
        [SerializeField, InspectorName("规则版本")] private string version = "attack.scale.1";
        [SerializeField, Min(0), InspectorName("伤害倍率")] private float multiplier = 1;
        [SerializeField, InspectorName("固定增减伤害"), Tooltip("输出 = floor(max(0, 输入伤害 × 倍率 + 固定值))。正数增伤，负数减伤。")]
        private long flatAdjustment;
        public override IAttackDamageModifier BuildRule()
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0)
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            return new ScaledAttackDamageModifier((decimal)multiplier, flatAdjustment, version);
        }
    }
}
