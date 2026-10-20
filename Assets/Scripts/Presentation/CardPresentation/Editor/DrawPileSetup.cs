#if UNITY_EDITOR
using System;
using System.Linq;
using CardInteraction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CardPresentation.Editor
{
    public static class DrawPileSetup
    {
        public const string PrefabPath = "Assets/GameAssets/CardPresentation/Prefabs/DrawPileIcon.prefab";

        [MenuItem("工具/卡牌表现/界面预制体/准备抽牌堆图标")]
        public static void Prepare()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null && existing.GetComponent<DrawPileView>().IsConfigured && existing.GetComponent<GraphicRaycaster>() != null)
            {
                if (existing.GetComponent<DrawPileView>().RemainingCount != null) return;
                var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                try { AddRemainingCount(contents.GetComponent<DrawPileView>()); PrefabUtility.SaveAsPrefabAsset(contents,PrefabPath); }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                return;
            }
            DeckOverviewSetup.Prepare();
            var atlas = AssetDatabase.LoadAssetAtPath<CardArt.CardAtlasData>(DeckOverviewSetup.AtlasPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(DeckOverviewSetup.MaterialPath);
            var root = existing != null ? PrefabUtility.LoadPrefabContents(PrefabPath) :
                new GameObject("Draw Pile UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            try
            {
                root.layer = LayerMask.NameToLayer("UI");
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.planeDistance = 1; canvas.sortingOrder = 300; canvas.additionalShaderChannels |= CardUIGraphic.RequiredChannels;
                if (root.GetComponent<GraphicRaycaster>() == null) root.AddComponent<GraphicRaycaster>();
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = .5f;
                var icon = root.transform.Find("Icon") as RectTransform;
                if (icon == null)
                {
                    icon = Rect("Icon",root.transform); icon.anchoredPosition = new Vector2(-20,20); icon.sizeDelta = new Vector2(64,96);
                }
                foreach (Transform child in icon.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                var hit = icon.GetComponent<Image>(); if (hit == null) hit = icon.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
                var motion = CenterRect("Deck",icon,Vector2.zero,icon.sizeDelta);
                var shadow = CenterRect("Shadow",motion,new Vector2(3,-5),icon.sizeDelta + new Vector2(4,4));
                var shadowGraphic = shadow.gameObject.AddComponent<Image>(); shadowGraphic.color = new Color(0,0,0,.35f); shadowGraphic.raycastTarget = false;
                var layers = new CardUIGraphic[8];
                for (int i = 0; i < layers.Length; i++)
                {
                    var card = CenterRect(i == layers.Length-1 ? "TopCard" : "CardBack"+i,motion,
                        new Vector2(-1.5f*(layers.Length-1-i),1.8f*(layers.Length-1-i)),icon.sizeDelta);
                    layers[i] = card.gameObject.AddComponent<CardUIGraphic>();
                }
                var view = root.GetComponent<DrawPileView>(); if (view == null) view = root.AddComponent<DrawPileView>();
                view.Configure(layers,motion,atlas,material); view.SetRemainingCards(52);
                AddRemainingCount(view);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally { if (existing != null) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
        }

        private static void AddRemainingCount(DrawPileView view)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CardTooltipPrefabBuilder.FontPath);
            if (font == null) throw new InvalidOperationException("先准备 Noto Sans CJK SC TMP 字体。");
            var badge = Rect("Remaining",view.MotionRoot);
            badge.anchoredPosition = new Vector2(-2,2); badge.sizeDelta = new Vector2(36,24);
            var background = badge.gameObject.AddComponent<Image>();
            background.color = new Color32(20,32,30,240); background.raycastTarget = false;
            var labelRect = CenterRect("Count",badge,Vector2.zero,badge.sizeDelta);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font;
            label.fontSize = 20; label.color = new Color32(244,237,212,255);
            label.alignment = TextAlignmentOptions.Center; label.enableWordWrapping = false;
            label.enableAutoSizing = true; label.fontSizeMin = 10; label.fontSizeMax = 20;
            view.BindRemainingCount(label);
        }

        [MenuItem("工具/卡牌表现/场景接入/配置牌堆图标与总览")]
        public static void ConfigureInScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请在 Edit 模式配置牌堆与牌组总览。");
            var scene = SceneManager.GetActiveScene();
            var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardZonePlayHost>(true))
                .First(item => item.HasSnapshot && item.GetComponent<HandZoneView>() != null);
            var camera = CardUICameraSetup.Register(scene,host.GetComponent<HandZoneView>().FacingCamera);
            var presenter = Register(host,camera); Selection.activeGameObject = presenter.gameObject;
        }
        private static RectTransform CenterRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = Rect(name,parent); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }

        public static DrawPilePresenter Register(CardZonePlayHost host, Camera uiCamera)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            DeckSummarySetup.Prepare();
            Prepare(); var scene = host.gameObject.scene;
            var presenter = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DrawPilePresenter>(true))
                .FirstOrDefault(item => item.Source == host);
            if (presenter == null)
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                Undo.RegisterCreatedObjectUndo(root, "创建抽牌堆图标"); presenter = Undo.AddComponent<DrawPilePresenter>(root);
            }
            Undo.RecordObject(presenter, "绑定局内抽牌堆"); presenter.Configure(host, presenter.GetComponent<DrawPileView>());
            presenter.BindSummary(AssetDatabase.LoadAssetAtPath<GameObject>(DeckSummarySetup.PrefabPath).GetComponent<DeckSummaryView>());
            EditorUtility.SetDirty(presenter);
            if (uiCamera != null)
            {
                var canvas = presenter.View.GetComponent<Canvas>(); Undo.RecordObject(canvas, "绑定抽牌堆 UI Camera");
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = uiCamera;
                PrefabUtility.RecordPrefabInstancePropertyModifications(canvas); EditorUtility.SetDirty(canvas);
            }
            if (uiCamera != null) presenter.BindOverview(DeckOverviewSetup.Register(host,uiCamera));
            EditorSceneManager.MarkSceneDirty(scene); return presenter;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0); return rect;
        }
    }
}
#endif
