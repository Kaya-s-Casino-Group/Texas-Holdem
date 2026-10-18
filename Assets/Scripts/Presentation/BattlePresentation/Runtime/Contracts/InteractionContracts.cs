using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlePresentation
{
    public enum CandidateState { Closed, Empty, Invalid, Valid, Committing }
    public enum ReleaseMode { Replaced, Cleared, Committed, Synchronized }

    /// <summary>Preview results are immutable local interaction data, never committed rule facts.</summary>
    public sealed class CandidatePresentationModel
    {
        public string BattleId { get; }
        public string ObserverId { get; }
        public long RuleRevision { get; }
        public long SelectionVersion { get; }
        public CandidateState State { get; }
        public IReadOnlyList<string> SelectedIds { get; }
        public string PatternKey { get; }
        public string PatternName { get; }
        public long Points { get; }
        public decimal Multiplier { get; }
        public long Total { get; }
        public bool CanPlay { get; }
        public bool CanDiscard { get; }
        public string Reason { get; }
        public IReadOnlyDictionary<string, string> Labels { get; }
        public string FeedbackKey => State == CandidateState.Valid ? "Valid/" + PatternKey : State.ToString();
        public CandidatePresentationModel(string battleId, string observerId, long ruleRevision, long selectionVersion,
            CandidateState state, IEnumerable<string> selectedIds, string patternKey = null, string patternName = null,
            long points = 0, decimal multiplier = 0, long total = 0, bool canPlay = false, bool canDiscard = false,
            string reason = null, IDictionary<string, string> labels = null)
        { BattleId = battleId; ObserverId = observerId; RuleRevision = ruleRevision; SelectionVersion = selectionVersion;
          State = state; SelectedIds = Frozen.List(selectedIds); PatternKey = patternKey; PatternName = patternName;
          Points = points; Multiplier = multiplier; Total = total; CanPlay = canPlay; CanDiscard = canDiscard;
          Reason = reason; Labels = Frozen.Map(labels); }

        internal CandidatePresentationModel WithState(CandidateState state, long version, bool clearSelection = false) =>
            new CandidatePresentationModel(BattleId, ObserverId, RuleRevision, version, state,
                clearSelection ? null : SelectedIds, clearSelection ? null : PatternKey, clearSelection ? null : PatternName,
                clearSelection ? 0 : Points, clearSelection ? 0 : Multiplier, clearSelection ? 0 : Total,
                false, false, Reason, Labels.ToDictionary(x => x.Key, x => x.Value));
    }

    public interface IInteractionFeedback
    {
        void Reconcile(CandidatePresentationModel next, PresentationScope scope);
        void Release(PresentationScope scope, ReleaseMode mode);
    }
}
