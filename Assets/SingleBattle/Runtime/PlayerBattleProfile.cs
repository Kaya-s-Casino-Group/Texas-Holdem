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
        public BattlePlayerRules BuildRules() => new BattlePlayerRules(maxEnergy, initialEnergy, playEnergyCost, discardEnergyCost,
            handSize, discards, new CardDuelDealPolicy(playRefillTiming, discardRefillTiming), unlimitedDiscards, maxPlayCards, maxDiscardCards);
        // Null deck means an independent standard 52-card deck. A Run supplies this player's current entries.
        public BattlePlayerSetup BuildSetup(ParticipantId id, IEnumerable<DeckEntry> deck = null, int? initialHealth = null) =>
            new BattlePlayerSetup(id, BuildCombatant(), deck, initialHealth, BuildRules());
    }
}
