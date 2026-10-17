#if UNITY_EDITOR
using System;
using System.Linq;
using CardInteraction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CardPresentation.Editor
{
    public static class HandActionPanelSetup
    {
        public const string PrefabPath = "Assets/CardPresentation/Prefabs/HandActionPanel.prefab";
        public static bool HasPrefab => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;

        [MenuItem("工具/卡牌表现/界面预制体/准备手牌操作面板")]
        public static void Prepare()
        {
            if (HasPrefab) return; // Preserve authored layout and styling when preparing again.
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CardTooltipPrefabBuilder.FontPath);
            if (font == null) throw new InvalidOperationException("先准备 Fusion Pixel TMP 字体。");
            if (!font.TryAddCharacters("出牌弃牌点数理牌花色", out string missing))
                throw new InvalidOperationException("按钮字体缺少字符：" + missing);
            var root = new GameObject("Hand Action UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                root.layer = LayerMask.NameToLayer("UI");
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.planeDistance = 1; canvas.sortingOrder = 100;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
                var bar = Rect("Actions", root.transform, new Vector2(316, 124));
                bar.anchorMin = bar.anchorMax = bar.pivot = Vector2.zero;
                bar.anchoredPosition = new Vector2(16, 16); bar.localScale = Vector3.one * 0.75f;
                var background = bar.gameObject.AddComponent<Image>(); background.color = new Color32(20, 32, 30, 235); background.raycastTarget = false;
                var play = Button("Play", bar, font, -74, "出牌", new Color32(35, 100, 81, 255));
                var discard = Button("Discard", bar, font, 74, "弃牌", new Color32(136, 53, 49, 255));
                var rank = Button("RankSort", bar, font, 74, "点数理牌", new Color32(65, 72, 83, 255));
                var suit = Button("SuitSort", bar, font, 222, "花色理牌", new Color32(65, 72, 83, 255));
                ((RectTransform)play.transform).anchoredPosition = new Vector2(-74, 26);
                ((RectTransform)discard.transform).anchoredPosition = new Vector2(74, 26);
                ((RectTransform)rank.transform).anchoredPosition = new Vector2(-74, -26);
                ((RectTransform)suit.transform).anchoredPosition = new Vector2(74, -26);
                var view = root.AddComponent<HandActionPanelView>();
                view.Configure(play, discard, rank, suit, play.GetComponentInChildren<TMP_Text>(), discard.GetComponentInChildren<TMP_Text>(),
                    rank.GetComponentInChildren<TMP_Text>(), suit.GetComponentInChildren<TMP_Text>());
                view.SetState(false, false, false, false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [MenuItem("工具/卡牌表现/界面布局/更新玩家手牌界面")]
        public static void UseCompactUICamera()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play 后配置 UI Camera。");
            Prepare();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.planeDistance = 1;
                var bar = (RectTransform)root.transform.Find("Actions");
                bar.anchorMin = bar.anchorMax = bar.pivot = Vector2.zero;
                bar.anchoredPosition = new Vector2(16, 16); bar.sizeDelta = new Vector2(316, 124);
                bar.localScale = Vector3.one * 0.75f;
                var oldDraw = bar.Find("Draw"); if (oldDraw != null) oldDraw.name = "Play";
                var status = bar.Find("Status"); if (status != null) Object.DestroyImmediate(status.gameObject);
                var names = new[] { "Play", "Discard", "RankSort", "SuitSort" };
                var positions = new[] { new Vector2(-74, 26), new Vector2(74, 26), new Vector2(-74, -26), new Vector2(74, -26) };
                for (int i = 0; i < names.Length; ++i) ((RectTransform)bar.Find(names[i])).anchoredPosition = positions[i];
                var view = root.GetComponent<HandActionPanelView>();
                var play = bar.Find("Play").GetComponent<Button>(); var discard = bar.Find("Discard").GetComponent<Button>();
                var rank = bar.Find("RankSort").GetComponent<Button>(); var suit = bar.Find("SuitSort").GetComponent<Button>();
                var font = play.GetComponentInChildren<TMP_Text>().font;
                if (!font.TryAddCharacters("出牌弃牌点数理牌花色", out string missing)) throw new InvalidOperationException("按钮字体缺少字符：" + missing);
                view.Configure(play, discard, rank, suit, play.GetComponentInChildren<TMP_Text>(), discard.GetComponentInChildren<TMP_Text>(),
                    rank.GetComponentInChildren<TMP_Text>(), suit.GetComponentInChildren<TMP_Text>());
                view.SetState(false, false, false, false); AssetDatabase.SaveAssetIfDirty(font);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            ConfigureInCurrentScene();
        }

        public static HandActionPresenter Register(CardZonePlayHost host)
        {
            if (host == null || host.GetComponent<HandZoneView>() == null) throw new ArgumentException("选择已固化的手牌区。", nameof(host));
            var scene = host.gameObject.scene;
            var presenter = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HandActionPresenter>(true))
                .FirstOrDefault(item => item.Hand == host);
            if (presenter == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) throw new InvalidOperationException("先准备 HandActionPanel Prefab。");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(root, "创建手牌操作按钮");
                presenter = Undo.AddComponent<HandActionPresenter>(root);
                presenter.Configure(host, root.GetComponent<HandActionPanelView>());
                EditorUtility.SetDirty(presenter);
            }
            Undo.RecordObject(presenter, "绑定手牌出牌按钮");
            var route = host.GetComponent<CardZoneTransferPresenter>();
            presenter.Configure(host, presenter.Panel, route != null && route.Source == host.GetComponent<HandZoneView>() ? route : null);
            EditorUtility.SetDirty(presenter);
            var uiCamera = CardUICameraSetup.Register(scene, host.GetComponent<HandZoneView>().FacingCamera);
            if (uiCamera != null)
            {
                var canvas = presenter.Panel.GetComponent<Canvas>();
                Undo.RecordObject(canvas, "绑定按钮 UI Camera");
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = uiCamera; canvas.planeDistance = 1;
                PrefabUtility.RecordPrefabInstancePropertyModifications(canvas); EditorUtility.SetDirty(canvas);
                foreach (var hover in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardHoverPresenter>(true)))
                {
                    if (hover.SceneCamera != null && hover.SceneCamera != uiCamera.GetComponent<RetroRendering.UICameraStack>().SceneCamera) continue;
                    Undo.RecordObject(hover, "绑定提示 UI Camera"); hover.SetRenderingCamera(uiCamera); EditorUtility.SetDirty(hover);
                }
            }
            DrawPileSetup.Register(host, uiCamera);
            // Card mouse input already excludes EventSystem UI hits. Only button graphics intercept the pointer.
            if (!Object.FindObjectsOfType<EventSystem>().Any(item => item.isActiveAndEnabled))
            {
                var root = new GameObject("Card UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(root, scene); Undo.RegisterCreatedObjectUndo(root, "创建 UI 输入");
            }
            EditorSceneManager.MarkSceneDirty(scene); return presenter;
        }

        [MenuItem("工具/卡牌表现/场景接入/配置手牌操作按钮")]
        public static void ConfigureInCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play 后配置操作按钮。");
            Prepare();
            var selected = Selection.activeGameObject?.GetComponentInParent<HandZoneView>(true)?.GetComponent<CardZonePlayHost>();
            var host = selected ?? SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardZonePlayHost>(true))
                .FirstOrDefault(item => item.Match != null && item.GetComponent<HandZoneView>() != null);
            if (host == null) throw new InvalidOperationException("请先在玩法场景中绑定手牌 View 和 CardZonePlayHost。");
            var presenter = Register(host); Selection.activeGameObject = presenter.gameObject;
            Debug.Log("Player hand UI configured: 出牌 / 弃牌 / 点数理牌 / 花色理牌，右下角抽牌堆只展示。进入 Play 后在 Game 操作。");
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.gameObject.layer = LayerMask.NameToLayer("UI"); rect.sizeDelta = size; return rect;
        }
        private static Button Button(string name, Transform parent, TMP_FontAsset font, float x, string text, Color color)
        {
            var rect = Rect(name, parent, new Vector2(132, 44)); rect.anchoredPosition = new Vector2(x, 12);
            var image = rect.gameObject.AddComponent<Image>(); image.color = color;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f); colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.65f); button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var label = Label("Label", rect, font, new Vector2(132, 44)); label.text = text; return button;
        }
        private static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, Vector2 size)
        {
            var label = Rect(name, parent, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = 24; label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white; label.raycastTarget = false; label.enableWordWrapping = false; return label;
        }
    }
}
#endif
