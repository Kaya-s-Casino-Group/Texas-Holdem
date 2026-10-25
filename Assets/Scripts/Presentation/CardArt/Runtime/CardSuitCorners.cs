using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardArt
{
    /// <summary>Reuses the FourBeasts corner ink; the illustration and source atlas stay unchanged.</summary>
    public static class CardSuitCorners
    {
        public static readonly Rect Upper = new Rect(.036f, .763f, .144f, .106f);
        public static readonly Rect Lower = new Rect(.820f, .131f, .144f, .106f);
        private static readonly Rect Source = new Rect(.055f, .787f, .108f, .080f);

        public static bool TryGetRegion(CardAtlasData atlas, string face, out CardAtlasRegion region)
        {
            region = null;
            if (atlas == null || atlas.texture == null || !atlas.name.StartsWith("FourBeastsAtlas", StringComparison.Ordinal) || string.IsNullOrEmpty(face)) return false;
            bool suited = face.StartsWith("spades_", StringComparison.Ordinal) || face.StartsWith("clubs_", StringComparison.Ordinal) ||
                face.StartsWith("hearts_", StringComparison.Ordinal) || face.StartsWith("diamonds_", StringComparison.Ordinal);
            return suited && atlas.TryGetCard(face, out region) && !region.clipped;
        }

        public static Vector2 SampleUV(CardAtlasRegion region, float x, float y, bool opposing)
        {
            if (opposing) { x = 1 - x; y = 1 - y; }
            return new Vector2(region.uv.x + (Source.x + Source.width * x) * region.uv.z,
                region.uv.y + (Source.y + Source.height * y) * region.uv.w);
        }

        private sealed class MaterialEntry { public Material Material; public int References; }
        private static readonly Dictionary<(Texture2D texture, bool canvas), MaterialEntry> Materials = new Dictionary<(Texture2D, bool), MaterialEntry>();

        public static Material AcquireMaterial(Texture2D texture, bool canvas)
        {
            var key = (texture, canvas);
            if (!Materials.TryGetValue(key, out var entry))
            {
                var shader = Resources.Load<Shader>("CardSuitCorner");
                if (shader == null) throw new InvalidOperationException("Missing card suit corner shader.");
                var material = new Material(shader) { name = "Card suit corners", hideFlags = HideFlags.HideAndDontSave, mainTexture = texture };
                material.SetInt("_ZTest", canvas ? (int)UnityEngine.Rendering.CompareFunction.Always : (int)UnityEngine.Rendering.CompareFunction.LessEqual);
                entry = new MaterialEntry { Material = material };
                Materials.Add(key, entry);
            }
            entry.References++;
            return entry.Material;
        }

        public static void ReleaseMaterial(Texture2D texture, bool canvas)
        {
            var key = (texture, canvas);
            if (!Materials.TryGetValue(key, out var entry) || --entry.References > 0) return;
            Materials.Remove(key);
            DestroyOwned(entry.Material);
        }

        public static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
