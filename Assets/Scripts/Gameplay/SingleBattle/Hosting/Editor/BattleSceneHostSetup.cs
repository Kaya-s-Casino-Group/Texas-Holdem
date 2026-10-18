using System;
using System.Linq;
using Poker.BattlePresentation;
using global::BattlePresentation.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.BattleHosting.Editor
{
    public static class BattleSceneHostSetup
    {
        public const string PrefabPath = "Assets/GameAssets/SingleBattle/Hosting/BattleSceneHostMinimal.prefab";
        public const string ContentPath = "Packages/com.kaya.battle-engine/Content/SingleBattleDemoNew.xml";

        [MenuItem("工具/卡牌战斗/Battle入口/在当前场景添加 BattleSceneHost")]
        public static void AddCurrent() => AddToScene(SceneManager.GetActiveScene(), Selection.activeGameObject);
        [MenuItem("GameObject/战斗/BattleSceneHost（最小入口）", false, 10)]
        public static void AddGameObject(MenuCommand _) => AddCurrent();
        [MenuItem("工具/卡牌战斗/Battle入口/新增独立 Battle（允许同场景多局）")]
        public static void AddIndependent() => AddToScene(SceneManager.GetActiveScene(), null, true);
        [MenuItem("工具/卡牌战斗/Battle入口/定位或生成最小入口预制体")]
        public static void SelectPrefab() { Selection.activeObject = EnsureMinimalPrefab(); EditorGUIUtility.PingObject(Selection.activeObject); }
        [MenuItem("工具/卡牌战斗/Battle入口/迁移当前场景已有 Battle 入口")]
        public static void MigrateCurrent()
        {
            var scene = SceneManager.GetActiveScene(); RequireScene(scene);
            foreach (var adapter in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component is IBattleSceneAdapter && component is IBattleSessionSource).ToArray())
                ConnectExisting(adapter);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static BattleSceneHost AddToScene(Scene scene, GameObject selected = null, bool forceNew = false)
        {
            RequireScene(scene);
            if (!forceNew)
            {
                var selectedHost = selected != null && selected.scene == scene ? selected.GetComponentInParent<BattleSceneHost>(true) : null;
                if (selectedHost != null) return SelectExisting(selectedHost);
                var hosts = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BattleSceneHost>(true)).ToArray();
                if (hosts.Length == 1) return SelectExisting(hosts[0]);
                if (hosts.Length > 1) throw new InvalidOperationException("当前场景有多个 Battle。请选中其中一个，或使用“新增独立 Battle”。");
                var adapters = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                    .Where(component => component is IBattleSceneAdapter && component is IBattleSessionSource).ToArray();
                if (adapters.Length == 1) return ConnectExisting(adapters[0]);
                if (adapters.Length > 1) throw new InvalidOperationException("请先使用“迁移当前场景已有 Battle 入口”。");
            }
            var prefab = EnsureMinimalPrefab();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = GameObjectUtility.GetUniqueNameForSibling(null, "Battle");
            Undo.RegisterCreatedObjectUndo(instance, "Add BattleSceneHost");
            EditorSceneManager.MarkSceneDirty(scene);
            var host = instance.GetComponent<BattleSceneHost>(); Selection.activeGameObject = instance;
            return host;
        }
        private static BattleSceneHost SelectExisting(BattleSceneHost host)
        {
            if (host.SceneAdapter == null || host.SessionSource == null)
            {
                var adapters = host.GetComponents<MonoBehaviour>().Where(component => component is IBattleSceneAdapter && component is IBattleSessionSource).ToArray();
                if (adapters.Length != 1) throw new InvalidOperationException("已有 BattleSceneHost 尚未配置，请绑定 Session Source 和 Scene Adapter。");
                return ConnectExisting(adapters[0]);
            }
            Selection.activeGameObject = host.gameObject; EditorGUIUtility.PingObject(host);
            return host;
        }
        private static BattleSceneHost ConnectExisting(MonoBehaviour adapter)
        {
            var host = adapter.GetComponent<BattleSceneHost>();
            if (host == null) host = Undo.AddComponent<BattleSceneHost>(adapter.gameObject);
            if (host.SceneAdapter != null && host.SceneAdapter != adapter)
                throw new InvalidOperationException("BattleSceneHost 已绑定另一适配器，未覆盖已有配置。");
            Undo.RecordObjects(new UnityEngine.Object[] { host, adapter }, "Connect BattleSceneHost");
            if (host.SceneAdapter == null || host.SessionSource == null) host.Configure(adapter, adapter, host.AutoStart);
            else ((IBattleSceneAdapter)adapter).BindHost(host);
            EditorUtility.SetDirty(host); EditorUtility.SetDirty(adapter);
            EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
            Selection.activeGameObject = host.gameObject;
            return host;
        }
        public static GameObject EnsureMinimalPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing; // Never overwrite designer changes on repeated menu use.
            var content = AssetDatabase.LoadAssetAtPath<TextAsset>(ContentPath);
            if (content == null) throw new InvalidOperationException("Missing sample XML: " + ContentPath);
            EnsureFolder("Assets/GameAssets/SingleBattle/Hosting");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("BattleSceneHost Minimal"); SceneManager.MoveGameObjectToScene(root, preview);
                var host = root.AddComponent<BattleSceneHost>();
                var source = root.AddComponent<XmlBattleSessionSource>(); source.Configure(content, "single-battle-new");
                var director = root.AddComponent<ScenePresentationDirector>();
                var adapter = root.AddComponent<BattleTimelineSceneAdapter>(); adapter.Configure(director);
                host.Configure(source, adapter);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
        private static void RequireScene(Scene scene)
        {
            if (Application.isPlaying || !scene.IsValid() || !scene.isLoaded || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("请在普通场景的编辑模式中添加 Battle 入口。");
        }
    }
}
