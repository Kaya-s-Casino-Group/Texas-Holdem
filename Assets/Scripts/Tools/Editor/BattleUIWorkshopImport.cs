#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Poker.SingleBattle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BattleUIWorkshopImport
{
    public const string ProfilePath = "Assets/GameAssets/SingleBattle/BattleUIWorkshop.asset";
    const string Target = "Assets/Scenes/SingleChapterAdventure.unity";
    static readonly string[] Roots = { "Battle HUD", "HandActionPanel", "DrawPileIcon", "DeckOverview", "Adventure UI" };
    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

    [MenuItem("工具/其他UI布局工作台/将调整接回正式战斗")]
    public static void Import()
    {
        if (Application.isPlaying) throw new InvalidOperationException("请先退出 Play。");
        var workshop = SceneManager.GetSceneByPath(BattleUIWorkshopSetup.ScenePath);
        bool closeWorkshop = !workshop.isLoaded;
        if (closeWorkshop) workshop = EditorSceneManager.OpenScene(BattleUIWorkshopSetup.ScenePath, OpenSceneMode.Additive);
        if (workshop.isDirty) EditorSceneManager.SaveScene(workshop);
        var nodes = new List<BattleUILayoutProfile.Node>();
        var canvases = new List<BattleUILayoutProfile.CanvasSettings>();
        foreach (var name in Roots)
        {
            var root = All<Canvas>(workshop).Single(c => c.name == name).transform;
            if (name != "Adventure UI" && root.TryGetComponent<CanvasScaler>(out var scaler))
                canvases.Add(new BattleUILayoutProfile.CanvasSettings { root = name, mode = scaler.uiScaleMode,
                    matchMode = scaler.screenMatchMode, referenceResolution = scaler.referenceResolution,
                    matchWidthOrHeight = scaler.matchWidthOrHeight, referencePixelsPerUnit = scaler.referencePixelsPerUnit,
                    scaleFactor = scaler.scaleFactor });
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                string path = BattleUILayoutProfile.CanonicalPath(AnimationUtility.CalculateTransformPath(rect, root));
                if (!BattleUILayoutProfile.Includes(name, path)) continue;
                var text = rect.GetComponent<TMP_Text>();
                var pose = new BattleLayoutProfile.RectPose { path = path,
                    anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot,
                    sizeDelta = rect.sizeDelta, position = rect.anchoredPosition3D,
                    scale = rect.localScale, rotation = rect.localRotation };
                if (text != null)
                {
                    pose.hasText = true; pose.autoSize = text.enableAutoSizing; pose.fontSize = text.fontSize;
                    pose.fontMin = text.fontSizeMin; pose.fontMax = text.fontSizeMax;
                    pose.characterSpacing = text.characterSpacing; pose.lineSpacing = text.lineSpacing;
                    pose.alignment = text.alignment; pose.fontStyle = text.fontStyle;
                }
                bool orb = rect.GetComponent<BattleActionPointBacking>() != null || rect.GetComponent<BattleActionPointLiquid>() != null;
                nodes.Add(new BattleUILayoutProfile.Node { root = name, pose = pose, font = text != null ? text.font : null,
                    hasOrbColor = orb, orbColor = orb ? rect.GetComponent<Graphic>().color : Color.white });
            }
        }
        var duplicate = nodes.GroupBy(n => n.root + "/" + n.pose.path).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null) throw new InvalidOperationException("工作台存在重复 UI 路径：" + duplicate.Key);
        var profile = AssetDatabase.LoadAssetAtPath<BattleUILayoutProfile>(ProfilePath);
        if (profile == null) { profile = ScriptableObject.CreateInstance<BattleUILayoutProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
        profile.nodes = nodes.ToArray(); profile.canvases = canvases.ToArray();
        EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
        var target = SceneManager.GetSceneByPath(Target);
        if (!target.isLoaded) target = EditorSceneManager.OpenScene(Target, OpenSceneMode.Additive);
        var bindings = Roots.Select(name => new BattleAuthoredUILayout.Root {
            name = name, transform = All<Transform>(target).Single(t => t.name == name) }).ToArray();
        var hud = bindings.Single(b => b.name == "Battle HUD").transform;
        var authored = hud.GetComponent<BattleAuthoredUILayout>();
        if (authored == null) authored = hud.gameObject.AddComponent<BattleAuthoredUILayout>();
        authored.Configure(profile, bindings);
        // Materialize the resource widgets in the edit scene, while keeping all bindings and callbacks.
        hud.GetComponent<DeskBattleLayout>().Apply();
        authored.Apply(); EditorUtility.SetDirty(authored);
        EditorSceneManager.MarkSceneDirty(target); EditorSceneManager.SaveScene(target);
        if (closeWorkshop) EditorSceneManager.CloseScene(workshop, true);
        SceneManager.SetActiveScene(target);
        // The enemy profile and BattleDemo Enemy World UI are intentionally outside this import.
        Debug.Log("已接回 " + nodes.Count + " 个 UI 布局参数；敌人/血条/意图/状态继续使用上一轮 BattleLayoutWorkshop 配置。");
    }
}
#endif
