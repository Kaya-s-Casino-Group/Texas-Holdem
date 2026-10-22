#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardArt;
using CardInteraction;
using CardPresentation;
using Poker.Adventure;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// A visual snapshot with the original hierarchy paths for subsequent layout import.
public static class BattleUIWorkshopSetup
{
    public const string ScenePath = "Assets/Scenes/BattleUIWorkshop.unity";
    const string Source = "Assets/Scenes/SingleChapterAdventure.unity";
    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    const string ScratchKey = "BattleUIWorkshopScratch";
    const string StageKey = "BattleUIWorkshopStage";
    const string Snapshot = "Assets/Scenes/__BattleUIWorkshopSnapshot.prefab";
    static double readyAt;
    static double captureDeadline;

    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.playModeStateChanged -= OnPlay;
        EditorApplication.playModeStateChanged += OnPlay;
        if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetInt(StageKey, 0) == 2)
            EditorApplication.delayCall += Finish;
    }

    [MenuItem("工具/其他UI布局工作台/创建或打开场景")]
    public static void Open()
    {
        if (Application.isPlaying) throw new InvalidOperationException("请先退出 Play 模式。");
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("请等待 Unity 完成编译后再创建场景。");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
            return;
        }
        var previous = SceneManager.GetActiveScene();
        if (previous.isDirty && !EditorSceneManager.SaveScene(previous)) throw new IOException("当前场景保存失败。");
        string scratch = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/__BattleUIWorkshopSource.unity");
        if (!AssetDatabase.CopyAsset(Source, scratch)) throw new IOException("无法复制战斗场景。");
        var scene = EditorSceneManager.OpenScene(scratch);
        var owner = All<AdventureSceneHost>(scene).Single();
        var fields = new SerializedObject(owner);
        fields.FindProperty("saveAutomatically").boolValue = false;
        fields.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        SessionState.SetString(ScratchKey, scratch);
        SessionState.SetInt(StageKey, 1);
        EditorApplication.isPlaying = true;
    }

    static void OnPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(StageKey, 0) == 1)
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    var host = All<AdventureSceneHost>(SceneManager.GetActiveScene()).Single();
                    if (!host.NewRun(host.AvailableTarots.First().Id, 42) ||
                        !host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, host.Adventure.AvailableNodes.First().Id)))
                        throw new InvalidOperationException(host.Message);
                    readyAt = EditorApplication.timeSinceStartup + 3;
                    captureDeadline = readyAt + 30;
                    EditorApplication.update += CaptureWhenReady;
                }
                catch (Exception error) { File.WriteAllText("Library/BattleUIWorkshop-error.txt", error.ToString()); Debug.LogException(error); SessionState.SetInt(StageKey, 0); EditorApplication.isPlaying = false; }
            };
        }
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(StageKey, 0) == 2)
            EditorApplication.delayCall += Finish;
    }

    static void CaptureWhenReady()
    {
        if (EditorApplication.timeSinceStartup < readyAt) return;
        var readyHost = All<SingleBattleSceneAdapter>(SceneManager.GetActiveScene()).FirstOrDefault();
        if (readyHost != null && readyHost.IsPresenting && EditorApplication.timeSinceStartup < captureDeadline) return;
        EditorApplication.update -= CaptureWhenReady;
        try
        {
            var scene = SceneManager.GetActiveScene();
            var host = All<SingleBattleSceneAdapter>(scene).Single();
            host.Actions.Hand.GetComponent<HandZoneView>().CompleteTransitions();
            Canvas.ForceUpdateCanvases();
            var originals = scene.GetRootGameObjects();
            var staging = new GameObject("UI Workshop Snapshot");
            var map = new Dictionary<Object, Object>();
            var components = new List<KeyValuePair<Component, Component>>();
            foreach (var root in originals) CopyTree(root.transform, staging.transform, map, components);
            File.WriteAllText("Library/BattleUIWorkshop-status.txt", "Copy " + components.Count);
            foreach (var pair in components)
            {
                try
                {
                    if (pair.Key is Camera camera) ((Camera)pair.Value).CopyFrom(camera);
                    else EditorUtility.CopySerialized(pair.Key, pair.Value);
                }
                catch (Exception error) { throw new InvalidOperationException("复制控件失败：" + pair.Key.name + " / " + pair.Key.GetType().FullName, error); }
                pair.Value.hideFlags = HideFlags.None;
            }
            foreach (var pair in components)
            {
                var serialized = new SerializedObject(pair.Value);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue != null && map.TryGetValue(property.objectReferenceValue, out var replacement))
                        property.objectReferenceValue = replacement;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (pair.Value is Selectable control) control.enabled = false;
            }
            if (PrefabUtility.SaveAsPrefabAsset(staging, Snapshot) == null) throw new IOException("无法保存 UI 快照。");
            Object.Destroy(staging);
            File.WriteAllText("Library/BattleUIWorkshop-status.txt", "Prefab saved");
            SessionState.SetInt(StageKey, 2);
        }
        catch (Exception error) { File.WriteAllText("Library/BattleUIWorkshop-error.txt", error.ToString()); Debug.LogException(error); SessionState.SetInt(StageKey, 0); }
        finally { EditorApplication.isPlaying = false; }
    }

    [MenuItem("工具/其他UI布局工作台/完成已保存的快照")]
    static void Finish()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Snapshot) == null) return;
        File.WriteAllText("Library/BattleUIWorkshop-status.txt", "Finish");
        SessionState.SetInt(StageKey, 0);
        var scene = SceneManager.GetActiveScene();
        if (scene.path != SessionState.GetString(ScratchKey, "")) throw new InvalidOperationException("UI 快照源场景已改变。");
        var originalRoots = scene.GetRootGameObjects();
        var snapshot = AssetDatabase.LoadAssetAtPath<GameObject>(Snapshot);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(snapshot, scene);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var root in originalRoots) Object.DestroyImmediate(root);
        while (instance.transform.childCount > 0) instance.transform.GetChild(0).SetParent(null, true);
        Object.DestroyImmediate(instance);
        foreach (var card in All<CardView>(scene)) { card.enabled = false; card.enabled = true; }
        Canvas.ForceUpdateCanvases();
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("UI 工作台保存失败。");
        AssetDatabase.DeleteAsset(SessionState.GetString(ScratchKey, ""));
        AssetDatabase.DeleteAsset(Snapshot);
        foreach (var temporary in Directory.GetFiles("Assets/Scenes", "__BattleUIWorkshopSource*.unity")) AssetDatabase.DeleteAsset(temporary);
        File.WriteAllText("Assets/Scenes/BattleUIWorkshop.notes.txt", Notes);
        AssetDatabase.ImportAsset("Assets/Scenes/BattleUIWorkshop.notes.txt");
        Selection.activeGameObject = All<Canvas>(scene).First(c => c.name == "Battle HUD").gameObject;
        Debug.Log("BattleUIWorkshop 已创建：主 HUD、操作区、牌堆、遗物、信息弹窗和手牌参照。自动布局已移除，可在编辑模式直接调整并保存。");
    }

    static void CopyTree(Transform source, Transform parent, Dictionary<Object, Object> map,
        List<KeyValuePair<Component, Component>> components)
    {
        var clone = source is RectTransform ? new GameObject(source.name, typeof(RectTransform)) : new GameObject(source.name);
        clone.SetActive(false);
        clone.layer = source.gameObject.layer; clone.tag = source.gameObject.tag;
        clone.transform.SetParent(parent, false);
        clone.transform.localPosition = source.localPosition;
        clone.transform.localRotation = source.localRotation;
        clone.transform.localScale = source.localScale;
        if (source is RectTransform rect)
        {
            var target = (RectTransform)clone.transform;
            target.anchorMin = rect.anchorMin; target.anchorMax = rect.anchorMax; target.pivot = rect.pivot;
            target.sizeDelta = rect.sizeDelta; target.anchoredPosition3D = rect.anchoredPosition3D;
        }
        map[source] = clone.transform; map[source.gameObject] = clone;
        foreach (var component in source.GetComponents<Component>())
        {
            if (component == null || component is Transform || !IsVisual(component)) continue;
            var copy = clone.GetComponent(component.GetType());
            if (copy == null) copy = clone.AddComponent(component.GetType());
            map[component] = copy; components.Add(new KeyValuePair<Component, Component>(component, copy));
        }
        foreach (Transform child in source) CopyTree(child, clone.transform, map, components);
        clone.SetActive(source.gameObject.activeSelf);
    }

    static bool IsVisual(Component component)
    {
        if (component is LayoutGroup || component is ContentSizeFitter || component is AspectRatioFitter) return false;
        return component is Graphic || component is TMP_Text || component is CardView ||
            component is Canvas || component is CanvasRenderer || component is CanvasGroup || component is CanvasScaler ||
            component is Mask || component is RectMask2D || component is BaseMeshEffect || component is Selectable ||
            component is Renderer || component is MeshFilter || component is Camera || component is Light ||
            (component.GetType().Namespace ?? "").StartsWith("UnityEngine.Rendering");
    }

    [MenuItem("工具/其他UI布局工作台/显示或隐藏战斗信息弹窗")]
    public static void ToggleTools() => Toggle("Battle Tools/Details");
    [MenuItem("工具/其他UI布局工作台/显示或隐藏结算面板")]
    public static void ToggleResult() => Toggle("Result");
    static void Toggle(string path)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("请先打开 BattleUIWorkshop。");
        var hud = All<Canvas>(scene).First(c => c.name == "Battle HUD").transform;
        var target = hud.Find(path) ?? hud.Find("Safe Area/" + path);
        if (target == null) throw new InvalidOperationException("未找到面板：" + path);
        Undo.RecordObject(target.gameObject, "切换布局面板"); target.gameObject.SetActive(!target.gameObject.activeSelf);
        Selection.activeGameObject = target.gameObject; EditorSceneManager.MarkSceneDirty(scene);
    }

    const string Notes = "其他运行时 UI 布局工作台\n\n" +
        "在编辑模式调整并保存此 scene，完成后告知我接回正式战斗。\n" +
        "Gameplay/UI/Battle HUD：玩家、血条、回合、行动点、遗物栏、金币、抽牌流程、结束回合、信息弹窗。\n" +
        "Gameplay/UI/HandActionPanel：出牌、弃牌、排序、效果预览。\n" +
        "Gameplay/UI/DrawPileIcon：牌堆和计数。DeckOverview：牌库面板模板。\n" +
        "Gameplay/Card Table/Hand Zone：手牌位置参照。敌人与背景为整体布局参照。\n" +
        "工具 > 其他UI布局工作台：可切换信息弹窗和结算面板；其他隐藏节点可在 Hierarchy 勾选激活。\n" +
        "控件保留原节点名称与层级，方便回接；游戏逻辑与自动布局未复制。数字和文字是布局样例。\n";
}
#endif
