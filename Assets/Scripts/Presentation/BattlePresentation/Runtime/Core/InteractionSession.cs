using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlePresentation
{
    /// <summary>
    /// Latest-request-wins preview lifecycle. Same feedback key updates an existing scope;
    /// replacements release only that scope's leases, never restore captured base state.
    /// </summary>
    public sealed class InteractionSession : IDisposable
    {
        private readonly IInteractionFeedback feedback;
        private PresentationScope scope;
        private string battleId, observerId;
        private long ruleRevision = -1;
        private IReadOnlyList<string> requestedIds = Array.Empty<string>();
        private bool hasRequest;
        public long SelectionVersion { get; private set; }
        public CandidatePresentationModel Current { get; private set; }
        public event Action<CandidatePresentationModel> Changed;

        public InteractionSession(IInteractionFeedback feedback = null) { this.feedback = feedback; }

        public long BeginRequest(string battle, string observer, long revision, IEnumerable<string> selectedIds)
        {
            battleId = battle; observerId = observer; ruleRevision = revision;
            requestedIds = Frozen.List(selectedIds); hasRequest = true;
            return ++SelectionVersion;
        }

        public bool Apply(CandidatePresentationModel next)
        {
            if (next == null || !hasRequest || next.BattleId != battleId || next.ObserverId != observerId ||
                next.RuleRevision != ruleRevision || next.SelectionVersion != SelectionVersion ||
                !requestedIds.SequenceEqual(next.SelectedIds)) return false;
            Publish(next, ReleaseMode.Replaced);
            return true;
        }

        /// <summary>Synchronous adapter entry. Asynchronous results must use BeginRequest + Apply.</summary>
        public bool Reconcile(CandidatePresentationModel next)
        {
            if (next == null) return false;
            if (Current != null && Current.BattleId == next.BattleId && Current.ObserverId == next.ObserverId &&
                (next.RuleRevision < ruleRevision || next.SelectionVersion < SelectionVersion)) return false;
            battleId = next.BattleId; observerId = next.ObserverId; ruleRevision = next.RuleRevision;
            SelectionVersion = next.SelectionVersion; requestedIds = next.SelectedIds; hasRequest = true;
            Publish(next, ReleaseMode.Replaced);
            return true;
        }

        /// <summary>Freeze this return value before synchronous Submit can dispatch Present.</summary>
        public CandidatePresentationModel BeginCommit()
        {
            var acceptedContext = Current;
            if (Current == null) return null;
            ++SelectionVersion;
            hasRequest = false;
            Publish(Current.WithState(CandidateState.Committing, SelectionVersion), ReleaseMode.Committed);
            return acceptedContext;
        }

        public void Close()
        {
            ++SelectionVersion; hasRequest = false;
            if (Current != null) Publish(Current.WithState(CandidateState.Closed, SelectionVersion, true), ReleaseMode.Committed);
            else Release(ReleaseMode.Cleared);
        }

        public void Clear()
        {
            ++SelectionVersion; hasRequest = false; requestedIds = Array.Empty<string>();
            if (Current != null) Publish(Current.WithState(CandidateState.Empty, SelectionVersion, true), ReleaseMode.Cleared);
            else Release(ReleaseMode.Cleared);
        }

        public void Dispose()
        {
            ++SelectionVersion; hasRequest = false;
            Release(ReleaseMode.Synchronized);
            Current = null;
        }

        private void Publish(CandidatePresentationModel next, ReleaseMode releaseMode)
        {
            var active = next.State != CandidateState.Closed && next.State != CandidateState.Committing;
            var sameScope = scope != null && scope.IsActive && Current != null &&
                Current.BattleId == next.BattleId && Current.ObserverId == next.ObserverId &&
                Current.FeedbackKey == next.FeedbackKey;
            if (!sameScope || !active) Release(releaseMode);
            Current = next;
            if (active)
            {
                if (scope == null) scope = new PresentationScope("interaction/" + next.BattleId + "/" + next.ObserverId + "/" + next.SelectionVersion);
                feedback?.Reconcile(next, scope);
            }
            Changed?.Invoke(next);
        }

        private void Release(ReleaseMode mode)
        {
            var previous = scope; scope = null;
            if (previous == null) return;
            // Invalidate old callbacks before asking the visual adapter to release its contribution.
            try { previous.Dispose(); }
            finally { feedback?.Release(previous, mode); }
        }
    }
}
