using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using global::BattlePresentation;
using PokerEngine;

namespace Poker.BattlePresentation
{
    /// <summary>The rule adapter owns localization, identity and forecasts. Renderers receive values only.</summary>
    public static class BattleDisplayProjector
    {
        public static BattleDisplaySnapshot Build(CardDuelState state, CardDuelEngine engine, ParticipantId observer,
            DisplayCursor cursor = null)
        {
            var duel = state.Duel;
            var resources = new List<ResourceDisplayData> {
                new ResourceDisplayData("", "started", state.HasStarted ? 1 : 0),
                new ResourceDisplayData("", "totalScore", duel.TotalScore),
                new ResourceDisplayData("", "roundLimit", engine.Duel.Definition.RoundLimit) };
            foreach (var p in duel.Players)
            {
                resources.Add(new ResourceDisplayData(p.Id.Value, "gold", state.GoldFor(p.Id)));
                resources.Add(new ResourceDisplayData(p.Id.Value, "actions", p.Rules.Flow.UsesTurnFlow ? state.FlowFor(p.Id).ActionsRemaining : p.Energy.Energy));
                resources.Add(new ResourceDisplayData(p.Id.Value, "discards", state.RemainingDiscardBudgetFor(p.Id) ?? -1));
                resources.Add(new ResourceDisplayData(p.Id.Value, "maxSelection", p.Rules.MaxSelection));
                resources.Add(new ResourceDisplayData(p.Id.Value, "canAct", state.HasStarted && duel.HasPlayerWindow(p.Id) ? 1 : 0));
                resources.Add(new ResourceDisplayData(p.Id.Value, "playCost", engine.Duel.QueryActionCost(duel, p.Id, ScoredDuelActionKind.ScoredHand).Cost));
                resources.Add(new ResourceDisplayData(p.Id.Value, "discardCost", engine.Duel.QueryActionCost(duel, p.Id, ScoredDuelActionKind.Discard).Cost));
            }
            var labels = new Dictionary<string, string> { ["statusSummary"] = BattlePresentationRuleText.Snapshot(state, observer, engine.Duel.Definition.StatusCatalog) };
            // Separate read-only presentation channels: status badges belong to their actor,
            // while the side rail contains only the observing player's relics.
            foreach (var unit in duel.Combat.Combatants)
                labels["status/" + unit.Id.Value] = string.Join("\n", duel.Combat.Statuses
                    .Where(s => s.Target.Kind == EntityKind.Participant && s.Target.Id == unit.Id.Value)
                    .Select(s => BattlePresentationRuleText.Status(s, engine.Duel.Definition.StatusCatalog)));
            labels["relics"] = BattlePresentationRuleText.Relics(state, observer);
            var wrath = BattleWrathDisplayProjector.Add(duel, engine.Duel, resources, labels);
            foreach (var enemy in duel.Enemies)
            {
                if (!duel.Combat.Get(enemy.Id).IsAlive || duel.IsFinished ||
                    !duel.Players.Any(player => duel.Combat.Get(player.Id).IsAlive)) continue;
                var preview = engine.Duel.PreviewEnemyAction(duel, enemy.Id, includeWrathUltimate: wrath == null || enemy.Id != wrath.BodyId);
                string skill = enemy.BehaviorState?.Intent?.Name;
                labels["intent/" + enemy.Id.Value] = preview == null || !preview.Accepted ? "意图不可用" : preview.IsSkipped ? "眩晕 · 跳过行动" :
                    (string.IsNullOrEmpty(skill) ? "" : skill + "\n") +
                    (wrath != null && enemy.Id == wrath.BodyId ? "攻击力 " + wrath.Attack + "\n" : AttackGrowth(enemy, duel.CompletedRounds)) +
                    string.Join("\n", BattlePresentationRuleText.Events(preview.RuleEvents, duel, engine.Duel.Definition.StatusCatalog, true));
                if (wrath != null && enemy.Id == wrath.BodyId)
                {
                    var hits = preview?.Hits.Where(hit => hit.Effect.Source == enemy.Id &&
                        duel.Players.Any(player => player.Id == hit.Effect.Target)).ToArray();
                    labels["wrath.current"] = preview == null || !preview.Accepted ? "当前常规动作预览不可用" :
                        preview.IsSkipped ? "眩晕 · 本轮跳过行动" : hits.Length == 0 ? "当前常规动作：" + skill :
                        "当前常规动作：" + skill + " · " + string.Join(" + ", hits.Select(hit =>
                            "伤害 " + hit.SettledDamage + "（扣血 " + hit.HealthLost + " / 护盾 " + hit.BlockAbsorbed + "）"));
                }
            }
            if (duel.Scores.Count > 0) {
                labels["lastScorePattern"] = HandName(duel.Scores.Last().Category);
                labels["lastScoreTotal"] = duel.Scores.Last().Total.ToString(CultureInfo.InvariantCulture);
            }
            if (duel.IsFinished) labels["resultReason"] = engine.Duel.ReadProgress(duel).Result.Reason.ToString();
            var zones = state.Table.Zones.Select(zone => new CardZoneDisplayData(zone.Id.Value,
                zone.ControllerId?.Value, zone.Kind, zone.Cards.Select(id => {
                    var card = state.Table.GetCard(id); var face = engine.Face(card);
                    bool hidden = zone.Kind == CardZoneKinds.Deck || zone.Kind == CardZoneKinds.Hand && zone.ControllerId != observer;
                    return new CardDisplayData(hidden ? "hidden/" + zone.Id.Value + "/" + zone.Cards.ToList().IndexOf(id) : id.Value,
                        id.Value, card.DefinitionId.Value, hidden ? null : StandardArt(face), hidden,
                        disabledReason: !hidden && card.IsDisabled ? "disabled" : null,
                        properties: hidden ? null : new Dictionary<string, string> { ["effects"] = ((int)card.Effects).ToString(CultureInfo.InvariantCulture) });
                })));
            string phase = duel.Phase == ScoredDuelPhase.Won ? "won" : duel.Phase == ScoredDuelPhase.Lost ? "lost" :
                duel.Phase == ScoredDuelPhase.AwaitingEnemyAction ? "enemy" : "player";
            return new BattleDisplaySnapshot(state.Table.MatchId.ToString(), observer.Value, state.Revision, phase,
                duel.CompletedRounds, duel.IsFinished ? (duel.Phase == ScoredDuelPhase.Won ? "Won" : "Lost") : null,
                duel.Combat.Combatants.Select(unit => Unit(unit, duel, state)), zones, resources, cursor,
                hasStarted: state.HasStarted, completedRounds: duel.CompletedRounds, labels: labels);
        }
        private static string AttackGrowth(BattleEnemySetup enemy, int completedRounds)
        {
            if (!(enemy.Behavior?.Attack > 0)) return "";
            try
            {
                return "攻击力 " + enemy.AttackAtRound(completedRounds) +
                    (enemy.Behavior.AttackPerRound > 0 ? " · 每回合结束 +" + enemy.Behavior.AttackPerRound : "") + "\n";
            }
            catch (OverflowException) { return ""; }
        }
        public static UnitDisplayData Unit(CombatantState unit, TeamBattleState duel, CardDuelState state = null)
        {
            var player = duel.Players.FirstOrDefault(p => p.Id == unit.Id);
            // Only full cursor snapshots carry phase poses. Intermediate damage facts omit
            // the label so receiving a hit cannot overwrite the currently displayed pose.
            Dictionary<string, string> labels = null;
            if (player != null && state != null)
            {
                string pose = "idle";
                if (state.HasStarted && !duel.IsFinished)
                {
                    var window = state.FlowFor(player.Id).Window;
                    pose = duel.Phase == ScoredDuelPhase.AwaitingEnemyAction ? "guard" :
                        !player.Rules.Flow.ManualDraw ? "action" :
                        window == BattlePlayerWindow.Draw || window == BattlePlayerWindow.ForcedDiscard ? "discard" :
                        window == BattlePlayerWindow.Ending ? "guard" : "action";
                }
                labels = new Dictionary<string, string> { ["portraitPose"] = pose };
            }
            return new UnitDisplayData(unit.Id.Value, BattlePresentationRuleText.Participant(duel, unit.Id),
                player == null ? "enemy" : "player", unit.Health, unit.MaxHealth, unit.Block,
                player?.Energy.Energy ?? 0, player?.Energy.MaxEnergy ?? 0,
                duel.Combat.Statuses.Where(s => s.Target.Kind == EntityKind.Participant && s.Target.Id == unit.Id.Value)
                    .Select(s => new StatusDisplayData(s.DefinitionId.Value, s.Stacks)), labels);
        }
        public static string StandardArt(CardDefinition card)
        {
            string rank = card.Rank == Rank.Ace ? "a" : card.Rank == Rank.King ? "k" : card.Rank == Rank.Queen ? "q" :
                card.Rank == Rank.Jack ? "j" : ((int)card.Rank).ToString("00", CultureInfo.InvariantCulture);
            return card.Suit.ToString().ToLowerInvariant() + "_" + rank;
        }
        public static string HandName(HandCategory category)
        {
            switch(category) {
                case HandCategory.HighCard: return "高牌"; case HandCategory.OnePair: return "一对";
                case HandCategory.TwoPair: return "两对"; case HandCategory.ThreeOfAKind: return "三条";
                case HandCategory.Straight: return "顺子"; case HandCategory.Flush: return "同花";
                case HandCategory.FullHouse: return "葫芦"; case HandCategory.FourOfAKind: return "四条";
                case HandCategory.StraightFlush: return "同花顺"; default: return category.ToString();
            }
        }
    }
}
