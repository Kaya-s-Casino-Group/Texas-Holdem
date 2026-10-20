using System;
using System.Collections.Generic;
using System.Linq;

namespace Poker.SingleBattle
{
    public sealed class ShieldCard
    {
        public string Id { get; }
        public string ArtId { get; }
        public ShieldCard(string id, string artId)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(artId)) throw new ArgumentException("Shield cards need identity and artwork.");
            Id = id; ArtId = artId;
        }
    }
    public sealed class ShieldBatch
    {
        public string Id { get; }
        public long InitialBlock { get; }
        public long RemainingBlock { get; }
        public IReadOnlyList<ShieldCard> Cards { get; }
        public int DissolvedCount
        {
            get
            {
                // The product can exceed Int64 even when the shield itself is valid.
                // Decimal holds this integer product exactly; the remainder avoids rounded division.
                decimal consumed = (decimal)(InitialBlock - RemainingBlock) * Cards.Count;
                decimal remainder = consumed % InitialBlock;
                return (int)((consumed - remainder) / InitialBlock) + (remainder > 0 ? 1 : 0);
            }
        }
        public ShieldBatch(string id, long initial, long remaining, IEnumerable<ShieldCard> cards)
        {
            if (string.IsNullOrEmpty(id) || initial <= 0 || remaining < 0 || remaining > initial) throw new ArgumentException("Invalid shield batch.");
            Id = id; InitialBlock = initial; RemainingBlock = remaining;
            Cards = Array.AsReadOnly((cards ?? Array.Empty<ShieldCard>()).ToArray());
            if (Cards.Any(card => card == null)) throw new ArgumentException("Missing shield card.");
        }
        internal ShieldBatch Consume(long amount) => new ShieldBatch(Id, InitialBlock, RemainingBlock - amount, Cards);
    }
    public sealed class ShieldSnapshot
    {
        public static readonly ShieldSnapshot Empty = new ShieldSnapshot(Array.Empty<ShieldBatch>());
        public IReadOnlyList<ShieldBatch> Batches { get; }
        public long TotalBlock => Batches.Sum(batch => batch.RemainingBlock);
        public ShieldSnapshot(IEnumerable<ShieldBatch> batches) { Batches = Array.AsReadOnly(batches.ToArray()); }
    }
    /// <summary>Presentation-only FIFO attribution. Unknown/initial block occupies invisible entries; never changes combat state.</summary>
    public sealed class ShieldLedger
    {
        private readonly List<ShieldBatch> batches = new List<ShieldBatch>();
        private readonly HashSet<string> applied = new HashSet<string>();
        private int unknownId;
        public ShieldSnapshot Snapshot => new ShieldSnapshot(batches);
        public void Reset(long block) { batches.Clear(); applied.Clear(); unknownId = 0; Reconcile(block); }
        public void Reconcile(long block)
        {
            if (block < 0) throw new ArgumentOutOfRangeException(nameof(block));
            long difference = block - batches.Sum(batch => batch.RemainingBlock);
            if (difference > 0) batches.Add(new ShieldBatch("untracked/" + unknownId++, difference, difference, null));
            else if (difference < 0) Consume(-difference);
        }
        public ShieldSnapshot Apply(string eventId, long before, long after, IEnumerable<ShieldCard> cards = null)
        {
            if (before < 0 || after < 0) throw new ArgumentOutOfRangeException(nameof(after));
            if (!applied.Add(eventId)) return Snapshot;
            Reconcile(before);
            if (after > before) batches.Add(new ShieldBatch(eventId, after - before, after - before, cards));
            else Consume(before - after);
            return Snapshot;
        }
        private void Consume(long amount)
        {
            for (int i = 0; amount > 0 && i < batches.Count; i++)
            {
                long taken = Math.Min(amount, batches[i].RemainingBlock);
                batches[i] = batches[i].Consume(taken); amount -= taken;
            }
            batches.RemoveAll(batch => batch.RemainingBlock == 0);
        }
    }
}
