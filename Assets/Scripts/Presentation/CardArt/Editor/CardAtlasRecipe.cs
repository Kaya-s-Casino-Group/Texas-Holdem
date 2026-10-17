using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CardArt.Editor
{
    public enum CardResizeFilter { Nearest, Bilinear }
    public enum CardFitMode { Fit, Fill, Stretch }
    public enum CardAtlasOrder { BySuit, Manual }

    [Serializable]
    public sealed class CardAtlasEntry
    {
        public string id;
        public string sourcePath;
        public bool included = true;
        public Texture2D replacement;
        public string replacementPath;

        public string EffectivePath => replacement != null
            ? AssetDatabase.GetAssetPath(replacement)
            : string.IsNullOrEmpty(replacementPath) ? sourcePath : replacementPath;
    }

    [Serializable]
    public sealed class CardAtlasSettings
    {
        public int cardWidth = 128;
        public int cardHeight = 192;
        public int columns = 8;
        public int padding = 4;
        public bool powerOfTwo = true;
        // Requested square side; rounded up only when powerOfTwo is enabled.
        public int maxSize = 2048;
        public CardFitMode fit = CardFitMode.Fit;
        public CardResizeFilter resizeFilter = CardResizeFilter.Nearest;
        public FilterMode filter = FilterMode.Point;
        public TextureWrapMode wrap = TextureWrapMode.Clamp;
        public bool mipmaps;
        public bool sRGB = true;
        public TextureImporterCompression compression = TextureImporterCompression.Uncompressed;
        public int anisoLevel = 1;
    }

    public sealed class CardAtlasRecipe : ScriptableObject
    {
        public string sourceFolder = "Assets/Art/Cards/FourBeasts";
        public bool recursive;
        public string outputFolder = "Assets/Art/CardAtlases";
        public string outputName = "FourBeastsAtlas";
        public CardAtlasSettings settings = new CardAtlasSettings();
        public CardAtlasOrder order = CardAtlasOrder.BySuit;
        public CardImageValidationSettings imageValidation = new CardImageValidationSettings();
        public List<CardAtlasEntry> cards = new List<CardAtlasEntry>();
    }
}
