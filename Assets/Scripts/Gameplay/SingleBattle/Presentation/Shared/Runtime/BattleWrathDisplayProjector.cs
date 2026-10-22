using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using global::BattlePresentation;
using PokerEngine;

namespace Poker.BattlePresentation
{
    /// <summary>Boss-only display channels. All timing comes from the immutable rule snapshot.</summary>
    internal static class BattleWrathDisplayProjector
    {
        internal static WrathBossView Add(TeamBattleState state, TeamBattleEngine engine,
            IList<ResourceDisplayData> resources, IDictionary<string, string> labels)
        {
            var view = WrathBossQuery.Read(state);
            if (view == null) return null;
            string bodyId = view.BodyId.Value;
            labels["wrath.body"] = bodyId;
            labels["wrath.opening"] = WrathPromptText.Opening;
            resources.Add(new ResourceDisplayData(bodyId, "wrath.enemyPhaseActive",
                state.Phase == ScoredDuelPhase.AwaitingEnemyAction && state.PendingEnemies.Count > 0 ? 1 : 0));
            resources.Add(new ResourceDisplayData(bodyId, "wrath.attack", view.Attack));
            resources.Add(new ResourceDisplayData(bodyId, "wrath.charges", view.Charges));
            resources.Add(new ResourceDisplayData(bodyId, "wrath.warning", !state.IsFinished && view.WarningActive ? 1 : 0));
            resources.Add(new ResourceDisplayData(bodyId, "wrath.releaseRound", view.ReleaseRound ?? -1));
            resources.Add(new ResourceDisplayData(bodyId, "wrath.cooldown", view.CooldownRemaining));
            var body = state.Enemies.Single(e => e.Id == view.BodyId);
            var definition = body.Behavior.Wrath;
            labels["wrath.attack"] = "攻击力 " + view.Attack + " · 蓄力 " + view.Charges + " 次 · 存活手 " + view.Hands.Count(h => h.IsAlive) + "/5";
            labels["wrath.ultimate"] = state.IsFinished ? "战斗结束" : view.WarningActive
                ? WrathPromptText.Ultimate + "\n第 " + view.ReleaseRound + " 轮常规动作后追加 · 断手或眩晕本体可打断"
                : view.CooldownRemaining > 0 ? "大招冷却 " + view.CooldownRemaining + " 轮 · 五手齐全才能预警"
                : "大招已就绪 · 等待五手齐全且本体能行动后预警";
            long damage = view.WarningActive ? view.PendingUltimateBaseDamage : view.UltimateBaseDamage;
            resources.Add(new ResourceDisplayData(bodyId, "wrath.ultimateDamage", damage));
            labels["wrath.ultimate"] += "\n" + (view.WarningActive ? "按当前状态预计基础伤害 " : "当前基础伤害 ") + damage +
                "（" + definition.UltimateScale.ToString("0.##", CultureInfo.InvariantCulture) + "D）";
            // Timeline checkpoints can show a lethal hit before a queued reviver restores the player.
            // Keep the authoritative boss data, but do not resolve future targets in that gap.
            var regular = state.Players.Any(player => state.Combat.Get(player.Id).IsAlive)
                ? engine.PreviewEnemyIntents(state, view.BodyId) : System.Array.Empty<EnemyBehaviorAction>();
            for (int i = 0; i < regular.Count; i++)
            {
                labels["wrath.intent/" + i.ToString(CultureInfo.InvariantCulture)] = regular[i]?.Name ?? "无行动";
                labels["wrath.intentId/" + i.ToString(CultureInfo.InvariantCulture)] = regular[i]?.Id ?? "";
            }
            int bloodIndex = 0;
            foreach (var hand in view.Hands)
            {
                string id = hand.Id.Value;
                resources.Add(new ResourceDisplayData(id, "wrath.reviveRemaining", hand.ReviveRemaining));
                resources.Add(new ResourceDisplayData(id, "wrath.reviveRound", hand.ReviveRound ?? -1));
                labels["wrath.role/" + id] = hand.Role == WrathHandRole.Binding ? "binding" : "blood";
                labels["wrath.name/" + id] = hand.Role == WrathHandRole.Binding ? "束缚手" : "饮血手 " + ++bloodIndex;
                labels["wrath.hand/" + id] = hand.IsAlive
                    ? (hand.Role == WrathHandRole.Binding ? "束缚 · 第5/10/15/20轮" : "饮血 · 每轮治疗本体5")
                    : state.IsFinished ? "已断手" : hand.ReviveRemaining == 0 ? "已断手 · 本轮敌方阶段再生" :
                        "已断手 · 再生剩余 " + hand.ReviveRemaining + " 轮";
            }
            return view;
        }
    }
}
