using System;
using System.Linq;
using TMPro;
using UIFoundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Poker.SingleBattle.Editor
{
    public static class HandCandidateSetup
    {
        [MenuItem("Tools/Poker/战局/配置左侧候选牌型")]
        public static void ConfigureCurrent()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后配置候选牌型面板。");
            var scene = SceneManager.GetActiveScene();
            var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleHud>(true)).Single();
            Apply(hud); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        public static void Apply(SingleBattleHud hud)
        {
            var host = (SingleBattleHost)new SerializedObject(hud).FindProperty("host").objectReferenceValue;
            var root = hud.transform.Find("Hand Candidate") as RectTransform;
            if (root == null)
            {
                var go = new GameObject("Hand Candidate", typeof(RectTransform), typeof(Image)); go.layer = hud.gameObject.layer;
                go.transform.SetParent(hud.transform, false); root = (RectTransform)go.transform;
            }
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0, 1);
            root.anchoredPosition = new Vector2(16, -16); root.sizeDelta = new Vector2(214, 158);
            var panel = root.GetComponent<Image>(); panel.color = new Color32(25, 30, 35, 210); panel.raycastTarget = false;
            var ink = new Color32(238, 232, 213, 255);
            Label(root, "Heading", "候选牌型", 13, 10, 8, 194, 22, ink);
            var name = Label(root, "Hand", "未选牌", 24, 10, 34, 194, 34, ink);
            var info = Label(root, "Details", "选中 0 · 计分 0", 12, 10, 69, 194, 18, ink);
            var blue = new Color32(119, 189, 232, 255); var red = new Color32(237, 125, 123, 255);
            Label(root, "Chips Caption", "筹码", 10, 10, 89, 81, 16, blue);
            Label(root, "Multiplier Caption", "倍率", 10, 116, 89, 88, 16, red);
            var chips = Number(root, "Chips", 10, 108, 81, new Color32(39, 96, 137, 255));
            Label(root, "Times", "×", 22, 92, 108, 24, 36, ink).alignment = TextAlignmentOptions.Center;
            var mult = Number(root, "Multiplier", 116, 108, 88, new Color32(165, 61, 64, 255));
            foreach (var path in new[] { "Equals", "Total" })
            {
                var obsolete = root.Find(path);
                if (obsolete != null) UnityEngine.Object.DestroyImmediate(obsolete.gameObject);
            }
            var presenter = root.GetComponent<HandCandidatePresenter>() ?? root.gameObject.AddComponent<HandCandidatePresenter>();
            presenter.Configure(host, name, info, chips, mult);
            EditorUtility.SetDirty(presenter);
        }
        private static TMP_Text Label(Transform parent, string name, string value, int size, float x, float top, float width, float height, Color color)
        {
            var label = parent.Find(name)?.GetComponent<TMP_Text>();
            if (label == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false); label = go.GetComponent<TMP_Text>();
            }
            var rect = label.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -top); rect.sizeDelta = new Vector2(width, height);
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/FusionPixelFont/TMP/FusionPixel12pxProportional.asset");
            label.text = value; label.fontSize = size; label.color = color; label.alignment = TextAlignmentOptions.TopLeft;
            label.enableWordWrapping = false; label.raycastTarget = false;
            return label;
        }
        private static RollingNumberText Number(Transform parent, string name, float x, float top, float width, Color color)
        {
            var frame = parent.Find(name + " Box") as RectTransform;
            if (frame == null)
            {
                var go = new GameObject(name + " Box", typeof(RectTransform), typeof(Image)); go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false); frame = (RectTransform)go.transform;
            }
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0, 1);
            frame.anchoredPosition = new Vector2(x, -top); frame.sizeDelta = new Vector2(width, 36);
            var background = frame.GetComponent<Image>(); background.color = Color.white; background.raycastTarget = false;
            var previous = parent.Find(name);
            if (previous != null) previous.SetParent(frame, false);
            var label = Label(frame, name, "0", 23, 0, 0, width, 36, color);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(4, 2); label.rectTransform.offsetMax = new Vector2(-4, -2);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true; label.fontSizeMin = 9; label.fontSizeMax = 23;
            var number = label.GetComponent<RollingNumberText>() ?? label.gameObject.AddComponent<RollingNumberText>();
            number.Configure("N0"); number.SnapTo(0); return number;
        }
    }
}
