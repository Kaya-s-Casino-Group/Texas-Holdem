using System;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    [CreateAssetMenu(menuName = "玩法/角色/承伤修正（倍率与固定值）", fileName = "IncomingDamageModifier")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Poker.SingleBattle", sourceAssembly: "Poker.SingleBattle.Runtime", sourceClassName: "ScaledIncomingDamageProfile")]
    public sealed class ScaledIncomingDamageProfile : IncomingDamageModifierProfile
    {
        [SerializeField, InspectorName("规则版本")] private string version = "incoming.scale.1";
        [SerializeField, Min(0), InspectorName("承伤倍率")] private float multiplier = 1;
        [SerializeField, InspectorName("固定增减承伤"), Tooltip("在格挡前应用：输出 = floor(max(0, 输入伤害 × 倍率 + 固定值))。负数减少承伤。")]
        private long flatAdjustment;
        public override IIncomingDamageModifier BuildRule()
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0)
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            return new ScaledIncomingDamageModifier((decimal)multiplier, flatAdjustment, version);
        }
    }
}
