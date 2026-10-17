using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardArt
{
    [Serializable]
    public sealed class CardAtlasRegion
    {
        public string id;
        public int index;
        // Pixel coordinates and UV coordinates both use a bottom-left origin.
        public RectInt pixels;
        // Fill may crop a card. Full UVs can then extend outside [0, 1].
        public bool clipped;
        public RectInt visiblePixels;
        // xy = offset, zw = scale. Excludes the extruded padding.
        public Vector4 uv;
    }

    public sealed class CardAtlasData : ScriptableObject
    {
        public Texture2D texture;
        public Vector2Int atlasSize;
        public Vector2Int cardSize;
        public int columns;
        public int rows;
        public int padding;
        public List<CardAtlasRegion> cards = new List<CardAtlasRegion>();

        public bool TryGetCard(string id, out CardAtlasRegion region)
        {
            region = cards.Find(card => card.id == id);
            return region != null;
        }
    }
}
