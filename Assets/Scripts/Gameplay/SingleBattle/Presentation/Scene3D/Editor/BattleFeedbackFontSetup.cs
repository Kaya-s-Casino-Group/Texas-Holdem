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
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                ApplyOutline(existing.material);
                AssetDatabase.SaveAssetIfDirty(existing.material);
                return;
            }
            AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (source == null) throw new InvalidOperationException("Missing feedback font: " + SourcePath);
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);
            if (font == null) throw new InvalidOperationException("Unable to create feedback font.");
            font.name = "HYWuZiQiU SDF";
            font.atlasTextures[0].name = font.name + " Atlas";
            font.material.name = font.name + " Material";
            ApplyOutline(font.material);
            font.TryAddCharacters("0123456789+- 格挡抵消消退复活治疗伤害中毒流血虚弱易伤眩晕护盾吸血驱散");
            AssetDatabase.CreateAsset(font, FontAssetPath);
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
        }

        private static void ApplyOutline(Material material)
        {
            material.SetColor("_OutlineColor", new Color(.025f, .015f, .01f, 1));
            material.SetFloat("_OutlineWidth", .22f);
            material.SetFloat("_OutlineSoftness", .02f);
            material.EnableKeyword("OUTLINE_ON");
            EditorUtility.SetDirty(material);
        }

        public static void Apply(TMP_Text text)
        {
            CreateFont();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.UpdateMeshPadding();
            EditorUtility.SetDirty(text);
        }
    }
}
