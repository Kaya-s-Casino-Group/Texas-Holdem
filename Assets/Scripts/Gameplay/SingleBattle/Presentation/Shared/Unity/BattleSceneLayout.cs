using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Poker.BattlePresentation
{
    [Serializable]
    public sealed class BattleLayoutSlot
    {
        public string id;
        public Transform anchor;
        public BattleLayoutSlot(string id, Transform anchor) { this.id = id; this.anchor = anchor; }
    }

    /// <summary>Scene-authored data only. Never moves, parents, spawns or subscribes to battle objects.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleSceneLayout : MonoBehaviour
    {
        public const int PlayerCapacity = 2, EnemyCapacity = 5;
        [SerializeField] private BattleLayoutSlot[] players = Array.Empty<BattleLayoutSlot>();
        [SerializeField] private BattleLayoutSlot[] enemies = Array.Empty<BattleLayoutSlot>();
        public IReadOnlyList<BattleLayoutSlot> Players => Array.AsReadOnly(players ?? Array.Empty<BattleLayoutSlot>());
        public IReadOnlyList<BattleLayoutSlot> Enemies => Array.AsReadOnly(enemies ?? Array.Empty<BattleLayoutSlot>());
        public void Configure(BattleLayoutSlot[] playerSlots, BattleLayoutSlot[] enemySlots)
        { players = playerSlots?.ToArray() ?? Array.Empty<BattleLayoutSlot>(); enemies = enemySlots?.ToArray() ?? Array.Empty<BattleLayoutSlot>(); }
        public void Validate()
        {
            if (players == null || enemies == null || players.Length != PlayerCapacity || enemies.Length != EnemyCapacity)
                throw new InvalidOperationException("Battle Layout requires exactly 2 player and 5 independent enemy anchors.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var anchors = new HashSet<Transform>();
            foreach (var slot in players.Concat(enemies))
            {
                if (slot == null || string.IsNullOrWhiteSpace(slot.id) || !ids.Add(slot.id))
                    throw new InvalidOperationException("Battle Layout slot IDs must be nonempty and unique.");
                if (slot.anchor == null || slot.anchor.gameObject.scene != gameObject.scene || !anchors.Add(slot.anchor))
                    throw new InvalidOperationException("Battle Layout anchors must be distinct transforms in this scene: " + slot.id);
            }
        }
    }
}
