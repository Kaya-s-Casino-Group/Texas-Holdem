using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    [CreateAssetMenu(menuName = "玩法/战局/玩家配置", fileName = "PlayerBattleProfile")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Poker.SingleBattle", sourceAssembly: "Poker.SingleBattle.Runtime", sourceClassName: "PlayerBattleProfile")]
    public sealed class PlayerBattleProfile : BattleActorProfile
    {
        [System.Serializable]
        private sealed class HandLevelEntry
        {
            [InspectorName("牌型")] public HandCategory category;
            [Min(1), InspectorName("等级")] public int level = 1;
            public HandLevelEntry(HandCategory category, int level = 1) { this.category = category; this.level = level; }
        }
        [Header("个人牌型等级（开战时生成快照）")]
        [SerializeField, InspectorName("各牌型等级"), Tooltip("每个牌型独立配置，最低 1 级；未列出的牌型默认 1 级。修改后下次开战生效。")]
        private HandLevelEntry[] handLevels = System.Enum.GetValues(typeof(HandCategory)).Cast<HandCategory>()
            .Select(category => new HandLevelEntry(category)).ToArray();
        public BattleHandLevels BuildHandLevels() => new BattleHandLevels(
            (handLevels ?? new HandLevelEntry[0]).Select(entry => entry != null
                ? new KeyValuePair<HandCategory, int>(entry.category, entry.level)
                : throw new System.InvalidOperationException("个人牌型等级不能包含空条目。")));
        public void ConfigureHandLevels(BattleHandLevels levels)
        {
            if (levels == null) throw new System.ArgumentNullException(nameof(levels));
            handLevels = levels.Levels.OrderBy(entry => entry.Key).Select(entry => new HandLevelEntry(entry.Key, entry.Value)).ToArray();
        }
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
        [SerializeField, InspectorName("友方目标策略（空为自身或指定队友）")] private AllyTargetPolicyProfile supportTargets;
        public AllyTargetPolicyProfile SupportTargets => supportTargets;
        public void ConfigureSupportTargets(AllyTargetPolicyProfile policy) => supportTargets = policy;
        public override BattleActorTargetRules BuildTargetRules() => new BattleActorTargetRules(BuildAttackTargetPolicy(),
            supportTargets != null ? supportTargets.BuildPolicy() ?? throw new System.InvalidOperationException("友方目标配置返回了空策略。") : null);
        public DiscardRewardProfile DiscardReward => discardReward;
        public void ConfigureDiscardReward(DiscardRewardProfile profile) => discardReward = profile;
        public BattlePlayerRules BuildRules() => new BattlePlayerRules(maxEnergy, initialEnergy, playEnergyCost, discardEnergyCost,
            handSize, discards, new CardDuelDealPolicy(playRefillTiming, discardRefillTiming), unlimitedDiscards, maxPlayCards, maxDiscardCards,
            discardReward: discardReward != null ? discardReward.BuildRule() ?? throw new System.InvalidOperationException("弃牌收益配置返回了空规则。") : null,
            damagePolicy: damage != null ? damage.BuildRule() ?? throw new System.InvalidOperationException("出牌伤害配置返回了空规则。")
                : throw new System.InvalidOperationException("请先为玩家绑定独立的出牌伤害配置。"),
            handLevels: BuildHandLevels());
        // Null deck means an independent standard 52-card deck. A Run supplies this player's current entries.
        public BattlePlayerSetup BuildSetup(ParticipantId id, IEnumerable<DeckEntry> deck = null, int? initialHealth = null) =>
            new BattlePlayerSetup(id, BuildCombatant(), deck, initialHealth, BuildRules(), BuildDamageRules(), BuildTargetRules());
    }
}
