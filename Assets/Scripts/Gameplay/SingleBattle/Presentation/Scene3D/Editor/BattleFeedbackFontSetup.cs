using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Poker.SingleBattle.Editor
{
    public static class BattleFeedbackFontSetup
    {
        public const string FontAssetPath = "Assets/Fonts/HYWuZiQiU/HYWuZiQiU SDF.asset";
        private const string SourcePath = "Assets/Fonts/HYWuZiQiU/HYWuZiQiU.ttf";

        [MenuItem("工具/卡牌战斗/表现时间线/生成跳字字体")]
        public static void CreateFont()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) != null) return;
            AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (source == null) throw new InvalidOperationException("Missing feedback font: " + SourcePath);
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);
            if (font == null) throw new InvalidOperationException("Unable to create feedback font.");
            font.name = "HYWuZiQiU SDF";
            font.atlasTextures[0].name = font.name + " Atlas";
            font.material.name = font.name + " Material";
            font.TryAddCharacters("0123456789+- 格挡抵消消退复活治疗伤害中毒流血虚弱易伤眩晕护盾吸血驱散");
            AssetDatabase.CreateAsset(font, FontAssetPath);
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
        }

        public static void Apply(TMP_Text text)
        {
            CreateFont();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            text.font = font;
            text.fontSharedMaterial = font.material;
            EditorUtility.SetDirty(text);
        }
    }
}
