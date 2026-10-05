using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow
    {
        [MenuItem("Tools/Cards/Create Played Zone In Current Scene")]
        public static void CreatePlayedInCurrentScene()
        {
            Open(); var window = GetWindow<CardZoneValidationWindow>();
            window.Run(() => { window.CreatePlayedPreview(); window.Frame(); });
        }

        private void CreatePlayedPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play 后创建出牌区。");
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
                throw new InvalidOperationException("请先打开一个普通场景。");
            var root = new GameObject("PlayedZoneView Preview");
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "创建出牌区");
            root.transform.position = new Vector3(0, .012f, .05f);
            var played = Undo.AddComponent<PlayedZoneView>(root);
            try { played.Configure(prefab != null ? prefab : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPrefab), CardZoneLayoutSettings.Default); }
            catch { Undo.DestroyObjectImmediate(root); throw; }
            SelectTarget(played); ResetSample(); Apply(false);
            page = 0; Selection.activeGameObject = root;
        }
    }
}
