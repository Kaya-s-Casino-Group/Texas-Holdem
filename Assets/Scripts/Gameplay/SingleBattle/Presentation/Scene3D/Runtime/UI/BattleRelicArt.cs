using System;
using System.Collections.Generic;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Small shared atlas, addressed by content definition ID; instance state remains in the rules.</summary>
    public static class BattleRelicArt
    {
        private static readonly string[] Ids = { "10001", "10002", "11002", "11003", "11004", "12001",
            "13001", "13002", "13003", "13004", "14001", "15001" };
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static Texture2D atlas;

        public static Sprite Get(string definitionId)
        {
            if (definitionId == null) return null;
            if (Sprites.TryGetValue(definitionId, out var sprite) && sprite != null) return sprite;
            int index = Array.IndexOf(Ids, definitionId);
            if (index < 0) return null;
            if (atlas == null) atlas = Resources.Load<Texture2D>("BattleRelics");
            if (atlas == null) return null;
            float width = atlas.width / 4f, height = atlas.height / 3f;
            sprite = Sprite.Create(atlas, new Rect(index % 4 * width, (2 - index / 4) * height, width, height),
                new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = "Relic " + definitionId;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Sprites[definitionId] = sprite;
            return sprite;
        }
    }
}
