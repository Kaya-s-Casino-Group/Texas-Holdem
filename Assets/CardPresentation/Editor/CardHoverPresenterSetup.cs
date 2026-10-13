#if UNITY_EDITOR
using System;
using System.Linq;
using CardInteraction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardPresentation.Editor
{
    public static class CardHoverPresenterSetup
    {
        public const string SceneObjectName = "Card Hover UI";

        /// <summary>Register a hover source with its scene/camera's shared presenter; preserve authored presentation settings.</summary>
        public static CardHoverPresenter Register(MonoBehaviour source, Camera camera = null)
        {
            if (source == null || !(source is ICardHoverSource)) throw new ArgumentException("A card hover source is required.", nameof(source));
            camera = camera != null ? camera : Camera.main;
            var scene = source.gameObject.scene;
            var presenter = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardHoverPresenter>(true))
                .FirstOrDefault(item => item.SceneCamera == camera || item.SceneCamera == null);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardTooltipPrefabBuilder.PrefabPath)?.GetComponent<CardTooltipView>();
            if (prefab == null) throw new InvalidOperationException("先准备 CardSuitChipsTooltip Prefab。");
            if (presenter == null)
            {
                var root = new GameObject(SceneObjectName);
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "创建场景悬停提示");
                presenter = Undo.AddComponent<CardHoverPresenter>(root);
                presenter.Configure(prefab, camera, new MonoBehaviour[] { source });
            }
            else
            {
                if (!presenter.Sources.Contains(source) || presenter.TooltipPrefab == null)
                {
                    Undo.RecordObject(presenter, "绑定悬停提示牌区");
                    if (presenter.TooltipPrefab == null)
                        presenter.Configure(prefab, camera, presenter.Sources.Where(source => source != null).Concat(new MonoBehaviour[] { source }));
                    else presenter.RegisterSource(source);
                }
            }
            var uiCamera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RetroRendering.UICameraStack>(true))
                .FirstOrDefault(item => item.SceneCamera == camera)?.UICamera;
            if (uiCamera != null && presenter.RenderingCamera != uiCamera)
            { Undo.RecordObject(presenter, "绑定提示 UI Camera"); presenter.SetRenderingCamera(uiCamera); }
            EditorUtility.SetDirty(presenter);
            EditorSceneManager.MarkSceneDirty(scene);
            return presenter;
        }

        [MenuItem("Tools/Cards/Configure Card Hover Tooltip")]
        public static void ConfigureInCurrentScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后配置场景悬停提示。");
            var selected = Selection.activeGameObject?.GetComponentInParent<HandZoneView>(true)?.GetComponent<CardZonePlayHost>();
            var hosts = selected != null ? new[] { selected } : SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CardZonePlayHost>(true))
                .Where(host => host.HasSnapshot && host.GetComponent<HandZoneView>() != null).ToArray();
            if (hosts.Length == 0) throw new InvalidOperationException("请先通过牌区验证窗口创建并固化一个手牌区。");
            CardHoverPresenter presenter = null;
            foreach (var host in hosts) presenter = Register(host);
            Selection.activeGameObject = presenter.gameObject;
            Debug.Log("Card hover tooltip connected to " + hosts.Length + " hand zone(s). Enter Play and hover in Game.");
        }
    }
}
#endif
