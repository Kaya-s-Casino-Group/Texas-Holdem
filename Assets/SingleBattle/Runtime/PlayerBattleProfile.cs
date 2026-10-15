using System.Collections.Generic;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    [CreateAssetMenu(menuName = "玩法/战局/玩家配置", fileName = "PlayerBattleProfile")]
    public sealed class PlayerBattleProfile : BattleActorProfile
    {
        [Header("玩家能量")]
        [SerializeField, Min(1), InspectorName("能量上限 / 每轮恢复值")] private int maxEnergy = 3;
        [SerializeField, Min(0), InspectorName("初始能量（可超过上限）")] private int initialEnergy = 3;
        [SerializeField, Min(1), InspectorName("出牌能耗")] private int playEnergyCost = 1;
        [SerializeField, Min(1), InspectorName("弃牌能耗")] private int discardEnergyCost = 1;
        [Header("玩家牌组与发牌")]
        [SerializeField, Range(1, 52), InspectorName("手牌上限")] private int handSize = 8;
        [SerializeField, Range(0, 1000), InspectorName("战局弃牌次数")] private int discards = 3;
        [SerializeField, InspectorName("不限制战局弃牌次数（仍消耗能量）")] private bool unlimitedDiscards;
        [SerializeField, Range(1, 5), InspectorName("单次出牌张数上限")] private int maxPlayCards = 5;
        [SerializeField, Range(1, 52), InspectorName("单次弃牌张数上限")] private int maxDiscardCards = 5;
        [SerializeField, InspectorName("出牌后补牌时机")] private CardDuelRefillTiming playRefillTiming = CardDuelRefillTiming.NextPlayerTurn;
        [SerializeField, InspectorName("弃牌后补牌时机")] private CardDuelRefillTiming discardRefillTiming = CardDuelRefillTiming.AfterAction;
        [Header("玩家出牌与弃牌规则（开战时生成快照）")]
        [SerializeField, InspectorName("出牌伤害配置")] private ScoredHandDamageProfile damage;
        public ScoredHandDamageProfile Damage => damage;
        public void ConfigureDamage(ScoredHandDamageProfile profile) => damage = profile != null ? profile : throw new System.ArgumentNullException(nameof(profile));
        [SerializeField, InspectorName("弃牌收益配置（空为无收益）")] private DiscardRewardProfile discardReward;
        public DiscardRewardProfile DiscardReward => discardReward;
        public void ConfigureDiscardReward(DiscardRewardProfile profile) => discardReward = profile;
        public BattlePlayerRules BuildRules() => new BattlePlayerRules(maxEnergy, initialEnergy, playEnergyCost, discardEnergyCost,
            handSize, discards, new CardDuelDealPolicy(playRefillTiming, discardRefillTiming), unlimitedDiscards, maxPlayCards, maxDiscardCards,
            discardReward: discardReward != null ? discardReward.BuildRule() ?? throw new System.InvalidOperationException("弃牌收益配置返回了空规则。") : null,
            damagePolicy: damage != null ? damage.BuildRule() ?? throw new System.InvalidOperationException("出牌伤害配置返回了空规则。")
                : throw new System.InvalidOperationException("请先为玩家绑定独立的出牌伤害配置。"));
        // Null deck means an independent standard 52-card deck. A Run supplies this player's current entries.
        public BattlePlayerSetup BuildSetup(ParticipantId id, IEnumerable<DeckEntry> deck = null, int? initialHealth = null) =>
            new BattlePlayerSetup(id, BuildCombatant(), deck, initialHealth, BuildRules(), BuildDamageRules());
    }
}
