using System;
using System.Linq;
using CardPresentation;
using global::BattlePresentation.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Poker.SingleBattle.Editor
{
    /// <summary>Updates the adventure's saved battle frontend without replacing its run host or encounter content.</summary>
    public static class AdventureBattlePresentationSetup
    {
        [MenuItem("工具/局外冒险/同步单局战斗表现")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出播放模式。");
            const string sourcePath = "Assets/Scenes/BattleDemo.unity";
            const string targetPath = "Assets/Scenes/SingleChapterAdventure.unity";
            var previous = SceneManager.GetActiveScene();
            var source = SceneManager.GetSceneByPath(sourcePath); bool closeSource = !source.IsValid() || !source.isLoaded;
            if (closeSource) source = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
            var target = SceneManager.GetSceneByPath(targetPath);
            if (!target.IsValid() || !target.isLoaded) target = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(target);
                var host = Find<SingleBattleSceneAdapter>(target);
                BattleCardFlightSceneSetup.Configure(host, host.GetComponent<ScenePresentationDirector>());
                CopySettings(Find<BattleCardProjectileAction>(source), Find<BattleCardProjectileAction>(target),
                    "liftSeconds", "flightSeconds", "volleyInterval", "straightInterval", "arcHeight", "horizontalAt", "tailMaterial", "tailWidth", "tailSeconds", "tailColor");
                CopySettings(Find<HandZoneView>(source), Find<HandZoneView>(target),
                    "discardSettings", "discardLocalPosition", "discardOffscreen", "discardViewport", "transitionSettings");
                BattleDemoOutcomeAndRelicSetup.Configure(target);
                EditorUtility.CopySerialized(Find<BattleDemoRelicArt>(source), Find<BattleDemoRelicArt>(target));
                BattleDemoAshSetup.Configure(target);
                foreach (var ash in target.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BattleDemoAshPresenter>(true)))
                    CopySettings(Find<BattleDemoAshPresenter>(source), ash, "ashSeconds");
                BattleDemoValidationSceneSetup.ConfigureEnemyWorldUI(target);
                EditorSceneManager.MarkSceneDirty(target);
                if (!EditorSceneManager.SaveScene(target)) throw new InvalidOperationException("无法保存冒险战斗表现。");
                Debug.Log("单章冒险已同步 BattleDemo 的弹道、灰烬、效果预览、遗物美术及悬浮使用；冒险内容与宿主绑定保留。");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (closeSource) EditorSceneManager.CloseScene(source, true);
            }
        }
        private static T Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).First();
        private static void CopySettings(Object source, Object target, params string[] properties)
        {
            var from = new SerializedObject(source); var to = new SerializedObject(target);
            foreach (string property in properties) to.CopyFromSerializedProperty(from.FindProperty(property));
            to.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
        }
    }
}
