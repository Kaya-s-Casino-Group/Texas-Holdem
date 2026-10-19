using System;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using Poker.BattleHosting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Poker.Battle2DValidation.Editor
{
    /// <summary>Authors serialized recipes. No runtime fallback order or SO rules remain in the 2D module.</summary>
    public static class Battle2DTimelineSetup
    {
        public const string ContentPath = "Packages/com.kaya.battle-engine/Content/SingleBattleDemoNew.xml";
        public const string EncounterId = "single-battle-new";

        [MenuItem("工具/卡牌战斗/2D验证/接入 XML 与可配置表现时间线")]
        public static void MigrateCurrent()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请先退出运行模式。");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != Battle2DValidationSetup.ScenePath)
                throw new InvalidOperationException("请先打开 Battle2DValidation 场景。");
            foreach (var root in scene.GetRootGameObjects())
                foreach (var view in root.GetComponentsInChildren<Battle2DView>(true)) ConfigureBattle(view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static BattleSceneHost ConfigureBattle(Battle2DView view)
        {
            var root = view.gameObject;
            var host = root.GetComponent<BattleSceneHost>() ?? Undo.AddComponent<BattleSceneHost>(root);
            if (host.Session != null) throw new InvalidOperationException("Configure an unstarted scene in EditMode.");
            var source = root.GetComponent<XmlBattleSessionSource>() ?? Undo.AddComponent<XmlBattleSessionSource>(root);
            var adapter = root.GetComponent<Battle2DSceneAdapter>() ?? Undo.AddComponent<Battle2DSceneAdapter>(root);
            var director = root.GetComponent<ScenePresentationDirector>() ?? Undo.AddComponent<ScenePresentationDirector>(root);
            Undo.RecordObjects(new UnityEngine.Object[] { host, source, adapter, director, view }, "Configure 2D battle");
            if (source.ContentXml == null)
                source.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>(ContentPath) ?? throw new InvalidOperationException("Missing battle XML."), EncounterId);
            view.ConfigureDirector(director);
            ConfigureTimeline(view, director);
            adapter.Configure(view);
            host.Configure(source, adapter, host.AutoStart, animate: true);
            foreach (var component in new UnityEngine.Object[] { host, source, adapter, director, view }) EditorUtility.SetDirty(component);
            return host;
        }

        public static void ConfigureTimeline(Battle2DView view, ScenePresentationDirector director)
        {
            if (director.HasConfiguredTimeline) return;
            var root = new GameObject("Presentation Timeline"); root.transform.SetParent(view.transform, false);
            Undo.RegisterCreatedObjectUndo(root, "Create 2D timeline");
            var frontend = view.GetComponent<Battle2DCardZonePresentation>() ?? Undo.AddComponent<Battle2DCardZonePresentation>(view.gameObject);
            var play = root.AddComponent<CardZoneTimelineAction>(); play.Configure(frontend, CardZoneActions.Prepare);
            var discard = root.AddComponent<CardZoneTimelineAction>(); discard.Configure(frontend, Battle2DCardZonePresentation.PrepareDiscard);
            var player = root.AddComponent<TransformTimelineAction>(); player.Configure(null, Attack(.6f));
            var enemy = root.AddComponent<TransformTimelineAction>(); enemy.Configure(null, Attack(-.6f));
            director.ConfigureTimeline(new[]
            {
                new SceneTimelineGroup { semanticKey = "ActionStarted", ruleAction = "Play", explicitCheckpoint = true,
                    nodes = new[] {
                        new SceneActionNode { id = "cards", action = play, targetKey = "cards" },
                        new SceneActionNode { id = "attack", action = player, targetKey = "player", after = new[] { Marker("cards", "Prepared") } } },
                    checkpoint = new[] { Marker("attack", "Impact") } },
                new SceneTimelineGroup { semanticKey = "ActionStarted", ruleAction = "Discard",
                    nodes = new[] { new SceneActionNode { id = "cards", action = discard, targetKey = "cards" } } },
                new SceneTimelineGroup { semanticKey = "ActionStarted", ruleAction = "EnemyAction", filter = new SceneTimelineFilter { skipped = SceneMatchBoolean.False },
                    explicitCheckpoint = true, nodes = new[] { new SceneActionNode { id = "attack", action = enemy, targetKey = "enemy" } },
                    checkpoint = new[] { new SceneActionDependency { actionId = "attack", kind = DependencyKind.Completed } } }
            }, new[] {
                new SceneTargetBinding { key = "cards", target = frontend },
                new SceneTargetBinding { key = "player", target = view.PlayerActor },
                new SceneTargetBinding { key = "enemy", target = view.EnemyActor }
            });
            EditorUtility.SetDirty(director);
        }
        private static TransformTimelineStep[] Attack(float offset) => new[]
        {
            new TransformTimelineStep { localOffset = Vector3.right * offset, duration = .09f, marker = "Impact" },
            new TransformTimelineStep { duration = .09f }
        };
        private static SceneActionDependency Marker(string id, string marker) => new SceneActionDependency
        { actionId = id, kind = DependencyKind.Marker, marker = marker };
    }
}
