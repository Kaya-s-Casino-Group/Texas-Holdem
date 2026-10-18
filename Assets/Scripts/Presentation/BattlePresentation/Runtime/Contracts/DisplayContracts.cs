using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace BattlePresentation
{
    internal static class Frozen
    {
        internal static IReadOnlyList<T> List<T>(IEnumerable<T> items) => Array.AsReadOnly((items ?? Enumerable.Empty<T>()).ToArray());
        internal static IReadOnlyDictionary<string, string> Map(IDictionary<string, string> items) =>
            new ReadOnlyDictionary<string, string>(items == null ? new Dictionary<string, string>() : new Dictionary<string, string>(items));
    }

    public sealed class StatusDisplayData
    {
        public string Id { get; }
        public long Stacks { get; }
        public long? Remaining { get; }
        public StatusDisplayData(string id, long stacks, long? remaining = null) { Id = id; Stacks = stacks; Remaining = remaining; }
    }

    public sealed class UnitDisplayData
    {
        public string Id { get; }
        public string Name { get; }
        public string Team { get; }
        public long Health { get; }
        public long MaxHealth { get; }
        public long Block { get; }
        public long Energy { get; }
        public long MaxEnergy { get; }
        public bool IsAlive => Health > 0;
        public IReadOnlyList<StatusDisplayData> Statuses { get; }
        public IReadOnlyDictionary<string, string> Labels { get; }
        public UnitDisplayData(string id, string name, string team, long health, long maxHealth, long block,
            long energy, long maxEnergy, IEnumerable<StatusDisplayData> statuses = null, IDictionary<string, string> labels = null)
        { Id = id; Name = name; Team = team; Health = health; MaxHealth = maxHealth; Block = block;
          Energy = energy; MaxEnergy = maxEnergy; Statuses = Frozen.List(statuses); Labels = Frozen.Map(labels); }
    }

    public sealed class CardDisplayData
    {
        public string ViewId { get; }
        public string CardId { get; }
        public string FaceKey { get; }
        public string ArtKey { get; }
        public bool Hidden { get; }
        public string DisabledReason { get; }
        public IReadOnlyDictionary<string, string> Properties { get; }
        public CardDisplayData(string viewId, string cardId, string faceKey, string artKey, bool hidden = false,
            string disabledReason = null, IDictionary<string, string> properties = null)
        { ViewId = viewId; CardId = hidden ? null : cardId; FaceKey = hidden ? null : faceKey;
          ArtKey = artKey; Hidden = hidden; DisabledReason = disabledReason; Properties = Frozen.Map(properties); }
    }

    public sealed class CardZoneDisplayData
    {
        public string Id { get; }
        public string OwnerId { get; }
        public string Kind { get; }
        public IReadOnlyList<CardDisplayData> Cards { get; }
        public CardZoneDisplayData(string id, string ownerId, string kind, IEnumerable<CardDisplayData> cards)
        { Id = id; OwnerId = ownerId; Kind = kind; Cards = Frozen.List(cards); }
    }

    public sealed class ResourceDisplayData
    {
        public string OwnerId { get; }
        public string Key { get; }
        public long Value { get; }
        public long? Max { get; }
        public ResourceDisplayData(string ownerId, string key, long value, long? max = null)
        { OwnerId = ownerId; Key = key; Value = value; Max = max; }
    }

    public sealed class DisplayCursor : IEquatable<DisplayCursor>
    {
        public static readonly DisplayCursor Empty = new DisplayCursor(string.Empty, -1);
        public string BatchId { get; }
        public long Sequence { get; }
        public DisplayCursor(string batchId, long sequence) { BatchId = batchId ?? string.Empty; Sequence = sequence; }
        public bool Equals(DisplayCursor other) => other != null && BatchId == other.BatchId && Sequence == other.Sequence;
        public override bool Equals(object obj) => Equals(obj as DisplayCursor);
        public override int GetHashCode() => (BatchId.GetHashCode() * 397) ^ Sequence.GetHashCode();
        public override string ToString() => BatchId + ":" + Sequence;
    }

    public sealed class BattleDisplaySnapshot
    {
        public string BattleId { get; }
        public string ObserverId { get; }
        public long RuleRevision { get; }
        public string Phase { get; }
        public long Round { get; }
        public string Result { get; }
        public IReadOnlyList<UnitDisplayData> Units { get; }
        public IReadOnlyList<CardZoneDisplayData> Zones { get; }
        public IReadOnlyList<ResourceDisplayData> Resources { get; }
        public DisplayCursor Cursor { get; }
        public long DisplayVersion { get; }
        public bool HasStarted { get; }
        public long CompletedRounds { get; }
        public IReadOnlyDictionary<string, string> Labels { get; }
        public bool IsFinished => !string.IsNullOrEmpty(Result) && Result != "None" && Result != "Ongoing";

        public BattleDisplaySnapshot(string battleId, string observerId, long ruleRevision, string phase, long round,
            string result, IEnumerable<UnitDisplayData> units, IEnumerable<CardZoneDisplayData> zones,
            IEnumerable<ResourceDisplayData> resources = null, DisplayCursor cursor = null, long displayVersion = 0,
            bool hasStarted = true, long completedRounds = 0, IDictionary<string, string> labels = null)
        { BattleId = battleId ?? throw new ArgumentNullException(nameof(battleId)); ObserverId = observerId ?? string.Empty;
          RuleRevision = ruleRevision; Phase = phase; Round = round; Result = result; Units = Frozen.List(units);
          Zones = Frozen.List(zones); Resources = Frozen.List(resources); Cursor = cursor ?? DisplayCursor.Empty;
          DisplayVersion = displayVersion; HasStarted = hasStarted; CompletedRounds = completedRounds; Labels = Frozen.Map(labels); }

        public UnitDisplayData Unit(string id) => Units.FirstOrDefault(x => x.Id == id);
        public CardZoneDisplayData Zone(string id) => Zones.FirstOrDefault(x => x.Id == id);
        public ResourceDisplayData Resource(string ownerId, string key) => Resources.FirstOrDefault(x => x.OwnerId == ownerId && x.Key == key);
        internal BattleDisplaySnapshot At(DisplayCursor cursor, long version) => new BattleDisplaySnapshot(BattleId,
            ObserverId, RuleRevision, Phase, Round, Result, Units, Zones, Resources, cursor, version, HasStarted, CompletedRounds,
            Labels.ToDictionary(x => x.Key, x => x.Value));
    }

    public sealed class EffectDisplayData
    {
        public string Kind { get; }
        public string SourceId { get; }
        public string TargetId { get; }
        public long Amount { get; }
        public long HealthLost { get; }
        public long BlockAbsorbed { get; }
        public UnitDisplayData Before { get; }
        public UnitDisplayData After { get; }
        public bool IsDeath { get; }
        public EffectDisplayData(string kind, string sourceId, string targetId, long amount, long healthLost,
            long blockAbsorbed, UnitDisplayData before, UnitDisplayData after, bool isDeath = false)
        { Kind = kind; SourceId = sourceId; TargetId = targetId; Amount = amount; HealthLost = healthLost;
          BlockAbsorbed = blockAbsorbed; Before = before; After = after; IsDeath = isDeath; }
    }

    /// <summary>A full immutable projected state at one fact, not a command or a rule object.</summary>
    public sealed class BattleDisplayChange
    {
        public long Sequence { get; }
        public DisplayCursor ExpectedCursor { get; }
        public BattleDisplaySnapshot Before { get; }
        public BattleDisplaySnapshot After { get; }
        public BattleDisplaySnapshot Snapshot => After;
        public string SemanticKey { get; }
        public string ActorId { get; }
        public IReadOnlyList<string> TargetIds { get; }
        public IReadOnlyList<string> CardIds { get; }
        public IReadOnlyDictionary<string, string> Values { get; }
        public EffectDisplayData Effect { get; }
        public BattleDisplayChange(long sequence, DisplayCursor expectedCursor, BattleDisplaySnapshot snapshot,
            string semanticKey, string actorId = null, IEnumerable<string> targetIds = null,
            IEnumerable<string> cardIds = null, IDictionary<string, string> values = null,
            BattleDisplaySnapshot before = null, EffectDisplayData effect = null)
        { Sequence = sequence; ExpectedCursor = expectedCursor ?? throw new ArgumentNullException(nameof(expectedCursor));
          After = snapshot ?? throw new ArgumentNullException(nameof(snapshot)); Before = before; SemanticKey = semanticKey;
          ActorId = actorId; TargetIds = Frozen.List(targetIds); CardIds = Frozen.List(cardIds); Values = Frozen.Map(values); Effect = effect; }
    }

    public sealed class PresentationBatch
    {
        public string Id { get; }
        public BattleDisplaySnapshot Initial { get; }
        public IReadOnlyList<BattleDisplayChange> Changes { get; }
        public BattleDisplaySnapshot FinalSnapshot { get; }
        public PresentationBatch(string id, BattleDisplaySnapshot initial, IEnumerable<BattleDisplayChange> changes,
            BattleDisplaySnapshot finalSnapshot)
        { Id = id ?? throw new ArgumentNullException(nameof(id)); Initial = initial ?? throw new ArgumentNullException(nameof(initial));
          Changes = Frozen.List(changes); FinalSnapshot = finalSnapshot ?? throw new ArgumentNullException(nameof(finalSnapshot)); }
    }

    public sealed class DisplayUpdate
    {
        public BattleDisplaySnapshot Previous { get; }
        public BattleDisplaySnapshot Current { get; }
        public bool IsSynchronization { get; }
        public DisplayUpdate(BattleDisplaySnapshot previous, BattleDisplaySnapshot current, bool synchronization)
        { Previous = previous; Current = current; IsSynchronization = synchronization; }
    }

    public interface IDisplayReader
    {
        BattleDisplaySnapshot Current { get; }
        event Action<DisplayUpdate> Changed;
    }
}
