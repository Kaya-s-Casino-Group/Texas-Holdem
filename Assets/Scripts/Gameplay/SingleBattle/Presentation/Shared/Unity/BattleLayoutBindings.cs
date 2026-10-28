using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using global::BattlePresentation;
using UnityEngine;

namespace Poker.BattlePresentation
{
    /// <summary>Game-specific identity mapping belongs to the bridge, not the scene layout.</summary>
    [Serializable]
    public sealed class BattleUnitLayoutOverride
    {
        public string unitId;
        [Tooltip("Optional explicit root slot. Leave empty for stable automatic allocation.")]
        public string slotId;
        [Tooltip("For a body part: independent root unit ID, not its summoner.")]
        public string ownerUnitId;
        [Tooltip("For a body part: key resolved by the future actor's part anchors.")]
        public string partKey;
    }

    /// <summary>Copied world pose. Consumers never receive a layout Transform to animate.</summary>
    public sealed class BattleUnitPlacement
    {
        public string UnitId { get; }
        public string RootUnitId { get; }
        public string SlotId { get; }
        public string PartKey { get; }
        public string Team { get; }
        public Vector3 RootPosition { get; }
        public Quaternion RootRotation { get; }
        public bool IsPart => UnitId != RootUnitId;
        public bool HasRootPose => SlotId != null;
        public bool OccupiesSlot { get; }
        internal BattleUnitPlacement(UnitDisplayData unit, string root, string part, BattleLayoutSlot slot, bool occupies)
        {
            UnitId = unit.Id; Team = unit.Team; RootUnitId = root; PartKey = part; SlotId = slot?.id;
            RootPosition = slot?.anchor.position ?? Vector3.zero; RootRotation = slot?.anchor.rotation ?? Quaternion.identity;
            OccupiesSlot = occupies;
        }
    }

    public sealed class BattleLayoutBindings
    {
        public string BattleId { get; }
        public IReadOnlyDictionary<string, BattleUnitPlacement> Units { get; }
        private BattleLayoutBindings(string battle, Dictionary<string, BattleUnitPlacement> units)
        { BattleId = battle; Units = new ReadOnlyDictionary<string, BattleUnitPlacement>(units); }
        public bool TryGet(string unitId, out BattleUnitPlacement placement) => Units.TryGetValue(unitId, out placement);

