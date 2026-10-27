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
                resources.Add(new ResourceDisplayData(p.Id.Value, "discards", state.RemainingDiscardBudgetFor(p.Id) ?? -1));
                resources.Add(new ResourceDisplayData(p.Id.Value, "maxSelection", p.Rules.MaxSelection));
                resources.Add(new ResourceDisplayData(p.Id.Value, "canAct", state.HasStarted && duel.HasPlayerWindow(p.Id) ? 1 : 0));
                resources.Add(new ResourceDisplayData(p.Id.Value, "playCost", engine.Duel.QueryActionCost(duel, p.Id, ScoredDuelActionKind.ScoredHand).Cost));
                resources.Add(new ResourceDisplayData(p.Id.Value, "discardCost", engine.Duel.QueryActionCost(duel, p.Id, ScoredDuelActionKind.Discard).Cost));
            }
            var labels = new Dictionary<string, string> { ["statusSummary"] = BattlePresentationRuleText.Snapshot(state, observer, engine.Duel.Definition.StatusCatalog) };
            foreach (var enemy in duel.Enemies)
            {
                if (!duel.Combat.Get(enemy.Id).IsAlive || duel.IsFinished) continue;
                var preview = engine.Duel.PreviewEnemyAction(duel, enemy.Id);
                labels["intent/" + enemy.Id.Value] = preview == null || !preview.Accepted ? "意图不可用" : preview.IsSkipped ? "眩晕 · 跳过行动" :
                    string.Join("\n", BattlePresentationRuleText.Events(preview.RuleEvents, duel, engine.Duel.Definition.StatusCatalog, true));
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
                        properties: hidden ? null : new Dictionary<string, string> {
                            ["effects"] = ((int)card.Effects).ToString(CultureInfo.InvariantCulture),
                            ["rank"] = ((int)face.Rank).ToString(CultureInfo.InvariantCulture), ["suit"] = face.Suit.ToString() });
                })));
            string phase = duel.Phase == ScoredDuelPhase.Won ? "won" : duel.Phase == ScoredDuelPhase.Lost ? "lost" :
                duel.Phase == ScoredDuelPhase.AwaitingEnemyAction ? "enemy" : "player";
            return new BattleDisplaySnapshot(state.Table.MatchId.ToString(), observer.Value, state.Revision, phase,
                duel.CompletedRounds, duel.IsFinished ? (duel.Phase == ScoredDuelPhase.Won ? "Won" : "Lost") : null,
                duel.Combat.Combatants.Select(unit => Unit(unit, duel)), zones, resources, cursor,
                hasStarted: state.HasStarted, completedRounds: duel.CompletedRounds, labels: labels);
        }
        public static UnitDisplayData Unit(CombatantState unit, TeamBattleState duel)
        {
            var player = duel.Players.FirstOrDefault(p => p.Id == unit.Id);
            return new UnitDisplayData(unit.Id.Value, BattlePresentationRuleText.Participant(duel, unit.Id),
                player == null ? "enemy" : "player", unit.Health, unit.MaxHealth, unit.Block,
                player?.Energy.Energy ?? 0, player?.Energy.MaxEnergy ?? 0,
                duel.Combat.Statuses.Where(s => s.Target.Kind == EntityKind.Participant && s.Target.Id == unit.Id.Value)
                    .Select(s => new StatusDisplayData(s.DefinitionId.Value, s.Stacks)));
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
