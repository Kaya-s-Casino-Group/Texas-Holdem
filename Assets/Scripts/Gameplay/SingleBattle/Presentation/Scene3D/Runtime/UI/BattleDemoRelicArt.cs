using System;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Optional scene-owned overrides. Definition IDs identify art; ownership and charges remain in the battle.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleDemoRelicArt : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            [InspectorName("遗物规则 ID")] public string definitionId = "14001";
            [InspectorName("遗物图标")] public Sprite sprite;
        }
        [SerializeField, InspectorName("美术映射")] private Entry[] entries = Array.Empty<Entry>();
        [SerializeField, Min(16), InspectorName("图标尺寸")] private float iconSize = 42;
        [SerializeField, Min(0), InspectorName("图标间距")] private float spacing = 8;
        public Entry[] Entries => entries;
        public int Version { get; private set; }
        public float IconSize => iconSize;
        public float Spacing => spacing;
        public Sprite Resolve(string definitionId)
        {
            foreach (var entry in entries)
                if (entry != null && entry.definitionId == definitionId && entry.sprite != null) return entry.sprite;
            return BattleRelicArt.Get(definitionId);
        }
        public void Configure(Entry[] bindings) { entries = bindings ?? Array.Empty<Entry>(); Version++; }
        private void OnValidate() => Version++;
    }
}
