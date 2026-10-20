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
            IEnumerable<CardInstanceId> selected, bool canInteract, long selectionVersion = 0)
        {
            var ids = (selected ?? Array.Empty<CardInstanceId>()).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
            var labels = new Dictionary<string, string>();
            var mode = !canInteract ? CandidateState.Closed : ids.Length == 0 ? CandidateState.Empty : CandidateState.Invalid;
            CardDuelTransition hand = null, play = null, discard = null;
            if (canInteract && ids.Length > 0) {
                hand = engine.PreviewHand(state, player, ids);
                play = engine.PreviewPlay(state, state.Revision, player, ids);
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
    }
}
