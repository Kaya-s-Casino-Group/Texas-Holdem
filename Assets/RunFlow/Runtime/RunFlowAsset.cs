using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using UnityEngine;

namespace Poker.RunFlow
{
    [Serializable]
    public sealed class RunFlowNodeData
    {
        public string Id;
        public string DisplayName;
        public RunNodeKind Kind = RunNodeKind.Encounter;
        public string StageId;
        public Vector2 Position;
        public long TargetScore = 200;
        public int RoundLimit = 6;
        public long ClearGold = 5;
        public int Energy = 3;
        public List<string> NextNodeIds = new List<string>();
        public List<string> OfferPool = new List<string>();

        public RunNodeDefinition Build()
        {
            if (Kind == RunNodeKind.Encounter || Kind == RunNodeKind.Elite || Kind == RunNodeKind.Boss)
                return new RunEncounterNodeDefinition(Id, new RunStageDefinition(string.IsNullOrWhiteSpace(StageId) ? Id : StageId,
                    TargetScore, RoundLimit, ClearGold, Energy), NextNodeIds, Kind);
            if (Kind == RunNodeKind.Reward) return new RunRewardNodeDefinition(Id, NextNodeIds, OfferPool);
            if (Kind == RunNodeKind.Shop) return new RunShopNodeDefinition(Id, NextNodeIds, OfferPool);
            throw new ArgumentException("未知节点类型：" + Id);
        }
    }

    [Serializable]
    public sealed class RunFlowFloorData
    {
        public string Id;
        public string DisplayName;
        public RunFloorKind Kind = RunFloorKind.Normal;
        public string TreeVersion = "1";
        public string EntryNodeId;
        public List<RunFlowNodeData> Nodes = new List<RunFlowNodeData>();
        public RunFloorDefinition Build()
        {
            try
            {
                if (Nodes == null || Nodes.Any(n => n == null)) throw new ArgumentException("存在空节点。");
                return new RunFloorDefinition(Id, new RunNodeTree(TreeVersion, EntryNodeId, Nodes.Select(n => n.Build())), Kind);
            }
            catch (ArgumentException error)
            { throw new ArgumentException("楼层「" + (string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName) + "」：" + error.Message, error); }
        }
    }

    /// <summary>Authoring asset. Compile once before creating a Run; never use the mutable asset as Run state.</summary>
    [CreateAssetMenu(fileName = "RunFlow", menuName = "Poker/Run Flow")]
    public sealed class RunFlowAsset : ScriptableObject
    {
        public const int CurrentFormatVersion = 1;
        public int FormatVersion = CurrentFormatVersion;
        public string RulesVersion = "run.flow.1";
        public int JokerCapacity = 5;
        public long StartingGold = 10;
        public long RerollCost = 2;
        public int OfferCount = 3;
        public List<string> StartingJokers = new List<string>();
        public List<string> ShopPool = new List<string> { "buy-steady" };
        public List<string> RewardPool = new List<string> { "buy-steady" };
        public List<RunFlowFloorData> Floors = new List<RunFlowFloorData>();

        public RunDefinition BuildDefinition(ScoreProfile scoring)
        {
            if (FormatVersion != CurrentFormatVersion) throw new ArgumentException("不支持的流程资产格式：" + FormatVersion);
            if (scoring == null) throw new ArgumentNullException(nameof(scoring));
            if (Floors == null || Floors.Any(f => f == null)) throw new ArgumentException("存在空楼层。");
            return new RunDefinition(RulesVersion, scoring, Floors.Select(f => f.Build()), ShopPool, RewardPool,
                StartingJokers, JokerCapacity, StartingGold, RerollCost, OfferCount);
        }
        public RunContent BuildContent(ContentCatalog catalog, ScoreProfile scoring, IEnumerable<JokerDefinition> jokers,
            IEnumerable<RunOfferDefinition> offers, string startingDeckVersion, IEnumerable<CardDefinitionId> startingDeck) =>
            new RunContent(catalog, BuildDefinition(scoring), jokers, offers, startingDeckVersion, startingDeck);

        /// <summary>Copies rule data into editable Unity data; no runtime definition or mutable list is shared.</summary>
        public void CopyFrom(RunDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            FormatVersion = CurrentFormatVersion; RulesVersion = definition.Version;
            JokerCapacity = definition.JokerCapacity; StartingGold = definition.StartingGold;
            RerollCost = definition.RerollCost; OfferCount = definition.OfferCount;
            StartingJokers = definition.StartingJokers.ToList(); ShopPool = definition.ShopPool.ToList(); RewardPool = definition.RewardPool.ToList();
            Floors = definition.Floors.Select((floor, index) => new RunFlowFloorData {
                Id = floor.Id, DisplayName = floor.Kind == RunFloorKind.Boss ? "Boss 层" : "第 " + (index + 1) + " 层",
                Kind = floor.Kind, TreeVersion = floor.NodeTree.Version, EntryNodeId = floor.NodeTree.EntryNodeId,
                Nodes = floor.NodeTree.Nodes.Select(node => {
                    var combat = node as RunEncounterNodeDefinition;
                    var reward = node as RunRewardNodeDefinition; var shop = node as RunShopNodeDefinition;
                    return new RunFlowNodeData { Id = node.Id, DisplayName = NodeName(node), Kind = node.Kind,
                        StageId = combat == null ? null : combat.Stage.Id,
                        NextNodeIds = node.NextNodeIds.ToList(),
                        TargetScore = combat == null ? 200 : combat.Stage.TargetScore,
                        RoundLimit = combat == null ? 6 : combat.Stage.RoundLimit,
                        ClearGold = combat == null ? 5 : combat.Stage.ClearGold,
                        Energy = combat == null ? 3 : combat.Stage.Energy,
                        OfferPool = reward != null ? reward.OfferPool.ToList() : shop != null ? shop.OfferPool.ToList() : new List<string>() };
                }).ToList()
            }).ToList();
        }
        private static string NodeName(RunNodeDefinition node)
        {
            if (node.Id == "opening") return "开局战斗";
            switch (node.Kind) {
                case RunNodeKind.Encounter: return "普通战斗"; case RunNodeKind.Elite: return "精英战斗";
                case RunNodeKind.Boss: return "Boss 战斗"; case RunNodeKind.Reward: return "奖励"; case RunNodeKind.Shop: return "商店";
                default: return node.Id;
            }
        }
    }
}
