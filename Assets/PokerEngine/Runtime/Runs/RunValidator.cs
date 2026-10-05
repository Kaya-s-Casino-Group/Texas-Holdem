using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerEngine
{
    public static class RunValidator
    {
        private static void Need(bool condition, string path) { if (!condition) throw new StateValidationException("InvalidRun", path); }
        private static void Unique(IEnumerable<string> ids, string path)
        { var values = ids.ToArray(); Need(values.All(v => !string.IsNullOrWhiteSpace(v)) && values.Distinct(StringComparer.Ordinal).Count() == values.Length, path); }
        public static void Validate<TState>(RunState<TState> state, RunContent content, IRunEncounterState<TState> encounter) where TState : class
        {
            if (content == null || encounter == null) throw new ArgumentNullException(nameof(content));
            try { Check(state, content, encounter); }
            catch (NullReferenceException) { throw new StateValidationException("MissingRunData", "Run"); }
            catch (ArgumentException) { throw new StateValidationException("InvalidRunContent", "Run"); }
            catch (OverflowException) { throw new StateValidationException("RunOverflow", "Run"); }
        }
        private static void Check<TState>(RunState<TState> s, RunContent content, IRunEncounterState<TState> encounter) where TState : class
        {
            Need(s != null && !string.IsNullOrWhiteSpace(s.Id) && s.Revision >= 0 && s.PlayerId.IsValid, "Header");
            var d = content.Definition;
            Need(s.ContentVersion == content.CardsAndAbilities.Version && s.RulesVersion == d.Version && s.ScoreVersion == d.Scoring.Version &&
                s.StartingDeckVersion == content.StartingDeckVersion && s.EncounterKind == encounter.Kind &&
                s.EncounterRulesVersion == encounter.RulesVersion && s.EncounterSchemaVersion == encounter.SchemaVersion, "Versions");
            Need(Enum.IsDefined(typeof(RunPhase), s.Phase) && s.StageIndex >= 0 && s.StageIndex < d.Stages.Count && s.Gold >= 0 && s.StageScore >= 0 &&
                s.RoundsScored >= 0 && s.RoundsScored <= d.Stages[s.StageIndex].RoundLimit, "Progress");
            Need(s.Random.Algorithm == HmacDeckShuffler.Algorithm && s.Random.AlgorithmVersion == 1 && s.Random.StateBytes.Count == 40, "Random");
            Need(s.Deck.Count >= encounter.MinimumDeckSize && s.Deck.Count <= 512, "Deck.Count"); Unique(s.Deck.Select(c => c.Id), "Deck");
            foreach (var card in s.Deck)
                Need(content.CardsAndAbilities.Contains(card.DefinitionId) && card.BonusPoints >= 0 && card.BonusMultiplier >= 0 && card.Retriggers >= 0 && card.Retriggers <= 8, "Deck.Enhancements");
            Need(s.Jokers.Count <= 32 && s.Jokers.Sum(j => content.Joker(j.DefinitionId).Slots) <= d.JokerCapacity, "Jokers.Capacity");
            Unique(s.Jokers.Select(j => j.Id), "Jokers");
            foreach (var j in s.Jokers)
            {
                var definition = content.Joker(j.DefinitionId);
                Need(j.Level > 0 && j.Level <= definition.MaxLevel && j.Growth >= 0 && j.CooldownRounds >= 0 &&
                    j.Charges.HasValue == definition.Charges.HasValue && !(j.Charges < 0) && (!j.Charges.HasValue || j.Charges <= definition.Charges), "Jokers.State");
                Need(s.Jokers.Count(x => x.DefinitionId == j.DefinitionId) <= definition.MaxCopies, "Jokers.Copies");
                Need(j.Memory.Version == 1 && j.Memory.Marks.Count == 0, "Jokers.Memory"); Unique(j.Memory.Counters.Select(c => c.Key), "Jokers.Memory.Keys");
                Need(j.Memory.Counters.All(c => c.Scope == MemoryScope.Encounter && c.Value >= 0), "Jokers.Memory.Scope");
            }
            Need(s.HandLevels.Count == Enum.GetValues(typeof(HandCategory)).Length && s.HandLevels.Select(l => l.Category).Distinct().Count() == s.HandLevels.Count &&
                s.HandLevels.All(l => Enum.IsDefined(typeof(HandCategory), l.Category) && l.Level > 0), "HandLevels");
            Unique(s.Offers.Select(o => o.Id), "Offers"); Unique(s.Offers.Select(o => o.DefinitionId), "Offers.Definitions");
            Need(s.Offers.Count <= d.OfferCount, "Offers.Count");
            var pool = s.Phase == RunPhase.Reward ? d.RewardPool : d.ShopPool;
            Need(s.Offers.All(o => pool.Contains(o.DefinitionId)), "Offers.Pool");
            Need((s.Phase == RunPhase.Shop || s.Phase == RunPhase.Reward) || s.Offers.Count == 0, "Offers.Phase");
            Need(s.Phase != RunPhase.Reward || s.Offers.Count > 0, "Reward.Empty");
            Unique(s.Scores.Select(r => r.Id), "Scores");
            foreach (var score in s.Scores) ScoreEngine.Validate(score);
            var stageScores = s.Scores.Where(r => r.Id.StartsWith(s.Id + "/stage/" + s.StageIndex + "/", StringComparison.Ordinal)).ToArray();
            Need(stageScores.Length == s.RoundsScored && stageScores.Sum(r => r.Total) == s.StageScore, "ScoreLedger");
            Need(s.Scores.All(r => Enumerable.Range(0, s.StageIndex + 1).Any(i => r.Id.StartsWith(s.Id + "/stage/" + i + "/", StringComparison.Ordinal))), "Scores.Stage");
            if (s.Phase == RunPhase.Preparing)
                Need(s.StageIndex == 0 && s.Encounter == null && s.Scores.Count == 0 && s.RoundsScored == 0, "Preparing");
            else
            {
                Need(s.Encounter != null, "Encounter.Missing");
                var context = RunEncounterContext.From(s, content);
                encounter.Validate(context, s.Encounter);
                var progress = encounter.ReadProgress(context, s.Encounter);
                Need(progress != null, "Encounter.Progress");
                if (progress.Score != null)
                {
                    ScoreEngine.Validate(progress.Score);
                    var recorded = s.Scores.FirstOrDefault(r => r.Id == progress.Score.Id);
                    Need(progress.IsCheckpoint && progress.Score.Id.StartsWith(context.EncounterId + "/", StringComparison.Ordinal) &&
                        recorded != null && SameScore(recorded, progress.Score), "Encounter.ScoreLedger");
                }
                if (s.Phase == RunPhase.Encounter)
                {
                    Need(s.StageScore < d.Stages[s.StageIndex].TargetScore && s.RoundsScored < d.Stages[s.StageIndex].RoundLimit && !progress.Exhausted, "Encounter.Budget");
                    Need(s.Random.Algorithm == progress.Random.Algorithm && s.Random.AlgorithmVersion == progress.Random.AlgorithmVersion &&
                        s.Random.StateBytes.SequenceEqual(progress.Random.StateBytes), "Encounter.Random");
                }
                else
                {
                    Need(progress.IsCheckpoint, "BetweenEncounters");
                    bool cleared = s.StageScore >= d.Stages[s.StageIndex].TargetScore;
                    Need(s.Phase == RunPhase.Failed ? !cleared && (s.RoundsScored == d.Stages[s.StageIndex].RoundLimit || progress.Exhausted) : cleared, "Outcome");
                    Need(s.Phase != RunPhase.Completed || s.StageIndex == d.Stages.Count - 1, "Completion");
                    Need(s.Phase != RunPhase.Shop || s.StageIndex < d.Stages.Count - 1, "Shop.LastStage");
                }
            }
        }
        private static bool SameScore(ScoreResult left, ScoreResult right)
        {
            if (left.Category != right.Category || left.BasePoints != right.BasePoints || left.BaseMultiplier != right.BaseMultiplier ||
                left.Points != right.Points || left.Multiplier != right.Multiplier || left.Total != right.Total || left.Steps.Count != right.Steps.Count) return false;
            for (int i = 0; i < left.Steps.Count; i++)
            {
                var a = left.Steps[i]; var b = right.Steps[i];
                if (a.SourceId != b.SourceId || a.CardId != b.CardId || a.Operation != b.Operation || a.Amount != b.Amount ||
                    a.PointsAfter != b.PointsAfter || a.MultiplierAfter != b.MultiplierAfter) return false;
            }
            return true;
        }
    }
}
