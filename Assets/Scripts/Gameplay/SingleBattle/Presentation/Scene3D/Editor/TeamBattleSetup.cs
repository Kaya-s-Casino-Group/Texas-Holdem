using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Editor
{
    /// <summary>A separate, editable 2v3 example using the same host and art as the 1v1 scene.</summary>
    public static class TeamBattleSetup
    {
        public const string ScenePath = "Assets/Scenes/TeamBattleDemo.unity";
        [MenuItem("工具/卡牌战斗/示例场景/打开多人战斗（2对3）")]
        public static void Open()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请先退出 Play。");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("请先保存当前场景，再打开多人 Demo。");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) { EditorSceneManager.OpenScene(ScenePath); return; }
            if (!AssetDatabase.CopyAsset(SingleBattleSetup.ScenePath, ScenePath)) throw new InvalidOperationException("无法复制单局 Demo。");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleHost>(true)).Single();
            var player = host.Player; var enemy = host.Enemy;
            var players = new[] { player, UnityEngine.Object.Instantiate(player, player.transform.parent) };
            var enemies = new[] { enemy, UnityEngine.Object.Instantiate(enemy, enemy.transform.parent), UnityEngine.Object.Instantiate(enemy, enemy.transform.parent) };
            for (int i = 0; i < players.Length; i++)
            {
                var actor = players[i]; actor.name = "Player " + (i + 1); actor.Configure(host, actor.Visual);
                actor.transform.position = new Vector3(-.40f + i * .22f, -.007f, .385f); actor.transform.localScale *= .75f;
            }
            for (int i = 0; i < enemies.Length; i++)
            {
                var actor = enemies[i]; actor.name = "Enemy " + (i + 1); actor.Configure(host, actor.Visual);
                actor.transform.position = new Vector3(.10f + i * .18f, 0, .406f); actor.transform.localScale *= .65f;
            }
            host.ConfigureActors(players, enemies);
            foreach (var label in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true)))
                if (label.text.Contains("THE GUARDIAN")) label.text = "多人战局 / 2 PLAYERS vs 3 ENEMIES";
            EditorUtility.SetDirty(host); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Selection.activeObject = host;
        }
    }
}
