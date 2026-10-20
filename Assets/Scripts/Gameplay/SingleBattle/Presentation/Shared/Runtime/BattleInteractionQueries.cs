using System;
using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using PokerEngine;

namespace Poker.BattlePresentation
{
    /// <summary>Read-only forecasts stay at the rule boundary, with independent recognition and legality.</summary>
    public static class BattleInteractionQueries
    {
        public static CandidatePresentationModel Query(CardDuelState state, CardDuelEngine engine, ParticipantId player,
            IEnumerable<CardInstanceId> selected, bool canInteract, long selectionVersion = 0, BattleTargetBindings targetBindings = null)
        {
            var ids = (selected ?? Array.Empty<CardInstanceId>()).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
            var labels = new Dictionary<string, string>();
            var mode = !canInteract ? CandidateState.Closed : ids.Length == 0 ? CandidateState.Empty : CandidateState.Invalid;
            CardDuelTransition hand = null, play = null, discard = null;
            if (canInteract && ids.Length > 0) {
                hand = engine.PreviewHand(state, player, ids);
                play = engine.PreviewPlay(state, state.Revision, player, ids,
                    LegacyTargets(state, player, targetBindings), EffectBindings(state, player, targetBindings));
                discard = engine.PreviewDiscard(state, state.Revision, player, ids);
                mode = hand.Accepted ? CandidateState.Valid : CandidateState.Invalid;
                labels["scoredCount"] = hand.Pattern == null ? "0" : hand.Pattern.ScoringCards.Count(id => CardEffectRules.CanScore(state.Table.GetCard(id).Effects)).ToString();
                labels["playPreview"] = BattlePresentationRuleText.Transition(play, state.Duel, engine.Duel.Definition.StatusCatalog);
                labels["discardPreview"] = BattlePresentationRuleText.Transition(discard, state.Duel, engine.Duel.Definition.StatusCatalog);
                if (hand.Accepted)
                {
                    var facts = engine.PreviewHandFacts(state, player, ids);
                    labels["mechanisms"] = string.Join("、", facts.CompatibleCategories.Select(BattleDisplayProjector.HandName));
                    labels["additionalMechanisms"] = string.Join("、", facts.CompatibleCategories
                        .Where(category => category != facts.Pattern.Category).Select(BattleDisplayProjector.HandName));
                }
            }
            return new CandidatePresentationModel(state.Table.MatchId.ToString(), player.Value, state.Revision, selectionVersion,
                mode, ids.Select(id => id.Value), hand?.Pattern?.Category.ToString(), hand?.Pattern == null ? null : BattleDisplayProjector.HandName(hand.Pattern.Category),
                hand?.Score?.Points ?? 0, hand?.Score?.Multiplier ?? 0, hand?.Score?.Total ?? 0,
                canInteract && play != null && play.Accepted, canInteract && discard != null && discard.Accepted,
                play != null && !play.Accepted ? BattlePresentationRuleText.Error(play.Error) : null, labels);
        }

        // Legacy damage policies accept the unnamed target argument; effect rules accept named bindings.
        public static IEnumerable<ParticipantId> LegacyTargets(CardDuelState state, ParticipantId actor, BattleTargetBindings bindings) =>
            state.Duel.Player(actor).Rules.EffectRule == null && bindings != null &&
            bindings.Slots.TryGetValue(BattleTargetBindings.DefaultSlot, out var targets) ? targets : null;
        public static BattleTargetBindings EffectBindings(CardDuelState state, ParticipantId actor, BattleTargetBindings bindings) =>
            state.Duel.Player(actor).Rules.EffectRule == null ? null : bindings;

        /// <summary>Discovers named choices from the same hand and relic effect plan used by the rules.
        /// Automatic targets never create UI choices; the rules still own retargeting during settlement.</summary>
        public static IReadOnlyList<BattleTargetChoice> Targets(CardDuelState state, CardDuelEngine engine,
            ParticipantId actor, IEnumerable<CardInstanceId> selected)
        {
            if (state == null || engine == null || !state.HasStarted || state.Duel.IsFinished)
                return Array.Empty<BattleTargetChoice>();
            var cards = (selected ?? Array.Empty<CardInstanceId>()).ToArray();
            var preview = engine.PreviewHand(state, actor, cards);
            if (!preview.Accepted) return Array.Empty<BattleTargetChoice>();
            try
            {
                var player = state.Duel.Player(actor);
                IEnumerable<EffectTargetSpec> specs;
                if (player.Rules.EffectRule == null) specs = new[] { EffectTargetSpec.Opponents() };
                else
                {
                    var hand = engine.PreviewHandFacts(state, actor, cards);
                    var score = ScoreEngine.RemoveFinalMultiplier(preview.Score, "item/roulette");
                    var context = new ScoredHandEffectContext(state.Duel.Combat, actor, score, hand);
                    var sequence = player.Rules.EffectRule.Resolve(context);
                    specs = new RelicHandEffectModifier().Modify(
                        new HandEffectModifierContext(player.Relics, context, sequence)).Output.Steps.Select(step => step.Target);
                }
                return Array.AsReadOnly(specs.Where(spec => spec.RequestSlot != null)
                    .GroupBy(spec => spec.RequestSlot, StringComparer.Ordinal).Select(group =>
                    {
                        var choices = group.ToArray();
                        var candidates = state.Duel.Combat.Combatants.Where(unit => unit.IsAlive &&
                            choices.All(spec => AcceptsTarget(state.Duel, actor, spec, unit.Id)))
                            .Select(unit => new BattleTargetCandidate(unit.Id.Value, BattlePresentationRuleText.Participant(state.Duel, unit.Id), unit.Health));
                        string label = choices.All(spec => spec.Group == EffectTargetGroup.Allies) ? "友方目标" :
                            preview.Pattern.Category == HandCategory.FullHouse && choices.Any(spec => spec.Selection == EffectTargetSelection.Requested)
                                ? "对子伤害目标" : "攻击目标";
                        return new BattleTargetChoice(group.Key, label,
                            choices.Any(spec => spec.Selection == EffectTargetSelection.Requested), candidates);
                    }).Where(choice => choice.Required || choice.Candidates.Count > 0).ToArray());
            }
            catch (ArgumentException) { return Array.Empty<BattleTargetChoice>(); }
            catch (OverflowException) { return Array.Empty<BattleTargetChoice>(); }
        }

