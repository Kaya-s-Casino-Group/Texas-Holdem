using System;
using System.Collections.Generic;

namespace BattlePresentation
{
    /// <summary>Single projected-state writer. Fact cursors, not publication versions, establish order.</summary>
    public sealed class DisplayStore : IDisplayReader
    {
        private long version;
        public BattleDisplaySnapshot Current { get; private set; }
        public event Action<DisplayUpdate> Changed;
        public void Initialize(BattleDisplaySnapshot snapshot) => Synchronize(snapshot);
        public void Synchronize(BattleDisplaySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Publish(snapshot.At(snapshot.Cursor, ++version), true);
        }

        public void Validate(PresentationBatch batch)
        {
            if (Current == null) throw new InvalidOperationException("DisplayStore must be initialized.");
            RequireIdentity(Current, batch.Initial);
            RequireIdentity(Current, batch.FinalSnapshot);
            if (!Current.Cursor.Equals(batch.Initial.Cursor)) throw new InvalidOperationException("Batch starts at a stale display cursor.");
            var cursor = Current.Cursor;
            long lastSequence = -1;
            foreach (var change in batch.Changes)
            {
                RequireIdentity(Current, change.After);
                if (!change.ExpectedCursor.Equals(cursor)) throw new InvalidOperationException("Fact cursor is not contiguous.");
                if (change.Sequence <= lastSequence) throw new InvalidOperationException("Fact sequences must strictly increase.");
                lastSequence = change.Sequence;
                cursor = new DisplayCursor(batch.Id, change.Sequence);
            }
        }

        public void Apply(string batchId, BattleDisplayChange change) => Apply(batchId, new[] { change });

        /// <summary>Validates an entire draft before one publication; never publishes a partial draft.</summary>
        public void Apply(string batchId, IEnumerable<BattleDisplayChange> changes)
        {
            if (Current == null) throw new InvalidOperationException("DisplayStore must be initialized.");
            var draft = Current;
            var changed = false;
            foreach (var change in changes)
            {
                RequireIdentity(Current, change.After);
                if (!draft.Cursor.Equals(change.ExpectedCursor)) throw new InvalidOperationException("Fact is stale, duplicated, or out of order.");
                if (draft.Cursor.BatchId == batchId && change.Sequence <= draft.Cursor.Sequence)
                    throw new InvalidOperationException("Fact sequence did not advance.");
                draft = change.After.At(new DisplayCursor(batchId, change.Sequence), 0);
                changed = true;
            }
            if (changed) Publish(draft.At(draft.Cursor, ++version), false);
        }

        public void ApplyInstant(PresentationBatch batch)
        {
            Validate(batch);
            var cursor = batch.Changes.Count == 0 ? Current.Cursor : new DisplayCursor(batch.Id, batch.Changes[batch.Changes.Count - 1].Sequence);
            // FinalSnapshot carries non-fact presentation metadata such as current command availability.
            Publish(batch.FinalSnapshot.At(cursor, ++version), false);
        }

        private static void RequireIdentity(BattleDisplaySnapshot left, BattleDisplaySnapshot right)
        {
            if (left.BattleId != right.BattleId || left.ObserverId != right.ObserverId)
                throw new InvalidOperationException("Battle or observer changed without synchronization.");
        }

        private void Publish(BattleDisplaySnapshot snapshot, bool synchronization)
        {
            var previous = Current;
            Current = snapshot;
            Changed?.Invoke(new DisplayUpdate(previous, snapshot, synchronization));
        }
    }
}
