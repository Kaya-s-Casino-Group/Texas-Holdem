using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using global::BattlePresentation;
using PokerEngine;

namespace Poker.BattlePresentation
{
    /// <summary>The only boundary that translates resolved rule facts into frontend contracts.</summary>
    public sealed class BattlePresentationBridge : IBattleSessionPresentation
    {
        private readonly Func<CardDuelEngine> engine;
        private readonly Func<ParticipantId> observer;
        private readonly IBattlePresentationFrontend frontend;
        private DisplayCursor cursor = DisplayCursor.Empty;
        private readonly Dictionary<DisplayCursor, CardDuelState> checkpoints = new Dictionary<DisplayCursor, CardDuelState>();
        private CardDuelState current;
        private string activeBatch;
        public Action<CardDuelState> BeforeInitialize { get; set; }
        public Action<BattleSessionResult> BeforePresent { get; set; }
        public Action<CardDuelState> BeforeSynchronize { get; set; }
        public bool IsPresenting => frontend.IsPresenting;
        public BattlePresentationBridge(CardDuelEngine engine, ParticipantId observer, IBattlePresentationFrontend frontend)
            : this(() => engine, () => observer, frontend) { }
        public BattlePresentationBridge(Func<CardDuelEngine> engine, Func<ParticipantId> observer, IBattlePresentationFrontend frontend)
        { this.engine = engine ?? throw new ArgumentNullException(nameof(engine)); this.observer = observer ?? throw new ArgumentNullException(nameof(observer)); this.frontend = frontend ?? throw new ArgumentNullException(nameof(frontend)); }
        public void Initialize(CardDuelState state)
        {
            current = state; cursor = DisplayCursor.Empty; checkpoints.Clear(); checkpoints[cursor] = state; activeBatch = null;
            BeforeInitialize?.Invoke(state);
            frontend.Initialize(BattleDisplayProjector.Build(state, engine(), observer(), cursor));
        }
        public void Present(BattleSessionResult result, bool animate)
        {
            if (result == null || !result.Accepted) throw new ArgumentException("Only accepted rule batches can be presented.");
            BeforePresent?.Invoke(result);
            string id = Guid.NewGuid().ToString("N"); activeBatch = id; checkpoints.Clear(); checkpoints[cursor] = result.Before;
            var before = BattleDisplayProjector.Build(result.Before, engine(), observer(), cursor);
            var initial = before; var changes = new List<BattleDisplayChange>();
            // Count only emitted primary damage facts. Plans and previews may contain later hits
            // that never executed, and reactions/periodic damage belong to separate feedback.
            var combos = result.Timeline.Where(entry => entry.RuleEvent is CombatEvent hit &&
                hit.Effect.Kind == CombatEffectKind.Damage && hit.Effect.Amount > 0 &&
                hit.Provenance?.Origin == CombatEffectOrigin.Primary)
                .GroupBy(entry => entry.ActionId)
                .Where(group => group.Count() > 1 && (IsSequentialHand(group) ||
                    group.GroupBy(entry => ((CombatEvent)entry.RuleEvent).Effect.Target).Any(target => target.Count() > 1)))
                .ToDictionary(group => group.Last().Sequence, group => group.Count().ToString(CultureInfo.InvariantCulture) +
                    (IsSequentialHand(group) || group.Select(entry => ((CombatEvent)entry.RuleEvent).Effect.Target).Distinct().Count() == 1 ?
                        "段连击·总伤害" : "次命中·总伤害") +
                    group.Sum(entry => (decimal)((CombatEvent)entry.RuleEvent).HealthLost).ToString(CultureInfo.InvariantCulture));
            foreach (var fact in result.Timeline)
            {
                var nextCursor = new DisplayCursor(id, fact.Sequence);
                var after = BattleDisplayProjector.Build(fact.State, engine(), observer(), nextCursor);
                checkpoints.Add(nextCursor, fact.State);
                var values = new Dictionary<string, string> { ["action"] = fact.Action.ToString(), ["actionId"] = fact.ActionId,
                    ["stepIndex"] = fact.StepIndex.ToString(CultureInfo.InvariantCulture), ["phase"] = fact.PhaseKey ?? "",
                    ["reason"] = fact.Reason ?? "", ["skipped"] = fact.IsActionSkipped ? "true" : "false", ["fromZone"] = fact.FromZone ?? "", ["toZone"] = fact.ToZone ?? "",
                    ["cardOwner"] = fact.CardOwner.Value ?? "", ["dealTrigger"] = fact.DealTrigger?.ToString() ?? "" };
                if (fact.Score != null) {
                    values["score"] = fact.Score.Total.ToString(CultureInfo.InvariantCulture);
                    values["points"] = fact.Score.Points.ToString(CultureInfo.InvariantCulture);
                    values["multiplier"] = fact.Score.Multiplier.ToString(CultureInfo.InvariantCulture);
                    values["pattern"] = BattleDisplayProjector.HandName(fact.Score.Category);
                }
                // Stable presentation metadata from the already resolved action; no frontend evaluates poker rules.
                var handScore = fact.Score ?? (fact.Kind == BattleTimelineKind.EffectSettled && fact.Action == CardDuelAction.Play
                    ? result.Steps[fact.StepIndex].Transition.Score : null);
                if (handScore != null) values["handCategory"] = handScore.Category.ToString();
                if (fact.Action == CardDuelAction.EnemyAction)
                {
                    var resolved = result.Steps[fact.StepIndex].Transition;
                    var primary = resolved.EffectPlan?.Effects ?? resolved.RuleEvents
                        .Where(item => item.Provenance?.Origin == CombatEffectOrigin.Primary && item.Effect != null).Select(item => item.Effect).ToArray();
                    values["enemyMotion"] = primary.Any(item => item.Source == fact.Actor && item.Target != fact.Actor &&
                        (item.Kind == CombatEffectKind.Damage || item.Kind == CombatEffectKind.Eliminate || item.Kind == CombatEffectKind.LoseHealth)) ? "Attack" : "Support";
                }
                if (fact.ActionCost != null) values["cost"] = fact.ActionCost.Cost.ToString(CultureInfo.InvariantCulture);
                if (fact.RuleEvent != null)
                {
                    values["feedback"] = string.Join("\n", BattlePresentationRuleText.Events(new[] { fact.RuleEvent }, fact.State.Duel, engine().Duel.Definition.StatusCatalog));
                    var floating = BattleFloatingFeedbackText.Event(fact.RuleEvent, fact.State.Duel, engine().Duel.Definition.StatusCatalog);
                    if (floating != null) values["floatingFeedback"] = floating;
                }
                if (fact.RelicActivation != null) values["feedback"] = "遗物触发：" + fact.RelicActivation.Before.DefinitionId + " / " + fact.RelicActivation.TriggerId;
                var effect = fact.RuleEvent as CombatEvent;
                if (BattleFloatingFeedbackText.IsDamageImmune(effect)) values["damagePrevented"] = "immune";
                if (combos.TryGetValue(fact.Sequence, out var combo)) values["comboSummary"] = combo;
                if (effect != null) values["effectKey"] = "rule/" + fact.ActionId + "/" + effect.Revision + "/" + effect.SequenceIndex;
                if (fact.RuleEvent?.Provenance != null) values["effectOrigin"] = fact.RuleEvent.Provenance.Origin.ToString();
                var appliedStatus = (fact.RuleEvent as CombatStatusEvent)?.Status;
                var changedStatus = fact.RuleEvent as CombatStatusChangedEvent;
                var status = appliedStatus ?? changedStatus?.StatusAfter ?? changedStatus?.StatusBefore;
                if (status != null)
                {
                    values["statusTarget"] = status.Target.Id;
                    values["statusDefinition"] = status.DefinitionId.Value;
                    values["statusChange"] = appliedStatus != null ? "Applied" : changedStatus.Reason.ToString();
                    values["statusTags"] = (engine().Duel.Definition.StatusCatalog?.Find(status.DefinitionId)?.Tags ?? StatusTags.None).ToString();
                }
                var projectedEffect = effect == null ? null : new EffectDisplayData(effect.Effect.Kind.ToString(), effect.Effect.Source.Value,
                    effect.Effect.Target.Value, effect.SettledAmount, effect.HealthLost, effect.BlockAbsorbed,
                    BattleDisplayProjector.Unit(effect.Before, fact.State.Duel), BattleDisplayProjector.Unit(effect.After, fact.State.Duel), effect.Died);
                changes.Add(new BattleDisplayChange(fact.Sequence, before.Cursor, after, fact.Kind.ToString(), fact.Actor.Value,
                    fact.Targets.Select(t => t.Value), VisibleCards(fact, before, after), values, before, projectedEffect));
                before = after;
            }
            cursor = before.Cursor; current = result.State;
            // The final checkpoint carries the exact authoritative snapshot; no rules are executed here.
            var final = BattleDisplayProjector.Build(result.State, engine(), observer(), cursor);
            frontend.Present(new PresentationBatch(id, initial, changes, final), animate ? PlaybackMode.Animated : PlaybackMode.Instant);
        }
        private static bool IsSequentialHand(IEnumerable<BattleTimelineEntry> entries) => entries.Any(entry => {
            var hand = ((CombatEvent)entry.RuleEvent).Effect.Attack?.Hand;
            return hand?.Pattern.Category == HandCategory.Straight || hand?.Pattern.Category == HandCategory.StraightFlush ||
                hand?.RankPattern.Category == HandCategory.Straight;
        });
        private static IEnumerable<string> VisibleCards(BattleTimelineEntry fact, BattleDisplaySnapshot before, BattleDisplaySnapshot after)
        {
            var requested = (fact.Cards.Count > 0 ? fact.Cards : fact.Selected).Select(id => id.Value).ToArray();
            var afterVisible = new HashSet<string>(after.Zones.SelectMany(zone => zone.Cards)
                .Where(card => !card.Hidden && card.CardId != null).Select(card => card.CardId), StringComparer.Ordinal);
            var beforeVisible = before.Zones.SelectMany(zone => zone.Cards)
                .Where(card => !card.Hidden && card.CardId != null).Select(card => card.CardId).ToArray();
            if (fact.Kind == BattleTimelineKind.DeckRecycled)
            {
                // The source identities were public; their shuffled destination order is private.
                var source = new HashSet<string>(requested, StringComparer.Ordinal);
                return beforeVisible.Where(source.Contains).ToArray();
            }
            var visible = new HashSet<string>(beforeVisible, StringComparer.Ordinal);
            visible.UnionWith(afterVisible);
            return requested.Where(visible.Contains).ToArray();
        }

        public CardDuelState StateAt(DisplayCursor position)
        {
            if (position != null && checkpoints.TryGetValue(position, out var state)) return state;
            throw new InvalidOperationException("Unknown or expired display cursor: " + position);
        }
        public void Synchronize(CardDuelState state)
        {
            current = state; cursor = DisplayCursor.Empty; checkpoints.Clear(); checkpoints[cursor] = state; activeBatch = null;
            BeforeSynchronize?.Invoke(state);
            frontend.Synchronize(BattleDisplayProjector.Build(state, engine(), observer(), cursor));
        }
    }
}
