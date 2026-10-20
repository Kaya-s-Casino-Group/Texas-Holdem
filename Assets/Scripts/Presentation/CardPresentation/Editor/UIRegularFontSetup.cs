#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace CardPresentation.Editor
{
    /// <summary>Keep existing scene/prefab font and material references while replacing the pixel face.</summary>
    public static class UIRegularFontSetup
    {
        private const string LegacyFontPath = "Assets/Fonts/FusionPixelFont/TMP/FusionPixel12pxProportional.asset";

        [MenuItem("Tools/BattleDemo/Use Regular UI Font")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before updating UI fonts.");

            var source = AssetDatabase.LoadAssetAtPath<Font>(CardTooltipPrefabBuilder.SourceFont);
            if (source == null) throw new FileNotFoundException("Missing Noto Sans CJK SC source font.");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CardTooltipPrefabBuilder.FontPath);
            if (font == null)
            {
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LegacyFontPath);
                if (font != null)
                {
                    if (!AssetDatabase.IsValidFolder("Assets/Fonts/NotoSansCJK/TMP"))
                        AssetDatabase.CreateFolder("Assets/Fonts/NotoSansCJK", "TMP");
                    var error = AssetDatabase.MoveAsset(LegacyFontPath, CardTooltipPrefabBuilder.FontPath);
                    if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                }
                else font = CardTooltipPrefabBuilder.PrepareFont();
            }

            if (font.sourceFontFile != source || font.atlasRenderMode != GlyphRenderMode.SDFAA)
                ReplaceFace(font, source);

            var settings = TMP_Settings.GetSettings();
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(settings);
            FitTooltipLineHeight(font);

            foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label.font != font) continue;
                label.UpdateMeshPadding();
                label.SetAllDirty();
            }
            Validate();
        }

        private static void FitTooltipLineHeight(TMP_FontAsset font)
        {
            var root = PrefabUtility.LoadPrefabContents(CardTooltipPrefabBuilder.PrefabPath);
            try
            {
                var tooltip = root.GetComponent<CardTooltipView>();
                foreach (var label in new[] { tooltip.SuitRankText, tooltip.ChipsText })
                {
                    var size = label.rectTransform.sizeDelta;
                    // Ellipsis hides the whole line when the font's line metrics exceed the text box.
                    size.y = Mathf.Max(size.y,
                        Mathf.Ceil(font.faceInfo.lineHeight * label.fontSize / font.faceInfo.pointSize + 2));
                    label.rectTransform.sizeDelta = size;
                }
                PrefabUtility.SaveAsPrefabAsset(root, CardTooltipPrefabBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ReplaceFace(TMP_FontAsset font, Font source)
        {
            var characters = font.characterTable.Select(c => c.unicode)
                .Concat(Enumerable.Range(32, 95).Select(c => (uint)c))
                .Concat("生命护盾能量回合出牌弃牌摸牌跳过结束阶段状态遗物结算伤害剩余筹码黑桃红桃梅花方块♠♥♣♦×".Select(c => (uint)c))
                .Distinct().Where(c => c >= 32 && c != 127).ToArray();
            var fresh = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA,
                2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (fresh == null) throw new InvalidOperationException("Unable to create the regular SDF font.");
            try
            {
                // Retain the persistent material and first atlas local IDs referenced by 220 UI labels.
                var data = new SerializedObject(font);
                data.FindProperty("m_SourceFontFileGUID").stringValue = AssetDatabase.AssetPathToGUID(CardTooltipPrefabBuilder.SourceFont);
                data.FindProperty("m_SourceFontFile_EditorRef").objectReferenceValue = source;
                data.FindProperty("m_SourceFontFile").objectReferenceValue = source;
                data.FindProperty("m_AtlasWidth").intValue = 2048;
                data.FindProperty("m_AtlasHeight").intValue = 2048;
                data.FindProperty("m_AtlasPadding").intValue = 8;
                data.FindProperty("m_AtlasRenderMode").intValue = (int)GlyphRenderMode.SDFAA;
                data.ApplyModifiedPropertiesWithoutUndo();
                font.name = "NotoSansCJKSCRegular";
                font.hashCode = TMP_TextUtilities.GetSimpleHashCode(font.name);
                font.faceInfo = fresh.faceInfo;
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                font.isMultiAtlasTexturesEnabled = true;
                font.creationSettings = new FontAssetCreationSettings
                {
                    sourceFontFileName = source.name,
                    sourceFontFileGUID = AssetDatabase.AssetPathToGUID(CardTooltipPrefabBuilder.SourceFont),
                    pointSize = 64, padding = 8, atlasWidth = 2048, atlasHeight = 2048,
                    renderMode = (int)GlyphRenderMode.SDFAA
                };
                font.ClearFontAssetData();
                EditorUtility.CopySerialized(fresh.material, font.material);
                font.material.name = font.name + " Material";
                font.materialHashCode = TMP_TextUtilities.GetSimpleHashCode(font.material.name);
                font.material.mainTexture = font.atlasTextures[0];
                if (!font.TryAddCharacters(characters, out uint[] missing))
                    throw new InvalidOperationException("Regular font is missing glyphs: " +
                        string.Join(", ", missing.Select(c => "U+" + c.ToString("X4"))));
                for (int i = 0; i < font.atlasTextures.Length; i++)
                {
                    var atlas = font.atlasTextures[i];
                    atlas.name = font.name + " Atlas " + i;
                    atlas.filterMode = FilterMode.Bilinear;
                    atlas.wrapMode = TextureWrapMode.Clamp;
                    if (!EditorUtility.IsPersistent(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
                    EditorUtility.SetDirty(atlas);
                }
                font.ReadFontAssetDefinition();
                EditorUtility.SetDirty(font.material);
                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            finally
            {
                Object.DestroyImmediate(fresh.material);
                foreach (var atlas in fresh.atlasTextures) Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(fresh);
            }
        }

        [MenuItem("Tools/BattleDemo/Validate Regular UI Font")]
        public static void Validate()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CardTooltipPrefabBuilder.FontPath);
            if (font == null || font.sourceFontFile == null || !font.faceInfo.familyName.Contains("Noto Sans CJK SC") ||
                font.atlasRenderMode != GlyphRenderMode.SDFAA || font.material.shader.name != "TextMeshPro/Mobile/Distance Field" ||
                TMP_Settings.defaultFontAsset != font || font.atlasTextures.Any(t => t.filterMode != FilterMode.Bilinear))
                throw new InvalidOperationException("Regular UI font configuration is invalid.");
            var prefabCount = 0;
            var labelCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/GameAssets/CardPresentation" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var labels = prefab.GetComponentsInChildren<TMP_Text>(true);
                if (labels.Any(t => t.font != font || t.fontSharedMaterial != font.material))
                    throw new InvalidOperationException("Unexpected font/material in " + prefab.name);
                if (labels.Length > 0) prefabCount++;
                labelCount += labels.Length;
            }
            foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label.font != font) throw new InvalidOperationException("Unexpected scene font: " + label.name);
                labelCount++;
            }
            Directory.CreateDirectory("Library/RegularUIFont");
            File.WriteAllText("Library/RegularUIFont/validation.txt",
                $"font={font.faceInfo.familyName}\nshader={font.material.shader.name}\nmode={font.atlasRenderMode}\n" +
                $"glyphs={font.characterTable.Count}\natlases={font.atlasTextureCount}\nprefabs={prefabCount}\nlabels={labelCount}\npassed=true\n");
            Debug.Log($"Regular UI font verified: {labelCount} labels, {prefabCount} UI prefabs.");
        }
    }
}
#endif