        public static BattleLayoutBindings Resolve(BattleSceneLayout layout, BattleDisplaySnapshot snapshot,
            IEnumerable<BattleUnitLayoutOverride> overrides = null, BattleLayoutBindings previous = null)
        {
            if (layout == null || snapshot == null) throw new ArgumentNullException("A scene layout and display snapshot are required.");
            layout.Validate();
            if (previous?.BattleId != snapshot.BattleId) previous = null;
            var rules = new Dictionary<string, BattleUnitLayoutOverride>(StringComparer.Ordinal);
            foreach (var item in overrides ?? Array.Empty<BattleUnitLayoutOverride>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.unitId) || rules.ContainsKey(item.unitId))
                    throw new InvalidOperationException("Layout overrides require unique unit IDs.");
                bool part = !string.IsNullOrEmpty(item.ownerUnitId);
                if (part && (item.ownerUnitId == item.unitId || string.IsNullOrWhiteSpace(item.partKey) || !string.IsNullOrEmpty(item.slotId)) ||
                    !part && !string.IsNullOrEmpty(item.partKey))
                    throw new InvalidOperationException("A part requires a root owner and part key, and cannot claim an independent slot: " + item.unitId);
                rules.Add(item.unitId, item);
            }
            var units = snapshot.Units.ToDictionary(u => u.Id, StringComparer.Ordinal);
            string Root(string id) => rules.TryGetValue(id, out var rule) && !string.IsNullOrEmpty(rule.ownerUnitId) ? rule.ownerUnitId : id;
            var partKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var unit in snapshot.Units)
            {
                if (unit.Team != "player" && unit.Team != "enemy") throw new InvalidOperationException("Unknown layout team: " + unit.Team);
                string root = Root(unit.Id);
                if (!units.TryGetValue(root, out var owner) || Root(root) != root || owner.Team != unit.Team)
                    throw new InvalidOperationException("Part owner must be an independent unit on the same team: " + unit.Id);
                if (root != unit.Id && !partKeys.Add(root + "/" + rules[unit.Id].partKey))
                    throw new InvalidOperationException("Duplicate part key on root: " + root);
            }
            var roots = snapshot.Units.Where(u => Root(u.Id) == u.Id).ToArray();
            var occupiedRoots = new HashSet<string>(snapshot.Units.Where(u => u.IsAlive).Select(u => Root(u.Id)), StringComparer.Ordinal);
            var slots = layout.Players.Concat(layout.Enemies).ToDictionary(s => s.id, StringComparer.Ordinal);
            var assigned = new Dictionary<string, BattleLayoutSlot>(StringComparer.Ordinal);
            var taken = new HashSet<string>(StringComparer.Ordinal);
            IEnumerable<BattleLayoutSlot> TeamSlots(UnitDisplayData unit) => unit.Team == "player" ? layout.Players : layout.Enemies;
            // Explicit placements reserve their slots before automatic placement.
            foreach (var unit in roots)
            {
                if (!rules.TryGetValue(unit.Id, out var rule) || string.IsNullOrEmpty(rule.slotId)) continue;
                var slot = TeamSlots(unit).FirstOrDefault(s => s.id == rule.slotId);
                if (slot == null) throw new InvalidOperationException("Unknown or wrong-team slot for " + unit.Id + ": " + rule.slotId);
                if (occupiedRoots.Contains(unit.Id) && !taken.Add(slot.id)) throw new InvalidOperationException("Occupied layout slot: " + slot.id);
                assigned.Add(unit.Id, slot);
            }
            // Existing living occupants have priority; a revived unit cannot displace one.
            foreach (var unit in roots.Where(u => occupiedRoots.Contains(u.Id)))
            {
                if (assigned.ContainsKey(unit.Id) || previous == null || !previous.TryGet(unit.Id, out var old) || !old.OccupiesSlot) continue;
                var slot = TeamSlots(unit).FirstOrDefault(s => s.id == old.SlotId);
                if (slot != null)
                {
                    if (!taken.Add(slot.id)) throw new InvalidOperationException("Explicit placement would displace a living unit from " + slot.id + ": " + unit.Id);
                    assigned.Add(unit.Id, slot);
                }
            }
            foreach (var unit in roots.Where(u => occupiedRoots.Contains(u.Id)))
            {
                if (assigned.ContainsKey(unit.Id)) continue;
                BattleLayoutSlot slot = null;
                if (previous != null && previous.TryGet(unit.Id, out var old))
                    slot = TeamSlots(unit).FirstOrDefault(s => s.id == old.SlotId && !taken.Contains(s.id));
                slot = slot ?? TeamSlots(unit).FirstOrDefault(s => !taken.Contains(s.id));
                if (slot == null) throw new InvalidOperationException("Battle Layout capacity exceeded for " + unit.Team + " at unit " + unit.Id + ". Configure the encounter/summon rules to fit this scene.");
                taken.Add(slot.id); assigned.Add(unit.Id, slot);
            }
            // Dead records retain their last pose for outgoing effects, without consuming a live slot.
            foreach (var unit in roots.Where(u => !occupiedRoots.Contains(u.Id) && !assigned.ContainsKey(u.Id)))
                if (previous != null && previous.TryGet(unit.Id, out var old) && old.SlotId != null && slots.TryGetValue(old.SlotId, out var slot))
                    assigned.Add(unit.Id, slot);
            var result = new Dictionary<string, BattleUnitPlacement>(StringComparer.Ordinal);
            foreach (var unit in snapshot.Units)
            {
                string root = Root(unit.Id); assigned.TryGetValue(root, out var slot);
                result.Add(unit.Id, new BattleUnitPlacement(unit, root, root == unit.Id ? null : rules[unit.Id].partKey, slot, occupiedRoots.Contains(root)));
            }
            return new BattleLayoutBindings(snapshot.BattleId, result);
        }
    }
}
