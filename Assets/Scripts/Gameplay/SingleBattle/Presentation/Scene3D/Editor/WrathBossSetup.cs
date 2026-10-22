using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Editor
{
    /// <summary>Creates an isolated functional boss scene; the authored BattleDemo remains untouched.</summary>
    public static class WrathBossSetup
    {
        public const string ScenePath = "Assets/Scenes/WrathBossDemo.unity";
        public const string SourceScenePath = "Assets/Scenes/BattleDemo.unity";
        public const string ContentPath = "Packages/com.kaya.battle-engine/Content/StandardSingleBattle-2026-10-21.xml";
        public const string EncounterId = "standard-wrath-thousand-hands";

        [MenuItem("工具/卡牌战斗/示例场景/打开暴怒 Boss（本体与五手）")]
        public static void Open()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请先退出 Play，再打开暴怒测试场景。");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
                throw new InvalidOperationException("无法复制 BattleDemo 到独立暴怒场景。");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var host = ConfigureScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = host;
            Debug.Log("暴怒测试场景已生成。点击 Play 开战；本体与五手按 PSD 图层拼装，目标和倒计时在独立 Boss 面板中显示。", host);
        }

        [MenuItem("工具/卡牌战斗/示例场景/更新暴怒示例布局")]
        public static void RefreshLayout()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请先退出 Play，再更新暴怒示例布局。");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) { Open(); return; }
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ConfigureScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Also usable by preview-scene tests. Never saves or changes the source asset.</summary>
        public static SingleBattleSceneAdapter ConfigureScene(Scene scene)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Configure boss actors before entering Play.");
            if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("Load the scene first.", nameof(scene));
            var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).Single();
            if (host.State != null) throw new InvalidOperationException("Configure boss actors before starting a session.");
            var sourceEnemies = host.Enemies.ToArray();
            if (sourceEnemies.Length != 1 && sourceEnemies.Length != 3 && sourceEnemies.Length != 6)
                throw new InvalidOperationException("Expected a one- or three-enemy source, or an existing six-actor boss layout.");
            if (sourceEnemies.Any(actor => actor == null || actor.Battle != host || actor.gameObject.scene != scene ||
                actor.Visual == null || actor.Presentation == null) || sourceEnemies.Distinct().Count() != sourceEnemies.Length)
                throw new InvalidOperationException("The source scene must have distinct bound enemy models and presentations.");
            var body = sourceEnemies[0];
            var enemies = new EnemyBattleActor[6]; enemies[0] = body;
            var bodyScale = body.transform.localScale;
            var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(value => value.CompareTag("MainCamera"));
            Vector3 bodyPosition = body.transform.position;
            for (int i = 1; i < enemies.Length; i++)
            {
                // Reuse every source actor in order, so the three-enemy demo leaves no unbound visual behind.
                // Existing six-actor boss scenes keep their actor identities when configured again.
                var actor = i < sourceEnemies.Length ? sourceEnemies[i] : UnityEngine.Object.Instantiate(body, body.transform.parent);
                actor.name = i == 5 ? "Wrath Binding Hand" : "Wrath Blood Hand " + i;
                actor.Configure(host, actor.Visual);
                actor.transform.localScale = bodyScale * .35f;
                if (camera != null)
                {
                    float depth = Mathf.Max(.1f, camera.WorldToViewportPoint(bodyPosition).z);
                    actor.transform.position = camera.ViewportToWorldPoint(new Vector3(.34f + (i - 1) * .105f, .34f, depth));
                    ConfigureBillboards(actor, camera, new Rect(.30f + (i - 1) * .105f, .26f, .08f, .16f), depth);
                }
                else actor.transform.position = bodyPosition + new Vector3((i - 3) * .1f, 0, -.12f);
                enemies[i] = actor; EditorUtility.SetDirty(actor);
            }
            body.name = "Wrath Body";
            body.Configure(host, body.Visual);
            if (camera != null) ConfigureBillboards(body, camera, new Rect(.47f, .38f, .23f, .19f),
                Mathf.Max(.1f, camera.WorldToViewportPoint(bodyPosition).z));
            host.ConfigureActors(host.Players.ToArray(), enemies);
            var content = AssetDatabase.LoadAssetAtPath<TextAsset>(ContentPath);
            if (content == null) throw new InvalidOperationException("未找到当前 .21 战斗内容包，请确认 BattleToolkit 本地包已导入。");
            host.ConfigureContent(content, EncounterId);
            BattlePresentationSceneSetup.ConfigureHost(host);
            var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleHud>(true)).Single();
            if (hud.AdministratorButton != null)
            {
                // The source's fixed entry overlaps the eight-intent strip in the dedicated boss layout.
                var rect = hud.AdministratorButton.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(24, -200);
                EditorUtility.SetDirty(rect);
            }
            // Validate compilation and roster before the generated scene is saved or played.
            host.BuildConfiguredSession("wrath-scene-validation");
            WrathBossArtworkSetup.Configure(host);
            EditorUtility.SetDirty(body); EditorUtility.SetDirty(host);
            return host;
        }

        private static void ConfigureBillboards(EnemyBattleActor actor, Camera camera, Rect area, float depth)
        {
            foreach (var layout in actor.GetComponentsInChildren<BattleBillboardLayout>(true))
            {
                var settings = new SerializedObject(layout);
                float aspect = settings.FindProperty("artworkAspect").floatValue;
                layout.Configure(camera, area, depth, aspect);
                EditorUtility.SetDirty(layout);
            }
        }
    }
}
