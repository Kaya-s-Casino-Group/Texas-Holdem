using System.Collections.Generic;
using System.Linq;
using PokerEngine;

namespace Poker.Adventure
{
    public sealed partial class AdventureSceneHost
    {
        private string goldIncomeFeedback = "", goldIncomeRunId;
        private long goldIncomeRevision;

        // A receipt describes an already saved command. Redrawing or restoring never creates one.
        public string GoldIncomeFeedback => Adventure != null && Adventure.RunId == goldIncomeRunId &&
            Adventure.Revision == goldIncomeRevision ? goldIncomeFeedback : "";

        private void ClearGoldIncomeFeedback()
        {
            goldIncomeFeedback = ""; goldIncomeRunId = null;
        }

        private void CaptureGoldIncomeFeedback(BattleAdventureSession previous, BattleAdventureSession next,
            AdventureCommand command, bool battleCommand)
        {
            ClearGoldIncomeFeedback();
            if (previous == null || previous.RunId != next.RunId) return;
            long delta = next.Gold - previous.Gold;
            var lines = new List<string>();
            bool battle = battleCommand || command?.Kind == AdventureCommandKind.EnterNode && next.LastBattleResult != null;
            if (battle)
            {
                long reward = next.Phase == AdventurePhase.Reward ? next.LastRewardGold : 0;
                long effect = delta - reward;
                if (reward > 0) lines.Add("战斗奖励 +" + reward + " 金币");
                if (effect != 0) lines.Add("战斗效果 " + SignedGold(effect) + " 金币");
                if (reward > 0 && effect != 0) lines.Add("净变化 " + SignedGold(delta) + " 金币");
            }
            else if (delta != 0 && command != null)
            {
                string source = null;
                if (command.Kind == AdventureCommandKind.EnterNode && next.LastLocationReward?.Kind == AdventureLocationRewardKind.Chest)
                    source = "宝箱奖励";
                else if (command.Kind == AdventureCommandKind.DrawRestReward && next.LastLocationReward?.Kind == AdventureLocationRewardKind.RestGold)
                    source = "休息抽奖";
                else if (command.Kind == AdventureCommandKind.ChooseRest) source = "休息奖励";
                else if (command.Kind == AdventureCommandKind.SellRelic)
                {
                    var sold = previous.Relics.FirstOrDefault(relic => relic.Id == command.Id);
                    source = "出售道具" + (sold == null ? "" : " · " + ItemName(sold.DefinitionId));
                }
                else if (command.Kind == AdventureCommandKind.Continue && delta > 0)
                {
                    var items = previous.Relics.Where(relic => relic.Charges != 0 &&
                        Content.Relics[relic.DefinitionId].Rules.Item?.LocationLeaveGoldRatio > 0)
                        .Select(relic => ItemName(relic.DefinitionId)).Distinct().ToArray();
                    if (items.Length > 0) source = string.Join("、", items) + " · 离开地点利息";
                }
                if (source != null) lines.Add(source + " " + SignedGold(delta) + " 金币");
            }
            if (lines.Count == 0) return;
            goldIncomeRunId = next.RunId; goldIncomeRevision = next.Revision;
            goldIncomeFeedback = string.Join(" · ", lines) + " · 余额 " + next.Gold;
        }

        private static string SignedGold(long value) => value > 0 ? "+" + value : value.ToString();
    }
}
