#if UNITY_EDITOR
using System;
using System.Linq;
using Poker.SingleBattle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BattleUIResourceSetup
{
    [MenuItem("工具/其他UI布局工作台/更新行动点与护盾")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != BattleUIWorkshopSetup.ScenePath)
            throw new InvalidOperationException("请在编辑模式打开 BattleUIWorkshop。");
        const string texturePath = "Assets/Art/UI/BattleConcept/Resources/BattleConcept/Textures/action_blood_liquid.png";
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        var nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var frame = nodes.Single(t => t.name == "Concept Action Points");
        var energy = frame.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Energy");
        long current = long.TryParse(energy.text, out var value) ? value : 3;
        BattleResourceWidgets.PresentActions(frame, current);
        var actions = nodes.Single(t => t.name == "HandActionPanel");
        var discard = actions.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Discard");
        // The static workshop has no selection authority; show a neutral preview until runtime selection exists.
        BattleResourceWidgets.PresentShield(discard, energy.font, 0);
        // Apply only the two requested widgets. In particular, never import the snapshot's enemy HUD.
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = frame.gameObject;
        Debug.Log("行动点暗红血液、深棕底与弃牌收益预览已更新；液体位于外框下方；敌人布局继续以上一轮工作台为准。");
    }

    [MenuItem("工具/其他UI布局工作台/行动点预览/0（空）")] public static void Empty() => Preview(0);
    [MenuItem("工具/其他UI布局工作台/行动点预览/1（三分之一）")] public static void One() => Preview(1);
    [MenuItem("工具/其他UI布局工作台/行动点预览/2（三分之二）")] public static void Two() => Preview(2);
    [MenuItem("工具/其他UI布局工作台/行动点预览/3（满）")] public static void Full() => Preview(3);
    static void Preview(int current)
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != BattleUIWorkshopSetup.ScenePath) throw new InvalidOperationException("请打开 UI 工作台。");
        var liquid = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleActionPointLiquid>(true)).Single();
        var label = liquid.transform.parent.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Energy");
        Undo.RecordObjects(new UnityEngine.Object[] { liquid, label }, "行动点预览");
        liquid.SetActions(current); label.text = current.ToString(); EditorSceneManager.MarkSceneDirty(scene);
    }
}
#endif
