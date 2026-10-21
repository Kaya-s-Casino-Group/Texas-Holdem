using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>BattleDemo-only sidecar: resolved primary effects become immutable visual cues.
    /// Does not classify cards, select targets, execute rules, or change shared presentation DTOs.</summary>
    public sealed class BattleCardEffectBridge : MonoBehaviour
    {
        public sealed class Shot
        {
            public string CardId { get; }
            public string TargetId { get; }
            public string CueId { get; }
            public int Order { get; }
            public Shot(string card, string target, string cue, int order = 0) { CardId = card; TargetId = target; CueId = cue; Order = order; }
        }
        public sealed class Wave
        {
            public string Id { get; }
            public string Category { get; }
            public bool Royal { get; }
            public bool Sequential => !Royal && (Category == "Straight" || Category == "StraightFlush");
            public IReadOnlyList<Shot> Shots { get; }
            public IReadOnlyList<string> UnscoredCards { get; }
            internal Wave(string id, string category, bool royal, IEnumerable<Shot> shots, IEnumerable<string> unscored)
            { Id = id; Category = category; Royal = royal; Shots = Array.AsReadOnly(shots.ToArray()); UnscoredCards = Array.AsReadOnly(unscored.ToArray()); }
        }
        public sealed class Cue
        {
            public string Id { get; }
            public Wave Flight { get; }
            internal Cue(string id, Wave wave) { Id = id; Flight = wave; }
        }
        private sealed class Root
        { internal int Wave, Order; internal string[] Cards; internal string Category; internal bool Royal; }
        private readonly Dictionary<long, Cue> cues = new Dictionary<long, Cue>();
        public bool TryGet(long sequence, out Cue cue) => cues.TryGetValue(sequence, out cue);
        public void Clear() => cues.Clear();

        public void Prepare(BattleSessionResult result, ParticipantId observer)
        {
            Clear();
            foreach (var action in result.Timeline.Where(fact => fact.Action == CardDuelAction.Play && fact.Actor == observer)
                .GroupBy(fact => new { fact.ActionId, fact.StepIndex }))
            {
                var transition = result.Steps[action.Key.StepIndex].Transition;
                var plan = transition.EffectPlan;
                if (plan == null) continue;
                var roots = new Dictionary<int, Root>();
                var occurrences = new Dictionary<ParticipantId, int>();
                bool royal = transition.Pattern?.Category == HandCategory.StraightFlush &&
                    transition.Pattern.TieBreakRanks[0] == (int)Rank.Ace;
                for (int index = 0; index < plan.Effects.Count; index++)
                {
                    var effect = plan.Effects[index];
                    if (effect.Source != observer) continue;
                    var hand = effect.Attack?.Hand;
                    bool attack = effect.Kind == CombatEffectKind.Damage && effect.Attack?.Origin == AttackOrigin.ScoredHand && hand != null;
                    if (!attack && !(royal && effect.Kind == CombatEffectKind.Eliminate)) continue;
                    int ordinal = occurrences.TryGetValue(effect.Target, out var value) ? value : 0;
                    occurrences[effect.Target] = ordinal + 1;
                    string[] cards;
                    string category = transition.Score?.Category.ToString();
                    if (royal) cards = transition.Pattern.ScoringCards.Select(id => id.Value).ToArray();
                    else if (hand.ModernScoring && hand.RankPattern.Category == HandCategory.Straight)
                    {
                        // The binder expands each authoritative AttackCards entry over its targets in order.
                        // Count against planned targets, not settled targets, so a kill/retarget cannot shift card identity.
                        cards = new[] { hand.AttackCards[ordinal % hand.AttackCards.Count].Id.Value };
                        category = hand.IsFlush ? "StraightFlush" : "Straight";
                    }
                    else if (transition.Score?.Category == HandCategory.FullHouse && transition.Score.Components.Count == 2)
                    {
                        var component = transition.Score.Components.Single(score => score.Category ==
                            (ordinal == 0 ? HandCategory.ThreeOfAKind : HandCategory.OnePair));
                        cards = component.Steps.Where(step => step.CardId != null).Select(step => step.CardId).Distinct().ToArray();
                        category = component.Category.ToString();
                    }
                    else cards = hand.PatternCards.Select(card => card.Id.Value).ToArray();
                    roots[index] = new Root { Wave = !royal && (category == "Straight" || category == "StraightFlush") ? 0 : ordinal, Order = ordinal, Cards = cards, Category = category, Royal = royal };
                }
                var actual = action.Where(fact => fact.Kind == BattleTimelineKind.EffectSettled && fact.RuleEvent is CombatEvent)
                    .Select(fact => new { Fact = fact, Event = (CombatEvent)fact.RuleEvent })
                    .Where(item => item.Event.Provenance?.Origin == CombatEffectOrigin.Primary &&
                        item.Event.Effect.Source == observer && roots.ContainsKey(item.Event.Provenance.RootEffectIndex) &&
                        (item.Event.Effect.Kind == CombatEffectKind.Damage && item.Event.Effect.Attack?.Origin == AttackOrigin.ScoredHand ||
                         item.Event.Effect.Kind == CombatEffectKind.Eliminate && roots[item.Event.Provenance.RootEffectIndex].Royal))
                    .ToArray();
                bool firstWave = true;
                var unscored = transition.Pattern.SubmittedCards.Except(transition.Pattern.ScoringCards).Select(card => card.Value).ToArray();
                foreach (var group in actual.GroupBy(item => roots[item.Event.Provenance.RootEffectIndex].Wave))
                {
                    var first = roots[group.First().Event.Provenance.RootEffectIndex];
                    var wave = new Wave(action.Key.ActionId + "/flight/" + group.Key, first.Category, first.Royal,
                        group.SelectMany(item => roots[item.Event.Provenance.RootEffectIndex].Cards.Select(card =>
                            new Shot(card, item.Event.Effect.Target.Value, item.Fact.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture), roots[item.Event.Provenance.RootEffectIndex].Order))), firstWave ? unscored : Array.Empty<string>());
                    firstWave = false;
                    foreach (var item in group) cues.Add(item.Fact.Sequence,
                        new Cue(item.Fact.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture), wave));
                }
            }
        }
    }
}
