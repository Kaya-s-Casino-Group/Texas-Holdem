using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Editor
{
    public static class BattleDemoAshSetup
    {
        [MenuItem("工具/卡牌战斗/BattleDemo/接入敌人灰烬消散")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode before authoring ash.");
            const string scenePath = "Assets/Scenes/BattleDemo.unity";
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            Configure(scene);
        }

        public static void Configure(Scene scene)
        {
            if (Application.isPlaying || (scene.path != "Assets/Scenes/BattleDemo.unity" && scene.path != "Assets/Scenes/SingleChapterAdventure.unity"))
                throw new System.InvalidOperationException("Open BattleDemo or SingleChapterAdventure in Edit Mode first.");
            const string path = "Assets/GameAssets/SingleBattle/Presentation/Scene3D/BattleDemoAsh.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Poker/BattleDemo Enemy Ash")); AssetDatabase.CreateAsset(material, path); }
            var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).Single();
            foreach (var actor in host.Enemies)
            {
                var existing = actor.Presentation as BattleActorPresenter;
                var presenter = existing as BattleDemoAshPresenter;
                if (presenter == null)
                {
                    if (existing == null) throw new System.InvalidOperationException("Expected an enemy body presenter: " + actor.name);
                    var go = existing.gameObject; string saved = JsonUtility.ToJson(existing);
                    Object.DestroyImmediate(existing);
                    presenter = go.AddComponent<BattleDemoAshPresenter>();
                    JsonUtility.FromJsonOverwrite(saved, presenter);
                }
                var artwork = actor.Visual.GetComponentsInChildren<MeshRenderer>().First(renderer => renderer.name != "Selected enemy contour");
                presenter.ConfigureAsh(artwork, material); actor.ConfigurePresentation(presenter);
                EditorUtility.SetDirty(actor); EditorUtility.SetDirty(presenter);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log(scene.name + ": enemy death now dissolves to shader-driven drifting ash.");
        }
    }
}