        private static bool AcceptsTarget(TeamBattleState state, ParticipantId actor, EffectTargetSpec spec, ParticipantId target)
        {
            try { return BattleTargetResolver.Resolve(state, actor, spec, new[] { target }).Contains(target); }
            catch (ArgumentException) { return false; }
        }
    }

    public sealed class BattleTargetCandidate
    {
        public string Id { get; }
        public string Label { get; }
        public long Health { get; }
        public BattleTargetCandidate(string id, string label, long health)
        { Id = id; Label = string.IsNullOrEmpty(label) ? id : label; Health = health; }
    }

    public sealed class BattleTargetChoice
    {
        public string Slot { get; }
        public string Label { get; }
        public bool Required { get; }
        public IReadOnlyList<BattleTargetCandidate> Candidates { get; }
        public string SelectedId { get; }
        public BattleTargetChoice(string slot, string label, bool required, IEnumerable<BattleTargetCandidate> candidates, string selectedId = null)
        { Slot = slot; Label = label; Required = required; Candidates = Array.AsReadOnly(candidates.ToArray()); SelectedId = selectedId; }
    }

    /// <summary>Local input state shared by frontends. Stale/dead choices are removed before every preview and submit.</summary>
    public sealed class BattleTargetSelection
    {
        private readonly Dictionary<string, string> selected = new Dictionary<string, string>(StringComparer.Ordinal);
        private string owner;
        private long revision = -1;
        public IReadOnlyList<BattleTargetChoice> Choices { get; private set; } = Array.Empty<BattleTargetChoice>();
        public string Key => string.Join(";", Choices.Select(choice => choice.Slot + "=" + choice.SelectedId));
        public void Clear()
        { selected.Clear(); Choices = Array.Empty<BattleTargetChoice>(); owner = null; revision = -1; }
        public BattleTargetBindings Reconcile(CardDuelState state, CardDuelEngine engine, ParticipantId actor, IEnumerable<CardInstanceId> cards)
        {
            string nextOwner = state.Table.MatchId + "/" + actor.Value;
            if (nextOwner != owner || state.Revision < revision) selected.Clear();
            owner = nextOwner; revision = state.Revision;
            var choices = BattleInteractionQueries.Targets(state, engine, actor, cards);
            foreach (string slot in selected.Keys.ToArray())
                if (!choices.Any(choice => choice.Slot == slot && choice.Candidates.Any(candidate => candidate.Id == selected[slot])))
                    selected.Remove(slot);
            foreach (var choice in choices)
                if (!selected.ContainsKey(choice.Slot) && choice.Candidates.Count == 1)
                    selected[choice.Slot] = choice.Candidates[0].Id;
            Choices = Array.AsReadOnly(choices.Select(choice => new BattleTargetChoice(choice.Slot, choice.Label, choice.Required,
                choice.Candidates, selected.TryGetValue(choice.Slot, out var id) ? id : null)).ToArray());
            return selected.Count == 0 ? null : new BattleTargetBindings(selected.Select(pair =>
                new KeyValuePair<string, IEnumerable<ParticipantId>>(pair.Key, new[] { new ParticipantId(pair.Value) })));
        }
        public bool Select(string slot, string participantId)
        {
            var choice = Choices.FirstOrDefault(item => item.Slot == slot);
            if (choice == null || !choice.Candidates.Any(candidate => candidate.Id == participantId)) return false;
            selected[slot] = participantId;
            Choices = Array.AsReadOnly(Choices.Select(item => item.Slot == slot
                ? new BattleTargetChoice(item.Slot, item.Label, item.Required, item.Candidates, participantId) : item).ToArray());
            return true;
        }
    }
}
