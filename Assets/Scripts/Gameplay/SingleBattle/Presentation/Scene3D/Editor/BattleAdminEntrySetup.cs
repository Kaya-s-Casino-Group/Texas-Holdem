using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Poker.SingleBattle.Editor
{
    public static class BattleAdminEntrySetup
    {
        [MenuItem("Tools/BattleDemo/Install Administrator Entry")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请先退出 Play，再保存固定 GUI 入口。");
            var scene = SceneManager.GetActiveScene();
            var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleHud>(true)).Single();
            Configure(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void Configure(SingleBattleHud hud)
        {
            Undo.RegisterFullObjectHierarchyUndo(hud.gameObject, "固定对局修改器入口");
            var data = new SerializedObject(hud);
            var panel = hud.GetComponent<BattleAdminPanel>();
            if (panel == null) panel = Undo.AddComponent<BattleAdminPanel>(hud.gameObject);
            var button = data.FindProperty("adminButton").objectReferenceValue as Button;
            if (button == null) button = hud.transform.Find("Open Battle Administrator")?.GetComponent<Button>();
            if (button == null)
            {
                var source = data.FindProperty("restart").objectReferenceValue as Button;
                if (source == null) throw new InvalidOperationException("HUD 缺少按钮样式模板。");
                button = UnityEngine.Object.Instantiate(source, hud.transform);
                Undo.RegisterCreatedObjectUndo(button.gameObject, "创建固定修改器按钮");
            }
            button.name = "Open Battle Administrator";
            button.gameObject.SetActive(false); button.gameObject.layer = hud.gameObject.layer;
            button.interactable = true;
            button.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(button.onClick, hud.OpenAdministrator);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(480, -158); rect.sizeDelta = new Vector2(150, 36);
            rect.localScale = Vector3.one;
            // Keep the fixed entry above other HUD panels, with its own UI raycaster (no extra camera).
            var canvas = button.GetComponent<Canvas>();
            if (canvas == null) canvas = Undo.AddComponent<Canvas>(button.gameObject);
            canvas.overrideSorting = true; canvas.sortingOrder = 100;
            var raycaster = button.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = Undo.AddComponent<GraphicRaycaster>(button.gameObject);
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
            hud.GetComponent<BattleDemoUITheme>()?.StyleButton(button, false);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.text = "对局修改器"; label.fontSize = 16; label.enableAutoSizing = false; label.raycastTarget = false;
            button.targetGraphic.raycastTarget = true;
            data.Update();
            data.FindProperty("adminPanel").objectReferenceValue = panel;
            data.FindProperty("adminButton").objectReferenceValue = button;
            data.ApplyModifiedProperties();
            EditorUtility.SetDirty(hud); EditorUtility.SetDirty(button); EditorUtility.SetDirty(canvas);
        }
    }
}
