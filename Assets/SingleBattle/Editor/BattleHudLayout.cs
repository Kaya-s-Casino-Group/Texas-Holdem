using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Poker.SingleBattle.Editor
{
    public static class BattleHudLayout
    {
        private const float Width = 190;
        [MenuItem("Tools/Poker/战局/整理信息层 HUD")]
        public static void ApplyCurrentScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后整理 HUD。");
            var scene = SceneManager.GetActiveScene();
            var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleHud>(true)).Single();
            Undo.RegisterFullObjectHierarchyUndo(hud.gameObject, "整理战局信息层");
            Apply(hud); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        public static void Apply(SingleBattleHud hud)
        {
            var root = hud.transform;
            Panel(root, "Turn", 16, 52);
            Hide(root, "Turn/Heading"); Hide(root, "Turn/Intent");
            Text(root, "Turn/Round", 10, 8, 170, 38, 14, "回合 1\n玩家行动");
            Panel(root, "Player", 76, 112);
            Hide(root, "Player/Heading");
            Text(root, "Player/Health", 10, 8, 170, 36, 14, "玩家 1   40 / 40\n格挡 0");
            Bar(root, "Player/Health Bar", 10, 48);
            Text(root, "Player/Energy", 10, 64, 170, 36, 13, "能量 3 / 3\n出牌 1 · 弃牌 1");
            var state = new SerializedObject(hud);
            var host = state.FindProperty("host").objectReferenceValue as SingleBattleHost;
            int enemies = host != null ? host.Enemies.Count : 1;
            float enemyHeight = 30 + Mathf.Max(1, enemies) * 19;
            Panel(root, "Enemy", 196, enemyHeight);
            Hide(root, "Enemy/Heading");
            Text(root, "Enemy/Health", 10, 8, 170, enemyHeight - 22, 13, "怪物 1   75 / 75");
            Bar(root, "Enemy/Health Bar", 10, enemyHeight - 12);
            float infoTop = 204 + enemyHeight;
            Panel(root, "Battle Info", infoTop, 108);
            Text(root, "Battle Info/Message", 10, 8, 170, 20, 13, "玩家 1 · 怪物 " + enemies);
            Text(root, "Battle Info/Counts", 10, 30, 170, 36, 13, "牌库 44 · 弃牌堆 0\n弃牌次数 3");
            Text(root, "Battle Info/Score", 10, 70, 170, 32, 13, "总得分 0");
            float controlsTop = infoTop + 116;
            Panel(root, "End Turn", controlsTop, 28); Button(root, "End Turn/Label", "结束回合");
            Panel(root, "Restart", controlsTop + 34, 28); Button(root, "Restart/Label", "重新开始");
            Panel(root, "Result", controlsTop, 140);
            Text(root, "Result/Title", 10, 10, 170, 28, 20, "战斗胜利", true);
            Text(root, "Result/Details", 10, 44, 170, 48, 13, "", true);
            var again = root.Find("Result/Again") as RectTransform;
            if (again != null)
            {
                again.anchorMin = again.anchorMax = again.pivot = new Vector2(.5f, 0);
                again.anchoredPosition = new Vector2(0, 10); again.sizeDelta = new Vector2(170, 28);
                Button(root, "Result/Again/Label", "再战一场");
            }
            root.Find("Result")?.gameObject.SetActive(false);
            // Retain the serialized label as the runtime template for per-enemy intent chips.
            var intent = state.FindProperty("intent").objectReferenceValue as TMP_Text;
            if (intent != null)
            {
                intent.gameObject.SetActive(true); intent.text = ""; intent.fontSize = 14; intent.raycastTarget = false;
            }
            EditorUtility.SetDirty(hud);
        }
        private static void Panel(Transform root, string path, float top, float height)
        {
            var rect = root.Find(path) as RectTransform; if (rect == null) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-16, -top); rect.sizeDelta = new Vector2(Width, height);
            var background = rect.GetComponent<Image>();
            if (background != null && rect.GetComponent<UnityEngine.UI.Button>() == null)
            { background.color = new Color32(25, 30, 35, 210); background.raycastTarget = false; }
        }
        private static void Text(Transform root, string path, float x, float y, float width, float height, int size, string value, bool center = false)
        {
            var label = root.Find(path)?.GetComponent<TMP_Text>(); if (label == null) return;
            var rect = label.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
            label.text = value; label.fontSize = size; label.enableAutoSizing = false; label.enableWordWrapping = false;
            label.alignment = center ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;
        }
        private static void Button(Transform root, string path, string title)
        {
            var label = root.Find(path)?.GetComponent<TMP_Text>(); if (label == null) return;
            var rect = label.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4, 2); rect.offsetMax = new Vector2(-4, -2);
            label.text = title; label.fontSize = 14; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        }
        private static void Bar(Transform root, string path, float x, float y)
        {
            var rect = root.Find(path) as RectTransform; if (rect == null) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(170, 4);
        }
        private static void Hide(Transform root, string path) => root.Find(path)?.gameObject.SetActive(false);
    }
}
