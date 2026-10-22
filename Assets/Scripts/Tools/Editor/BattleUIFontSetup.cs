#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Poker.SingleBattle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BattleUIFontSetup
{
    private const string FontPath = "Assets/Fonts/NotoSansCJK/TMP/NotoSansCJKSCRegular.asset";
    private static TMP_Text[] Labels() => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true)).ToArray();
    private static bool IsBadge(TMP_Text label) => label.name == "Block Count" || label.name == "Discard Pile Count" ||
        label.name == "Remaining Uses" || label.GetComponentInParent<CardPresentation.DrawPileView>()?.RemainingCount == label ||
        label.name == "Value" && label.GetComponentInParent<BattleDemoEnemyBadgeTooltip>() != null;

    [MenuItem("工具/局内UI/修复TMP字体与角标材质")]
    public static void Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop playback before saving font repairs.");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null || font.material == null || font.atlasTexture == null)
            throw new InvalidOperationException("The regular UI font or its atlas is missing.");
        var settings = TMP_Settings.GetSettings();
        var settingsData = new SerializedObject(settings);
        settingsData.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
        settingsData.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(settings);
        var badge = Resources.Load<Material>("NotoSansCJKSCRegular Badge");
        if (badge == null) throw new InvalidOperationException("Missing badge material preset.");
        badge.mainTexture = font.atlasTexture;
        badge.SetColor("_FaceColor", Color.black);
        badge.SetColor("_OutlineColor", Color.white);
        badge.SetFloat("_OutlineWidth", .139f);
        badge.EnableKeyword("OUTLINE_ON");
        EditorUtility.SetDirty(badge); AssetDatabase.SaveAssetIfDirty(badge);
        int repaired = 0, badges = 0;
        foreach (var label in Labels())
        {
            Undo.RecordObject(label, "Repair TMP font material");
            if (label.font == null) { label.font = font; repaired++; }
            if (label.fontSharedMaterial == null || label.fontSharedMaterial.mainTexture == null)
            { label.fontSharedMaterial = label.font.material; repaired++; }
            if (IsBadge(label))
            { BattleBadgeText.Apply(label); badges++; }
            label.UpdateMeshPadding(); label.SetAllDirty();
            EditorUtility.SetDirty(label);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        }
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Validate();
        Debug.Log($"TMP font repair saved: {repaired} repaired references, {badges} independent badge materials.");
    }

    [MenuItem("工具/局内UI/检查TMP字体与角标材质")]
    public static void Validate()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var badge = Resources.Load<Material>("NotoSansCJKSCRegular Badge");
        if (font == null || font.material == null || font.material.mainTexture != font.atlasTexture ||
            TMP_Settings.defaultFontAsset != font || badge == null || badge == font.material ||
            badge.mainTexture != font.atlasTexture || badge.GetColor("_FaceColor") != Color.black ||
            badge.GetColor("_OutlineColor") != Color.white || badge.GetFloat("_OutlineWidth") <= 0 ||
            !badge.IsKeywordEnabled("OUTLINE_ON"))
            throw new InvalidOperationException($"UI font / badge preset configuration is invalid: font={font}, default={TMP_Settings.defaultFontAsset}, " +
                $"font atlas={font?.atlasTexture}, material atlas={font?.material?.mainTexture}, badge={badge}, badge atlas={badge?.mainTexture}, " +
                $"face={badge?.GetColor("_FaceColor")}, outline={badge?.GetColor("_OutlineColor")}, width={badge?.GetFloat("_OutlineWidth")}, keyword={badge?.IsKeywordEnabled("OUTLINE_ON")}");
        var labels = Labels();
        foreach (var label in labels)
        {
            if (label.font == null || label.fontSharedMaterial == null || label.fontSharedMaterial.shader == null ||
                label.fontSharedMaterial.mainTexture != label.font.atlasTexture)
                throw new InvalidOperationException("Missing or mismatched TMP font/material: " + label.name);
            if (IsBadge(label) && (label.fontSharedMaterial != badge || label.color != Color.white))
                throw new InvalidOperationException("Badge material or tint was overridden: " + label.name);
        }
        Directory.CreateDirectory("Library/BattleUIFont");
        File.WriteAllLines("Library/BattleUIFont/validation.txt", new[] {
            "scene=" + SceneManager.GetActiveScene().path, "playMode=" + Application.isPlaying,
            "labels=" + labels.Length, "badges=" + labels.Count(IsBadge), "passed=true" });
        if (Application.isPlaying) ScreenCapture.CaptureScreenshot("Library/BattleUIFont/game.png");
        Debug.Log($"TMP font validation passed: {labels.Length} labels, {labels.Count(IsBadge)} badges, no missing font/material/atlas references; black-face white-outline badge preset verified.");
    }
}
#endif
