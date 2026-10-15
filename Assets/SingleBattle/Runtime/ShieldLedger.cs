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
        public int InitialBlock { get; }
        public int RemainingBlock { get; }
        public IReadOnlyList<ShieldCard> Cards { get; }
        public int DissolvedCount => InitialBlock <= 0 ? Cards.Count :
            (int)(((long)(InitialBlock - RemainingBlock) * Cards.Count + InitialBlock - 1) / InitialBlock);
        public ShieldBatch(string id, int initial, int remaining, IEnumerable<ShieldCard> cards)
        {
            if (string.IsNullOrEmpty(id) || initial <= 0 || remaining < 0 || remaining > initial) throw new ArgumentException("Invalid shield batch.");
            Id = id; InitialBlock = initial; RemainingBlock = remaining;
            Cards = Array.AsReadOnly((cards ?? Array.Empty<ShieldCard>()).ToArray());
            if (Cards.Any(card => card == null)) throw new ArgumentException("Missing shield card.");
        }
        internal ShieldBatch Consume(int amount) => new ShieldBatch(Id, InitialBlock, RemainingBlock - amount, Cards);
    }
    public sealed class ShieldSnapshot
    {
        public static readonly ShieldSnapshot Empty = new ShieldSnapshot(Array.Empty<ShieldBatch>());
        public IReadOnlyList<ShieldBatch> Batches { get; }
        public int TotalBlock => Batches.Sum(batch => batch.RemainingBlock);
        public ShieldSnapshot(IEnumerable<ShieldBatch> batches) { Batches = Array.AsReadOnly(batches.ToArray()); }
    }
    /// <summary>Presentation-only FIFO attribution. Unknown/initial block occupies invisible entries; never changes combat state.</summary>
    public sealed class ShieldLedger
    {
        private readonly List<ShieldBatch> batches = new List<ShieldBatch>();
        private readonly HashSet<string> applied = new HashSet<string>();
        private int unknownId;
        public ShieldSnapshot Snapshot => new ShieldSnapshot(batches);
        public void Reset(int block) { batches.Clear(); applied.Clear(); unknownId = 0; Reconcile(block); }
        public void Reconcile(int block)
        {
            if (block < 0) throw new ArgumentOutOfRangeException(nameof(block));
            int difference = block - batches.Sum(batch => batch.RemainingBlock);
            if (difference > 0) batches.Add(new ShieldBatch("untracked/" + unknownId++, difference, difference, null));
            else if (difference < 0) Consume(-difference);
        }
        public ShieldSnapshot Apply(string eventId, int before, int after, IEnumerable<ShieldCard> cards = null)
        {
            if (before < 0 || after < 0) throw new ArgumentOutOfRangeException(nameof(after));
            if (!applied.Add(eventId)) return Snapshot;
            Reconcile(before);
            if (after > before) batches.Add(new ShieldBatch(eventId, after - before, after - before, cards));
            else Consume(before - after);
            return Snapshot;
        }
        private void Consume(int amount)
        {
            for (int i = 0; amount > 0 && i < batches.Count; i++)
            {
                int taken = Math.Min(amount, batches[i].RemainingBlock);
                batches[i] = batches[i].Consume(taken); amount -= taken;
            }
            batches.RemoveAll(batch => batch.RemainingBlock == 0);
        }
    }
}
